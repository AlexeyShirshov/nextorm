using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for the one-shot <see cref="ProcedureResult.ReadSets"/> /
/// <see cref="ProcedureResult.ReadSetsAsync"/> traversal and the <see cref="ResultSet"/> cursors it
/// yields, against a real SQLite database.
/// </summary>
public class RawResultSetTraversalTests
{
    private static (IDataContext Context, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-sets-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
        }

        return (new SqliteDataContext($"Data Source={path}", new DataContextBuilder()), path);
    }

    [Fact]
    public void ReadSets_YieldsSetsWithIndexFieldCountAndStableColumnNames()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a, 2 as b; select 3 as c");

            var indices = new List<int>();
            var fields = new List<int>();
            var names = new List<IReadOnlyList<string>>();
            var values = new List<int>();
            foreach (var set in result)
            {
                indices.Add(set.Index);
                fields.Add(set.FieldCount);
                names.Add(set.ColumnNames);
                values.Add(set.Read<int>()[0]);
            }

            // Metadata snapshots stay readable after the traversal advanced past their set.
            indices.Should().Equal(0, 1);
            fields.Should().Equal(2, 1);
            names[0].Should().Equal("a", "b");
            names[1].Should().Equal("c");
            values.Should().Equal(1, 3);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_SkipsLeadingAndTrailingColumnLessSets_WithoutCountingThem()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw(
                "create table rs_skip (id integer); select 5 as a; insert into rs_skip (id) values (1)");

            var indices = new List<int>();
            var counts = new List<int>();
            foreach (var set in result)
            {
                indices.Add(set.Index);
                counts.Add(set.Read<int>().Count);
            }

            indices.Should().Equal(0);
            counts.Should().Equal(1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_ZeroRowColumnBearingSet_IsStillYielded()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a where 0; select 2 as b");

            var fields = new List<int>();
            var counts = new List<int>();
            var values = new List<int>();
            foreach (var set in result)
            {
                fields.Add(set.FieldCount);
                counts.Add(set.Read<int>().Count);
                values.Add(set.Index);
            }

            fields.Should().Equal(1, 1);
            counts.Should().Equal(0, 1);
            values.Should().Equal(0, 1);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_NoColumnBearingSets_IsEmpty()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("create table rs_empty (id integer)");

            result.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_UnreadRowsOfCurrentSet_AreAutoSkippedOnAdvance()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a union all select 10; select 2 as b");

            using var enumerator = result.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            // Deliberately do not consume set 0's rows.
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Read<int>().Should().Equal(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_CursorFromAdvancedSet_IsStale()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            using var enumerator = result.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            var first = enumerator.Current;
            enumerator.MoveNext().Should().BeTrue();

            Action act = () => first.Read<int>();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_SecondReadOfSameSet_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a");

            using var enumerator = result.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            var cursor = enumerator.Current;
            cursor.Read<int>().Should().Equal(1);

            Action act = () => cursor.Read<int>();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_AfterLegacyRead_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");
            result.Read<int>().Should().Equal(1);

            Action act = () => result.GetEnumerator();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyRead_AfterReadSets_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            using var enumerator = result.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();

            Action act = () => result.Read<int>();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_ReEnumeration_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");
            IEnumerable<ResultSet> sets = result;
            sets.ToList();

            Action act = () => sets.ToList();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_OuterEnumeratorDisposal_InvalidatesCursorButKeepsOutputsReadable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            ResultSet cursor;
            using (var enumerator = result.GetEnumerator())
            {
                enumerator.MoveNext().Should().BeTrue();
                cursor = enumerator.Current;
            }

            Action act = () => cursor.Read<int>();
            act.Should().Throw<InvalidOperationException>();

            // The outer iteration ended, so outputs remain reachable and the result stays usable.
            result.OutputParameters.Should().BeEmpty();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_OutputsDuringActiveTraversal_AreRejected()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            Action act = () =>
            {
                foreach (var set in result)
                {
                    set.Index.Should().Be(0);
                    _ = result.OutputParameters;
                }
            };

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DefaultResultSetCursor_Read_Throws()
    {
        Action act = () => default(ResultSet).Read<int>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadSetsAsync_YieldsSetsAndLazyReadAsync()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            var indices = new List<int>();
            var names = new List<IReadOnlyList<string>>();
            var values = new List<int>();
            await foreach (var set in result.WithCancellation(ct))
            {
                indices.Add(set.Index);
                names.Add(set.ColumnNames);
                await foreach (var value in set.ReadAsync<int>(ct))
                    values.Add(value);
            }

            indices.Should().Equal(0, 1);
            names[0].Should().Equal("a");
            names[1].Should().Equal("b");
            values.Should().Equal(1, 2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // D1: a returned-but-never-enumerated sequence must not block output parameters. Activation
    // happens on the first MoveNext/MoveNextAsync, not when ReadSets/ReadSetsAsync is called.
    [Fact]
    public void ReadSets_NotEnumerated_OutputsAvailable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            _ = result.GetEnumerator(); // one-shot sequence is never enumerated

            result.OutputParameters.Should().BeEmpty();
            result.ReturnValue.Should().BeNull();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_NotEnumerated_OutputsAvailable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            _ = result.WithCancellation(ct); // one-shot sequence is never enumerated

            result.OutputParameters.Should().BeEmpty();
            result.ReturnValue.Should().BeNull();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_OutputsReadAfterCall_EnumerationThrows_NoResurrection()
    {
        var (ctx, path) = CreateDb();
        try
        {
            using var result = ctx.ExecuteRaw("select 1 as a; select 2 as b");

            IEnumerable<ResultSet> sets = result;
            // Draining outputs after the sequence was obtained closes the reader; enumerating the
            // already-returned sequence afterwards must fail instead of reviving the closed reader.
            result.OutputParameters.Should().BeEmpty();

            Action act = () => sets.ToList();

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // D4: an inner async read that finishes (or is disposed) after the outer cursor advanced must
    // not reset the owner's current-set index and thereby stale the new current cursor.
    [Fact]
    public async Task ReadSetsAsync_InnerRead_DoesNotStaleCurrentCursor()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            var outer = result.GetAsyncEnumerator(ct);
            try
            {
                (await outer.MoveNextAsync()).Should().BeTrue();
                var inner = outer.Current.ReadAsync<int>(ct).GetAsyncEnumerator(ct);
                try
                {
                    (await inner.MoveNextAsync()).Should().BeTrue();
                    inner.Current.Should().Be(1);
                }
                finally
                {
                    // Finish the previous set's inner read only after the outer set is current again.
                    await outer.MoveNextAsync();
                    await inner.DisposeAsync();
                }

                var values = new List<int>();
                await foreach (var value in outer.Current.ReadAsync<int>(ct))
                    values.Add(value);

                values.Should().Equal(2);
            }
            finally
            {
                await outer.DisposeAsync();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_OuterAdvance_StalesPreviousCursor()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            var outer = result.GetAsyncEnumerator(ct);
            try
            {
                (await outer.MoveNextAsync()).Should().BeTrue();
                var first = outer.Current;

                (await outer.MoveNextAsync()).Should().BeTrue();

                Func<Task> act = async () =>
                {
                    await foreach (var _ in first.ReadAsync<int>(ct))
                    {
                    }
                };

                await act.Should().ThrowAsync<InvalidOperationException>();
            }
            finally
            {
                await outer.DisposeAsync();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_SecondReadOfSameSet_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a", [], ct);

            var outer = result.GetAsyncEnumerator(ct);
            try
            {
                (await outer.MoveNextAsync()).Should().BeTrue();
                var cursor = outer.Current;

                var values = new List<int>();
                await foreach (var value in cursor.ReadAsync<int>(ct))
                    values.Add(value);
                values.Should().Equal(1);

                Func<Task> act = async () =>
                {
                    await foreach (var _ in cursor.ReadAsync<int>(ct))
                    {
                    }
                };

                await act.Should().ThrowAsync<InvalidOperationException>();
            }
            finally
            {
                await outer.DisposeAsync();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_ReEnumeration_Throws()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            IAsyncEnumerable<ResultSet> sets = result;
            await foreach (var set in sets)
            {
                await foreach (var _ in set.ReadAsync<int>(ct))
                {
                }
            }

            Func<Task> act = async () =>
            {
                await foreach (var _ in sets)
                {
                }
            };

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSets_OwnerDisposed_CursorRead_ThrowsObjectDisposed()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var result = ctx.ExecuteRaw("select 1 as a");
            using var enumerator = result.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            var cursor = enumerator.Current;

            result.Dispose();

            Action act = () => cursor.Read<int>();

            act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_OwnerDisposed_CursorReadAsync_ThrowsObjectDisposed()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a", [], ct);

            var outer = result.GetAsyncEnumerator(ct);
            (await outer.MoveNextAsync()).Should().BeTrue();
            var cursor = outer.Current;

            await result.DisposeAsync();

            Func<Task> act = async () =>
            {
                await foreach (var _ in cursor.ReadAsync<int>(ct))
                {
                }
            };

            await act.Should().ThrowAsync<ObjectDisposedException>();
            await outer.DisposeAsync();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSetsAsync_Cancelled_RestartForbidden()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await using var result = await ctx.ExecuteRawAsync("select 1 as a; select 2 as b", [], ct);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = async () =>
            {
                await foreach (var _ in result.WithCancellation(cts.Token))
                {
                }
            };

            await act.Should().ThrowAsync<OperationCanceledException>();

            // The cancelled traversal consumed the single allowed traversal; restart is rejected.
            Action restart = () => result.GetEnumerator();
            restart.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
