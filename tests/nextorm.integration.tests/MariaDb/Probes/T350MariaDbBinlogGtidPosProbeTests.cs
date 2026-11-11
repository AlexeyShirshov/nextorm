using System.Globalization;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MySqlConnector;

namespace NextORM.Integration.Tests;

/// <summary>
/// T350 (issue #284) S2-P: the one bounded observation the open <c>BINLOG_GTID_POS</c> row needs. The
/// function resolves on the stock image but the image runs with binary logging off (error 1381), so its
/// return type, NULL shape and value were never observed. This probe owns its own MariaDB 11.4 instance
/// with binary logging enabled — a self-contained <see cref="ContainerBuilder"/> rather than the shared
/// <see cref="MariaDbContainer"/>, because enabling <c>--log-bin</c> would change the server every other
/// MariaDB test runs against.
/// <para>
/// The bound is structural and asserted: one instance, one binlogged write, one <c>SHOW MASTER STATUS</c>
/// read, one <c>BINLOG_GTID_POS(...)</c> evaluation. Nothing here asserts a product contract — the record
/// is the server's answer, and the disposition belongs to PLAN.
/// </para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T350MariaDbBinlogGtidPosProbeTests
{
    private const string Image = "mariadb:11.4";

    private const int ContainerPort = 3306;

    private const string DefaultProbePath = "/tmp/opencode/t350-binlog-s2.jsonl";

    [Fact]
    public async Task T350BinlogGtidPos_ShouldBeObservedOnceOnABinlogEnabledServer()
    {
        var probePath = T350RawOracle.PathOf("T350_BINLOG_PATH", DefaultProbePath);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            using var unavailable = T350RawOracle.Open(probePath, FileMode.Create);
            Write(unavailable, new Dictionary<string, object?>
            {
                ["kind"] = "probe-unavailable",
                ["reason"] = "DOCKER_HOST is not set, so no container runtime can be reached; no observation was attempted."
            });
            Assert.Skip("DOCKER_HOST is not set: the binlog probe cannot start its own MariaDB instance.");
            return;
        }

        IContainer? container = null;
        try
        {
            container = new ContainerBuilder(Image)
                .WithEnvironment("MARIADB_DATABASE", "nextorm")
                .WithEnvironment("MARIADB_USER", "nextorm")
                .WithEnvironment("MARIADB_PASSWORD", "nextorm")
                .WithEnvironment("MARIADB_ROOT_PASSWORD", "nextorm")
                .WithCommand("--log-bin=mysql-bin", "--server-id=1", "--log-basename=mysqld")
                .WithPortBinding(ContainerPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("healthcheck.sh", "--connect", "--innodb_initialized"))
                .Build();

            await container.StartAsync(TestContext.Current.CancellationToken);

            var connectionString =
                $"Server={container.Hostname};Port={container.GetMappedPublicPort(ContainerPort)};Database=nextorm;" +
                "User ID=root;Password=nextorm";

            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            using var writer = T350RawOracle.Open(probePath, FileMode.Create);
            Probe(connection, writer);

            writer.Flush();
        }
        catch (Exception exception)
        {
            using var unavailable = T350RawOracle.Open(probePath, FileMode.Create);
            Write(unavailable, new Dictionary<string, object?>
            {
                ["kind"] = "probe-unavailable",
                ["reason"] = $"{exception.GetType().Name}: {exception.Message}"
            });
            unavailable.Flush();
            Assert.Skip($"The binlog probe instance could not be started: {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (container is not null)
            {
                try
                {
                    await container.DisposeAsync();
                }
                catch
                {
                    // Best effort; Testcontainers' resource reaper remains the primary cleanup mechanism.
                }
            }
        }
    }

    /// <summary>
    /// Confirms binary logging, performs the single binlogged write, reads the binlog file/position and the
    /// GTID state, then evaluates <c>BINLOG_GTID_POS(file, position)</c> exactly once and records the CLR
    /// field type, the provider's nullability metadata and the value.
    /// </summary>
    private static void Probe(MySqlConnection connection, StreamWriter writer)
    {
        var server = new Dictionary<string, object?>
        {
            ["kind"] = "server",
            ["serverVersion"] = Scalar(connection, "select version()"),
            ["serverId"] = Scalar(connection, "select @@global.server_id"),
            ["logBin"] = Scalar(connection, "show variables like 'log_bin'"),
            ["binlogFormat"] = Scalar(connection, "select @@global.binlog_format"),
            ["gtidStrictMode"] = Scalar(connection, "select @@global.gtid_strict_mode")
        };
        Write(writer, server);
        writer.Flush();

        T350RawOracle.Execute(connection, "create table if not exists t350_binlog_probe (id int not null primary key) engine=innodb");

        var before = MasterStatus(connection);
        Write(writer, new Dictionary<string, object?>
        {
            ["kind"] = "binlog-before-write",
            ["file"] = before.File,
            ["position"] = before.Position,
            ["gtidBinlogPos"] = Scalar(connection, "select @@global.gtid_binlog_pos")
        });

        // The one isolated binlogged write.
        T350RawOracle.Execute(connection, "insert into t350_binlog_probe (id) values (1)");

        var after = MasterStatus(connection);
        Write(writer, new Dictionary<string, object?>
        {
            ["kind"] = "binlog-after-write",
            ["file"] = after.File,
            ["position"] = after.Position,
            ["gtidBinlogPos"] = Scalar(connection, "select @@global.gtid_binlog_pos"),
            ["gtidCurrentPos"] = Scalar(connection, "select @@global.gtid_current_pos")
        });
        writer.Flush();

        // The one lookup: the function evaluated with the post-write file/position of the single write.
        var sql = after.File is null || after.Position is null
            ? null
            : string.Create(
                CultureInfo.InvariantCulture,
                $"select BINLOG_GTID_POS('{after.File}', {after.Position})");

        if (sql is null)
        {
            Write(writer, new Dictionary<string, object?>
            {
                ["kind"] = "evaluation",
                ["skipped"] = "no binlog file/position was reported, so BINLOG_GTID_POS could not be evaluated"
            });
            writer.Flush();
            return;
        }

        var evaluation = new Dictionary<string, object?>
        {
            ["kind"] = "evaluation",
            ["sql"] = sql,
            ["binlogFile"] = after.File,
            ["binlogPosition"] = after.Position
        };

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                evaluation["error"] = "no rows";
            }
            else
            {
                evaluation["fieldType"] = reader.GetFieldType(0).FullName;
                evaluation["dataTypeName"] = reader.GetDataTypeName(0);
                evaluation["isDbNull"] = reader.IsDBNull(0);
                evaluation["allowDbNull"] = AllowDbNull(reader);
                if (reader.IsDBNull(0))
                {
                    evaluation["valueType"] = "NULL";
                }
                else
                {
                    var value = reader.GetValue(0);
                    evaluation["valueType"] = T350RawOracle.ClrName(value.GetType());
                    evaluation["textValue"] = T350RawOracle.Format(value);
                }
            }
        }
        catch (MySqlException exception)
        {
            evaluation["errorNumber"] = exception.Number;
            evaluation["sqlState"] = exception.SqlState;
            evaluation["error"] = $"{exception.Number}: {exception.Message}";
        }
        catch (Exception exception)
        {
            evaluation["error"] = $"{exception.GetType().Name}: {exception.Message}";
        }

        Write(writer, evaluation);
        writer.Flush();
    }

    /// <summary>
    /// The provider's declared nullability for the first column, when the schema table reports it; the
    /// observed value's own NULL state is recorded separately as <c>isDbNull</c>.
    /// </summary>
    private static object? AllowDbNull(MySqlDataReader reader)
    {
        try
        {
            var schema = reader.GetSchemaTable();
            if (schema is null || schema.Rows.Count == 0 || !schema.Columns.Contains("AllowDbNull"))
                return null;
            return schema.Rows[0]["AllowDbNull"];
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static (string? File, long? Position) MasterStatus(MySqlConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "show master status";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return (null, null);

            var file = reader.IsDBNull(0) ? null : reader.GetString(0);
            var position = reader.IsDBNull(1) ? (long?)null : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            while (reader.Read())
            {
                // Drain the remaining rows so the connection stays usable.
            }

            return (file, position);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static void Write(StreamWriter writer, Dictionary<string, object?> record)
    {
        writer.WriteLine(JsonSerializer.Serialize(record));
        writer.Flush();
    }

    private static string? Scalar(MySqlConnection connection, string sql)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }
}
