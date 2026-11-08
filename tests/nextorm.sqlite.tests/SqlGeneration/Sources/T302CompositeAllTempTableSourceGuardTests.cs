using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests.SqlGeneration;

/// <summary>
/// S3/T302 (VM-S3-15): interaction of a lazy temporary-table source with
/// <c>EnsureExtremeRowCompatible</c> for the composite <c>All</c> shape. A temporary-table read is a
/// name-addressed physical source (no <c>SubQuery</c>), so the physical-source half of the guard keeps
/// accepting it exactly as before, while a derived source is still refused. Preparation/render only —
/// the batch is rendered, never executed, so no database connection is opened. The sticky plan-cache
/// flag of the user command is asserted unchanged by rendering.
/// </summary>
public sealed class T302CompositeAllTempTableSourceGuardTests
{
    [Fact]
    public void CompositeAll_OverTempTableSource_IsAcceptedAndRendersStagedChain()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<IT302TempSourceEntity>()
            .Where(x => x.V1 > 1)
            .Select(x => new { x.Id, x.V1, x.V2, x.Grp })
            .AsTempTable();

        var command = ctx.From(source)
            .SelectWhereMaxOld(
                t => new { V1 = t.GetInt32("v1"), V2 = t.GetInt32("v2") },
                t => new { Id = t.GetInt32("id") },
                ExtremeRowTies.All,
                t => t.GetInt32("grp"));

        var cacheBefore = command.Cache;
        var sql = command.ToBatchSql().ToLowerInvariant();

        // The temp table is materialised in the same batch and the staged composite chain renders over
        // it: the guard accepted the name-addressed source instead of rejecting it as a derived one.
        sql.Should().Contain("create temporary table " + source.Name.ToLowerInvariant());
        sql.Should().Contain("from " + source.Name.ToLowerInvariant());
        sql.Should().Contain("max(");
        sql.Should().Contain("__nextorm_ext_1_v1");
        sql.Should().Contain("__nextorm_ext_2_v1");
        sql.Should().Contain("__nextorm_ext_2_v2");
        sql.Should().Contain("group by");
        sql.Should().Contain("join");
        sql.Should().NotContain("rank(");
        sql.Should().NotContain("row_number(");

        // Rendering never mutates the shared sticky Cache/_dontCache flag.
        cacheBefore.Should().BeTrue();
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void CompositeAll_OverDerivedSubquerySource_IsStillRejectedByThePhysicalSourceCheck()
    {
        // The physical-source half of EnsureExtremeRowCompatible is unchanged: a derived SubQuery source
        // is refused for the composite All shape, before any staged chain is built.
        using var ctx = SqliteTestContext.Create();
        var derived = ctx.From(ctx.From<IT302TempSourceEntity>());

        var command = derived
            .SelectWhereMaxOld(x => new { x.V1, x.V2 }, ExtremeRowTies.All)
            .ToCommand();

        FluentActions
            .Invoking(() => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*single physical-table source*");
    }
}

[SqlTable("t302_temp_source")]
public interface IT302TempSourceEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("v1")]
    int V1 { get; set; }

    [Column("v2")]
    int V2 { get; set; }

    [Column("grp")]
    int Grp { get; set; }
}
