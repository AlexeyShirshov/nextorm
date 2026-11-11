using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep4BaseExpressionVisitorTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void ConstantAddition_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => 1 + 2).Single().Should().Be(3);
    }

    [Fact]
    public void ConstantMultiplication_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => 3 * 4).Single().Should().Be(12);
    }

    [Fact]
    public void ConstantDivision_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => 10 / 2).Single().Should().Be(5);
    }

    [Fact]
    public void ConstantModulo_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => 10 % 3).Single().Should().Be(1);
    }

    [Fact]
    public void StringConcatenation_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => "a" + "b").Single().Should().Be("ab");
    }

    [Fact]
    public void Coalesce_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => (string?)null ?? "fallback").Single().Should().Be("fallback");
    }

    [Fact]
    public void Conditional_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => x.Id == 1 ? "yes" : "no").Single().Should().Be("yes");
    }

    [Fact]
    public void BooleanAnd_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => x.Id > 0 && x.Id < 10).Single().Should().BeTrue();
    }

    [Fact]
    public void BooleanOr_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => x.Id == 2 || x.Id == 1).Single().Should().BeTrue();
    }

    [Fact]
    public void ComparisonEqual_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => x.Id == 1).Single().Should().BeTrue();
    }

    [Fact]
    public void ComparisonGreater_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => x.Id > 0).Single().Should().BeTrue();
    }

    [Fact]
    public void Negation_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => !(x.Id == 2)).Single().Should().BeTrue();
    }

    [Fact]
    public void MathAbs_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => Math.Abs(-5)).Single().Should().Be(5);
    }

    [Fact]
    public void MathMax_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => Math.Max(3, 7)).Single().Should().Be(7);
    }

    [Fact]
    public void MathMin_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => Math.Min(3, 7)).Single().Should().Be(3);
    }

    [Fact]
    public void MathRound_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => Math.Round(2.4)).Single().Should().Be(2.0);
    }

    [Fact]
    public void DateTimeNow_ShouldMaterialize()
    {
        var r = Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => DateTime.Now.Year).Single();
        r.Should().Be(DateTime.Now.Year);
    }

    [Fact]
    public void GuidNewGuid_ShouldMaterialize()
    {
        var r = Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => Guid.NewGuid() != Guid.Empty).Single();
        r.Should().BeTrue();
    }

    [Fact]
    public void StringLength_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => "abcd".Length).Single().Should().Be(4);
    }

    [Fact]
    public void ArrayContains_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => new[] { 1, 2, 3 }.Contains(2)).Single().Should().BeTrue();
    }
    [Fact]
    public void ConstantSubtraction_ShouldMaterialize()
    {
        Context.From<ISimpleEntity>().Where(x => x.Id == 1).Select(x => 5 - 2).Single().Should().Be(3);
    }

}
