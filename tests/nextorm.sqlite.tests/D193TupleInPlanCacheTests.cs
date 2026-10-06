using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D193 W1 — plan-cache safety of a tuple value-list <c>IN</c>/<c>Contains</c> (#193). Only a tuple list
/// in WHERE/PREWHERE is folded into the plan key (<c>InValuesShapeHash</c>) and stays cacheable. A tuple
/// list in HAVING, a JOIN condition or a SELECT column is not keyed, so the command must be flagged and
/// its call-local cache suppressed instead of reusing a stale plan. These tests drive a real temp-file
/// SQLite database (the placeholder <c>:memory:</c> context cannot execute).
/// </summary>
public class D193TupleInPlanCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d193cache-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table simple_entity (id integer primary key);" +
                "insert into simple_entity (id) values (1),(2),(3);" +
                "create table complex_entity (id integer primary key, nullableint integer, somestring text);" +
                "insert into complex_entity (id, nullableint, somestring) values (1, 10, 'a'),(2, null, 'b'),(3, 30, 'c');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, Ct);

    private static int ParamCount<T>(DbPreparedQueryCommand<T> command)
        => command.DbCommandParams.Cast<DbParameter>().Count();

    // A field (not a fresh local per call) so the two shapes share the exact same captured member
    // (`this._tuples`): the plan-key comparer keys a captured closure member by its name, so a
    // different local name would itself change the plan key and mask the flag under test.
    private List<(long, int?)> _tuples = [];
    private List<(int, int?)> _joinTuples = [];

    private static QueryCommand<long> WhereTuple(EntityBuilder<IComplexEntity> e, List<(long, int?)> tuples)
        => e.Where(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => x.Id);

    private static QueryCommand<long> WhereScalar(EntityBuilder<IComplexEntity> e)
        => e.Where(x => x.Id == 1).Select(x => x.Id);

    private QueryCommand<long> HavingFieldTuple(EntityBuilder<IComplexEntity> e)
        => e.GroupBy(x => x.Id)
            .Having(x => _tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
            .Select(x => x.Id);

    private QueryCommand<int> JoinFieldTuple(EntityBuilder<ISimpleEntity> simple, EntityBuilder<IComplexEntity> complex)
        => simple.Join<IComplexEntity>(complex, (s, c) => _joinTuples.Contains(new ValueTuple<int, int?>(s.Id, c.Int)))
            .Select(p => p.Item1.Id);

    private QueryCommand<bool> SelectFieldTuple(EntityBuilder<IComplexEntity> e)
        => e.Select(x => _tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int)));

    private QueryCommand<long> NestedHavingFieldTuple(EntityBuilder<IComplexEntity> e, IDataContext ctx)
        => e.Select(x => ctx.From<IComplexEntity>()
            .GroupBy(c => c.Id)
            .Having(c => _tuples.Contains(new ValueTuple<long, int?>(c.Id, c.Int)))
            .Select(c => c.Id)
            .First());

    private QueryCommand<long> WhereFieldTuple(EntityBuilder<IComplexEntity> e)
        => e.Where(x => _tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => x.Id);

    // ------------------------------------------------------------------ unkeyed clauses: must rebuild

    [Fact]
    public void TupleContains_InHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // Both builds capture the SAME field (`this._tuples`), so only the collection shape differs;
            // without the unkeyed-tuple flag the plan key would be identical and the second build could
            // reuse the first SQL.
            _tuples = twoRows;
            var first = Prepare(ctx, HavingFieldTuple(e));
            ParamCount(first).Should().Be(4, "the first HAVING shape has two two-component rows");

            _tuples = oneNullRow;
            var second = Prepare(ctx, HavingFieldTuple(e));

            // A stale plan would have reused the 4-parameter SQL; the null-component row emits a
            // different (smaller) parameter set, so a fresh build has a different parameter count.
            ParamCount(second).Should().Be(1, "the null-component HAVING shape only parameterises the non-null id cell");
            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in HAVING is not folded into the plan key, so it must not be served from the cache");

            ctx.ToList(second).OrderBy(x => x).Should().Equal(2L);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_InJoinOn_DifferentSizes_ShouldRebuildAndReturnCurrentResult()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var simple = ctx.From<ISimpleEntity>();
            var complex = ctx.From<IComplexEntity>();

            var twoRows = new List<(int, int?)> { (1, 10), (3, 30) };
            var oneNullRow = new List<(int, int?)> { (2, null) };

            // Same captured field (`this._joinTuples`) for both builds: the JOIN condition is unkeyed, so
            // only the flag can distinguish the two shapes.
            _joinTuples = twoRows;
            var first = Prepare(ctx, JoinFieldTuple(simple, complex));
            ParamCount(first).Should().Be(4);

            _joinTuples = oneNullRow;
            var second = Prepare(ctx, JoinFieldTuple(simple, complex));
            ParamCount(second).Should().Be(1);

            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in a JOIN condition is not folded into the plan key");
            ctx.ToList(second).OrderBy(x => x).Should().Equal(2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_InSelectColumn_DifferentSizes_ShouldRebuildAndReturnCurrentResult()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // Same captured field for both projections.
            _tuples = twoRows;
            var first = Prepare(ctx, SelectFieldTuple(e));
            ParamCount(first).Should().Be(4);

            _tuples = oneNullRow;
            var second = Prepare(ctx, SelectFieldTuple(e));
            ParamCount(second).Should().Be(1);

            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in a SELECT column is not folded into the plan key");
            ctx.ToList(second).Should().Equal(false, true, false);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_InNestedReferencedHaving_DifferentSizes_ShouldRebuild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // The tuple list sits in the HAVING of a nested referenced (scalar) subquery; the owning
            // command must visit the referenced command recursively and flag the unkeyed tuple so its
            // call-local cache is suppressed. Unlike an EXISTS projection, the scalar subquery renders
            // the inner GROUP BY/HAVING, so the nested tuple shape is actually exercised. Both builds use
            // the SAME captured field (`this._tuples`), so only the shape differs.
            _tuples = twoRows;
            var first = Prepare(ctx, NestedHavingFieldTuple(e, ctx));
            ParamCount(first).Should().Be(4, "the nested HAVING shape has two two-component rows");

            _tuples = oneNullRow;
            var second = Prepare(ctx, NestedHavingFieldTuple(e, ctx));
            ParamCount(second).Should().Be(1, "the null-component nested HAVING shape only parameterises the non-null id cell");

            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in a nested referenced query's HAVING is not folded into the owning plan key");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------ WHERE: shape-hashed => cacheable

    [Fact]
    public void TupleContains_InWhere_SecondIdenticalCall_ShouldBeCacheHit()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            var tuples = new List<(long, int?)> { (1, 10), (2, 20) };

            var first = Prepare(ctx, WhereTuple(e, tuples));
            var second = Prepare(ctx, WhereTuple(e, tuples));

            ReferenceEquals(first, second).Should().BeTrue(
                "a tuple IN in WHERE has its shape folded into the plan key, so the plan is reusable");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// F12: exercises the real plan-cache path (<c>storeInCache: true</c>) for a WHERE tuple
    /// <c>Contains</c> whose collection shape changes between calls, reusing the SAME captured field
    /// (<c>this._tuples</c>) so only the shape differs. The shape is folded into the plan key, so the
    /// changed shape must be a miss that rebuilds the SQL/parameters and returns the current rows, while
    /// an unchanged shape and the scalar path stay cacheable. The <c>D193TupleInSqlGenerationTests</c>
    /// sequential-shape test builds with <c>storeInCache: false</c>, so it never reaches this path.
    /// </summary>
    [Fact]
    public void WhereTuple_ShapeChangeThroughCache_ShouldRebindAndKeepScalarCacheable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            _tuples = [(1, 10), (3, 30)];
            var first = Prepare(ctx, WhereFieldTuple(e));
            ParamCount(first).Should().Be(4, "two two-component rows parameterise four cells");
            ctx.ToList(first).OrderBy(x => x).Should().Equal(1L, 3L);

            // Same captured member / expression tree, different shape: the plan key changes, so the
            // second call must miss and rebuild rather than reuse the stale two-row SQL.
            _tuples = [(1, 10)];
            var second = Prepare(ctx, WhereFieldTuple(e));
            ParamCount(second).Should().Be(2, "the changed WHERE shape re-keys the plan and rebuilds the parameters");
            second.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("in (VALUES ($p0, $p1))");
            ReferenceEquals(first, second).Should().BeFalse(
                "the WHERE shape is part of the plan key, so a changed shape is a cache miss");
            ctx.ToList(second).OrderBy(x => x).Should().Equal(1L);

            // The last shape is unchanged: the hit path returns the same cached command.
            var third = Prepare(ctx, WhereFieldTuple(e));
            ReferenceEquals(second, third).Should().BeTrue("an unchanged WHERE tuple shape stays cacheable");

            // The tuple preparations must not poison the scalar path on the same context.
            var scalarFirst = Prepare(ctx, WhereScalar(e));
            var scalarSecond = Prepare(ctx, WhereScalar(e));
            ReferenceEquals(scalarFirst, scalarSecond).Should().BeTrue(
                "the tuple path must not disable the plan cache for later scalar queries on the context");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ScalarQuery_AfterWhereTupleQuery_ShouldStillBeCacheHit()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            var tuples = new List<(long, int?)> { (1, 10) };

            // A tuple query on the context (WHERE shape-hashed) must not poison later scalar queries.
            Prepare(ctx, WhereTuple(e, tuples));

            var first = Prepare(ctx, WhereScalar(e));
            var second = Prepare(ctx, WhereScalar(e));

            ReferenceEquals(first, second).Should().BeTrue(
                "the tuple path must not disable the plan cache for later scalar queries on the context");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SharedAnyCommand_AfterTupleQuery_ShouldStayCacheable()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var tuples = new List<(long, int?)> { (2, null) };

            // Prepare (and execute) a tuple query first, then the shared Any command.
            ctx.ToList(Prepare(ctx, WhereTuple(ctx.From<IComplexEntity>(), tuples)));

            var any = ctx.From<IComplexEntity>()
                .Where(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
                .Any();

            any.Should().BeTrue();
            ctx.AnyCommand.Should().NotBeNull();
            ctx.AnyCommand!.Value.Cache.Should().BeTrue(
                "the tuple value-list path must not set the sticky Cache=false flag on the shared Any command");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_InReferencedQueryHavingOnly_ShouldFlagOuterAndRebuild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // The tuple list lives ONLY in the captured inner command's HAVING. Because the outer
            // projection references the inner command by closure variable, its raw projection expression
            // contains no Contains call, so the outer's own _exp/_having/_joins scans cannot see the
            // tuple: only the recursive descent through _referencedQueries can flag the unkeyed tuple.
            var innerTwo = ctx.From<IComplexEntity>()
                .GroupBy(c => c.Id)
                .Having(c => twoRows.Contains(new ValueTuple<long, int?>(c.Id, c.Int)))
                .Select(c => c.Id);
            var outerTwo = e.Select(x => innerTwo.First());

            var first = Prepare(ctx, outerTwo);

            outerTwo.HasUnkeyedTupleInValues.Should().BeTrue(
                "the unkeyed tuple is reachable only through the referenced inner query, so the recursive scan must flag the outer command");

            var innerOneNull = ctx.From<IComplexEntity>()
                .GroupBy(c => c.Id)
                .Having(c => oneNullRow.Contains(new ValueTuple<long, int?>(c.Id, c.Int)))
                .Select(c => c.Id);
            var outerOneNull = e.Select(x => innerOneNull.First());

            var second = Prepare(ctx, outerOneNull);

            outerOneNull.HasUnkeyedTupleInValues.Should().BeTrue(
                "the recursive scan must flag the outer command for the second (null-component) referenced shape too");

            ParamCount(first).Should().Be(4, "two two-component rows in the referenced HAVING");
            ParamCount(second).Should().Be(1, "the null-component row only parameterises the non-null id cell");
            ReferenceEquals(first, second).Should().BeFalse(
                "the owning command is flagged through the referenced query, so its call-local cache is suppressed");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_RepeatedReferencedQuery_ShouldNotRevisit()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var e = ctx.From<IComplexEntity>();
            var inner = ctx.From<ISimpleEntity>().Where(s => s.Id > 0).Select(s => s.Id);

            // The same referenced command is used twice; the scan must not descend into it a second
            // time (the visited/cycle guard). The inner query has no tuple, so the outer stays keyed.
            var outer = e.Select(x => new { A = inner.First(), B = inner.First() });
            var prepared = Prepare(ctx, outer);

            outer.HasUnkeyedTupleInValues.Should().BeFalse(
                "a repeated reference to a tuple-free subquery must not be flagged");
            ctx.ToList(prepared).Should().HaveCount(3);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------ F1: widened unkeyed scan

    [Fact]
    public void TupleContains_InUnionBranchHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // The tuple list sits only in the UNION branch's HAVING. The root command carries the set
            // operation; its own SELECT/HAVING/JOIN probes miss the branch, so only recursing into
            // _union can flag the unkeyed tuple. The branch's HAVING expression shape is identical
            // between the two builds (the captured collection changes), so the root plan key would be
            // reused without the widening.
            QueryCommand<long> Build(List<(long, int?)> tuples)
                => ctx.From<IComplexEntity>().Where(x => x.Id >= 3).Select(x => x.Id)
                    .Union(ctx.From<IComplexEntity>()
                        .GroupBy(x => x.Id)
                        .Having(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
                        .Select(x => x.Id));

            var firstCmd = Build(twoRows);
            var first = Prepare(ctx, firstCmd);
            firstCmd.HasUnkeyedTupleInValues.Should().BeTrue(
                "the tuple is reachable only through the UNION branch, so the scan must recurse into _union");
            ctx.ToList(first).OrderBy(x => x).Should().Equal(1L, 3L);

            var secondCmd = Build(oneNullRow);
            var second = Prepare(ctx, secondCmd);
            secondCmd.HasUnkeyedTupleInValues.Should().BeTrue(
                "the tuple is reachable only through the UNION branch, so the scan must recurse into _union");
            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in a UNION branch is not folded into the root plan key");

            ctx.ToList(second).OrderBy(x => x).Should().Equal(2L, 3L);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TupleContains_InDerivedTableHaving_DifferentSizes_ShouldRebuildAndReturnCurrentResult()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var twoRows = new List<(long, int?)> { (1, 10), (2, 20) };
            var oneNullRow = new List<(long, int?)> { (2, null) };

            // The tuple list sits only in the HAVING of the derived-table subquery, which the root
            // renders inline (SqlSourceRenderer). The root's own _exp/_having/_joins probes miss it, so
            // only recursing into _from.SubQuery can flag the unkeyed tuple.
            QueryCommand<long> Build(List<(long, int?)> tuples)
                => ctx.From(ctx.From<IComplexEntity>()
                        .GroupBy(x => x.Id)
                        .Having(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
                        .Select(x => x.Id))
                    .Select(t => t);

            var firstCmd = Build(twoRows);
            var first = Prepare(ctx, firstCmd);
            firstCmd.HasUnkeyedTupleInValues.Should().BeTrue(
                "the tuple is reachable only through the derived-table subquery, so the scan must recurse into _from.SubQuery");
            ctx.ToList(first).OrderBy(x => x).Should().Equal(1L);

            var secondCmd = Build(oneNullRow);
            var second = Prepare(ctx, secondCmd);
            secondCmd.HasUnkeyedTupleInValues.Should().BeTrue(
                "the tuple is reachable only through the derived-table subquery, so the scan must recurse into _from.SubQuery");
            ReferenceEquals(first, second).Should().BeFalse(
                "a tuple IN in a derived table is not folded into the root plan key");

            ctx.ToList(second).OrderBy(x => x).Should().Equal(2L);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
