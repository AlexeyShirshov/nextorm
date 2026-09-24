using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextORM.Core;
using Npgsql;
using NpgsqlTypes;

namespace NextORM.Postgres;

/// <summary>
/// Data context for PostgreSQL. Wraps the <c>Npgsql</c> driver for connections and parameters and
/// renders SQL through <c>PostgresDialect</c>.
/// </summary>
public class PostgresDataContext : DataContext
{
    /// <summary>
    /// Creates a PostgreSQL context that owns a connection built lazily from
    /// <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string used to create the <c>NpgsqlConnection</c>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public PostgresDataContext(string connectionString, DataContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a PostgreSQL context over a caller-supplied connection, which the context does not
    /// dispose.
    /// </summary>
    /// <param name="connection">An already-created <c>DbConnection</c> owned by the caller.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public PostgresDataContext(DbConnection connection, DataContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    /// <summary>Creates a new <c>NpgsqlConnection</c> for <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>A new, unopened PostgreSQL connection.</returns>
    protected override DbConnection CreateDbConnection(string? connectionString)
        => new NpgsqlConnection(connectionString);

    /// <inheritdoc/>
    public override ISqlDialect Dialect => PostgresDialect.Instance;

    /// <summary>
    /// Creates an <c>NpgsqlParameter</c>, mapping a <see langword="null"/> value to
    /// <c>DBNull.Value</c> and binding JSON documents, elements and nodes as <c>jsonb</c> so that the
    /// JSON operators accept them without an explicit cast.
    /// </summary>
    /// <param name="name">The parameter name, without the provider prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new PostgreSQL parameter.</returns>
    public override DbParameter CreateParam(string name, object? value)
    {
        // Npgsql rejects a null parameter value, so unset/null values must be passed as DBNull.
        var parameter = new NpgsqlParameter(name, value ?? DBNull.Value);

        // A JSON document/element/node is bound as jsonb so that the json/jsonb operators and
        // functions accept it without an explicit cast. A plain string is left as text and can be
        // parsed on demand with SqlFunctions.Postgres.json_cast(...).
        if (value is JsonDocument or JsonElement or JsonNode)
            parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        return parameter;
    }

    /// <summary>
    /// Writes <paramref name="rows"/> through <c>COPY &lt;table&gt; (&lt;cols&gt;) FROM STDIN (FORMAT
    /// BINARY)</c> with <c>NpgsqlBinaryImporter</c>, streaming each row without buffering the set.
    /// </summary>
    /// <param name="tableName">The convention-resolved (unquoted) target table name.</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds; rejected because <c>COPY</c> has none.</param>
    /// <param name="maxBatchSize">Ignored: the binary importer streams one row at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count every <paramref name="notifyEvery"/> rows, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <returns>The number of rows written.</returns>
    protected override int BulkInsertRows(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery)
    {
        EnsureCopyTimeoutSupported(commandTimeoutSeconds);
        EnsureConnectionOpen();
        var connection = (NpgsqlConnection)GetConnection();

        using var enumerator = rows.GetEnumerator();
        if (!enumerator.MoveNext())
            return 0;

        using var writer = connection.BeginBinaryImport(BuildCopyCommand(tableName, columnNames));
        var written = 0;

        do
        {
            writer.WriteRow(enumerator.Current);
            written++;
            if (progress is not null && written % notifyEvery == 0)
                progress(written);
        }
        while (enumerator.MoveNext());

        return (int)writer.Complete();
    }

    /// <summary>Asynchronously writes <paramref name="rows"/> through a binary <c>COPY</c>.</summary>
    /// <param name="tableName">The convention-resolved (unquoted) target table name.</param>
    /// <param name="columnNames">The convention-resolved (unquoted) written column names, in row order.</param>
    /// <param name="columns">The mapped columns, in row order.</param>
    /// <param name="rows">The rows to write; each array matches <paramref name="columnNames"/> by ordinal.</param>
    /// <param name="commandTimeoutSeconds">The command timeout in seconds; rejected because <c>COPY</c> has none.</param>
    /// <param name="maxBatchSize">Ignored: the binary importer streams one row at a time.</param>
    /// <param name="progress">Called with the cumulative written-row count every <paramref name="notifyEvery"/> rows, or <see langword="null"/>.</param>
    /// <param name="notifyEvery">The progress reporting interval in rows.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written.</returns>
    protected override async Task<int> BulkInsertRowsAsync(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<IPropertyMetadata> columns, IAsyncEnumerable<object?[]> rows, int? commandTimeoutSeconds, int? maxBatchSize, Action<int>? progress, int notifyEvery, CancellationToken cancellationToken)
    {
        EnsureCopyTimeoutSupported(commandTimeoutSeconds);
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = (NpgsqlConnection)GetConnection();

        await using var enumerator = rows.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            return 0;

        using var writer = await connection.BeginBinaryImportAsync(BuildCopyCommand(tableName, columnNames), cancellationToken).ConfigureAwait(false);
        var written = 0;

        do
        {
            await writer.WriteRowAsync(cancellationToken, enumerator.Current).ConfigureAwait(false);
            written++;
            if (progress is not null && written % notifyEvery == 0)
                progress(written);
        }
        while (await enumerator.MoveNextAsync().ConfigureAwait(false));

        return (int)await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureCopyTimeoutSupported(int? commandTimeoutSeconds)
    {
        if (commandTimeoutSeconds is not null)
            throw new NotSupportedException("A bulk-insert Timeout is not supported by the PostgreSQL COPY path; omit Timeout or use a provider whose native bulk API supports it.");
    }

    private string BuildCopyCommand(string tableName, IReadOnlyList<string> columnNames)
    {
        var quotedColumns = string.Join(", ", columnNames.Select(Dialect.QuoteIdentifier));
        return $"COPY {Dialect.QuoteIdentifier(tableName)} ({quotedColumns}) FROM STDIN (FORMAT BINARY)";
    }
}
