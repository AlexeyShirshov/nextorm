using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep4StringFunctionResidualTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void ToUpper_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.ToUpper()).Single().Should().Be("SDF");
    }

    [Fact]
    public void ToLower_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.ToLower()).Single().Should().Be("sdf");
    }

    [Fact]
    public void Trim_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => "  sdf  ".Trim()).Single().Should().Be("sdf");
    }

    [Fact]
    public void TrimStart_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => "  sdf".TrimStart()).Single().Should().Be("sdf");
    }

    [Fact]
    public void TrimEnd_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => "sdf  ".TrimEnd()).Single().Should().Be("sdf");
    }

    [Fact]
    public void Substring_OneArg_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Substring(1)).Single().Should().Be("df");
    }

    [Fact]
    public void Substring_TwoArgs_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Substring(0, 2)).Single().Should().Be("sd");
    }

    [Fact]
    public void Replace_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Replace("s", "z")).Single().Should().Be("zdf");
    }

    [Fact]
    public void Remove_OneArg_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Remove(1)).Single().Should().Be("s");
    }

    [Fact]
    public void Remove_TwoArgs_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Remove(1, 1)).Single().Should().Be("sf");
    }

    [Fact]
    public void Insert_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Insert(1, "X")).Single().Should().Be("sXdf");
    }

    [Fact]
    public void IndexOf_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.IndexOf("d")).Single().Should().Be(1);
    }

    [Fact]
    public void Contains_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Contains("df")).Single().Should().BeTrue();
    }

    [Fact]
    public void StartsWith_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.StartsWith("sd")).Single().Should().BeTrue();
    }

    [Fact]
    public void EndsWith_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.EndsWith("df")).Single().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrEmpty_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => string.IsNullOrEmpty(x.RequiredString)).Single().Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.Equals("sdf")).Single().Should().BeTrue();
    }

    [Fact]
    public void CompareOrdinal_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => string.CompareOrdinal(x.RequiredString, "sdf")).Single().Should().Be(0);
    }

    [Fact]
    public void PadLeft_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.PadLeft(5, 'x')).Single().Should().Be("xxsdf");
    }

    [Fact]
    public void PadRight_ShouldMaterialize()
    {
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(x => x.RequiredString.PadRight(5, 'x')).Single().Should().Be("sdfxx");
    }

}
