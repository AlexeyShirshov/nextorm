using FluentAssertions;
using NextORM.Core;
using NextORM.ClickHouse;

namespace NextORM.ClickHouse.Extensions.Tests;

/// <summary>
/// State-carry and guard coverage for the relocated ClickHouse fluent extensions: projection-independent
/// state carried through <c>ArrayJoinElement</c>, <c>Having</c> surviving a clone, and the eager-load
/// guard on the moved paths. This project has no <c>InternalsVisibleTo</c>, so every call binds to public
/// extension methods.
/// </summary>
public class ClickHouseExtensionStateTests
{
    public sealed class GuardParent
    {
        public int Id { get; set; }
        public string[] Tags { get; set; } = [];
        public ICollection<GuardChild> Children { get; set; } = new List<GuardChild>();
    }

    public sealed class GuardChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void ArrayJoinElement_CarriesProjectionIndependentState()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e
            .Final()
            .Settings(("max_threads", "2"))
            .PreWhere(x => x.Id > 1)
            .LimitBy(2, x => x.Id)
            .ArrayJoinElement(x => x.Tags)
            .Select(p => new { p.Item1.Id, p.Element }));

        sql.Should().Contain(" final");
        sql.Should().Contain(" settings max_threads = 2");
        sql.Should().Contain(" prewhere ");
        sql.Should().Contain("limit 2 by id");
        sql.Should().Contain("array join tags as __nextorm_aj_element");
    }

    [Fact]
    public void ArrayJoinElement_AfterHaving_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e
            .GroupBy(x => x.Id)
            .Having(x => SqlFunctions.Sql.count() > 1)
            .ArrayJoinElement(x => x.Tags);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Where/Having must be applied after*");
    }

    [Fact]
    public void ArrayJoinElement_WithSingleQueryEagerState_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<GuardParent>();

        var act = () => e
            .LoadWith(p => p.Children, c => c.From<GuardChild>(), p => p.Id, c => c.ParentId)
            .ArrayJoinElement(x => x.Tags);

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*ArrayJoinElement*");
    }

    [Fact]
    public void Having_SurvivesClone_AndIsIndependent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var withHaving = e.GroupBy(x => x.Int).Having(x => SqlFunctions.Sql.count() > 1);
        var clone = withHaving.Clone();

        SqlOf(ctx, withHaving.Select(x => new { x.Int })).Should().Contain(" having ");
        SqlOf(ctx, clone.Select(x => new { x.Int })).Should().Contain(" having ");

        // Composing on the clone must not leak state back into the source builder.
        var composed = clone.Where(x => x.Int != null).Select(x => new { x.Int });
        SqlOf(ctx, composed).Should().Contain(" having ").And.Contain(" where ");
        SqlOf(ctx, withHaving.Select(x => new { x.Int })).Should().Contain(" having ").And.NotContain(" where ");
    }

    [Fact]
    public void JoinOptions_OnJoinedContinuation_ShouldReachPasteJoinAndKeepTheModifierGuard()
    {
        using var ctx = ClickHouseTestContext.Create();
        var s = ctx.From<ISimpleEntity>();
        var c = ctx.From<IComplexEntity>();

        var joined = s.Join(c, (p, x) => p.Id == x.Id);

        // Without a modifier the relocated joined continuation still renders PASTE JOIN.
        SqlOf(ctx, joined.PasteJoin(s).Select(p => new { p.Item1.Id }))
            .Should().Contain(" paste join ");

        // A modifier passed through the joined continuation reaches the renderer and keeps the
        // pre-existing rejection instead of being silently dropped.
        var act = () => SqlOf(ctx, joined.PasteJoin(s, j => j.Global()).Select(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be applied to a Paste join*");
    }

    [Fact]
    public void Settings_WithWhiteSpaceKey_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.Settings(("  ", "2"));

        act.Should().Throw<ArgumentException>().WithMessage("*SETTINGS key must not be empty*");
    }

    [Fact]
    public void ArrayJoin_MixedWithLeftArrayJoin_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.ArrayJoin(x => x.Tags).LeftArrayJoin(x => x.Nums);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot mix ARRAY JOIN and LEFT ARRAY JOIN*");
    }

    [Fact]
    public void ArrayJoinElement_MixedKinds_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        // ArrayJoinElement changes the receiver type to ArrayJoinProjection<,>, so the mixed-kind
        // guard in ToArrayJoinElement cannot be reached by chaining LeftArrayJoinElement directly.
        // It is exercised through the same-entity ARRAY JOIN form on a fresh builder, in both
        // directions: inner state then element-left, and left state then element-inner.
        var innerThenLeftElement = () => e.ArrayJoin(x => x.Tags).LeftArrayJoinElement(x => x.Nums);
        innerThenLeftElement.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot mix ARRAY JOIN and LEFT ARRAY JOIN*");

        var leftThenInnerElement = () => e.LeftArrayJoin(x => x.Nums).ArrayJoinElement(x => x.Tags);
        leftThenInnerElement.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot mix ARRAY JOIN and LEFT ARRAY JOIN*");
    }
}
