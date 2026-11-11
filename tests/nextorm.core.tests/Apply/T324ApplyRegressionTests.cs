using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T324 D6a — E09 (AC09) unit side: the pre-existing APPLY/correlation surfaces are unchanged by the
/// flat correlated-APPLY-after-join work, and preparation never disables the plan cache.
/// <para>
/// Each case pins the rendered SQL of a legacy shape as a frozen baseline (whitespace-flattened) rather
/// than re-deriving it, so any change in alias allocation, correlation or projection shows up as a diff.
/// The last case is the sticky-cache guard: a correlated-after-join preparation must leave both the
/// command and the context's plan cache usable. SQL is rendered by the real SQL Server planner over a
/// placeholder connection string; no connection is opened.
/// </para>
/// </summary>
public class T324ApplyRegressionTests
{
    private static SqlServerDataContext CreateContext()
        => new(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

    // ---------------------------------------------------------------------------------------------
    // Single-entity correlated APPLY — the legacy path with no left-prefix binding metadata.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Single_entity_correlated_apply_keeps_its_legacy_shape()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>().CrossApply(a => ctx.From<T324B>().Where(b => b.Id == a.Code));
        var command = applied.Select(q => q.Item1.Id);

        Flatten(T324ApplyFixture.SqlOf(ctx, command)).Should().Be(
            "select t1.Id from t324_apply_a as [t1] " +
            "cross apply (select t2.Id, t2.Code from t324_apply_b as [t2] where t2.Id = t1.Code) as [t3]");

        var applyJoin = command.Joins!.Single(j => j.ApplySource is not null);
        applyJoin.ApplyLeftSlots.Should().BeNull("a single-entity APPLY keeps its existing resolution path");
        command.GetOuterReferenceBinding(0).Should().BeNull("the legacy outer reference stays unbound");
    }

    // ---------------------------------------------------------------------------------------------
    // Plain value APPLY — an already-derived source, never a correlated ApplySource.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Plain_value_apply_keeps_its_legacy_shape()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>().CrossApply(ctx.From<T324B>().Select(b => b.Code));
        var command = applied.Select(q => q.Item1.Id);

        Flatten(T324ApplyFixture.SqlOf(ctx, command)).Should().Be(
            "select t1.Id from t324_apply_a as [t1] cross apply (select Code from t324_apply_b) as [t2]");

        var applyJoin = command.Joins!.Single();
        applyJoin.JoinType.Should().Be(JoinType.CrossApply);
        applyJoin.ApplySource.Should().BeNull("a derived value source is not a correlated ApplySource");
        applyJoin.ApplyLeftSlots.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------------
    // Legacy unbound scalar-subquery correlation — must not be migrated to the binding path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Legacy_unbound_scalar_correlation_is_not_migrated()
    {
        using var ctx = CreateContext();
        var command = ctx.From<T324A>()
            .Where(a => a.Code == ctx.From<T324B>().Where(b => b.Id == a.Id).Select(b => b.Id).First())
            .Select(q => q.Id);

        Flatten(T324ApplyFixture.SqlOf(ctx, command)).Should().Be(
            "select t1.Id from t324_apply_a as [t1] " +
            "where t1.Code = (select top(1) t2.Id from t324_apply_b as [t2] where t2.Id = t1.Id)");

        command.OuterReferences.Should().NotBeNull().And.HaveCount(1);
        command.GetOuterReferenceBinding(0).Should().BeNull("an unbound scalar correlation keeps its existing path");
    }

    // ---------------------------------------------------------------------------------------------
    // Bare alias root — unchanged, and no APPLY/binding metadata involved.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_bare_alias_root_is_unchanged()
    {
        using var ctx = CreateContext();
        var rooted = ctx.From<T324A>().AliasRoot<EntityBuilder<RootAlias<T324A>>, RootAlias<T324A>>(
            static dc => new EntityBuilder<RootAlias<T324A>>(dc));
        var command = rooted.Select(q => q.Root.Code);

        Flatten(T324ApplyFixture.SqlOf(ctx, command)).Should().Be("select Code from t324_apply_a");
        command.Joins.Should().BeNull("a bare alias root adds no join");
    }

    // ---------------------------------------------------------------------------------------------
    // Sticky-cache guard — the correlated-after-join preparation must not disable the plan cache.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_correlated_apply_after_a_join_leaves_the_plan_cache_usable()
    {
        using var ctx = CreateContext();
        QueryCommand<T324FlatRow> command = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

        var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        command.IsPrepared.Should().BeTrue();
        command.Cache.Should().BeTrue("preparation must not set the sticky _dontCache flag");

        // The same command hits its own cached plan instead of preparing again.
        var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        second.Should().BeSameAs(first);
        command.Cache.Should().BeTrue();

        // A later, unrelated query on the same context still uses the plan cache: a context-wide
        // disablement is the failure this guards against.
        var later = ctx.From<T324D>().Where(d => d.Id > 0).Select(d => d.Id);
        var laterFirst = ctx.GetPreparedQueryCommand(later, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var laterSecond = ctx.GetPreparedQueryCommand(later, createEnumerator: false, storeInCache: true, CancellationToken.None);
        laterSecond.Should().BeSameAs(laterFirst);
        later.Cache.Should().BeTrue();

        // And the APPLY command's own SQL is unaffected by the cache round trip.
        Flatten(T324ApplyFixture.SqlOf(ctx, command)).Should().Contain("where t3.Id = t2.Code");
    }

    private static string Flatten(string sql) => Regex.Replace(T324ApplyFixture.Normalize(sql), @"\s+", " ").Trim();

    /// <summary>Hand-written root alias projection (the generator is not attached to this project).</summary>
    private sealed class RootAlias<T1> : Projection<T1>
    {
        [JoinSlot(1)]
        public T1 Root => throw new NotSupportedException();
    }
}
