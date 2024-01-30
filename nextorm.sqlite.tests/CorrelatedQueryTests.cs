using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlite.tests;

public class CorrelatedQueryTests(TestDataRepository sut)
{
    private readonly TestDataRepository _sut = sut;

    [Fact]
    public void TestWhere()
    {
        var r = _sut.SimpleEntity.Where(s => NORM.SQL.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id))).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(3);
    }
}