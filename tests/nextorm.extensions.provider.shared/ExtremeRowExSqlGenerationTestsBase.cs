using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using Xunit;

namespace NextORM.Extensions.ProviderTests;

/// <summary>
/// SQL-generation contract of <c>SelectWhereMin</c>/<c>SelectWhereMax</c> on every provider. The expected
/// statement structure is asserted directly; nothing here compares against the compatibility operator,
/// which is scheduled for removal and is therefore not an oracle.
/// </summary>
public abstract class ExtremeRowExSqlGenerationTestsBase
{
    protected abstract IDataContext CreateContext();

    protected sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public int? GroupKey { get; set; }
    }

    protected static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None))
            .SqlStmt!.Replace("\r\n", "\n");

    private static void Reset(IDataContext ctx)
    {
        if (ctx is DataContext dataContext)
            dataContext.PurgeQueryCache();
    }

    private static string OuterSelect(string sql)
    {
        var index = sql.IndexOf("from (", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? sql : sql[..index];
    }

    private static int Count(string sql, string token)
        => Regex.Matches(sql, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase).Count;

    [Fact]
    public void Ex_Provider_MinOneScalarGlobal_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One));

        ext.Should().ContainEquivalentOf("row_number()");
        ext.Should().NotContainEquivalentOf("partition by");
        ext.Should().NotContainEquivalentOf(" desc");
        ext.Should().MatchRegex(@"=\s*1");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        ext.Should().ContainEquivalentOf("Id");
        ext.Should().ContainEquivalentOf("Score");
        ext.Should().ContainEquivalentOf("Name");

        // One derived source over the table: the outer select reads it, the ranked select reads the table.
        Count(ext, "from").Should().Be(2);
        Count(ext, "join").Should().Be(0);
        Count(ext, "group by").Should().Be(0);
    }

    [Fact]
    public void Ex_Provider_MaxAllCompositeGrouped_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMax(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => x.Id));

        ext.Should().ContainEquivalentOf("max(");
        ext.Should().ContainEquivalentOf("join");
        ext.Should().ContainEquivalentOf("group by");
        ext.Should().NotContainEquivalentOf("row_number");
        ext.Should().NotContainEquivalentOf("rank(");
        ext.Should().NotContainEquivalentOf("partition by");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        ext.Should().ContainEquivalentOf("Id");
        ext.Should().ContainEquivalentOf("Score");
        ext.Should().ContainEquivalentOf("Name");

        // One aggregate stage per value component, each joined back to the source.
        Count(ext, "max(").Should().Be(2);
        Count(ext, "join").Should().Be(2);
    }

    [Fact]
    public void Ex_Provider_AllNullableGroupKey_RankWindow_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        // Providers that permit the null-safe aggregate join use that shape; ClickHouse uses
        // rank() because its JOIN ON rejects the emulated null-safe predicate (#296).
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMax(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => new { x.GroupKey, x.Name }));

        if (ext.Contains("rank(", StringComparison.OrdinalIgnoreCase))
        {
            ext.Should().ContainEquivalentOf("partition by");
            ext.Should().NotContainEquivalentOf("row_number");
            ext.Should().NotContainEquivalentOf(" join ");
            ext.Should().NotContainEquivalentOf(" is null");
        }
        else
        {
            ext.Should().ContainEquivalentOf("max(");
            ext.Should().ContainEquivalentOf(" join ");
            ext.Should().ContainEquivalentOf(" is null");
            ext.Should().NotContainEquivalentOf("rank(");
        }
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        ext.Should().ContainEquivalentOf("Score");
        ext.Should().ContainEquivalentOf("GroupKey");
    }

    [Fact]
    public void Ex_Provider_Projection_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, ctx.From<Entity>().SelectWhereMin(x => x.Score, x => new { x.Id, x.Name }, ExtremeRowTies.One));

        ext.Should().ContainEquivalentOf("row_number()");
        ext.Should().MatchRegex(@"=\s*1");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        OuterSelect(ext).Should().ContainEquivalentOf("Id");
        OuterSelect(ext).Should().ContainEquivalentOf("Name");
    }

    [Fact]
    public void Ex_Provider_Filter_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > 15)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One));

        var predicate = ext.IndexOf("15", StringComparison.Ordinal);
        var rankFilter = ext.IndexOf("= 1", StringComparison.Ordinal);
        predicate.Should().BeGreaterThan(0);
        rankFilter.Should().BeGreaterThan(0);
        predicate.Should().BeLessThan(rankFilter, "the input condition filters the source before the window is evaluated");

        Count(ext, "join").Should().Be(0);
        Count(ext, "from").Should().Be(2);
        Count(ext, "15").Should().Be(1);
    }

    [Fact]
    public void Ex_Provider_NoComponents_NoRankArtifacts()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var cmd = (QueryCommand<Entity>)ctx.From<Entity>();
        var sql = SqlOf(ctx, cmd);

        sql.Should().NotContainEquivalentOf("row_number");
        sql.Should().NotContainEquivalentOf("rank(");
        sql.Should().NotContainEquivalentOf("__nextorm_ext_rn");
        (cmd.Components is null || cmd.Components.Count == 0).Should().BeTrue();
        (cmd.PreparedComponents is null || cmd.PreparedComponents.Count == 0).Should().BeTrue();
        cmd.From.Should().NotBeNull();
        cmd.From!.SubQuery.Should().BeNull();
    }
}
