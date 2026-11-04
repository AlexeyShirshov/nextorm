using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("MySql")]
public sealed class MySqlStep2UnitBTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    [Fact]
    public void LpadTwoArguments_ShouldUseDefaultSpace()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left = SqlFunctions.Sql.lpad("abc", 5),
                Right = SqlFunctions.Sql.rpad("abc", 5)
            })
            .First();

        r.Left.Should().Be("  abc");
        r.Right.Should().Be("abc  ");
    }

    [Fact]
    public void ExportSetCountOutOfRange_ShouldBeRefusedBeforeSql()
    {
        FluentActions.Invoking(() => _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.MySql.export_set(5UL, "Y", "N", ",", 65))
            .First())
            .Should().Throw<NotSupportedException>().WithMessage("The export_set function requires a bit count between 0 and 64 (got 65).");
    }
}
