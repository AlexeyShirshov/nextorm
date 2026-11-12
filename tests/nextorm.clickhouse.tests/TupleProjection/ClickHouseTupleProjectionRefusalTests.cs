using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// The pre-reader refusal for ClickHouse tuple columns. <c>ClickHouse.Driver</c> materializes a native
/// <c>Tuple(...)</c> column into <c>System.Tuple&lt;...&gt;</c> through a run-time constructor lookup
/// (<c>TupleType.Read</c> → <c>Activator.CreateInstance</c>) that a trimmed/native image does not keep,
/// so the shape must fail closed at preparation with an actionable message rather than as an opaque
/// driver type-initialization failure. Under JIT the same shape is readable and is not refused.
/// </summary>
public sealed class ClickHouseTupleProjectionRefusalTests
{
    [Theory]
    [InlineData(typeof(Tuple<double, double>))]
    [InlineData(typeof(Tuple<byte, double>[]))]
    [InlineData(typeof(ValueTuple<long, long>))]
    [InlineData(typeof(ValueTuple<string, int>[]))]
    public void ShouldRefuseDriverTuple_WithoutDynamicCode_RefusesTheTupleShape(Type propertyType)
        => ClickHouseDataContext.ShouldRefuseDriverTuple(propertyType, dynamicCodeSupported: false)
            .Should().BeTrue("the driver's run-time tuple construction is unavailable in a trimmed/native image");

    [Theory]
    [InlineData(typeof(Tuple<double, double>))]
    [InlineData(typeof(Tuple<byte, double>[]))]
    public void ShouldRefuseDriverTuple_WithDynamicCode_KeepsTheShape(Type propertyType)
        => ClickHouseDataContext.ShouldRefuseDriverTuple(propertyType, dynamicCodeSupported: true)
            .Should().BeFalse("JIT keeps the tuple constructor and the projection works");

    [Theory]
    [InlineData(typeof(double[]))]
    [InlineData(typeof(string))]
    [InlineData(typeof(Dictionary<string, string>))]
    [InlineData(typeof(List<Tuple<double, double>>))]
    public void ShouldRefuseDriverTuple_ForNonTupleShapes_IsFalse(Type propertyType)
        => ClickHouseDataContext.ShouldRefuseDriverTuple(propertyType, dynamicCodeSupported: false)
            .Should().BeFalse();

    [Fact]
    public void ThrowIfDriverTuple_WithoutDynamicCode_ThrowsTheActionableRefusal()
    {
        var column = new SelectExpression(typeof(Tuple<byte, double>[])) { PropertyName = "RetentionMatrix" };

        FluentActions.Invoking(() => ClickHouseDataContext.ThrowIfDriverTuple(column, column.PropertyType, dynamicCodeSupported: false))
            .Should().Throw<NotSupportedException>()
            .Which.Message.Should()
            .Contain("RetentionMatrix")
            .And.Contain("ClickHouse.Driver")
            .And.Contain("Activator.CreateInstance")
            .And.Contain("tupleElement");
    }

    [Fact]
    public void ThrowIfDriverTuple_WithDynamicCode_DoesNotThrow()
    {
        var column = new SelectExpression(typeof(Tuple<byte, double>[])) { PropertyName = "RetentionMatrix" };

        FluentActions.Invoking(() => ClickHouseDataContext.ThrowIfDriverTuple(column, column.PropertyType, dynamicCodeSupported: true))
            .Should().NotThrow("JIT keeps the tuple constructor and the column is readable");
    }
}
