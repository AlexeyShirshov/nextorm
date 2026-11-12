using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Issue #319 phase guard: the evaluation contract makes collection a pure raw-description step.
/// Building any frozen comparison shape must not prepare the command, compute a plan hash or render
/// SQL, and it must not touch the shared-cache opt-out. These guards pin the "before" side of the
/// contract; the matching "after" test proves preparation does happen at materialization, so the
/// guards cannot pass vacuously.
/// </summary>
public class Issue319PhaseGuardTests
{
    private sealed class CteRow
    {
        public int Id { get; set; }
        public int SimpleId { get; set; }
    }

    private sealed class NumberRow
    {
        public int n { get; set; }
    }

    private sealed record Shape(string Name, QueryCommand Command, Func<string> PrepareSql);

    private static Shape Of<T>(IDataContext ctx, string name, QueryCommand<T> command) => new(
        name,
        command,
        () => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    private static Shape[] BuildFrozenShapes(IDataContext ctx) =>
    [
        Of(ctx, "Cte", BuildCte(ctx)),
        Of(ctx, "RecursiveCte", BuildRecursiveCte(ctx)),
        Of(ctx, "Except", BuildExcept(ctx)),
        Of(ctx, "Intersect", BuildIntersect(ctx)),
        Of(ctx, "Select", ctx.From<IComplexEntity>().Select(c => c.Id)),
        Of(ctx, "WhereSelect", ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => c.Id)),
    ];

    [Fact]
    public void Collection_ShouldNotPrepareHashOrOptOutOfTheCache()
    {
        using var ctx = SqliteTestContext.Create();

        foreach (var shape in BuildFrozenShapes(ctx))
        {
            shape.Command.IsPrepared.Should().BeFalse($"{shape.Name} collection must not prepare the command");
            shape.Command.HasEverPrepared.Should().BeFalse($"{shape.Name} collection must not have prepared before");
            shape.Command.Cache.Should().BeTrue($"{shape.Name} collection must not disable the shared plan cache");
            PlanHashes(shape.Command).Should().OnlyContain(hash => hash == 0,
                $"{shape.Name} collection must not compute plan hashes");
        }
    }

    [Fact]
    public void Materialization_ShouldPrepareAndRenderSql()
    {
        using var ctx = SqliteTestContext.Create();

        foreach (var shape in BuildFrozenShapes(ctx))
        {
            shape.Command.IsPrepared.Should().BeFalse($"{shape.Name} must start unprepared");

            var sql = shape.PrepareSql();

            shape.Command.HasEverPrepared.Should().BeTrue($"{shape.Name} materialization must prepare the command");
            sql.Should().NotBeNullOrWhiteSpace($"{shape.Name} must render SQL on miss");
        }
    }

    [Fact]
    public void RepeatedMaterialization_ShouldKeepTheSameSql()
    {
        using var ctx = SqliteTestContext.Create();
        var first = BuildFrozenShapes(ctx);
        var second = BuildFrozenShapes(ctx);

        for (var i = 0; i < first.Length; i++)
        {
            second[i].PrepareSql().Should().Be(first[i].PrepareSql(),
                $"{first[i].Name} must render identical SQL for the same shape");
        }
    }

    private static int[] PlanHashes(QueryCommand command) =>
    [
        command.ColumnsPlanHash,
        command.JoinPlanHash,
        command.SortingPlanHash,
        command.WherePlanHash,
        command.GroupingPlanHash,
        command.ResultPlanHash,
        command.FromPlanHash,
        command.UnionPlanHash,
        command.ReferencedQueriesPlanHash,
        command.CtesPlanHash,
        command.HintsPlanHash,
        command.WindowsPlanHash,
    ];

    private static QueryCommand<CteRow> BuildCte(IDataContext ctx)
    {
        var recent = ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.String });
        return ctx.With("recent", recent)
            .From("recent")
            .Join(ctx.From<ISimpleEntity>(), (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id });
    }

    private static QueryCommand<int> BuildRecursiveCte(IDataContext ctx)
    {
        var anchor = ctx.From<ISimpleEntity>().Where(s => s.Id == 1).Select(s => new NumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new NumberRow { n = t["n"].AsInt + 1 });
        return ctx.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
    }

    private static QueryCommand<int> BuildExcept(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Select(s => s.Id).Except(ctx.From<IComplexEntity>().Select(c => (int)c.Id));

    private static QueryCommand<int> BuildIntersect(IDataContext ctx)
        => ctx.From<ISimpleEntity>().Select(s => s.Id).Intersect(ctx.From<IComplexEntity>().Select(c => (int)c.Id));
}
