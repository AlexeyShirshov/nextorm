namespace NextORM.Core;

/// <summary>
/// The resolved destination of a bulk insert: the target table together with its independently qualified
/// schema, database and server parts. Construction resolves the option aliases once (so the render path
/// never re-decides which qualifier applies) and refuses a server qualification outright: a bulk insert
/// never reroutes to another server.
/// </summary>
internal sealed class BulkInsertDestination
{
    private BulkInsertDestination(string? table, string? schema, string? database, string? server)
    {
        if (server is not null)
            throw new NotSupportedException(
                "BulkInsertOptions.ServerName is not supported: a bulk insert never reroutes to another server. "
                + "Set only SchemaName/DatabaseName, or use a connection that targets the required server.");

        Table = table;
        Schema = schema;
        Database = database;
        Server = server;
    }

    /// <summary>The table override from <see cref="BulkInsertOptions.TableName"/>, or <see langword="null"/> for the mapped table.</summary>
    public string? Table { get; }

    /// <summary>The resolved schema part (<see cref="BulkInsertOptions.TableSchema"/> or <see cref="BulkInsertOptions.SchemaName"/>), or <see langword="null"/>.</summary>
    public string? Schema { get; }

    /// <summary>The database part from <see cref="BulkInsertOptions.DatabaseName"/>, or <see langword="null"/>.</summary>
    public string? Database { get; }

    /// <summary>The server part; always <see langword="null"/> because a server qualification is refused at construction.</summary>
    public string? Server { get; }

    /// <summary>Whether no qualification was requested, so the mapped table reference is rendered unchanged.</summary>
    public bool IsEmpty => Table is null && Schema is null && Database is null && Server is null;

    /// <summary>
    /// Resolves the destination from the options. The two schema aliases are treated as one part: equal
    /// values collapse, disagreeing values are rejected before any row is consumed.
    /// </summary>
    /// <param name="options">The bulk-insert options.</param>
    /// <returns>The resolved destination.</returns>
    /// <exception cref="ArgumentException"><see cref="BulkInsertOptions.TableSchema"/> and <see cref="BulkInsertOptions.SchemaName"/> disagree.</exception>
    /// <exception cref="NotSupportedException"><see cref="BulkInsertOptions.ServerName"/> is set.</exception>
    public static BulkInsertDestination From(BulkInsertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new BulkInsertDestination(options.TableName, ResolveSchema(options), options.DatabaseName, options.ServerName);
    }

    /// <summary>
    /// Renders the qualified target reference, quoting each qualification part independently. The schema-only
    /// form uses <paramref name="mappedTableName"/>, so a destination that overrides only the schema keeps the
    /// mapped table. A qualification the dialect cannot express is rejected.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="mappedTableName">The convention-resolved table name (unquoted) used when no table override is set.</param>
    /// <returns>The rendered target reference.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the requested qualification.</exception>
    public string Render(ISqlDialect dialect, bool quoteIdentifiers, string mappedTableName)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        if (Database is not null && !dialect.SupportsCrossDatabase)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot address another database in a bulk insert; BulkInsertOptions.DatabaseName is not supported by this dialect.");

        var qualified = dialect.MakeQualifiedTableName(server: null, Database, Schema, Table ?? mappedTableName);
        return quoteIdentifiers ? QuoteParts(dialect, qualified) : qualified;
    }

    private static string? ResolveSchema(BulkInsertOptions options)
    {
        var legacy = options.TableSchema;
        var independent = options.SchemaName;

        if (legacy is not null && independent is not null && !string.Equals(legacy, independent, StringComparison.Ordinal))
            throw new ArgumentException(
                "BulkInsertOptions.TableSchema and BulkInsertOptions.SchemaName both qualify the destination schema and disagree; set only one of them, or make them equal.",
                nameof(options));

        return independent ?? legacy;
    }

    // Quote each dot-separated part separately, so a qualified name stays qualified instead of becoming one
    // identifier that contains dots.
    private static string QuoteParts(ISqlDialect dialect, string qualifiedName)
    {
        if (qualifiedName.IndexOf('.') < 0)
            return dialect.QuoteIdentifier(qualifiedName);

        var parts = qualifiedName.Split('.');
        for (var i = 0; i < parts.Length; i++)
            parts[i] = dialect.QuoteIdentifier(parts[i]);

        return string.Join('.', parts);
    }
}
