using FluentAssertions;
using NextORM.Postgres;

namespace NextORM.Postgres.Tests;

/// <summary>Verifies the provider-owned pgvector CLR types: representation, validation and arithmetic.</summary>
public sealed class PgVectorTypeTests
{
    [Fact]
    public void PgVectorArithmetic_ShouldApplyElementWise()
    {
        var left = PgVector.Parse("[1,2,3]");
        var right = PgVector.Parse("[4,5,6]");

        (left + right).Should().Be(PgVector.Parse("[5,7,9]"));
        (left - right).Should().Be(PgVector.Parse("[-3,-3,-3]"));
        (left * right).Should().Be(PgVector.Parse("[4,10,18]"));
        (left * 2f).Should().Be(PgVector.Parse("[2,4,6]"));
        (2f * left).Should().Be(PgVector.Parse("[2,4,6]"));
    }

    [Fact]
    public void PgVectorArithmetic_ShouldRejectDifferentDimensions()
    {
        var left = PgVector.Parse("[1,2]");
        var right = PgVector.Parse("[1,2,3]");

        FluentActions.Invoking(() => left + right).Should().Throw<ArgumentException>().WithParameterName("right");
        FluentActions.Invoking(() => left - right).Should().Throw<ArgumentException>().WithParameterName("right");
        FluentActions.Invoking(() => left * right).Should().Throw<ArgumentException>().WithParameterName("right");
    }

    [Fact]
    public void Normalize_ShouldProduceAUnitVector()
    {
        var normalized = PgVector.Parse("[3,4]").Normalize();

        normalized[0].Should().BeApproximately(0.6f, 0.000001f);
        normalized[1].Should().BeApproximately(0.8f, 0.000001f);
    }

    [Fact]
    public void Normalize_ShouldRejectAZeroVector()
    {
        FluentActions.Invoking(() => PgVector.Parse("[0,0]").Normalize())
            .Should().Throw<ArgumentException>()
            .WithParameterName("Normalize");
    }

    [Fact]
    public void PgHalfVector_ShouldRoundTripThroughText()
    {
        var vector = PgHalfVector.Parse("[1.5,-2,0.25]");

        vector.Dimensions.Should().Be(3);
        vector[1].Should().Be((Half)(-2f));
        PgHalfVector.Parse(vector.ToString()).Should().Be(vector);
    }

    [Fact]
    public void PgHalfVector_ShouldConvertSinglePrecisionCoordinates()
    {
        var vector = new PgHalfVector(new[] { 1.5f, 0.25f });

        vector.ToArray().Should().Equal((Half)1.5f, (Half)0.25f);
    }

    [Theory]
    [InlineData("[NaN]")]
    [InlineData("[Infinity]")]
    [InlineData("[not-a-number]")]
    [InlineData("1,2")]
    public void PgHalfVector_ShouldRejectMalformedValues(string text)
    {
        FluentActions.Invoking(() => PgHalfVector.Parse(text)).Should().Throw<FormatException>();
    }

    [Fact]
    public void PgSparseVector_ShouldDropZerosWhenBuiltFromDenseCoordinates()
    {
        var vector = new PgSparseVector(new[] { 0f, 1.5f, 0f, -2f });

        vector.Dimensions.Should().Be(4);
        vector.Count.Should().Be(2);
        vector.Indices.Span.ToArray().Should().Equal(1, 3);
        vector.Values.Span.ToArray().Should().Equal(1.5f, -2f);
        vector.ToArray().Should().Equal(0f, 1.5f, 0f, -2f);
    }

    [Fact]
    public void PgSparseVector_ShouldRoundTripThroughTextWithOneBasedIndices()
    {
        var vector = new PgSparseVector(5, new[] { 0, 2 }, new[] { 1f, 2f });

        vector.ToString().Should().Be("{1:1,3:2}/5");
        PgSparseVector.Parse(vector.ToString()).Should().Be(vector);
        PgSparseVector.Parse("{}/5").Dimensions.Should().Be(5);
        PgSparseVector.Parse("{}/5").Count.Should().Be(0);
    }

    [Fact]
    public void PgSparseVector_ShouldRejectInvalidShapes()
    {
        FluentActions.Invoking(() => new PgSparseVector(3, new[] { 1, 0 }, new[] { 1f, 2f }))
            .Should().Throw<ArgumentException>().WithParameterName("indices");
        FluentActions.Invoking(() => new PgSparseVector(3, new[] { 1, 1 }, new[] { 1f, 2f }))
            .Should().Throw<ArgumentException>().WithParameterName("indices");
        FluentActions.Invoking(() => new PgSparseVector(3, new[] { 3 }, new[] { 1f }))
            .Should().Throw<ArgumentException>().WithParameterName("indices");
        FluentActions.Invoking(() => new PgSparseVector(3, new[] { 0 }, new[] { 0f }))
            .Should().Throw<ArgumentException>().WithParameterName("values");
        FluentActions.Invoking(() => new PgSparseVector(3, new[] { 0, 1 }, new[] { 1f }))
            .Should().Throw<ArgumentException>().WithParameterName("values");
        FluentActions.Invoking(() => new PgSparseVector(-1, Array.Empty<int>(), Array.Empty<float>()))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("{1:1,3:2}")]
    [InlineData("{0:1}/5")]
    [InlineData("{1:1}/abc")]
    [InlineData("1:1/5")]
    public void PgSparseVector_ShouldRejectMalformedText(string text)
    {
        FluentActions.Invoking(() => PgSparseVector.Parse(text)).Should().Throw<FormatException>();
    }

    [Fact]
    public void PgBit_ShouldRoundTripThroughText()
    {
        var bits = PgBit.Parse("10110");

        bits.Length.Should().Be(5);
        bits[0].Should().BeTrue();
        bits[1].Should().BeFalse();
        bits.ToString().Should().Be("10110");
        new PgBit("10110").Should().Be(bits);
        PgBit.Parse("").Length.Should().Be(0);
    }

    [Theory]
    [InlineData("102")]
    [InlineData("1 0")]
    [InlineData("abc")]
    public void PgBit_ShouldRejectNonBinaryText(string text)
    {
        FluentActions.Invoking(() => PgBit.Parse(text)).Should().Throw<FormatException>();
    }

    [Fact]
    public void PgBit_ShouldCompareBits()
    {
        PgBit.Parse("101").Should().NotBe(PgBit.Parse("100"));
        PgBit.Parse("101").GetHashCode().Should().Be(new PgBit(new[] { true, false, true }).GetHashCode());
        default(PgBit).Length.Should().Be(0);
        default(PgBit).ToString().Should().BeEmpty();
    }
}
