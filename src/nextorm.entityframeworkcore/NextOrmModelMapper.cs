using System.Reflection;
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
    /// The model uses a shape the integration cannot represent: a global query filter, EF inheritance
    /// (TPH/TPT/TPC), or a schema-qualified table. Mapping it would silently produce wrong SQL.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A CLR type is already mapped to a different table/column layout. The process-wide metadata cache
    /// holds one mapping per type; call <see cref="DataContextCache.Clear"/> to reset it.
    /// </exception>
    public static void Register(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            // Query filters change which rows a SELECT returns; nextorm does not carry them, so a
            // mapped type would quietly drop soft-delete / multi-tenant filtering.
            if (entityType.GetDeclaredQueryFilters().Any())
                throw new NotSupportedException(
                    $"The entity type '{entityType.ClrType}' declares an EF Core query filter, which the EF Core integration does not support: nextorm would read unfiltered rows.");

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

            var metadata = new EfEntityMetadata
            {
                TableName = tableName,
                Properties = properties,
            };

            // One CLR type maps to one table/column layout per process. Re-registering the identical
            // mapping (e.g. CreateNextOrmContext runs Register after an explicit call) is fine; a
            // different layout must fail loudly instead of silently winning the last-writer race.
            if (DataContextCache.Metadata.TryGetValue(entityType.ClrType, out var existing)
                && !IsSameMapping(existing, metadata))
                throw new InvalidOperationException(
                    $"The CLR type '{entityType.ClrType}' is already mapped to a different table/column layout. " +
                    "nextorm keeps one mapping per CLR type per process: clear DataContextCache.Metadata " +
                    "(DataContextCache.Clear()) before registering a second, different model for the same type.");

            DataContextCache.Metadata[entityType.ClrType] = metadata;
        }
    }

    private static bool IsSameMapping(IEntityMetadata left, IEntityMetadata right)
    {
        if (!string.Equals(left.TableName, right.TableName, StringComparison.Ordinal))
            return false;
        if (left.Properties.Count != right.Properties.Count)
            return false;

        foreach (var property in right.Properties)
        {
            var match = left.Properties.FirstOrDefault(
                candidate => candidate.PropertyInfo.Name == property.PropertyInfo.Name);

            if (match is null
                || !string.Equals(match.ColumnName, property.ColumnName, StringComparison.Ordinal)
                || match.IsKey != property.IsKey
                || match.IsIdentity != property.IsIdentity
                || match.IsComputed != property.IsComputed)
                return false;
        }

        return true;
    }

    /// <summary>Entity metadata projected from an EF Core entity type.</summary>
    private sealed class EfEntityMetadata : IEntityMetadata
    {
        public required IReadOnlyList<IPropertyMetadata> Properties { get; init; }

        public required string? TableName { get; init; }
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
