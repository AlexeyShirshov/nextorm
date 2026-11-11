using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_sqlite_tests;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T324 D6b1 — E08/AC08 negative N01: a correlated <c>CROSS APPLY</c>/<c>OUTER APPLY</c> placed after a
/// join must be refused on SQLite for the right reason — SQLite has no lateral/APPLY capability — and the
/// refusal must happen while the command is prepared, before any SQL reaches the database.
/// <para>
/// The guard is the dialect gate in <c>SqlSourceRenderer.MakeApplyJoin</c>
/// (<c>ISqlDialect.SupportsApply == false</c>), so the message names the APPLY operator and the missing
/// dialect capability. It must never be the removed "join projection" refusal: the left source being a
/// projection is not the reason, and a joined projection without an APPLY prepares normally (see the
/// control below).
/// </para>
/// </summary>
public class CorrelatedApplyAfterJoinLateralGuardTests
{
    [Fact]
    public void Sqlite_dialect_reports_no_lateral_or_apply_capability()
    {
        using var ctx = SqliteTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsApply.Should().BeFalse(
            "the N01 refusal is the dialect capability gate, not a projection-shape heuristic");
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
        // Control: the left source is a join projection in every negative above. SQLite refusing those
        // commands must not be attributed to the projection shape - the same projection without an APPLY
        // prepares (and would execute) normally.
        using var ctx = SqliteTestContext.Create();
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

    private static (SqliteDataContext Context, RecordingInterceptor Recorder) CreateRecordingContext()
    {
        var recorder = new RecordingInterceptor();
        var context = new SqliteDataContext(
            "Data Source=:memory:",
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
