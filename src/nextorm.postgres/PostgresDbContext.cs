using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using nextorm.core;
using Npgsql;
using NpgsqlTypes;

namespace nextorm.postgres;

public class PostgresDbContext : DbContext
{
    public PostgresDbContext(string connectionString, DbContextBuilder optionsBuilder)
        : base(connectionString, null, optionsBuilder)
    {
    }

    public PostgresDbContext(DbConnection connection, DbContextBuilder optionsBuilder)
        : base(null, connection, optionsBuilder)
    {
    }

    protected override DbConnection CreateDbConnection(string? connectionString)
        => new NpgsqlConnection(connectionString);

    public override ISqlDialect Dialect => PostgresDialect.Instance;

    public override DbParameter CreateParam(string name, object? value)
    {
        // Npgsql rejects a null parameter value, so unset/null values must be passed as DBNull.
        var parameter = new NpgsqlParameter(name, value ?? DBNull.Value);

        // A JSON document/element/node is bound as jsonb so that the json/jsonb operators and
        // functions accept it without an explicit cast. A plain string is left as text and can be
        // parsed on demand with NORM.SQL.json_cast(...).
        if (value is JsonDocument or JsonElement or JsonNode)
            parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        return parameter;
    }
}
