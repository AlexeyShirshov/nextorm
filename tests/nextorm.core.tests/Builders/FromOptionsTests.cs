using FluentAssertions;

namespace NextORM.Core.Tests;

public class FromOptionsTests
{
    [Fact]
    public void TableSample_ShouldChain()
    {
        var options = new FromOptions();

        options.TableSample(10, TableSampleMethod.Bernoulli, 42).Should().BeSameAs(options);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TableSample_WithInvalidPercent_ShouldThrow(double percent)
    {
        var act = () => new FromOptions().TableSample(percent);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Sample_ShouldChain()
    {
        var options = new FromOptions();

        options.Sample(0.1, 0.5).Should().BeSameAs(options);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Sample_WithInvalidRatio_ShouldThrow(double ratio)
    {
        var act = () => new FromOptions().Sample(ratio);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Sample_WithInvalidOffset_ShouldThrow(double offset)
    {
        var act = () => new FromOptions().Sample(0.5, offset);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
