using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public sealed class PostgresArrayElementIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void ReadsScalarAndPreservesNativeBoundsAndNulls()
    {
        using var context = Provider.CreateContext();
        var result = context.FromSql("""
            select '{{1,2,3},{4,5,6}}'::real[] as matrix,
                   '[-2:0]={7,NULL,9}'::integer[] as shifted,
                   '{{{{{{42}}}}}}'::integer[] as six,
                   null::integer[] as missing,
                   '{}'::integer[] as empty,
                   '{hello}'::text[] as words
            """)
            .Select(x => new
            {
                Matrix = SqlFunctions.Postgres.array_element(SqlFunctions.Column<float[,]>(x, "matrix"), 2, 3),
                Shifted = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[]>(x, "shifted"), -2),
                NullElement = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int?[]>(x, "shifted"), -1),
                Outside = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[]>(x, "shifted"), 1),
                Mismatch = SqlFunctions.Postgres.array_element(SqlFunctions.Column<float[]>(x, "matrix"), 2),
                Missing = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[]>(x, "missing"), 1),
                Empty = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[]>(x, "empty"), 1),
                Six = SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[,,,,,]>(x, "six"), 1, 1, 1, 1, 1, 1),
                Word = SqlFunctions.Postgres.array_element(SqlFunctions.Column<string[]>(x, "words"), 1)
            })
            .First();

        result.Matrix.Should().Be(6f);
        result.Shifted.Should().Be(7);
        result.NullElement.Should().BeNull();
        result.Outside.Should().BeNull();
        result.Mismatch.Should().BeNull();
        result.Missing.Should().BeNull();
        result.Empty.Should().BeNull();
        result.Six.Should().Be(42);
        result.Word.Should().Be("hello");
    }
}
