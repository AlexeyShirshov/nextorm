namespace NextORM.Core;

/// <summary>
/// Expands the physical-name placeholders of a raw SQL fragment from the metadata of the entity bound to
/// the source and the mapping variant of the command. Expansion runs while the plan is built, so the
/// emitted SQL carries the scope's real schema and table exactly like a mapped source does.
/// </summary>
/// <remarks>
/// The recognised tokens are <c>{schema}</c>, <c>{table}</c> and <c>{qualified}</c>. They are only
/// expanded when the raw source is bound to an entity (<c>BindEntity&lt;TEntity&gt;()</c>); a fragment
/// without any of them is emitted verbatim, which keeps raw SQL that merely contains braces untouched.
/// Identifiers are quoted with the provider's delimiter when the context renders quoted identifiers.
/// </remarks>
internal static class RawSqlPlaceholders
{
    /// <summary>The mapped schema of the bound entity.</summary>
    internal const string SchemaToken = "{schema}";

    /// <summary>The mapped table name of the bound entity, without the bucket.</summary>
    internal const string TableToken = "{table}";

    /// <summary><c>{schema}.{table}</c>, or the table alone when the entity declares no schema.</summary>
    internal const string QualifiedToken = "{qualified}";

    /// <summary>Whether the fragment contains any of the physical-name tokens.</summary>
    /// <param name="sql">The caller-supplied fragment.</param>
    /// <returns><see langword="true"/> when at least one token is present.</returns>
    internal static bool ContainsToken(string sql)
        => sql.Contains(SchemaToken, StringComparison.Ordinal)
            || sql.Contains(TableToken, StringComparison.Ordinal)
            || sql.Contains(QualifiedToken, StringComparison.Ordinal);

    /// <summary>Replaces every physical-name token with the bound entity's names under the command's scope.</summary>
    /// <param name="sql">The caller-supplied fragment.</param>
    /// <param name="entityType">The entity the raw source is bound to, or <see langword="null"/>.</param>
    /// <param name="ctx">The build context carrying the scope and the metadata provider.</param>
    /// <returns>The fragment with the tokens replaced, or the original when it has none.</returns>
    /// <exception cref="InvalidOperationException">
    /// A token is present but the source is not bound to an entity, the entity has no table name, or
    /// <c>{schema}</c> is used for an entity that declares no schema.
    /// </exception>
    internal static string Expand(string sql, Type? entityType, in SqlBuildContext ctx)
    {
        if (!ContainsToken(sql))
            return sql;

        if (entityType is null)
        {
            throw new InvalidOperationException(
                $"The raw SQL uses a physical-name placeholder ({SchemaToken}, {TableToken} or {QualifiedToken}) " +
                "but the source is not bound to an entity, so there is nothing to resolve it from. Call " +
                "BindEntity<TEntity>(...) on the raw source, or write the physical name yourself.");
        }

        if (typeof(IProjection).IsAssignableFrom(entityType))
        {
            throw new InvalidOperationException(
                $"The raw SQL uses a physical-name placeholder ({SchemaToken}, {TableToken} or {QualifiedToken}) " +
                $"but is bound to the join projection '{entityType}'. Bind the source to a concrete entity " +
                "(BindEntity<TEntity>()) or write the physical name yourself.");
        }

        var provider = ctx.MetadataProvider
            ?? throw new InvalidOperationException(
                "The raw SQL placeholders require a metadata provider, but the build context has none.");

        var scope = ctx.MappingVariant;
        var metadata = provider.GetMetadata(entityType, in scope);
        var table = metadata.TableName;
        if (string.IsNullOrEmpty(table))
        {
            throw new InvalidOperationException(
                $"Entity '{entityType}' has no mapped table name, so the raw SQL placeholders cannot be resolved.");
        }

        var schema = string.IsNullOrEmpty(metadata.Schema) ? null : metadata.Schema;
        var tableSql = Quote(ctx, table);
        var schemaSql = schema is null ? null : Quote(ctx, schema);

        var expanded = sql;

        if (expanded.Contains(SchemaToken, StringComparison.Ordinal))
        {
            if (schemaSql is null)
            {
                throw new InvalidOperationException(
                    $"The raw SQL uses {SchemaToken}, but entity '{entityType}' declares no schema. Use " +
                    $"{TableToken} or {QualifiedToken} instead, or map the entity to a schema.");
            }

            expanded = expanded.Replace(SchemaToken, schemaSql, StringComparison.Ordinal);
        }

        expanded = expanded.Replace(TableToken, tableSql, StringComparison.Ordinal);
        expanded = expanded.Replace(
            QualifiedToken,
            schemaSql is null ? tableSql : string.Concat(schemaSql, ".", tableSql),
            StringComparison.Ordinal);

        return expanded;
    }

    private static string Quote(in SqlBuildContext ctx, string name)
        => ctx.QuoteIdentifiers ? ctx.Dialect.QuoteIdentifier(name) : name;
}
