using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

public sealed class T406OutputIntoBatchTests
{
    [Fact]
    public void RendersParameterizedWriteBeforeFilteredReadWithoutDeclaringCapture()
    {
        using var ctx = SqlServerTestContext.Create();
        var cutoff = 42;
        var mutation = ctx.CreateInsertBuilder<ISimpleEntity>().Value(x => x.Id, 41)
            .Returning(x => new { x.Id }).OutputInto("capture_log");
        var batch = ctx.CreateBatchBuilder().OutputInto(mutation);

        batch.Query(ctx.From("capture_log").Where(row => row.GetInt32("id") <= cutoff)
                .Select(row => row.GetInt32("id"))).ToSql()
            .Should().Contain("output inserted.id into capture_log (id)")
            .And.Contain("; select").And.Contain("from capture_log")
            .And.Contain("where").And.NotContain("declare");
    }

    [Fact]
    public void RejectsStandaloneTableVariableTerminalWithRegistrationGuidance()
    {
        using var ctx = SqlServerTestContext.Create();
        var mutation = ctx.CreateInsertBuilder<ISimpleEntity>().Value(x => x.Id, 41)
            .Returning(x => new { x.Id }).OutputIntoTableVariable("@capture", "id int");

        FluentActions.Invoking(() => ctx.CreateBatchBuilder().OutputInto(mutation))
            .Should().Throw<ArgumentException>().WithParameterName("mutation")
            .WithMessage("*OutputIntoTableVariable registration*");
    }
}
