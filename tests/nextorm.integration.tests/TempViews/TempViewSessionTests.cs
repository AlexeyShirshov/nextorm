using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using Npgsql;

namespace NextORM.Integration.Tests;

public sealed class TempViewSessionTests
{
    private const string TableName = "nextorm_413_messages";
    private const string KeysName = "nextorm_413_keys";
    private const string DeferredSourceName = "nextorm_418_source";

    [Fact]
    public async Task Sqlite_ViewShadowsPermanentTableAndTracksCurrentKeys()
    {
        var path = Path.GetTempFileName();
        try
        {
            var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
            await using var first = new SqliteConnection(connection);
            await using var second = new SqliteConnection(connection);
            using var db = new SqliteDataContext(first, new DataContextBuilder());
            await ExerciseView(db, first, second, "main", "temp", false);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Postgres_ViewShadowsPermanentTableAndTracksCurrentKeys()
    {
        var connection = Environment.GetEnvironmentVariable(PostgresContainer.ConnectionStringVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "PostgreSQL external test endpoint is not configured.");
        await using var first = new NpgsqlConnection(connection);
        await using var second = new NpgsqlConnection(connection);
        using var db = new PostgresDataContext(first, new DataContextBuilder());
        await ExerciseView(db, first, second, "public", "pg_temp", true);
    }

    [Fact]
    public async Task Sqlite_AsTempViewCreatesReadsAndDropsInOneBatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new SqliteDataContext(connection, new DataContextBuilder());
        await ExerciseDeferredView(db, connection, "temp", "sqlite_temp_master", "type = 'view' and name");
    }

    [Fact]
    public async Task Postgres_AsTempViewCreatesReadsAndDropsInOneBatch()
    {
        var endpoint = Environment.GetEnvironmentVariable(PostgresContainer.ConnectionStringVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(endpoint), "PostgreSQL external test endpoint is not configured.");
        await using var connection = new NpgsqlConnection(endpoint);
        using var db = new PostgresDataContext(connection, new DataContextBuilder());
        await ExerciseDeferredView(db, connection, "pg_temp", "pg_class",
            "relnamespace = pg_my_temp_schema() and relkind = 'v' and relname");
    }

    private static async Task ExerciseDeferredView(DataContext db, DbConnection connection,
        string tempSchema, string catalog, string viewNameSelector)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var definition = db.From<DeferredSource>().WithSchema(tempSchema)
            .Select(t => new { projected_id = t.Id });
        var inner = definition.AsTempView();
        var outer = db.From(inner).Select(t => new { id = t.GetInt32("projected_id") }).AsTempView();
        var read = db.From(outer).OrderBy(t => t.GetInt32("id")).Select(t => t.GetInt32("id"));

        // The definition reads a table that is created by the explicit batch, not before it runs.
        var batch = db.CreateBatchBuilder()
            .Raw($"create temp table {DeferredSourceName} (id int not null)")
            .Raw($"insert into {tempSchema}.{DeferredSourceName} (id) values (2), (1)")
            .Query(read);
        (await batch.ToListAsync(cancellationToken)).Should().Equal(1, 2);

        await using var objects = connection.CreateCommand();
        objects.CommandText = $"select count(*) from {catalog} where {viewNameSelector} in ('{inner.Name}', '{outer.Name}')";
        Convert.ToInt64(await objects.ExecuteScalarAsync(cancellationToken)).Should().Be(0);

        await Execute(connection, null, $"insert into {tempSchema}.{DeferredSourceName} (id) values (3)", cancellationToken);
        read.ToList().Should().Equal(1, 2, 3);
        await foreach (var first in read.ToAsyncEnumerable(cancellationToken))
        {
            first.Should().Be(1);
            break;
        }
        Convert.ToInt64(await objects.ExecuteScalarAsync(cancellationToken)).Should().Be(0);
        await Execute(connection, null, $"drop table {tempSchema}.{DeferredSourceName}", cancellationToken);
    }

    private static async Task ExerciseView(DataContext db, DbConnection first, DbConnection second,
        string permanentSchema, string tempSchema, bool postgres)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.OpenAsync(cancellationToken);
        await second.OpenAsync(cancellationToken);
        var manager = (ITransactionManager)db;
        await using var transaction = await first.BeginTransactionAsync(cancellationToken);
        manager.UseTransaction(transaction);
        try
        {
            var permanentTable = $"{permanentSchema}.{TableName}";
            var keyTable = $"{tempSchema}.{KeysName}";
            await Execute(first, transaction, $"create table {permanentTable} (id int not null, ignored int not null)", cancellationToken);
            await Execute(first, transaction, $"insert into {permanentTable} values (1, 10), (2, 20)", cancellationToken);
            await Execute(first, transaction, $"create temp table {KeysName} (id int not null)", cancellationToken);
            await Execute(first, transaction, $"insert into {keyTable} values (1)", cancellationToken);

            var permanent = db.From<Message>().WithSchema(permanentSchema);
            var keys = db.From<SelectedKey>().WithSchema(tempSchema);
            var definition = db.From(permanent.Select(row => new { row.Id }))
                .Join(keys.Select(row => new { row.Id }), (message, key) => message.Id == key.Id)
                .Select(pair => new { pair.Item1.Id });
            await definition.ToTempView(TableName, cancellationToken);
            db.From<Message>().Select(row => row.Id).ToList().Should().Equal(1);

            await Execute(first, transaction, $"delete from {keyTable}", cancellationToken);
            await Execute(first, transaction, $"insert into {keyTable} values (2)", cancellationToken);
            db.From<Message>().Select(row => row.Id).ToList().Should().Equal(2);
            permanent.Select(row => row.Id).ToList().Should().BeEquivalentTo(new[] { 1, 2 });

            const string escapedName = "nextorm 413 \"projected";
            await definition.ToTempView(escapedName, cancellationToken);
            await using (var read = first.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "select * from \"nextorm 413 \"\"projected\"";
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                reader.FieldCount.Should().Be(1);
                (await reader.ReadAsync(cancellationToken)).Should().BeTrue();
                reader.GetInt32(0).Should().Be(2);
                (await reader.ReadAsync(cancellationToken)).Should().BeFalse();
            }

            await using var absent = second.CreateCommand();
            // Query the second physical session's temporary namespace, never the creator's.
            absent.CommandText = postgres
                ? $"select count(*) from pg_class where relnamespace = pg_my_temp_schema() and relkind = 'v' and relname = '{TableName}'"
                : $"select count(*) from sqlite_temp_master where type = 'view' and name = '{TableName}'";
            (await absent.ExecuteScalarAsync(cancellationToken)).Should().Be(0L);
        }
        finally
        {
            manager.UseTransaction(null);
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static async Task Execute(DbConnection connection, DbTransaction? transaction, string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [SqlTable(TableName)]
    private sealed class Message
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable(KeysName)]
    private sealed class SelectedKey
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable(DeferredSourceName)]
    private sealed class DeferredSource
    {
        [Column("id")]
        public int Id { get; set; }
    }
}
