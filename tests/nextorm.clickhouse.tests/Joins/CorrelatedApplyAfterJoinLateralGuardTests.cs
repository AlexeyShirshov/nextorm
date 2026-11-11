using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Generated.nextorm_clickhouse_tests;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T324 D6b1 — E08/AC08 negative N02: a correlated <c>CROSS APPLY</c>/<c>OUTER APPLY</c> placed after a
/// join must be refused on ClickHouse because the dialect declares no lateral/APPLY capability
/// (<c>ISqlDialect.SupportsApply == false</c>), and the refusal must happen while the command is
/// prepared, before any SQL reaches the database.
/// <para>
/// The refusal is the same dialect gate N01 asserts for SQLite, so the message names the APPLY operator
/// and the missing dialect capability - never the left source's projection shape. The control below
/// prepares the identical joined projection without an APPLY.
/// </para>
/// </summary>
public class CorrelatedApplyAfterJoinLateralGuardTests
{
    [Fact]
    public void ClickHouse_dialect_reports_no_lateral_or_apply_capability()
    {
        using var ctx = ClickHouseTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsApply.Should().BeFalse(
            "the N02 refusal is the dialect capability gate, not a projection-shape heuristic");
    }

    [Theory]
    [InlineData("CrossApply", "EntityBuilder")]
    [InlineData("CrossApply", "QueryCommand")]
    [InlineData("OuterApply", "EntityBuilder")]
    [InlineData("OuterApply", "QueryCommand")]
    public void Positional_correlated_apply_after_join_is_refused_before_sql_execution(string operation, string form)
    {
        var (ctx, recorder) = CreateRecordingContext();
        using (ctx)
        {
            var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

            var act = () => operation == "CrossApply"
                ? SqlOf(ctx, form == "EntityBuilder"
                    ? joined.CrossApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id))
                        .Select(q => new { Left = q.Item1.Id, Applied = q.Item3.Id })
                    : joined.CrossApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).ToCommand())
                        .Select(q => new { Left = q.Item1.Id, Applied = q.Item3.Id }))
                : SqlOf(ctx, form == "EntityBuilder"
                    ? joined.OuterApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Item2.Id))
                        .Select(q => new { Left = q.Item1.Id, Applied = q.Item3.Id })
                    : joined.OuterApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Item2.Id).ToCommand())
                        .Select(q => new { Left = q.Item1.Id, Applied = q.Item3.Id }));

            AssertLateralRefusal(act, operation);
            recorder.Executed.Should().Be(0, "the refusal must precede SQL execution");
        }
    }

    [Theory]
    [InlineData("CrossApply", "EntityBuilder")]
    [InlineData("CrossApply", "QueryCommand")]
    [InlineData("OuterApply", "EntityBuilder")]
    [InlineData("OuterApply", "QueryCommand")]
    public void Alias_correlated_apply_after_join_is_refused_before_sql_execution(string operation, string form)
    {
        var (ctx, recorder) = CreateRecordingContext();
        using (ctx)
        {
            var receiver = ctx.From<ISimpleEntity>()
                .WithAlias(Alias.Root)
                .Join<IComplexEntity>(ctx.From<IComplexEntity>(), (a, b) => a.Root.Id == b.Id, Alias.Buyer);

            var act = () => operation == "CrossApply"
                ? SqlOf(ctx, form == "EntityBuilder"
                    ? receiver.CrossApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Buyer.Id), Alias.Applied)
                        .Select(q => new { Left = q.Root.Id, Applied = q.Applied.Id })
                    : receiver.CrossApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Buyer.Id).ToCommand(), Alias.Applied)
                        .Select(q => new { Left = q.Root.Id, Applied = q.Applied.Id }))
                : SqlOf(ctx, form == "EntityBuilder"
                    ? receiver.OuterApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Buyer.Id), Alias.Applied)
                        .Select(q => new { Left = q.Root.Id, Applied = q.Applied.Id })
                    : receiver.OuterApply(p => ctx.From<IComplexEntity>().Where(c => c.Id == p.Buyer.Id).ToCommand(), Alias.Applied)
                        .Select(q => new { Left = q.Root.Id, Applied = q.Applied.Id }));

            AssertLateralRefusal(act, operation);
            recorder.Executed.Should().Be(0, "the refusal must precede SQL execution");
        }
    }

    [Fact]
    public void A_joined_projection_without_apply_prepares_normally()
    {
        // Control: the left source is a join projection in every negative above. ClickHouse refusing those
        // commands must not be attributed to the projection shape - the same projection without an APPLY
        // prepares normally.
        using var ctx = ClickHouseTestContext.Create();
        var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var sql = SqlOf(ctx, joined.Select(p => new { Left = p.Item1.Id, Right = p.Item2.String }));

        sql.Should().Contain("join complex_entity");
        sql.Should().NotContain("apply");
        sql.Should().NotContain("lateral");
    }

    private static void AssertLateralRefusal(Func<string> act, string operation)
    {
        var exception = act.Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain(operation);
        exception.Message.Should().Contain("not supported by this SQL dialect");
        exception.Message.Should().NotContain(
            "join projection", "a correlated APPLY over a projection must be refused for the missing lateral capability");
    }

    private static (ClickHouseDataContext Context, RecordingInterceptor Recorder) CreateRecordingContext()
    {
        var recorder = new RecordingInterceptor();
        var context = new ClickHouseDataContext(
            "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm",
            new DataContextBuilder().AddInterceptor(recorder));
        return (context, recorder);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private sealed class RecordingInterceptor : IQueryInterceptor
    {
        public int Executed { get; private set; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Executed++;
    }
}
