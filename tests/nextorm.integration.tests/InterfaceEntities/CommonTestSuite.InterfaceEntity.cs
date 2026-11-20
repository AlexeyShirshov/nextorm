using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void InterfaceEntityQueriesMatchConcreteRowsOnThisProvider()
    {
        var expected = _sut.SimpleEntityAsClass.OrderBy(row => row.Id).ToList().Select(row => row.Id).ToArray();
        expected.Should().NotBeEmpty();
        var context = _sut.DataProvider;

        var direct = context.From<ID34Simple>().OrderBy(row => row.Id).ToList();
        var identity = context.From<ID34Simple>().OrderBy(row => row.Id).Select(row => row).ToList();
        var composite = context.From<ID34Simple>()
            .Join(context.From<ID34Simple>(), (a, b) => a.Id == b.Id)
            .OrderBy(row => row.Item1.Id).ToList();

        direct.Select(row => row.Id).Should().Equal(expected);
        identity.Select(row => row.Id).Should().Equal(expected);
        composite.Select(row => row.Item1.Id).Should().Equal(expected);
        composite.Select(row => row.Item2.Id).Should().Equal(expected);
    }
}

[SqlTable("simple_entity")]
internal interface ID34Simple
{
    [Column("id")]
    int Id { get; }
}
