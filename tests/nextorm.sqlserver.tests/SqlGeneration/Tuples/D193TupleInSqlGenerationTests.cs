using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D193.3 — SQL Server has no row-value constructor, so every tuple value-list form must be rejected at
/// preparation time with the exact message <c>SQL Server does not support tuple IN/Contains translation.</c>
/// before any SQL is submitted. SQL Server keeps its rejection even for an empty tuple collection.
/// </summary>
public class D193TupleInSqlGenerationTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    // The caching path folds the captured-collection shape during preparation, so it is where the
    // evaluation-time failures (null collection/entry, arity/nested validation) would otherwise surface.
    // The provider preflight must reject first with the pinned message.
    private static IPreparedQueryCommand<T> PrepareCaching<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    [Fact]
    public void TupleContains_ShouldThrowAtPreparation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { (1, 10), (2, 20) };

        // Only the preparation path runs: no command is executed, so the rejection provably happens
        // before any SQL is submitted to the server.
        var act = () => Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_Empty_ShouldStillThrowBeforeEmptyFolding()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var act = () => Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_NullCollection_ShouldThrowPinnedMessageBeforeNullArgument()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        List<(long, int)>? tuples = null;

        var act = () => PrepareCaching(ctx, e.Where(x => tuples!.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_NullEntry_ShouldThrowPinnedMessageBeforeNullTupleEntry()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int>> { Tuple.Create(1L, 10), null! };

        var act = () => PrepareCaching(ctx, e.Where(x => tuples.Contains(Tuple.Create(x.Id, x.Code))).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_ArityEight_ShouldThrowPinnedMessageBeforeArityValidation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int, int, int, int, int, int, int)>();

        var act = () => PrepareCaching(ctx, e
            .Where(x => tuples.Contains(new ValueTuple<long, int, int, int, int, int, int, ValueTuple<int>>(
                x.Id, 2, 3, 4, 5, 6, 7, new ValueTuple<int>(8))))
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_Nested_ShouldThrowPinnedMessageBeforeNestedValidation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, (long, int))> { (1L, (2L, 3)) };

        var act = () => PrepareCaching(ctx, e
            .Where(x => tuples.Contains(new ValueTuple<long, (long, int)>(x.Id, new ValueTuple<long, int>(2L, 3))))
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_TupleTypedLhs_ShouldThrowPinnedMessageBeforeShapeValidation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleEntity>();
        var tuples = new List<Tuple<int, string>> { Tuple.Create(1, "a") };

        var act = () => PrepareCaching(ctx, e.Where(x => tuples.Contains(x.Pair)).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    // F7 — HAVING/SELECT/JOIN are unkeyed clauses: they are not covered by the preparation-time
    // preflight (which runs for WHERE/PREWHERE only), so the rejection must come from the render-time
    // guard. The render guard is the first statement of the tuple translator, before the collection is
    // evaluated and before the empty-list fold, so an empty collection still emits the pinned message.
    // These tests call Prepare only and never execute, so no SQL is submitted to a server.

    [Fact]
    public void TupleContains_InHaving_Empty_ShouldThrowPinnedMessageBeforeEmptyFolding()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var act = () => Prepare(ctx, e
            .GroupBy(x => x.Id)
            .Having(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code)))
            .Select(x => x.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_InSelectColumn_Empty_ShouldThrowPinnedMessageBeforeEmptyFolding()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var act = () => Prepare(ctx, e
            .Select(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }

    [Fact]
    public void TupleContains_InJoinOn_Empty_ShouldThrowPinnedMessageBeforeEmptyFolding()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var other = ctx.From<ITupleEntity>();
        var tuples = new List<(long, int)>();

        var act = () => Prepare(ctx, e
            .Join<ITupleEntity>(other, (a, b) => tuples.Contains(new ValueTuple<long, int>(a.Id, b.Id)))
            .Select(p => p.Item1.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQL Server does not support tuple IN/Contains translation.");
    }
}

[SqlTable("tuple_in_entity")]
public interface ITupleInMembershipEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("code")]
    int Code { get; set; }
}
