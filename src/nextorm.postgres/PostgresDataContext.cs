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
}
