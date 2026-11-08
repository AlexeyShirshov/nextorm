using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// T323 (#323): exact historical SQL over the legacy Select/GroupBy/HAVING surface. These are
/// generation-only assertions over the placeholder connection: the API under test is the rendered text,
/// never a connection. Every statement is asserted in full against the expected mysql rendering; the old
/// external capture/compare mode and its recorded baseline are gone, so no previous implementation is
/// used as the oracle.
/// </summary>
public class T323LegacySqlBaselineTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>The expected literal uses the file's line endings; generated SQL always uses LF.</summary>
    private static string Lf(string sql) => sql.ReplaceLineEndings("\n");

    [Fact]
    public void PlainSelect_ShouldRenderExactLegacyProjection()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .Select(x => new { x.Id, x.Name }));

        sql.Should().Contain("select");
        sql.Should().Be(Lf("""
select Id, Name
 from T323LegacyEntity
"""));
    }

    [Fact]
    public void GroupScalarCount_ShouldRenderExactGroupBy()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { C = SqlFunctions.Sql.count() }));

        sql.Should().Contain("group by");
        sql.Should().Be(Lf("""
select count(*) as `C`
 from T323LegacyEntity
 group by Id
"""));
    }

    [Fact]
    public void GroupAnonymousSum_ShouldRenderExactGroupBy()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount) }));

        sql.Should().Contain("group by");
        sql.Should().Be(Lf("""
select Id, Group2, sum(Amount) as `S`
 from T323LegacyEntity
 group by Id, Group2
"""));
    }

    [Fact]
    public void HavingGrouped_ShouldRenderExactHaving()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 10)
            .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }));

        sql.Should().Contain("group by");
        sql.Should().Contain("having");
        sql.Should().Be(Lf("""
select Id, sum(Amount) as `S`
 from T323LegacyEntity
 group by Id
 having (sum(Amount) > 10)
"""));
    }

    [Fact]
    public void HavingUngrouped_ShouldDropHavingExactly()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .Having(x => x.Id > 1)
            .Select(x => new { x.Id }));

        sql.Should().NotContain("having");
        sql.Should().Be(Lf("""
select Id
 from T323LegacyEntity
"""));
    }

    [Fact]
    public void GlobalAggregate_ShouldRenderExactSumWithoutGroupBy()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .Select(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));

        sql.Should().Contain("sum(");
        sql.Should().NotContain("group by");
        sql.Should().Be(Lf("""
select sum(Amount) as `S`
 from T323LegacyEntity
"""));
    }

    [Fact]
    public void UdfScalar_ShouldRenderExactMappedFunctionName()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .Select(x => new { V = T323LegacyUdf.MyScalar(x.Id) }));

        sql.Should().Contain("t323_my_scalar");
        sql.Should().Be(Lf("""
select t323_my_scalar(Id) as `V`
 from T323LegacyEntity
"""));
    }

    [Fact]
    public void GroupOrderBy_ShouldRenderExactOrderBy()
    {
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() })
            .OrderBy(x => x.Id));

        sql.Should().Contain("group by");
        sql.Should().Contain("order by");
        sql.Should().Be(Lf("""
select Id, count(*) as `C`
 from T323LegacyEntity
 group by Id
 order by Id
"""));
    }
}

public class T323LegacyEntity
{
    public int Id { get; set; }
    public int? Group2 { get; set; }
    public decimal Amount { get; set; }
    public string? Name { get; set; }
    public DateTime OrderedAt { get; set; }
}

public static class T323LegacyUdf
{
    [SqlFunction("t323_my_scalar")]
    public static int MyScalar(int value) => value;
}
