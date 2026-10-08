using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Eager-load guards for the relocated SQL Server fluent extensions. The tests never open a connection.
/// </summary>
public class ProviderExtensionEagerGuardTests
{
    [Fact]
    public void Pivot_WithSingleQueryEagerState_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        var act = () => e.AsSingleQuery()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));

        act.Should().Throw<NotSupportedException>().WithMessage("*AsSingleQuery*Pivot*");
    }

    [Fact]
    public void Unpivot_WithSingleQueryEagerState_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        var act = () => e.AsSingleQuery()
            .Unpivot("val", "qtr", UnpivotColumn.Create("quarter"));

        act.Should().Throw<NotSupportedException>().WithMessage("*AsSingleQuery*Unpivot*");
    }
}
