using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Exercises <see cref="SqlFunctionAttribute"/> against a real database. The mapped helpers point at
/// portable built-in functions (<c>upper</c>, <c>abs</c>, <c>coalesce</c>) so no object has to be
/// created in the target database.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void UserDefinedFunction_Upper_ShouldConvertToUpperCase()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => Udf.ToUpper(e.String!))
            .First();

        value.Should().Be("XXX");
    }

    [Fact]
    public void UserDefinedFunction_Abs_ShouldReturnAbsoluteValue()
    {
        var values = _sut.ComplexEntity
            .Select(e => Udf.Abs(e.Id - 5))
            .ToList();

        // ids 1, 2 and 3 become 4, 3 and 2.
        values.OrderBy(x => x).Should().Equal(2L, 3L, 4L);
    }

    [Fact]
    public void UserDefinedFunction_WithCapturedParameter_ShouldPassParameterThrough()
    {
        var fallback = 42;

        var values = _sut.ComplexEntity
            .OrderBy(e => e.Id)
            .Select(e => Udf.Coalesce(e.Int, fallback))
            .ToList();

        // row 1 has a null nullableint, rows 2 and 3 have 1.
        values.Should().Equal(42, 1, 1);
    }

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();

        [SqlFunction("abs")]
        public static long Abs(long value) => throw new NotSupportedException();

        [SqlFunction("coalesce")]
        public static int? Coalesce(int? value, int? fallback) => throw new NotSupportedException();
    }
}
