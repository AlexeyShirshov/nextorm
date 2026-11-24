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

    private static async Task Execute(DbConnection connection, DbTransaction transaction, string sql,
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
}
