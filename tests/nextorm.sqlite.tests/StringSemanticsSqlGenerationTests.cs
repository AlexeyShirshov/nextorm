using FluentAssertions;
using NextORM.Core;
using System.Text.RegularExpressions;

namespace NextORM.Sqlite.Tests;

/// <summary>SQL generation for the C# string semantics surface on SQLite.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseStrftime()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("strftime('%Y-%m-%d', dt)");
    }

    [Fact]
    public void FixedPoint_ShouldUsePrintf()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("{0:F2}", x.Id) }))
            .Should().Contain("printf('%.2f', id)");
    }

    [Fact]
    public void Collate_ShouldRenderCollate()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "BINARY") }))
            .Should().Contain("somestring collate BINARY");
    }

    [Fact]
    public void CompareOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring collate binary < 'a'");
    }

    [Fact]
    public void GroupedNumber_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*'N'*");
    }

    [Fact]
    public void ContainsOrdinalCaseSensitive_ShouldThrowBecauseLikeIsCaseInsensitive()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.String!.Contains("X", StringComparison.Ordinal)).Select(x => new { x.Id }));
        act.Should().Throw<NotSupportedException>().WithMessage("*ordinal*");
    }

    [Fact]
    public void ContainsOrdinalIgnoreCase_ShouldRenderLowerLike()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("X", StringComparison.OrdinalIgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("lower(somestring) collate binary like lower('%X%')");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseRegexpOperator()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("somestring regexp '^a'");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldPrefixIgnoreCaseFlag()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("somestring regexp '(?i)^a'");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '[0-9]+', '#')");
    }
}
