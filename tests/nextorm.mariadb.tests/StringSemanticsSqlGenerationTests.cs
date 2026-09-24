using FluentAssertions;
using NextORM.Core;

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
}
