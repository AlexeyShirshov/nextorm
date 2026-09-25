using FluentAssertions;
using NextORM.Core;
using System.Text.RegularExpressions;

namespace NextORM.MariaDb.Tests;

/// <summary>SQL generation for the C# string semantics surface on MariaDB.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseDateFormat()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("date_format(dt, '%Y-%m-%d')");
    }

    [Fact]
    public void Collate_ShouldRenderCollate()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "utf8mb4_bin") }))
            .Should().Contain("somestring collate utf8mb4_bin");
    }

    [Fact]
    public void CompareOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring collate utf8mb4_bin < 'a'");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseRegexpOperatorWithCaseSensitiveFlag()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("somestring regexp '(?-i)^a'");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldUseRegexpOperatorWithIgnoreCaseFlag()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("somestring regexp '(?i)^a'");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '(?-i)[0-9]+', '#')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldUseIgnoreCaseFlag()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("regexp_replace(somestring, '(?i)a', '#')");
    }
}
