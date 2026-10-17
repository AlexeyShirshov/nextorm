using FluentAssertions;
using NextORM.Core;
using System.Text.RegularExpressions;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation for the C# string semantics surface on SQL Server.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseFormat()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("format(dt, 'yyyy-MM-dd')");
    }

    [Fact]
    public void NumberFormat_ShouldUseFormat()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }))
            .Should().Contain("format(id, 'N2')");
    }

    [Fact]
    public void Collate_ShouldRenderCollate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "Latin1_General_100_BIN2") }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2");
    }

    [Fact]
    public void CompareOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2 < 'a'");
    }

    [Fact]
    public void ContainsOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.String!.Contains("a", StringComparison.Ordinal)).Select(x => new { x.Id }))
            .Should().Contain("somestring collate Latin1_General_100_BIN2 like '%a%'");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseRegexpLikeWithCaseSensitiveFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'c')");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldUseRegexpLikeWithIgnoreCaseFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'i')");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '[0-9]+', '#', 1, 0, 'c')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldUseIgnoreCaseFlag()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("regexp_replace(somestring, 'a', '#', 1, 0, 'i')");
    }
}
