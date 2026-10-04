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
            var result = ctx.CreateBatchBuilder()
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
            var result = await ctx.CreateBatchBuilder()
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
            var result = ctx.CreateBatchBuilder()
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
            var result = ctx.CreateBatchBuilder()
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
            var result = ctx.CreateBatchBuilder()
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
            var result = ctx.CreateBatchBuilder()
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
            var result = ctx.CreateBatchBuilder()
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
            var batch = ctx.CreateBatchBuilder();
            batch.AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id));

            var act = () => batch.Delete(ctx.CreateDeleteBuilder<ISimpleEntity>().Where(x => x.Id == 1));

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
            var batch = ctx.CreateBatchBuilder();
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
            var batch = ctx.CreateBatchBuilder();
            batch.Delete(ctx.CreateDeleteBuilder<ISimpleEntity>().Where(x => x.Id == 1));

            var act = () => batch.Execute();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_ShouldYieldCursorsWithIndexFieldCountColumnNamesAndRows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .AddQuery(ctx.From<IBatchMixedEntity>().OrderBy(x => x.Id).Select(x => x.Label!))
                .Execute();

            var indexes = new List<int>();
            var fieldCounts = new List<int>();
            var columnNames = new List<string>();
            List<int>? first = null;
            List<string>? second = null;

            foreach (var set in result)
            {
                indexes.Add(set.Index);
                fieldCounts.Add(set.FieldCount);
                columnNames.Add(set.ColumnNames[0]);
                if (set.Index == 0)
                    first = set.Read<int>().ToList();
                else
                    second = set.Read<string>().ToList();
            }

            indexes.Should().Equal(0, 1);
            fieldCounts.Should().Equal(1, 1);
            columnNames.Should().Equal("id", "label");
            first.Should().Equal(2);
            second.Should().Equal("one", "two");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EnumerateAsync_ShouldYieldCursorsInOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = await ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .ExecuteAsync(TestContext.Current.CancellationToken);

            var sets = new List<List<int>>();
            await foreach (var set in result)
                sets.Add(set.Read<int>().ToList());

            sets.Should().HaveCount(2);
            sets[0].Should().Equal(1, 2);
            sets[1].Should().Equal(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_SecondEnumeration_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            foreach (var _ in result)
            {
            }

            var act = () => result.GetEnumerator();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PositionalRead_ThenEnumerate_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            result.Read<int>();

            var act = () => result.GetEnumerator();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_ThenPositionalRead_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            foreach (var _ in result)
            {
            }

            var act = () => result.Read<int>();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_CursorReadTwice_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            var act = () =>
            {
                foreach (var set in result)
                {
                    _ = set.Read<int>();
                    _ = set.Read<int>();
                }
            };

            act.Should().Throw<InvalidOperationException>().WithMessage("*already been read*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_CursorWrongType_ShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().Select(x => x.Id))
                .Execute();

            var act = () =>
            {
                foreach (var set in result)
                    _ = set.Read<string>();
            };

            act.Should().Throw<InvalidOperationException>().WithMessage("*projects*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_CursorFromAdvancedSet_IsStale()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id))
                .Execute();

            ResultSet captured = default;
            var act = () =>
            {
                foreach (var set in result)
                {
                    if (set.Index == 0)
                    {
                        captured = set;
                        continue;
                    }

                    _ = captured.Read<int>();
                }
            };

            act.Should().Throw<InvalidOperationException>().WithMessage("*stale*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Enumerate_CursorReadAsyncNotEnumerated_DoesNotConsumeSet()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.CreateBatchBuilder()
                .AddQuery(ctx.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id))
                .Execute();

            foreach (var set in result)
            {
                _ = set.ReadAsync<int>(TestContext.Current.CancellationToken);
                set.Read<int>().Should().Equal(1, 2);
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
