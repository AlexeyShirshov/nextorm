namespace NextORM.Core;

internal static class EmptyTempTableSql
{
    internal static string Build(ISqlDialect dialect, KeywordCase keywordCase, string name,
        IReadOnlyList<(string Name, Type Type)> columns, string primaryKey, bool withoutRowId)
    {
        if (dialect is not SqlDialectBase { SupportsEmptyTempTables: true } provider)
            throw new NotSupportedException("Schema-defined temporary tables are supported only by SQLite and PostgreSQL.");
        if (withoutRowId && !provider.SupportsTempTableWithoutRowId)
            throw new NotSupportedException("WITHOUT ROWID is supported only by SQLite.");

        CheckIdentifier(name, nameof(name));
        CheckIdentifier(primaryKey, nameof(primaryKey));
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
            throw new ArgumentException("At least one column is required.", nameof(columns));

        var names = new HashSet<string>(provider.SupportsTempTableWithoutRowId ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var definitions = new string[columns.Count];
        var keyFound = false;
        for (var i = 0; i < definitions.Length; i++)
        {
            var (column, type) = columns[i];
            CheckIdentifier(column, nameof(columns));
            if (!names.Add(column))
                throw new ArgumentException("Column names must be unique.", nameof(columns));

            var isKey = column == primaryKey;
            keyFound |= isKey;
            definitions[i] = $"{dialect.QuoteIdentifier(column)} {StorageType(type, provider.SupportsTempTableWithoutRowId)}"
                + (isKey ? SqlKeywords.Of(keywordCase, " not null primary key") : "");
        }

        if (!keyFound)
            throw new ArgumentException("The primary key must name a declared column exactly.", nameof(primaryKey));

        return $"{SqlKeywords.Of(keywordCase, "create temp table ")}{dialect.QuoteIdentifier(name)} ({string.Join(", ", definitions)})"
            + (withoutRowId ? SqlKeywords.Of(keywordCase, " without rowid") : "");
    }

    private static void CheckIdentifier(string identifier, string parameter)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains('\0'))
            throw new ArgumentException("A nonempty identifier without NUL is required.", parameter);
    }

    private static string StorageType(Type type, bool sqlite)
    {
        ArgumentNullException.ThrowIfNull(type);
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type switch
        {
            _ when type == typeof(string) => "text",
            _ when type == typeof(int) => "integer",
            _ when type == typeof(long) => "bigint",
            _ when type == typeof(double) => "double precision",
            _ when type == typeof(decimal) => "numeric",
            _ when type == typeof(bool) => "boolean",
            _ when type == typeof(byte[]) => sqlite ? "blob" : "bytea",
            _ => throw new ArgumentException("Unsupported CLR storage type.", nameof(type))
        };
    }
}
