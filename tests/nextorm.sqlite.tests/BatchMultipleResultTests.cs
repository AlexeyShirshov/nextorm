using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Sqlite.Tests;

[SqlTable("batch_mixed_entity")]
public interface IBatchMixedEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>
/// Behavioural coverage for the multi-result batch surface (<c>BatchBuilder.AddQuery</c> plus
/// <c>Execute</c>/<c>ExecuteAsync</c>) against a real SQLite database: every result set of one
/// <c>;</c>-joined command is materialised eagerly and in order by a single <see cref="BatchResult"/>.
/// </summary>
public class BatchMultipleResultTests
{
    private static (IDataContext Context, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-batch-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table simple_entity (id integer primary key);" +
                "insert into simple_entity (id) values (1);" +
                "insert into simple_entity (id) values (2);" +
                "create table batch_mixed_entity (id integer primary key, label text);" +
                "insert into batch_mixed_entity (id, label) values (1, 'one');" +
                "insert into batch_mixed_entity (id, label) values (2, 'two');";
            command.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void Execute_MultipleResultQueries_ShouldMaterialiseEverySetInOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .AddQuery(ctx.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .Execute();

            result.ResultSetCount.Should().Be(2);
            result.Read<int>().Should().Equal(2);
            result.Read<int>().Should().Equal(1, 2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MultipleResultQueries_ShouldMaterialiseEverySetInOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = await ctx.Batch()
                .AddQuery(ctx.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .ExecuteAsync(TestContext.Current.CancellationToken);

            result.ResultSetCount.Should().Be(2);
            result.Read<int>().Should().Equal(1, 2);
            result.Read<int>().Should().Equal(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Execute_ZeroRowResultSetFollowedByNonEmpty_ShouldKeepOrderAndEmptyList()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 100).Select(x => x.Id))
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .Execute();

            result.ResultSetCount.Should().Be(2);
            result.Read<int>().Should().BeEmpty();
            result.Read<int>().Should().Equal(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Execute_MixedTypeResultSets_ShouldReadEachSetWithItsOwnType()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<IBatchMixedEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .AddQuery(ctx.From<IBatchMixedEntity>().OrderBy(x => x.Id).Select(x => x.Label!))
                .Execute();

            result.ResultSetCount.Should().Be(2);
            result.Read<int>().Should().Equal(1, 2);
            result.Read<string>().Should().Equal("one", "two");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WithWrongTypeInSecondPosition_ShouldThrowAndNotAdvance()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<IBatchMixedEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .AddQuery(ctx.From<IBatchMixedEntity>().OrderBy(x => x.Id).Select(x => x.Label!))
                .Execute();

            result.Read<int>().Should().Equal(1, 2);

            var act = () => result.Read<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("*projects*");
            // The failed read must not consume the set: the matching type still reads it.
            result.Read<string>().Should().Equal("one", "two");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WithWrongType_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            var act = () => result.Read<string>();

            act.Should().Throw<InvalidOperationException>().WithMessage("*projects*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_PastLastResultSet_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.Batch()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            result.Read<int>();

            var act = () => result.Read<int>();

            act.Should().Throw<InvalidOperationException>().WithMessage("*already been read*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AddQuery_ThenMutation_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var batch = ctx.Batch();
            batch.AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id));

            var act = () => batch.Delete(ctx.DeleteFrom<ISimpleEntity>().Where(x => x.Id == 1));

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AddQuery_ThenQuery_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var batch = ctx.Batch();
            batch.AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id));

            var act = () => batch.Query(ctx.From<ISimpleEntity>().Select(x => x.Id));

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Execute_WithoutResultQuery_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var batch = ctx.Batch();
            batch.Delete(ctx.DeleteFrom<ISimpleEntity>().Where(x => x.Id == 1));

            var act = () => batch.Execute();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
