using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// The plan cache is keyed by <see cref="QueryPlan"/> (a <see cref="QueryCommand"/> compared with
/// <see cref="QueryPlanEqualityComparer"/>). If two logically different queries ever produce the same
/// key, a cached plan built for one is reused for the other and the wrong SQL/materializer is served -
/// a bug that only shows up on repeated calls and is very hard to reproduce.
///
/// These tests build a broad matrix of query variants, prepare every one of them, and assert that:
/// <list type="bullet">
/// <item>distinct variants are never considered equal (no key intersection);</item>
/// <item>a structurally identical rebuild is considered equal (the key is stable, so the cache hits);</item>
/// <item>equality implies equal hashes (the <see cref="IEqualityComparer{T}"/> contract);</item>
/// <item><see cref="QueryCommand.CloneForCache"/> preserves the key (otherwise a stored plan could not
///   be found by an equivalent rebuild, or the Debug.Assert in <see cref="QueryPlan.GetCacheVersion"/> would fail).</item>
/// </list>
/// Every clause that participates in the key gets a variant, including the provider-specific ones
/// (LIMIT BY, DISTINCT ON, SETTINGS, FINAL, PREWHERE, ARRAY JOIN, TABLESAMPLE, FOR SYSTEM_TIME,
/// FOR JSON/XML, PIVOT/UNPIVOT, xml.nodes): the builder sets them on any context, and the comparer is
/// provider independent, so a single SQLite context exercises the whole key surface.
/// </summary>
public class PlanKeyUniquenessTests
{
    /// <summary>The common result shape every variant projects, so the comparison walks past the early
    /// ResultType guard and reaches the clause-specific branches.</summary>
    public sealed class PKRow
    {
        public long? Id { get; set; }
        public int? Int { get; set; }
    }

    [SqlTable("array_entity")]
    public interface IArrayEntity
    {
        [Column("id")] long Id { get; set; }

        [Column("tags")] List<int>? Tags { get; set; }
    }

    public interface ITvfRow
    {
        long Id { get; set; }
        int? Int { get; set; }
    }

    public static class PkTvf
    {
        [SqlTableFunction("pk_rows")]
        public static IQueryable<ITvfRow> Rows() => throw new NotSupportedException();

        [SqlTableFunction("pk_rows_by_id")]
        public static IQueryable<ITvfRow> RowsById(long id) => throw new NotSupportedException();
    }

    private static EntityBuilder<IComplexEntity> E(IDataContext ctx) => ctx.From<IComplexEntity>();

    private static QueryCommand Row(EntityBuilder<IComplexEntity> e)
        => e.Select(x => new PKRow { Id = x.Id, Int = x.Int });

    /// <summary>
    /// All query variants that must have a unique plan key. Each builds a fresh command so the matrix
    /// compares independent instances, exactly like two separate query call sites.
    /// </summary>
    public static IEnumerable<(string Name, Func<IDataContext, QueryCommand> Build)> Variants()
    {
        // Projection / source
        yield return ("base", ctx => Row(E(ctx)));
        yield return ("base-scalar", ctx => E(ctx).Select(x => x.Id));
        yield return ("base-identity", ctx => E(ctx).Select(x => x));

        // Query tag
        yield return ("tag-a", ctx => Row(E(ctx).WithTag("a")));
        yield return ("tag-b", ctx => Row(E(ctx).WithTag("b")));

        // WHERE
        yield return ("where-gt0", ctx => Row(E(ctx).Where(x => x.Id > 0)));
        yield return ("where-gt1", ctx => Row(E(ctx).Where(x => x.Id > 1)));
        yield return ("where-int-null", ctx => Row(E(ctx).Where(x => x.Int != null)));

        // DISTINCT / DISTINCT ON
        yield return ("distinct", ctx => Row(E(ctx).Distinct()));
        yield return ("distinct-on-id", ctx => Row(E(ctx).DistinctOn(x => x.Id)));
        yield return ("distinct-on-int", ctx => Row(E(ctx).DistinctOn(x => x.Int)));

        // ORDER BY
        yield return ("orderby-id", ctx => Row(E(ctx).OrderBy(x => x.Id)));
        yield return ("orderby-id-desc", ctx => Row(E(ctx).OrderByDescending(x => x.Id)));
        yield return ("orderby-int", ctx => Row(E(ctx).OrderBy(x => x.Int)));
        yield return ("orderby-col0", ctx => Row(E(ctx).OrderBy(1)));
        yield return ("orderby-col1", ctx => Row(E(ctx).OrderBy(2)));

        // Paging
        yield return ("limit", ctx => Row(E(ctx).Limit(3)));
        yield return ("limit-offset", ctx => Row(E(ctx).Limit(3).Offset(2)));
        yield return ("offset", ctx => Row(E(ctx).Offset(2)));
        yield return ("with-ties", ctx => Row(E(ctx).Limit(3).WithTies()));

        // GROUP BY / HAVING
        yield return ("group-int", ctx => Row(E(ctx).GroupBy(x => x.Int)));
        yield return ("group-rollup", ctx => Row(E(ctx).GroupByRollup(x => x.Int)));
        yield return ("group-cube", ctx => Row(E(ctx).GroupByCube(x => x.Int)));
        yield return ("group-sets", ctx => Row(E(ctx).GroupByGroupingSets(x => new { x.Int }, [0], [1])));
        yield return ("group-totals", ctx => Row(E(ctx).GroupBy(x => x.Int).WithTotals()));
        yield return ("having", ctx => Row(E(ctx).GroupBy(x => x.Int).Having(x => SqlFunctions.Sql.count() > 1)));

        // Named windows
        yield return ("window", ctx => Row(E(ctx).Window("w", orderBy: [E(ctx).Asc(x => x.Id)])));
        yield return ("window-other", ctx => Row(E(ctx).Window("v", orderBy: [E(ctx).Asc(x => x.Id)])));
        yield return ("window-desc", ctx => Row(E(ctx).Window("w", orderBy: [E(ctx).Desc(x => x.Id)])));
        yield return ("window-frame", ctx => Row(E(ctx).Window("w", orderBy: [E(ctx).Asc(x => x.Id)], frame: WindowFrame.Groups(1, 1))));

        // JOINs
        yield return ("join-inner", ctx => E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
        yield return ("join-left", ctx => E(ctx).LeftJoin(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
        yield return ("join-cross", ctx => E(ctx).CrossJoin(ctx.From<ISimpleEntity>())
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
        yield return ("join-condition", ctx => E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id != b.Id)
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));

        // Set operations
        yield return ("union", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Union(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));
        yield return ("unionall", ctx => E(ctx).Select(x => x.Id).UnionAll(E(ctx).Select(x => x.Id)));
        yield return ("intersect", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Intersect(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));
        yield return ("except", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Except(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));

        // Derived sources / CTEs
        yield return ("derived", ctx => ctx.From(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .Select(t => new PKRow { Id = t.Id, Int = t.Int }));
        yield return ("cte", ctx => ctx.With("c", E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .From("c").Select(t => new PKRow { Id = t.GetNullableInt64("Id"), Int = t.GetNullableInt32("Int") }));
        yield return ("cte-other-name", ctx => ctx.With("d", E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .From("d").Select(t => new PKRow { Id = t.GetNullableInt64("Id"), Int = t.GetNullableInt32("Int") }));
        yield return ("cte-other-body", ctx => ctx.With("c", E(ctx).Where(x => x.Id > 0).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .From("c").Select(t => new PKRow { Id = t.GetNullableInt64("Id"), Int = t.GetNullableInt32("Int") }));
        yield return ("cte-recursive", ctx => ctx.WithRecursive("n",
                E(ctx).Where(x => x.Id == 1).Select(x => new PKRow { Id = x.Id, Int = x.Int })
                    .UnionAll(ctx.From("n").Select(t => new PKRow { Id = t.GetNullableInt64("Id") + 1, Int = null })))
            .From("n").Select(t => new PKRow { Id = t.GetNullableInt64("Id"), Int = t.GetNullableInt32("Int") }));

        // Correlated subquery (outer references)
        yield return ("correlated", ctx =>
        {
            var outer = E(ctx);
            var inner = ctx.From<ISimpleEntity>();
            return Row(outer.Where(x => x.Id == inner.Where(s => s.Id == x.Id).Select(s => s.Id).First()));
        });
        yield return ("correlated-other", ctx =>
        {
            var outer = E(ctx);
            var inner = ctx.From<ISimpleEntity>();
            return Row(outer.Where(x => x.Id == inner.Where(s => s.Id != x.Id).Select(s => s.Id).First()));
        });

        // Provider-specific clauses
        yield return ("limitby", ctx => Row(E(ctx).LimitBy(2, x => x.Int)));
        yield return ("limitby-limit", ctx => Row(E(ctx).LimitBy(3, x => x.Int)));
        yield return ("limitby-offset", ctx => Row(E(ctx).LimitBy(2, 1, x => x.Int)));
        yield return ("limitby-expr", ctx => Row(E(ctx).LimitBy(2, x => x.Id + 1)));
        yield return ("settings", ctx => Row(E(ctx).Settings(("max_threads", "2"))));
        yield return ("settings-key", ctx => Row(E(ctx).Settings(("max_rows", "2"))));
        yield return ("settings-value", ctx => Row(E(ctx).Settings(("max_threads", "4"))));
        yield return ("final", ctx => Row(E(ctx).Final()));
        yield return ("prewhere", ctx => Row(E(ctx).PreWhere(x => x.Id > 0L)));
        yield return ("prewhere-other", ctx => Row(E(ctx).PreWhere(x => x.Id > 1L)));
        yield return ("arrayjoin", ctx => ctx.From<IArrayEntity>().ArrayJoin(x => x.Tags).Select(x => new PKRow { Id = x.Id }));
        yield return ("leftarrayjoin", ctx => ctx.From<IArrayEntity>().LeftArrayJoin(x => x.Tags).Select(x => new PKRow { Id = x.Id }));
        yield return ("tablesample", ctx => Row(ctx.From<IComplexEntity>(o => o.TableSample(10))));
        yield return ("tablesample-method", ctx => Row(ctx.From<IComplexEntity>(o => o.TableSample(10, TableSampleMethod.Bernoulli))));
        yield return ("tablesample-seed", ctx => Row(ctx.From<IComplexEntity>(o => o.TableSample(10, TableSampleMethod.System, 3))));
        yield return ("temporal", ctx => Row(E(ctx).ForSystemTime(TemporalClause.AsOf(new DateTime(2020, 1, 1)))));
        yield return ("temporal-between", ctx => Row(E(ctx).ForSystemTime(TemporalClause.Between(new DateTime(2020, 1, 1), new DateTime(2021, 1, 1)))));
        yield return ("rowlock-update", ctx => Row(E(ctx).ForUpdate()));
        yield return ("rowlock-share", ctx => Row(E(ctx).ForShare()));
        yield return ("forjson", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).WithForJson());
        yield return ("forjson-root", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).WithForJson(root: "r"));
        yield return ("forxml", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).WithForXml());
        yield return ("forxml-mode", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).WithForXml(ForXmlMode.Raw));
        yield return ("hints", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Hint("recompile"));
        yield return ("hints-two", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Hint("recompile", "maxdop 1"));
        yield return ("table-hint", ctx => Row(E(ctx).WithTableHint("INDEX(ix)")));
        yield return ("index-hint", ctx => Row(E(ctx).WithIndex(IndexHintKind.Force, "ix")));
        yield return ("index-hint-kind", ctx => Row(E(ctx).WithIndex(IndexHintKind.Ignore, "ix")));
        yield return ("join-hint", ctx => E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id).WithJoinHint("hash")
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
        yield return ("join-hint-other", ctx => E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id).WithJoinHint("loop")
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
        yield return ("subquery-hint", ctx => ctx.From(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .WithSubQueryHint("NestLoop(t1)").Select(t => new PKRow { Id = t.Id, Int = t.Int }));
        yield return ("subquery-hint-other", ctx => ctx.From(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .WithSubQueryHint("SeqScan(t1)").Select(t => new PKRow { Id = t.Id, Int = t.Int }));
        yield return ("scope-hint", ctx => Row(E(ctx).WithTablesInScopeHint("nolock")));
        yield return ("scope-hint-other", ctx => Row(E(ctx).WithTablesInScopeHint("index(ix)")));

        // PIVOT / UNPIVOT
        yield return ("pivot", ctx => E(ctx).Pivot(PivotAggregate.Count, s => s.Id, s => s.Int, PivotValue.Create("1"))
            .Select(t => new PKRow { Id = t.GetNullableInt64("id"), Int = t.GetNullableInt32("1") }));
        yield return ("pivot-value", ctx => E(ctx).Pivot(PivotAggregate.Count, s => s.Id, s => s.Int, PivotValue.Create("2"))
            .Select(t => new PKRow { Id = t.GetNullableInt64("id"), Int = t.GetNullableInt32("2") }));
        yield return ("unpivot", ctx => E(ctx).Unpivot("val", "qtr", UnpivotColumn.Create("x1"), UnpivotColumn.Create("x2"))
            .Select(t => new PKRow { Id = null, Int = t.GetNullableInt32("val") }));

        // Table-valued functions
        yield return ("tvf", ctx => ctx.FromTableFunction(() => PkTvf.Rows())
            .Select(r => new PKRow { Id = r.Id, Int = r.Int }));
        yield return ("tvf-arg", ctx => ctx.FromTableFunction(() => PkTvf.RowsById(1))
            .Select(r => new PKRow { Id = r.Id, Int = r.Int }));

        // xml.nodes rowset
        yield return ("xmlnodes", ctx => E(ctx).CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item1.Int }));
    }

    /// <summary>Builds and prepares every variant, returning the prepared commands keyed by name.</summary>
    private static Dictionary<string, QueryCommand> PrepareAll(IDataContext ctx)
    {
        var result = new Dictionary<string, QueryCommand>(StringComparer.Ordinal);
        foreach (var (name, build) in Variants())
        {
            var cmd = build(ctx);
            cmd.PrepareCommand(false, CancellationToken.None);
            result.Add(name, cmd);
        }

        return result;
    }

    [Fact]
    public void EveryVariant_ShouldHaveAUniquePlanKey()
    {
        using var ctx = SqliteTestContext.Create();
        var prepared = PrepareAll(ctx);

        var names = prepared.Keys.ToArray();
        var comparer = prepared[names[0]].GetQueryPlanEqualityComparer();

        for (var i = 0; i < names.Length; i++)
        {
            var a = prepared[names[i]];

            // Equals is reflexive and handles nulls.
            comparer.Equals(a, a).Should().BeTrue($"{names[i]} must equal itself");
            comparer.Equals(null, a).Should().BeFalse();
            comparer.Equals(a, null).Should().BeFalse();

            for (var j = i + 1; j < names.Length; j++)
            {
                var b = prepared[names[j]];

                if (comparer.Equals(a, b))
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Plan cache key collision: '{names[i]}' and '{names[j]}' compare equal but are different queries. " +
                        "A cached plan would be reused for the wrong query.");
                }
            }
        }
    }

    [Fact]
    public void EveryVariant_Rebuilt_ShouldReuseTheSamePlanKey()
    {
        using var ctx = SqliteTestContext.Create();

        foreach (var (_, build) in Variants())
        {
            var first = build(ctx);
            first.PrepareCommand(false, CancellationToken.None);

            var second = build(ctx);
            second.PrepareCommand(false, CancellationToken.None);

            var comparer = first.GetQueryPlanEqualityComparer();
            comparer.Equals(first, second).Should().BeTrue("a structurally identical rebuild must reuse the cached plan");
            comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second), "Equal plans must have equal hashes");
        }
    }

    [Fact]
    public void CloneForCache_ShouldPreserveThePlanKey()
    {
        using var ctx = SqliteTestContext.Create();

        foreach (var (name, build) in Variants())
        {
            var cmd = build(ctx);
            cmd.PrepareCommand(false, CancellationToken.None);

            var clone = cmd.CloneForCache();
            var comparer = cmd.GetQueryPlanEqualityComparer();

            // This is exactly the invariant QueryPlan.GetCacheVersion asserts in Debug: the stored clone
            // must compare equal to the original and hash identically, or the cached key cannot be found.
            comparer.Equals(cmd, clone).Should().BeTrue($"CloneForCache must preserve the key of '{name}'");
            comparer.GetHashCode(cmd).Should().Be(comparer.GetHashCode(clone), $"CloneForCache must preserve the hash of '{name}'");
        }
    }

    [Fact]
    public void EqualPlans_ShouldHaveEqualHashes()
    {
        using var ctx = SqliteTestContext.Create();
        var prepared = PrepareAll(ctx);
        var names = prepared.Keys.ToArray();

        for (var i = 0; i < names.Length; i++)
        {
            var comparer = prepared[names[i]].GetQueryPlanEqualityComparer();

            for (var j = i + 1; j < names.Length; j++)
            {
                if (comparer.Equals(prepared[names[i]], prepared[names[j]]))
                {
                    comparer.GetHashCode(prepared[names[i]]).Should()
                        .Be(comparer.GetHashCode(prepared[names[j]]), "Equal plans must have equal hashes");
                }
            }
        }
    }

    [Fact]
    public void SubComparers_ShouldDistinguishThePreparedParts()
    {
        using var ctx = SqliteTestContext.Create();

        // JOIN clause comparer.
        var joinA = E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id).Select(p => new PKRow { Id = p.Item1.Id });
        var joinB = E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id != b.Id).Select(p => new PKRow { Id = p.Item1.Id });
        joinA.PrepareCommand(false, CancellationToken.None);
        joinB.PrepareCommand(false, CancellationToken.None);

        var joinComparer = joinA.GetJoinExpressionPlanEqualityComparer();
        joinComparer.Equals(joinA.Joins![0], joinA.Joins[0]).Should().BeTrue();
        joinComparer.Equals(joinA.Joins[0], joinB.Joins![0]).Should().BeFalse("different join conditions must not share a plan");
        joinComparer.GetHashCode(joinA.Joins[0]).Should().Be(joinComparer.GetHashCode(joinA.Joins[0]));

        // ORDER BY comparer (both the interface Equals/GetHashCode and the by-ref ValueEquals).
        var sortA = Row(E(ctx).OrderBy(x => x.Id));
        var sortB = Row(E(ctx).OrderByDescending(x => x.Id));
        var sortC = Row(E(ctx).OrderBy(1));
        sortA.PrepareCommand(false, CancellationToken.None);
        sortB.PrepareCommand(false, CancellationToken.None);
        sortC.PrepareCommand(false, CancellationToken.None);

        var sortComparer = sortA.GetSortingExpressionPlanEqualityComparer();
        sortComparer.Equals(sortA.Sorting![0], sortA.Sorting[0]).Should().BeTrue();
        sortComparer.Equals(sortA.Sorting[0], sortB.Sorting![0]).Should().BeFalse("different directions must not share a plan");
        sortComparer.Equals(sortC.Sorting![0], sortA.Sorting[0]).Should().BeFalse("a column index differs from an expression key");
        sortComparer.ValueEquals(in sortA.Sorting[0], in sortA.Sorting[0]).Should().BeTrue();
        sortComparer.GetHashCode(sortA.Sorting[0]).Should().Be(sortComparer.GetHashCode(sortA.Sorting[0]));
        sortComparer.GetHashCodeRef(in sortA.Sorting[0]).Should().Be(sortComparer.GetHashCodeRef(in sortA.Sorting[0]));

        // SELECT-list comparer.
        var selectA = Row(E(ctx));
        var selectB = Row(E(ctx));
        var selectC = E(ctx).Select(x => new PKRow { Id = x.Int, Int = x.Int });
        selectA.PrepareCommand(false, CancellationToken.None);
        selectB.PrepareCommand(false, CancellationToken.None);
        selectC.PrepareCommand(false, CancellationToken.None);

        var selectComparer = selectA.GetSelectExpressionPlanEqualityComparer();
        selectComparer.Equals(selectA.SelectList![0], selectA.SelectList[0]).Should().BeTrue();
        selectComparer.Equals(selectA.SelectList[0], selectC.SelectList![0]).Should().BeFalse("different projected expressions must not share a plan");
        selectComparer.GetHashCode(selectA.SelectList[0]).Should().Be(selectComparer.GetHashCode(selectA.SelectList[0]));
        selectComparer.GetHashCode(selectA.SelectList[0]).Should().Be(selectComparer.GetHashCode(selectB.SelectList![0]));

        // FROM-source comparer.
        var fromA = Row(E(ctx));
        var fromB = Row(E(ctx));
        var fromD = ctx.FromTableFunction(() => PkTvf.Rows()).Select(r => new PKRow { Id = r.Id, Int = r.Int });
        fromA.PrepareCommand(false, CancellationToken.None);
        fromB.PrepareCommand(false, CancellationToken.None);
        fromD.PrepareCommand(false, CancellationToken.None);

        var fromComparer = fromA.GetFromExpressionPlanEqualityComparer();
        fromComparer.Equals(fromA.From, fromB.From).Should().BeTrue("the same table must share a plan");
        fromComparer.Equals(fromA.From, fromD.From).Should().BeFalse("a table and a table function are different sources");
        fromComparer.GetHashCode(fromA.From!).Should().Be(fromComparer.GetHashCode(fromB.From!));
    }

    [Fact]
    public void HashCode_ShouldBeStableAcrossCalls()
    {
        using var ctx = SqliteTestContext.Create();

        foreach (var (_, build) in Variants())
        {
            var cmd = build(ctx);
            cmd.PrepareCommand(false, CancellationToken.None);
            var comparer = cmd.GetQueryPlanEqualityComparer();

            comparer.GetHashCode(cmd).Should().Be(comparer.GetHashCode(cmd));
        }
    }

    /// <summary>
    /// The renderable subset used for the end-to-end cache test. Features SQLite cannot render (PIVOT,
    /// xml.nodes, ...) are exercised by the comparer matrix above and by the provider test suites; here
    /// the plan cache itself must never hand one variant the SQL generated for another.
    /// </summary>
    public static IEnumerable<(string Name, Func<IDataContext, QueryCommand> Build)> RenderableVariants()
    {
        yield return ("base", ctx => Row(E(ctx)));
        yield return ("where", ctx => Row(E(ctx).Where(x => x.Id > 0)));
        yield return ("where-other", ctx => Row(E(ctx).Where(x => x.Id > 1)));
        yield return ("distinct", ctx => Row(E(ctx).Distinct()));
        yield return ("orderby", ctx => Row(E(ctx).OrderBy(x => x.Id)));
        yield return ("orderby-desc", ctx => Row(E(ctx).OrderByDescending(x => x.Id)));
        yield return ("paging", ctx => Row(E(ctx).Limit(3).Offset(2)));
        yield return ("group", ctx => Row(E(ctx).GroupBy(x => x.Int)));
        yield return ("having", ctx => Row(E(ctx).GroupBy(x => x.Int).Having(x => SqlFunctions.Sql.count() > 1)));
        yield return ("join", ctx => E(ctx).Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item2.Id }));
        yield return ("join-other", ctx => E(ctx).LeftJoin(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Select(p => new PKRow { Id = p.Item1.Id, Int = p.Item2.Id }));
        yield return ("union", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Union(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));
        yield return ("intersect", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Intersect(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));
        yield return ("except", ctx => E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }).Except(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int })));
        yield return ("derived", ctx => ctx.From(E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .Select(t => new PKRow { Id = t.Id, Int = t.Int }));
        yield return ("cte", ctx => ctx.With("c", E(ctx).Select(x => new PKRow { Id = x.Id, Int = x.Int }))
            .From("c").Select(t => new PKRow { Id = t.GetNullableInt64("Id"), Int = t.GetNullableInt32("Int") }));
        yield return ("window", ctx => Row(E(ctx).Window("w", orderBy: [E(ctx).Asc(x => x.Id)])));
        yield return ("tvf", ctx => ctx.FromTableFunction(() => PkTvf.Rows()).Select(r => new PKRow { Id = r.Id, Int = r.Int }));
    }

    [Fact]
    public void PlanCache_ShouldNeverReturnAnotherVariantsSql()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();

        // First pass: generate each variant's own SQL without touching the cache.
        var ownSql = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, build) in RenderableVariants())
        {
            var prepared = (DbPreparedQueryCommand<PKRow>)ctx.GetPreparedQueryCommand(
                (QueryCommand<PKRow>)build(ctx), false, false, CancellationToken.None);
            ownSql[name] = prepared.DbCommand.CommandText;
        }

        // Second pass: run through the cache. If two variants collided, the second would receive the
        // first's cached command and therefore the first's SQL.
        foreach (var (name, build) in RenderableVariants())
        {
            var prepared = (DbPreparedQueryCommand<PKRow>)ctx.GetPreparedQueryCommand(
                (QueryCommand<PKRow>)build(ctx), false, true, CancellationToken.None);

            prepared.DbCommand.CommandText.Should().Be(
                ownSql[name], $"the cached plan for '{name}' must keep its own SQL");
        }
    }
}
