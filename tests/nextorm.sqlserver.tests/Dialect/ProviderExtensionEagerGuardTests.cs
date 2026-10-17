using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Eager-load guards for the relocated SQL Server fluent extensions. The tests never open a connection.
/// </summary>
public class ProviderExtensionEagerGuardTests
{
    public sealed class GuardParent
    {
        public int Id { get; set; }
        public decimal? Margin { get; set; }
        public int Quarter { get; set; }
        public ICollection<GuardChild> Children { get; set; } = new List<GuardChild>();
    }

    public sealed class GuardChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void Pivot_WithSingleQueryEagerState_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<GuardParent>();

        var act = () => e.LoadWith(p => p.Children, c => c.From<GuardChild>(), p => p.Id, c => c.ParentId)
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"));

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Pivot*");
    }

    [Fact]
    public void Unpivot_WithSingleQueryEagerState_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<GuardParent>();

        var act = () => e.LoadWith(p => p.Children, c => c.From<GuardChild>(), p => p.Id, c => c.ParentId)
            .Unpivot("val", "qtr", UnpivotColumn.Create("quarter"));

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Unpivot*");
    }
}
