using FluentAssertions;
using NextORM.Core;
using System.Text.RegularExpressions;

namespace NextORM.MySql.Tests;

/// <summary>SQL generation for the C# string semantics surface on MySQL.</summary>
public class StringSemanticsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void DateFormat_ShouldUseDateFormat()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = x.Datetime!.Value.ToString("yyyy-MM-dd") }))
            .Should().Contain("date_format(dt, '%Y-%m-%d')");
    }

    [Fact]
    public void NumberFormat_ShouldUseFormat()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = string.Format("{0:N2}", x.Id) }))
            .Should().Contain("format(id, 2)");
    }

    [Fact]
    public void Collate_ShouldRenderCollate()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = SqlFunctions.Sql.collate(x.String, "utf8mb4_bin") }))
            .Should().Contain("somestring collate utf8mb4_bin");
    }

    [Fact]
    public void CompareOrdinal_ShouldUseBinaryCollation()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => string.CompareOrdinal(x.String, "a") > 0).Select(x => new { x.Id }))
            .Should().Contain("somestring collate utf8mb4_bin < 'a'");
    }

    [Fact]
    public void FixedPoint_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { F = string.Format("{0:F2}", x.Id) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*'F'*");
    }

    [Fact]
    public void RegexIsMatch_ShouldUseRegexpLike()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a")).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'c')");
    }

    [Fact]
    public void RegexIsMatchIgnoreCase_ShouldUseIgnoreCaseMatchType()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => Regex.IsMatch(x.String!, "^a", RegexOptions.IgnoreCase)).Select(x => new { x.Id }))
            .Should().Contain("regexp_like(somestring, '^a', 'i')");
    }

    [Fact]
    public void RegexReplace_ShouldUseRegexpReplace()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "[0-9]+", "#") }))
            .Should().Contain("regexp_replace(somestring, '[0-9]+', '#', 1, 0, 'c')");
    }

    [Fact]
    public void RegexReplaceIgnoreCase_ShouldUseIgnoreCaseMatchType()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { F = Regex.Replace(x.String!, "a", "#", RegexOptions.IgnoreCase) }))
            .Should().Contain("regexp_replace(somestring, 'a', '#', 1, 0, 'i')");
    }
}
