using FluentAssertions;
using NextORM.Core;
using System.Text.RegularExpressions;

namespace NextORM.ClickHouse.Tests;

/// <summary>SQL generation for the C# string semantics surface on ClickHouse.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseFormatDateTime()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("formatDateTime(dt, '%Y-%m-%d')");
    }

    [Fact]
    public void FixedPoint_ShouldUseFormat()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("{0:F2}", x.Id) }))
            .Should().Contain("format('{:.2f}', id)");
    }

    [Fact]
    public void CompareOrdinal_ShouldKeepNativeByteOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring < 'a'");
    }

    [Fact]
    public void Collate_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "C") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*collation*");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseMatch()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("match(somestring, '^a')");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldPrefixIgnoreCaseFlag()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("match(somestring, '(?i)^a')");
    }

    [Fact]
    public void RegexReplace_ShouldUseReplaceRegexpAll()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("replaceRegexpAll(somestring, '[0-9]+', '#')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldPrefixIgnoreCaseFlag()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("replaceRegexpAll(somestring, '(?i)a', '#')");
    }
}
