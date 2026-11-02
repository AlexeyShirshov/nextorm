using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests.SqlGeneration;

/// <summary>
/// S3/T302 guard surface of the composite <c>All</c> lowering (arity 2, <c>Ties == All</c>): the
/// user-command modifiers are rejected by <c>EnsureExtremeRowCompatible</c> before any staged aggregate
/// chain is built, while the internal stage joins/groupings render without tripping the same guard.
/// Only metadata is used here — the guard cases throw at preparation and the successful render is
/// never executed, so no database connection is needed.
/// </summary>
public sealed class T302CompositeAllGuardSqlGenerationTests
{
    [Fact]
    public void ExtremeRow_CompositeAll_WithPaging_ShouldThrowBeforeLowering()
    {
        // S3/T302: the composite All shape is lowered after the user-command guard, so paging must be
        // rejected at preparation, before any staged aggregate chain is built.
        var context = SqliteTestContext.CreateSqlite();

        foreach (var command in new[]
                 {
                     context.From<GuardCompositeEntity>()
                         .SelectWhereMax(x => new { x.V1, x.V2 }, ExtremeRowTies.All)
                         .Limit(1)
                         .ToCommand(),
                     context.From<GuardCompositeEntity>()
                         .SelectWhereMin(x => new { x.V1, x.V2 }, ExtremeRowTies.All)
                         .Offset(1)
                         .ToCommand(),
                     context.From<GuardCompositeEntity>()
                         .SelectWhereMax(x => new { x.V1, x.V2 }, ExtremeRowTies.All)
                         .Page(1, 1)
                         .ToCommand(),
                 })
        {
            FluentActions
                .Invoking(() => context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
                .Should().Throw<BuildSqlCommandException>();
        }
    }

    [Fact]
    public void ExtremeRow_CompositeAll_WithUserGrouping_ShouldThrowBeforeLowering()
    {
        // A user GroupBy is expressed through the grouping argument of the composite lowering, so the
        // explicit user-level GroupBy must still be refused by the guard, not merged into the stages.
        var context = SqliteTestContext.CreateSqlite();
        var command = context.From<GuardCompositeEntity>()
            .GroupBy(x => x.V1)
            .SelectWhereMax(x => new { x.V1, x.V2 }, ExtremeRowTies.All)
            .ToCommand();

        FluentActions
            .Invoking(() => context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ExtremeRow_CompositeAll_NormalRender_SucceedsAndInternalStagesPassGuards()
    {
        // The staged joins and per-stage GROUP BY built by the composite lowering are internal nodes:
        // they must render without tripping EnsureExtremeRowCompatible.
        var context = SqliteTestContext.CreateSqlite();
        var command = context.From<GuardCompositeEntity>()
            .Where(x => x.V1 > 1)
            .SelectWhereMax(x => new { x.V1, x.V2 }, ExtremeRowTies.All, x => x.Grp)
            .ToCommand();

        var prepared = (DbPreparedQueryCommand<GuardCompositeEntity>)context.GetPreparedQueryCommand(
            command, false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.ToLowerInvariant();

        sql.Should().Contain("max(");
        sql.Should().Contain("join");
        sql.Should().Contain("group by");
        sql.Should().NotContain("rank(");
        sql.Should().NotContain("row_number(");
    }
}

/// <summary>
/// S3/T302 guard fixture: a plain table whose composite value key is <c>(v1, v2)</c> and whose
/// nullable group key is <c>grp</c>. Only metadata is used here — the guard cases throw at
/// preparation and the successful render is never executed.
/// </summary>
[SqlTable("t302_s3_guard")]
public sealed class GuardCompositeEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("v1")]
    public int V1 { get; set; }

    [Column("v2")]
    public int V2 { get; set; }

    [Column("grp")]
    public string? Grp { get; set; }
}
