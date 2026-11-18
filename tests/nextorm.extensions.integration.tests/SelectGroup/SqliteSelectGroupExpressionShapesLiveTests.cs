using FluentAssertions;
using NextORM.Extensions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class SqliteSelectGroupExpressionShapesLiveTests : ProviderTestSuite
{
    private EntityBuilder<ExtremaEntity> Extrema => _sut.DataProvider.From<ExtremaEntity>();

    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void SelectGroup_ConditionalAndConvertedKeys_ExactGroups()
    {
        var rows = Extrema.SelectGroup(
            x => new
            {
                Category = x.Category == null ? "(unknown)" : x.Category.ToUpper(),
                Parity = (long)(x.Id % 2),
                Rows = SqlFunctions.Sql.count(x.Id)
            }).ToList();

        rows.OrderBy(row => row.Category, StringComparer.Ordinal)
            .ThenBy(row => row.Parity)
            .Select(row => (row.Category, row.Parity, row.Rows))
            .Should().Equal(
                ("(unknown)", 0L, 1),
                ("(unknown)", 1L, 1),
                ("A", 0L, 2),
                ("A", 1L, 2),
                ("B", 0L, 2),
                ("B", 1L, 1),
                ("C", 1L, 1),
                ("D", 1L, 1));
    }

    [Fact]
    public void SelectGroupHaving_ComputedKeysAndMatchingSourceHaving_ExactGroup()
    {
        var rows = Extrema
            .Having(x =>
                (long)(x.Id % 2) == 1
                && (x.Category == null ? "(unknown)" : x.Category.ToUpper()) != "B")
            .SelectGroupHaving(
                x => new
                {
                    Category = x.Category == null ? "(unknown)" : x.Category.ToUpper(),
                    Parity = (long)(x.Id % 2),
                    Rows = SqlFunctions.Sql.count(x.Id)
                },
                group => group.Category == "A" && group.Rows > 1)
            .ToList();

        rows.Select(row => (row.Category, row.Parity, row.Rows))
            .Should().Equal(("A", 1L, 2));
    }

    [Fact]
    public void SelectGroupHaving_StaticMethodCallModuloKeys_ExactGroup()
    {
        var rows = Extrema
            .Having(x => Math.Abs(x.Id % 2) == 1)
            .SelectGroupHaving(
                x => new
                {
                    Odd = Math.Abs(x.Id % 2),
                    OddAgain = Math.Abs(x.Id % 2),
                    ModThree = Math.Abs(x.Id % 3),
                    Rows = SqlFunctions.Sql.count(x.Id)
                },
                group => group.ModThree == 0 && group.Rows > 1)
            .ToList();

        rows.Select(row => (row.Odd, row.OddAgain, row.ModThree, row.Rows))
            .Should().Equal((1, 1, 0, 2));
    }

    [Fact]
    public void SelectGroupHaving_CoalesceKeyAndMatchingSourceHaving_ExactGroup()
    {
        var rows = Extrema
            .Having(x => (x.Score ?? 0) >= 1)
            .SelectGroupHaving(
                x => new
                {
                    Score = x.Score ?? 0,
                    Rows = SqlFunctions.Sql.count(x.Id)
                },
                group => group.Score > 1 && group.Rows > 1)
            .ToList();

        rows.Select(row => (row.Score, row.Rows))
            .Should().Equal((9, 2));
    }

    [Fact]
    public void SelectGroupHaving_SubstringKeys_DistinctArgumentsRepeatedKeyAndNullGroup()
    {
        var rows = Extrema
            .Having(x => x.Category == null || x.Category!.Substring(0, 2) != "b")
            .SelectGroupHaving(
                x => new
                {
                    Category = x.Category,
                    First = x.Category!.Substring(0, 1),
                    Prefix = x.Category!.Substring(0, 2),
                    FirstAgain = x.Category!.Substring(0, 1),
                    Rows = SqlFunctions.Sql.count(x.Id)
                },
                group => (group.First == null || group.First != "d") && group.Rows > 1)
            .ToList();

        rows.Should().HaveCount(2);

        var nullGroup = rows.Single(row => row.Category == null);
        nullGroup.First.Should().BeNull();
        nullGroup.Prefix.Should().BeNull();
        nullGroup.FirstAgain.Should().BeNull();
        nullGroup.Rows.Should().Be(2);

        var categoryA = rows.Single(row => row.Category == "a");
        (categoryA.First, categoryA.Prefix, categoryA.FirstAgain, categoryA.Rows)
            .Should().Be(("a", "a", "a", 4));
    }
}
