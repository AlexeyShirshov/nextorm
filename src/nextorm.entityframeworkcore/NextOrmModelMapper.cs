using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Reads an EF Core <see cref="IModel"/> and registers the resulting entity mapping in nextorm's
/// process-wide metadata cache, so that nextorm queries over the same CLR types address the table and
/// columns declared in the EF model.
/// </summary>
/// <remarks>
/// The mapping is derived from the EF model alone: no nextorm attribute or fluent configuration is
/// required on the entity. Properties without a CLR member (shadow properties) and owned types are
/// skipped. Because <see cref="DataContextCache.Metadata"/> is process-wide and keyed by
/// <see cref="Type"/>, at most one mapping per CLR type is kept; a later registration for the same
/// type replaces the previous one only when it is identical, and otherwise throws.
/// <para>
/// The global query filters declared in the EF model are imported too:
/// <see cref="EfQueryFilterTranslator"/> turns each named/keyed and anonymous EF filter into a
/// nextorm filter reading its live owning <see cref="DbContext"/> through
/// <see cref="EfCoreFilterBinding"/>. Every entity's mapping and filters are translated and staged
/// first; under the process-wide metadata registration gate the current entries are re-read, the merge
/// is recomputed and conflicts revalidated immediately before publication, and a failure restores the
/// prior entries / removes the newly added ones.
/// Import is failure-atomic: failed publication preserves prior metadata. Concurrent-reader snapshot atomicity is not guaranteed.
/// </para>
/// </remarks>
public static class NextOrmModelMapper
{
    /// <summary>
    /// Builds nextorm entity metadata for every relational type of <paramref name="model"/> and
    /// writes it to <see cref="DataContextCache.Metadata"/>, keyed by the entity CLR type.
    /// </summary>
    /// <param name="model">The EF Core model to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The model uses a shape the integration cannot represent: an untranslatable global query filter,
    /// EF inheritance (TPH/TPT/TPC), or a schema-qualified table. Mapping it would silently produce
    /// wrong SQL.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A CLR type is already mapped to a different table/column layout, or an imported named filter
    /// collides with an existing registration. The process-wide metadata cache holds one mapping per
    /// type; call <see cref="DataContextCache.Clear"/> to reset it.
    /// </exception>
    public static void Register(IModel model) => RegisterAndCollect(model);

    /// <summary>
    /// Implements <see cref="Register(IModel)"/> and additionally returns the imported filter
    /// expectations, so the bridge can register the fail-closed guard on the created context. The public
    /// entry point discards them; nothing is returned (and any failure propagates) unless the whole
    /// model was published successfully.
    /// </summary>
    /// <param name="model">The EF Core model to map.</param>
    /// <returns>The entity types that imported at least one filter, with their expected filter keys.</returns>
    internal static IReadOnlyList<(Type EntityType, IReadOnlyList<string> Keys)> RegisterAndCollect(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var staged = new List<StagedEntity>();

        // Stage the whole model: translate every mapping and filter before touching the cache, so a
        // failure anywhere leaves the previously published metadata intact (all-or-none publication).
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            // nextorm maps one CLR type to one table and has no discriminator support, so any mapped
            // inheritance hierarchy (TPH/TPT/TPC) would return or address the wrong rows.
            if (entityType.BaseType is not null || entityType.GetDerivedTypes().Any())
                throw new NotSupportedException(
                    $"The entity type '{entityType.ClrType}' uses EF Core inheritance (TPH/TPT/TPC), which the EF Core integration does not support: nextorm does not carry discriminators.");

            var tableName = entityType.GetTableName() ?? entityType.GetViewName();
            if (string.IsNullOrEmpty(tableName))
                continue;

            // nextorm entity metadata has no schema member: a schema-qualified name would qualify
            // SELECT but not DML (which reads TableSchema), so refuse instead of mapping it wrongly.
            var schema = entityType.GetSchema();
            if (!string.IsNullOrEmpty(schema))
                throw new NotSupportedException(
                    $"The entity type '{entityType.ClrType}' is mapped to the schema-qualified table '{schema}.{tableName}', which the EF Core integration cannot represent: nextorm entity metadata has no schema member.");

            var keyProperties = entityType.FindPrimaryKey()?.Properties;

            var properties = new List<IPropertyMetadata>();
            foreach (var property in entityType.GetProperties())
            {
                var propertyInfo = property.PropertyInfo;
                if (propertyInfo is null)
                    continue;

                properties.Add(new EfPropertyMetadata
                {
                    PropertyInfo = propertyInfo,
                    ColumnName = property.GetColumnName() ?? propertyInfo.Name,
                    IsKey = keyProperties?.Contains(property) == true,
                    IsIdentity = property.ValueGenerated == ValueGenerated.OnAdd,
                    IsComputed = property.ValueGenerated == ValueGenerated.OnAddOrUpdate,
                });
            }

            if (properties.Count == 0)
                continue;

            var translation = EfQueryFilterTranslator.Translate(entityType);

            staged.Add(new StagedEntity(entityType.ClrType, new EfEntityMetadata
            {
                TableName = tableName,
                Properties = properties,
                Filters = translation.Filters,
                EfFingerprint = ComputeFingerprint(tableName, properties, translation.Filters, translation.Provenance),
            }));
        }

        lock (DataContextCache.MetadataRegistrationGate)
        {
            // Publish one entity at a time, re-reading the current entry and recomputing the merge /
            // revalidating conflicts immediately before each write. A failure while publishing an entry
            // rolls every already-written entry back, so the batch is all-or-none and prior metadata is
            // preserved even if a concurrent write (coordinated through the same gate) changed an entry.
            var applied = new List<(Type Type, IEntityMetadata? Prior)>(staged.Count);
            try
            {
                foreach (var (entityType, metadata) in staged)
                {
                    DataContextCache.Metadata.TryGetValue(entityType, out var existing);
                    var resolved = ResolvePublication(entityType, existing, metadata);

                    applied.Add((entityType, existing));
                    DataContextCache.Metadata[entityType] = resolved;
                }
            }
            catch
            {
                for (var i = applied.Count - 1; i >= 0; i--)
                {
                    var (type, prior) = applied[i];
                    if (prior is null)
                        DataContextCache.Metadata.Remove(type);
                    else
                        DataContextCache.Metadata[type] = prior;
                }

                throw;
            }
        }

        // The model was published successfully; report exactly the entity types that imported filters and
        // their keys, so the bridge can fail closed if that metadata is later dropped.
        var expectations = new List<(Type EntityType, IReadOnlyList<string> Keys)>();
        foreach (var (entityType, metadata) in staged)
        {
            if (metadata.Filters.Count == 0)
                continue;

            var keys = new string[metadata.Filters.Count];
            for (var i = 0; i < metadata.Filters.Count; i++)
                keys[i] = metadata.Filters[i].Key;

            expectations.Add((entityType, keys));
        }

        return expectations;
    }

    // Resolves what to publish for one staged entity against the entry currently in the cache:
    // an absent entry publishes the staged mapping, an identical EF registration is reused, a mapping
    // published outside the bridge is merged (the EF import contributes only its filters), and any
    // other shape is a conflict. Runs under the registration gate, so the decision and the write are
    // not interleaved with another registration.
    private static IEntityMetadata ResolvePublication(Type entityType, IEntityMetadata? existing, EfEntityMetadata metadata)
    {
        if (existing is null)
            return metadata;

        if (existing is EfEntityMetadata existingEf)
        {
            // Repeating the identical complete bridge registration is idempotent: the owner-free
            // fingerprint covers the EF model's table, columns, filters and provenance exactly, so the
            // existing entry is reused without re-comparing it. A merged entry keeps the pre-existing
            // core mapping's table/columns, which may legitimately diverge from the staged EF table, so
            // the mapping comparison must not run before this short-circuit.
            if (existingEf.EfFingerprint == metadata.EfFingerprint)
                return existingEf;

            if (!IsSameMapping(existingEf, metadata))
                throw new InvalidOperationException(
                    $"The CLR type '{entityType}' is already mapped to a different table/column layout. " +
                    "nextorm keeps one mapping per CLR type per process: clear DataContextCache.Metadata " +
                    "(DataContextCache.Clear()) before registering a second, different model for the same type.");

            throw new InvalidOperationException(
                $"The CLR type '{entityType}' was already bridged from an EF Core model with a different query-filter or mapping shape. " +
                "nextorm keeps one mapping per CLR type per process: clear DataContextCache.Metadata (DataContextCache.Clear()) before registering a changed model.");
        }

        // A mapping published outside the bridge (fluent From<T>(cfg) / attributes): the explicit
        // mapping is authoritative for its table and columns and is preserved whole; the EF import
        // layers its filters onto it. The table name may legitimately differ from the staged EF model
        // (the explicit core mapping wins), but an incompatible property layout is still a conflict.
        if (!IsSamePropertyMapping(existing, metadata))
            throw new InvalidOperationException(
                $"The CLR type '{entityType}' is already mapped to a different table/column layout. " +
                "nextorm keeps one mapping per CLR type per process: clear DataContextCache.Metadata " +
                "(DataContextCache.Clear()) before registering a second, different model for the same type.");

        return MergeWithExisting(entityType, existing, metadata);
    }

    // Merges a mapping already registered outside the bridge with the staged EF mapping: anonymous
    // filters are additive (both nextorm and EF anonymous predicates are AND-ed), a named filter with a
    // key already present is a collision (no implicit replace/AND), and other named filters are kept.
    private static EfEntityMetadata MergeWithExisting(Type entityType, IEntityMetadata existing, EfEntityMetadata staged)
    {
        var namedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filter in staged.Filters)
        {
            if (filter.Key.Length > 0)
                namedKeys.Add(filter.Key);
        }

        List<IQueryFilterMetadata>? merged = null;
        foreach (var filter in existing.Filters)
        {
            if (filter.Key.Length == 0)
            {
                (merged ??= new List<IQueryFilterMetadata>(staged.Filters)).Add(filter);
                continue;
            }

            if (namedKeys.Contains(filter.Key))
                throw new InvalidOperationException(
                    $"The query filter '{filter.Key}' on '{entityType}' is already registered outside the EF Core bridge; " +
                    "an imported named filter must not replace it. Remove the existing registration or rename the EF filter key.");

            if (!staged.Filters.Any(candidate => string.Equals(candidate.Key, filter.Key, StringComparison.Ordinal)))
                (merged ??= new List<IQueryFilterMetadata>(staged.Filters)).Add(filter);
        }

        // Preserve the complete pre-existing mapping (table, properties, relationships, dynamic-columns
        // store, auto-name flag and any other interface state). MergeWithExisting is only reached when
        // IsSameMapping already accepted the staged table/property layout, so the EF import contributes
        // only its filters and fingerprint; rebuilding a fresh EfEntityMetadata from the staged fields
        // would silently drop the core mapping's relationships / dynamic-columns store / auto-name flag.
        return new EfEntityMetadata
        {
            TableName = existing.TableName,
            Properties = existing.Properties,
            Filters = merged ?? staged.Filters,
            EfFingerprint = staged.EfFingerprint,
            IsTableNameAuto = existing.IsTableNameAuto,
            DynamicColumnsStore = existing.DynamicColumnsStore,
            Relationships = existing.Relationships,
        };
    }

    // A deterministic, owner-free fingerprint of the mapping, the imported filters and their provenance
    // (the owning DbContext CLR type names). The translated lambda names
    // EfCoreFilterBinding.GetOwner(context) instead of the owning DbContext instance, so two contexts of
    // the *same* type over the same model produce the same fingerprint and are idempotent; a different
    // context type or changed filter body is a different registration. The order of AND-ed filters does
    // not change the meaning, so the filter list is sorted before hashing.
    private static string ComputeFingerprint(string tableName, IReadOnlyList<IPropertyMetadata> properties, IReadOnlyList<IQueryFilterMetadata> filters, string provenance)
    {
        var builder = new StringBuilder();
        builder.Append(tableName).Append('|').Append(provenance).Append('|');

        foreach (var property in properties)
        {
            builder.Append(property.PropertyInfo.Name).Append(':')
                .Append(property.PropertyInfo.PropertyType.FullName ?? property.PropertyInfo.PropertyType.Name).Append(':')
                .Append(property.ColumnName).Append(':')
                .Append(property.IsKey).Append(':')
                .Append(property.IsIdentity).Append(':')
                .Append(property.IsComputed).Append(';');
        }

        var normalized = new List<(string Key, string Body)>(filters.Count);
        foreach (var filter in filters)
            normalized.Add((filter.Key, filter.Lambda?.ToString() ?? string.Empty));

        normalized.Sort(static (left, right) =>
        {
            var byKey = string.CompareOrdinal(left.Key, right.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(left.Body, right.Body);
        });

        foreach (var (key, body) in normalized)
            builder.Append(key).Append('=').Append(body).Append(';');

        return builder.ToString();
    }

    private static bool IsSameMapping(IEntityMetadata left, IEntityMetadata right)
        => string.Equals(left.TableName, right.TableName, StringComparison.Ordinal)
            && IsSamePropertyMapping(left, right);

    // The property half of the mapping identity, without the table name. An explicit core mapping is
    // authoritative for its table, so the EF import only has to agree on the columns it layers filters
    // onto; a different CLR type, column, key/identity/computed flag is still a conflict.
    private static bool IsSamePropertyMapping(IEntityMetadata left, IEntityMetadata right)
    {
        if (left.Properties.Count != right.Properties.Count)
            return false;

        foreach (var property in right.Properties)
        {
            var match = left.Properties.FirstOrDefault(
                candidate => candidate.PropertyInfo.Name == property.PropertyInfo.Name);

            if (match is null
                || match.PropertyInfo.PropertyType != property.PropertyInfo.PropertyType
                || !string.Equals(match.ColumnName, property.ColumnName, StringComparison.Ordinal)
                || match.IsKey != property.IsKey
                || match.IsIdentity != property.IsIdentity
                || match.IsComputed != property.IsComputed)
                return false;
        }

        return true;
    }

    private readonly record struct StagedEntity(Type Type, EfEntityMetadata Metadata);

    /// <summary>Entity metadata projected from an EF Core entity type, including imported filters.</summary>
    private sealed class EfEntityMetadata : IEntityMetadata
    {
        public required IReadOnlyList<IPropertyMetadata> Properties { get; init; }

        public required string? TableName { get; init; }

        public IReadOnlyList<IQueryFilterMetadata> Filters { get; init; } = Array.Empty<IQueryFilterMetadata>();

        /// <summary>
        /// Whether <see cref="TableName"/> came from the auto-derived CLR name of the pre-existing core
        /// mapping this entry was merged onto.
        /// </summary>
        public bool IsTableNameAuto { get; init; }

        /// <summary>The dynamic-columns store of the pre-existing core mapping this entry was merged onto, or <see langword="null"/>.</summary>
        public IPropertyMetadata? DynamicColumnsStore { get; init; }

        /// <summary>The relationships of the pre-existing core mapping this entry was merged onto; empty for a pure EF mapping.</summary>
        public IReadOnlyList<IRelationshipMetadata> Relationships { get; init; } = Array.Empty<IRelationshipMetadata>();

        /// <summary>Owner-free fingerprint of the mapping and the EF-imported filters; empty for merged mappings that were not read back.</summary>
        public string EfFingerprint { get; init; } = string.Empty;
    }

    /// <summary>Property metadata projected from an EF Core property.</summary>
    private sealed class EfPropertyMetadata : IPropertyMetadata
    {
        public required PropertyInfo PropertyInfo { get; init; }

        public required string ColumnName { get; init; }

        public bool IsKey { get; init; }

        public bool IsIdentity { get; init; }

        public bool IsComputed { get; init; }
    }
}
