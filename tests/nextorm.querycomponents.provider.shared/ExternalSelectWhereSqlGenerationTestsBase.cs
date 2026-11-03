using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.QueryComponents;
using Xunit;

namespace NextORM.QueryComponents.ProviderTests;

public abstract class ExternalSelectWhereSqlGenerationTestsBase
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
    public void Ext_Provider_MinOneScalarGlobal_Structure()
    {
        using var ctx = CreateContext();
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One));
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
    public void Ext_Provider_MaxAllCompositeGrouped_Structure()
    {
        using var ctx = CreateContext();
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>().ExternalSelectWhereMax(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => new { x.GroupKey, x.Name }));

        ext.Should().ContainEquivalentOf("rank()");
        ext.Should().ContainEquivalentOf("partition by");
        ext.Should().ContainEquivalentOf(" desc");
        ext.Should().MatchRegex(@"=\s*1");
        OuterSelect(ext).Should().NotContainEquivalentOf("__nextorm_ext_rn");

        var built = SqlOf(reference, (QueryCommand<Entity>)reference.From<Entity>().SelectWhereMax(
            x => new { x.Score, x.Name },
            ExtremeRowTies.All,
            x => new { x.GroupKey, x.Name }));

        // The built-in All arm uses aggregate lowering; the external arm uses RANK. The external arm
        // must never add joins/scans relative to the reference even though the strategies differ.
        Count(ext, "join").Should().BeLessThanOrEqualTo(Count(built, "join"));
        Count(ext, "from").Should().BeLessThanOrEqualTo(Count(built, "from"));
    }

    [Fact]
    public void Ext_Provider_Projection_Structure()
    {
        using var ctx = CreateContext();
        Reset(ctx);
        var ext = SqlOf(ctx, ctx.From<Entity>().ExternalSelectWhereMin(
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
    public void Ext_Provider_Filter_Structure()
    {
        using var ctx = CreateContext();
        using var reference = CreateBuiltInReferenceContext();
        Reset(ctx);
        Reset(reference);
        var ext = SqlOf(ctx, (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > 15)
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.One));
        var built = SqlOf(reference, (QueryCommand<Entity>)reference.From<Entity>()
            .Where(x => x.Score > 15)
            .SelectWhereMin(x => x.Score, ExtremeRowTies.One));

        var predicate = ext.IndexOf("15", StringComparison.Ordinal);
        var rankFilter = ext.IndexOf("= 1", StringComparison.Ordinal);
        predicate.Should().BeGreaterThan(0);
        rankFilter.Should().BeGreaterThan(0);
        predicate.Should().BeLessThan(rankFilter);
        Count(ext, "join").Should().Be(Count(built, "join"));
    }

    [Fact]
    public void Ext_Provider_NoComponents_NoRankArtifacts()
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
