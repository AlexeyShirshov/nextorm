using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.VectorSearch;

public sealed class VectorTests
{
    [Fact]
    public void EqualCoordinates_ShouldCompareAsEqualValues()
    {
        var first = new Vector(new[] { 1f, 2f });
        var second = new Vector(new[] { 1f, 2f });

        first.Equals(second).Should().BeTrue();
        ((object)first).Equals(second).Should().BeTrue();
        ((IEquatable<Vector>)first).Equals(second).Should().BeTrue();
        EqualityComparer<Vector>.Default.Equals(first, second).Should().BeTrue();
        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void DifferentCoordinatesOrderOrDimensions_ShouldCompareAsUnequal()
    {
        var vector = new Vector(new[] { 1f, 2f });

        vector.Equals(new Vector(new[] { 1f, 2.0000002f })).Should().BeFalse();
        vector.Equals(new Vector(new[] { 2f, 1f })).Should().BeFalse();
        vector.Equals(new Vector(new[] { 1f, 2f, 3f })).Should().BeFalse();
        (vector == new Vector(new[] { 2f, 1f })).Should().BeFalse();
        (vector != new Vector(new[] { 1f, 2f, 3f })).Should().BeTrue();
    }

    [Fact]
    public void ReferenceNullAndUnrelatedObjectComparisons_ShouldBeHandled()
    {
        var vector = new Vector(new[] { 1f, 2f });
        Vector? nullableVector = vector;
        Vector? firstNull = null;
        Vector? secondNull = null;

        vector.Should().BeSameAs(vector);
        vector!.Equals(vector).Should().BeTrue();
        vector!.Equals((Vector?)null).Should().BeFalse();
        vector!.Equals((object?)null).Should().BeFalse();
        vector!.Equals(new object()).Should().BeFalse();
        ((IEquatable<Vector>)vector!).Equals(null).Should().BeFalse();
        EqualityComparer<Vector>.Default.Equals(firstNull, secondNull).Should().BeTrue();

        (nullableVector == null).Should().BeFalse();
        (null == nullableVector).Should().BeFalse();
        (nullableVector != null).Should().BeTrue();
        (null != nullableVector).Should().BeTrue();
        (firstNull == secondNull).Should().BeTrue();
        (firstNull != secondNull).Should().BeFalse();
    }

    [Fact]
    public void EmptyVectors_ShouldCompareAsEqualValues()
    {
        var first = new Vector(Array.Empty<float>());
        var second = Vector.Parse("[]");

        first.Dimensions.Should().Be(0);
        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void SignedZeroCoordinates_ShouldCompareAndHashEqually()
    {
        var positiveZero = new Vector(new[] { 0f });
        var negativeZero = new Vector(new[] { -0f });

        positiveZero.Equals(negativeZero).Should().BeTrue();
        (positiveZero == negativeZero).Should().BeTrue();
        positiveZero.GetHashCode().Should().Be(negativeZero.GetHashCode());
    }

    [Fact]
    public void ParsedAndConstructedVectors_ShouldCompareAsEqualValues()
    {
        var constructed = new Vector(new[] { 1.25f, -2.5f });
        var parsed = Vector.Parse("[1.25,-2.5]");

        constructed.Equals(parsed).Should().BeTrue();
        constructed.GetHashCode().Should().Be(parsed.GetHashCode());
    }

    [Fact]
    public void HashBasedCollectionsAndDistinct_ShouldUseValueEquality()
    {
        var first = new Vector(new[] { 1f, 2f });
        var equal = new Vector(new[] { 1f, 2f });
        var different = new Vector(new[] { 1f, 3f });
        var set = new HashSet<Vector> { first, equal, different };
        var dictionary = new Dictionary<Vector, string> { [first] = "matched" };

        set.Count.Should().Be(2);
        set.Contains(equal).Should().BeTrue();
        dictionary.TryGetValue(equal, out var value).Should().BeTrue();
        value.Should().Be("matched");
        new[] { first, equal, different }.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void ConstructorInputAndToArrayCopies_ShouldNotChangeVectorEqualityOrHash()
    {
        var source = new[] { 1f, 2f };
        var vector = new Vector(source);
        var expected = new Vector(new[] { 1f, 2f });
        var hash = vector.GetHashCode();

        source[0] = 99f;
        var copy = vector.ToArray();
        copy[1] = 88f;

        vector.Equals(expected).Should().BeTrue();
        vector.GetHashCode().Should().Be(hash);
    }
}
