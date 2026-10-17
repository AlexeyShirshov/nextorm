using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for the raw-SQL LOB guard: a single-column raw SQL override
/// (<c>WithSql</c>) cannot stream on SQLite, because the dialect appends its <c>rowid</c> locator
/// only to generated SQL. The unsafe shape is rejected up front with a clear
/// <see cref="NotSupportedException"/>; every other raw shape keeps the generic
/// <see cref="InvalidOperationException"/> projection-mismatch error. Runs against a real temp-file
/// SQLite database so the <c>FieldCount</c> guard observes the reader the driver actually returns.
/// </summary>
public class SqliteLobGuardTests
{
    private const string ExpectedRawMessage =
        "SQLite LOB streaming with raw SQL (WithSql) is not supported because a rowid locator cannot be added safely.";

    private static (DataContext Context, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-lob-guard-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table lob_probe (id integer primary key, payload blob, body text);" +
                "insert into lob_probe (id, payload, body) values (1, x'0102', 'hi');";
            command.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    private static void WithDb(Action<DataContext> test)
    {
        var (ctx, path) = CreateDb();
        try
        {
            test(ctx);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RawSqlSingleColumnBinary_ToStream_ShouldThrowNotSupported()
        => WithDb(ctx =>
        {
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!).WithSql("select payload from lob_probe");

            var act = () => command.ToStream();

            act.Should().Throw<NotSupportedException>().WithMessage(ExpectedRawMessage);
        });

    [Fact]
    public async Task RawSqlSingleColumnBinary_ToStreamAsync_ShouldThrowNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!).WithSql("select payload from lob_probe");

            var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedRawMessage);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RawSqlSingleColumnText_ToTextReader_ShouldThrowNotSupported()
        => WithDb(ctx =>
        {
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Body!).WithSql("select body from lob_probe");

            var act = () => command.ToTextReader();

            act.Should().Throw<NotSupportedException>().WithMessage(ExpectedRawMessage);
        });

    [Fact]
    public async Task RawSqlSingleColumnText_ToTextReaderAsync_ShouldThrowNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Body!).WithSql("select body from lob_probe");

            var act = async () => await command.ToTextReaderAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedRawMessage);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RawSqlTwoColumnBinary_ToStream_ShouldThrowInvalidOperation()
        => WithDb(ctx =>
        {
            // A multi-column raw projection is not a single-value terminal regardless of the locator.
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!).WithSql("select payload, id from lob_probe");

            var act = () => command.ToStream();

            act.Should().Throw<InvalidOperationException>();
        });

    [Fact]
    public async Task RawSqlTwoColumnText_ToTextReaderAsync_ShouldThrowInvalidOperation()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var command = ctx.From<ILobProbeEntity>().Select(x => x.Body!).WithSql("select body, id from lob_probe");

            var act = async () => await command.ToTextReaderAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [SqlTable("lob_probe")]
    internal interface ILobProbeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("payload")]
        byte[]? Payload { get; set; }
        [Column("body")]
        string? Body { get; set; }
    }
}
