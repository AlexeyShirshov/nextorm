using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using Npgsql;

namespace NextORM.Integration.Tests;

public sealed class EmptyTempTableSessionTests
{
    [Fact]
    public async Task Sqlite_BulkInsertEnforcesKeyAndWithoutRowId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new SqliteDataContext(connection, new DataContextBuilder());
        var token = TestContext.Current.CancellationToken;
        await connection.OpenAsync(token);
        var duplicate = await ExerciseTable(db, connection, true, token);
        var keyError = await duplicate.Should().ThrowAsync<SqliteException>();
        keyError.Which.SqliteExtendedErrorCode.Should().Be(1555);
        await using var command = connection.CreateCommand();
        command.CommandText = "select rowid from nextorm_412_keys";
        var rowIdError = await FluentActions.Awaiting(() => command.ExecuteScalarAsync(token)).Should().ThrowAsync<SqliteException>();
        rowIdError.Which.Message.Should().Contain("no such column: rowid");
    }

    [Fact]
    public async Task Postgres_BulkInsertEnforcesKeyOnSameSession()
    {
        var value = Environment.GetEnvironmentVariable(PostgresContainer.ConnectionStringVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(value), "PostgreSQL external test endpoint is not configured.");
        var options = new NpgsqlConnectionStringBuilder(value) { Pooling = false };
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        using var db = new PostgresDataContext(connection, new DataContextBuilder());
        var token = TestContext.Current.CancellationToken;
        await connection.OpenAsync(token);
        var duplicate = await ExerciseTable(db, connection, false, token);
        var keyError = await duplicate.Should().ThrowAsync<PostgresException>();
        keyError.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    private static async Task<Func<Task>> ExerciseTable(DataContext db, DbConnection connection, bool withoutRowId, CancellationToken token)
    {
        const string table = "nextorm_412_keys";
        await db.CreateEmptyTempTable(table, [("id", typeof(string)), ("qty", typeof(int))], "id", withoutRowId, token);
        await using var command = connection.CreateCommand();
        command.CommandText = withoutRowId
            ? "select type from pragma_table_info('nextorm_412_keys','temp') order by cid"
            : "select data_type from information_schema.columns where table_schema = (select nspname from pg_namespace where oid = pg_my_temp_schema()) and table_name = 'nextorm_412_keys' order by ordinal_position";
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            var types = new List<string>();
            while (await reader.ReadAsync(token))
                types.Add(reader.GetString(0).ToLowerInvariant());
            types.Should().Equal("text", "integer");
        }
        command.CommandText = "select count(*) from nextorm_412_keys";
        (await command.ExecuteScalarAsync(token)).Should().Be(0L);
        var row = new KeyRow { Id = "alpha", Quantity = 7 };
        var inserted = await Insert();
        inserted.Should().Be(1);
        command.CommandText = "select id from nextorm_412_keys where qty = 7";
        (await command.ExecuteScalarAsync(token)).Should().Be("alpha");
        return async () => { await Insert(); };

        async Task<int> Insert() => await db.CreateBulkInsertBuilder<KeyRow>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary).Table(table))
            .Values([row]).BulkInsertAsync(token);
    }

    private sealed class KeyRow
    {
        [Column("id")]
        public string Id { get; init; } = "";

        [Column("qty")]
        public int Quantity { get; init; }
    }
}
