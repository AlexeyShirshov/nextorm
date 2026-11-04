using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using Xunit;

namespace NextORM.Extensions.ProviderTests;

public abstract class ExtremeRowExSqlGenerationTestsBase
{
    protected abstract IDataContext CreateContext();

    protected virtual IDataContext CreateBuiltInReferenceContext() => CreateContext();

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
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMinEx(x => x.Score, ExtremeRowTies.One));
        var built = SqlOf(reference, (QueryCommand<Entity>)reference.From<Entity>().SelectWhereMin(x => x.Score, ExtremeRowTies.One));

        ext.Should().ContainEquivalentOf("row_number()");
        ext.Should().NotContainEquivalentOf("partition by");
        ext.Should().NotContainEquivalentOf(" desc");
        ext.Should().MatchRegex(@"=\s*1");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        ext.Should().ContainEquivalentOf("Id");
        ext.Should().ContainEquivalentOf("Score");
        ext.Should().ContainEquivalentOf("Name");

        Count(ext, "from").Should().Be(Count(built, "from"));
        Count(ext, "join").Should().Be(Count(built, "join"));
    }

    [Fact]
    public void Ex_Provider_MaxAllCompositeGrouped_Structure()
    {
        using var ctx = CreateContext();
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMaxEx(
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

        var built = SqlOf(reference, (QueryCommand<Entity>)reference.From<Entity>().SelectWhereMax(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => x.Id));

        Count(ext, "Entity").Should().Be(Count(built, "Entity"));
        Count(ext, "join").Should().Be(Count(built, "join"));
    }

    [Fact]
    public void Ex_Provider_AllNullableGroupKey_RankWindow_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        // A nullable group key selects the rank-window `All` shape: it partitions on the group keys
        // directly and never emits the null-safe `IS NULL` join predicate, which ClickHouse rejects
        // inside JOIN ON (#296). No dialect-name check is involved.
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMaxEx(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => new { x.GroupKey, x.Name }));

        ext.Should().ContainEquivalentOf("rank()");
        ext.Should().ContainEquivalentOf("partition by");
        ext.Should().NotContainEquivalentOf("row_number");
        ext.Should().NotContainEquivalentOf(" join ");
        ext.Should().NotContainEquivalentOf(" is null");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");
        ext.Should().ContainEquivalentOf("Score");
        ext.Should().ContainEquivalentOf("GroupKey");
    }

    [Fact]
    public void Ex_Provider_Projection_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, ctx.From<Entity>().SelectWhereMinEx(
            x => x.Score,
            x => new { x.Id, x.Name },
            ExtremeRowTies.One));

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
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > 15)
            .SelectWhereMinEx(x => x.Score, ExtremeRowTies.One));
        var built = SqlOf(reference, (QueryCommand<Entity>)reference.From<Entity>()
            .Where(x => x.Score > 15)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One));

        var predicate = ext.IndexOf("15", StringComparison.Ordinal);
        var rankFilter = ext.IndexOf("= 1", StringComparison.Ordinal);
        predicate.Should().BeGreaterThan(0);
        rankFilter.Should().BeGreaterThan(0);
        predicate.Should().BeLessThan(rankFilter);
        Count(ext, "join").Should().Be(Count(built, "join"));
        Count(ext, "from").Should().Be(Count(built, "from"));
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
