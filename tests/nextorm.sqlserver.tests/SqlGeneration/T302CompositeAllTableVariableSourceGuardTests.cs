using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests.SqlGeneration;

/// <summary>
/// S3/T302 (VM-S3-15): interaction of a SQL Server table-variable source (the batch
/// <c>OUTPUT ... INTO @t</c> capture, the TVP-shaped read path) with the extreme-row source contract for
/// the composite <c>All</c> shape. Standalone preparation is still refused by the pre-existing
/// batch-output-source guard (a <see cref="NotSupportedException"/>, never <c>EnsureExtremeRowCompatible</c>),
/// while the composed batch renders the staged chain over the bare <c>@t</c> identifier. Preparation and
/// rendering only — no connection is opened.
/// </summary>
public sealed class T302CompositeAllTableVariableSourceGuardTests
{
    [Fact]
    public void CompositeAll_ReadingATableVariableStandalone_IsRefusedByTheBatchOutputSourceGuard()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = CreateBatchWithCapture(ctx);

        var command = batch.FromOutput<T302TableVariableRow>("@t")
            .SelectWhereMax(r => new { r.V1, r.V2 }, ExtremeRowTies.All)
            .ToCommand();

        var cacheBefore = command.Cache;

        // The refusal belongs to the batch-output-source contract and happens before any extreme-row
        // lowering or guard: the message names the table-variable ownership, not a modifier conflict.
        FluentActions
            .Invoking(() => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*batch output source*");

        cacheBefore.Should().BeTrue();
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void CompositeAll_ComposedIntoItsOwnBatch_KeepsTheStagedChain()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = CreateBatchWithCapture(ctx);

        var command = batch.FromOutput<T302TableVariableRow>("@t")
            .SelectWhereMax(
                r => new { r.V1, r.V2 },
                r => new { Id = r.Id },
                ExtremeRowTies.All,
                r => r.Grp);

        var cacheBefore = command.Cache;
        var sql = batch.Query(command).ToSql().ToLowerInvariant();

        sql.Should().Contain("declare @t table");
        sql.Should().Contain("from @t");
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

    private static BatchBuilder CreateBatchWithCapture(IDataContext ctx)
        => ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new T302TableVariableRow { Id = x.Id, V1 = x.Id, V2 = x.Id, Grp = x.Id }),
                "@t",
                "id bigint, v1 bigint, v2 bigint, grp bigint");
}

/// <summary>The captured row shape read back from the batch table variable.</summary>
public sealed class T302TableVariableRow
{
    public long Id { get; set; }
    public long V1 { get; set; }
    public long V2 { get; set; }
    public long Grp { get; set; }
}
