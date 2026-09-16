using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void TestWhere()
    {
        var r = _sut.SimpleEntity.Where(s => NORM.SQL.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(3);
    }
}
