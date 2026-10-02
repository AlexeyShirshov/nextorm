using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation of global query filters on SQL Server (#108): the filter reaches the SELECT
/// <c>WHERE</c>, a selective ignore drops only the named predicate, the target filter is ANDed to the
/// UPDATE/DELETE <c>WHERE</c>, an INSERT target is never filtered and an INSERT ... SELECT filters only
/// its source. No database connection is opened.
/// </summary>
public class QueryFilterSqlGenerationTests
{
    private const string TenantKey = "qf_tenant_mssql_sqlgen";

    [SqlTable("qf_select_entity")]
    public sealed class QfSelectEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    [SqlTable("qf_insert_source")]
    public sealed class QfInsertSourceEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    [SqlTable("qf_insert_target")]
    public sealed class QfInsertTargetEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    private static void ConfigureSelect(IDataContext ctx)
    {
        ctx.Properties[TenantKey] = 1;
        ctx.From<QfSelectEntity>(b => b
            .HasQueryFilter((e, _) => e.Id > 5)
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
    }

    private static string SelectSql(IDataContext ctx, EntityBuilder<QfSelectEntity> builder)
    {
        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(x => x.Id), false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public void Select_Filter_ShouldRenderInWhere()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>());

        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Select_SelectiveIgnoreByKey_ShouldDropOnlyNamedFilter()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>().IgnoreFilters(["tenant"]));

        sql.Should().Contain("id > 5");
        sql.Should().NotContain("tenant_id = @");
    }

    [Fact]
    public void Update_Where_ShouldAndFilter()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.CreateUpdateBuilder<QfSelectEntity>()
            .Set(x => x.IsDeleted, true)
            .Where(x => x.Id == 10)
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("id = 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Delete_Where_ShouldAndFilter()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.CreateDeleteBuilder<QfSelectEntity>()
            .Where(x => x.Id == 10)
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("id = 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Insert_Target_ShouldNotBeFiltered()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.Properties[TenantKey] = 1;

        var sql = ctx.CreateInsertBuilder<QfInsertTargetEntity>(b => b
                .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .Values(new QfInsertTargetEntity { Id = 1, TenantId = 1, IsDeleted = false })
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().StartWith("insert into qf_insert_target");
        sql.Should().NotContain("where");
    }

    [Fact]
    public void InsertSelect_SourceFiltered_TargetNotFiltered()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.Properties[TenantKey] = 1;
        ctx.From<QfInsertSourceEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.CreateInsertBuilder<QfInsertTargetEntity>(b => b
                .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .Values(
                ctx.From<QfInsertSourceEntity>().Where(x => x.Id > 0),
                s => new { s.Id, s.TenantId, s.IsDeleted })
            .ToSql()
            .Replace("\r\n", "\n");

        sql.Should().Contain("from qf_insert_source");
        CountOccurrences(sql, "tenant_id = @").Should().Be(1, "only the source filter renders");
    }

    // --- PR4: the builder-function (FilterFunc) form renders the same bound parameter as the
    // --- context-based predicate form, on both the main source and a join ON condition.

    private const string FuncTenantKey = "qf_func_tenant_sqlgen";

    [SqlTable("qf_func_entity")]
    public sealed class QfFuncEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("qf_func_join_left")]
    public sealed class QfFuncJoinLeftEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("qf_func_join_right")]
    public sealed class QfFuncJoinRightEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("qf_func_parameter_entity")]
    public sealed class QfFuncParameterEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    private static string FuncSelectSql(IDataContext ctx, EntityBuilder<QfFuncEntity> builder)
    {
        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(x => x.Id), false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    [Fact]
    public void FuncFilter_Select_ShouldRenderBoundParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.Properties[FuncTenantKey] = 1;
        ctx.From<QfFuncEntity>(b => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[FuncTenantKey])));

        var sql = FuncSelectSql(ctx, ctx.From<QfFuncEntity>());

        sql.Should().Contain("tenant_id = @");
        sql.Should().NotContain("= 1", "the context value is bound, not inlined into the SQL");
    }

    [Fact]
    public void FuncFilter_JoinOn_ShouldRenderBoundParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.Properties[FuncTenantKey] = 1;
        ctx.From<QfFuncJoinRightEntity>(b => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[FuncTenantKey])));

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<QfFuncJoinLeftEntity>()
                .Join(ctx.From<QfFuncJoinRightEntity>(), (l, r) => l.Id == r.Id)
                .Select(p => p.Item2.TenantId),
            false, false, CancellationToken.None);

        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");
        CountOccurrences(sql, "tenant_id = @").Should().Be(1, "the function filter is injected into the join ON condition");
        sql.Should().NotContain("= 1", "the context value is bound, not inlined into the ON condition");
    }

    [Fact]
    public void FuncFilter_ParameterPlaceholder_ShouldRenderBoundParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.From<QfFuncParameterEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => e.TenantId == SqlFunctions.Parameter<int>(0))));

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<QfFuncParameterEntity>().Select(x => x.Id), false, false, CancellationToken.None);

        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");
        sql.Should().Contain("tenant_id = @");
        sql.Should().NotContain("= 0", "the placeholder is bound, not inlined");
    }

    [Fact]
    public void FuncFilter_CachedPlan_SecondExecution_ShouldBindChangedValue()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.Properties[FuncTenantKey] = 1;
        ctx.From<QfFuncEntity>(b => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[FuncTenantKey])));

        // First execution stores the plan with the tenant-1 value.
        var first = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<QfFuncEntity>().Select(x => x.Id), false, true, CancellationToken.None);
        first.DbCommand.Parameters.Count.Should().Be(1);
        first.DbCommand.Parameters[0].Value.Should().Be(1);

        // The same shape executes on the cached plan after the property changed: the value is re-bound per
        // execution, so an implementation that inlined the first literal would fail the assertion below.
        ctx.Properties[FuncTenantKey] = 2;
        var second = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<QfFuncEntity>().Select(x => x.Id), false, true, CancellationToken.None);
        ReferenceEquals(first, second).Should().BeTrue("the second execution must hit the cached plan, so the changed value is observed on it");
        second.DbCommand.Parameters.Count.Should().Be(1);
        second.DbCommand.Parameters[0].Value.Should().Be(2, "the cached plan re-reads the context value and binds the second one");
    }

    // --- #123: the write target is isolated under an active global filter. SQL Server renders a general
    // --- MERGE, so the target predicate joins the ON search condition (and every WHEN NOT MATCHED BY
    // --- SOURCE arm), and the key upsert routes through the same ON renderer instead of a second
    // --- mechanism. No database connection is opened.

    private const string MergeTenantKey = "qf_merge_tenant_mssql";

    [SqlTable("qf_merge_target")]
    public sealed class QfMergeTargetEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [SqlTable("qf_merge_other")]
    public sealed class QfMergeOtherEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    private static void ConfigureMergeTarget(IDataContext ctx)
    {
        ctx.Properties[MergeTenantKey] = 1;
        ctx.From<QfMergeTargetEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[MergeTenantKey]));
    }

    private static string OnSegment(string sql)
    {
        var start = sql.IndexOf(" on ", StringComparison.Ordinal);
        var end = sql.IndexOf(" when ", StringComparison.Ordinal);
        return start >= 0 && end > start ? sql[start..end] : sql;
    }

    private static string BySourceArm(string sql)
    {
        var start = sql.IndexOf("when not matched by source", StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = sql.IndexOf(" then ", start, StringComparison.Ordinal);
        return end > start ? sql[start..end] : sql[start..];
    }

    // Returns the branch head/body from the first occurrence of <paramref name="head"/> up to the next
    // " when " arm or the statement terminator, so a filter injected into an unrelated arm is observed.
    private static string ArmBody(string sql, string head)
    {
        var start = sql.IndexOf(head, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = sql.IndexOf(" when ", start + head.Length, StringComparison.Ordinal);
        if (end < 0)
            end = sql.IndexOf(';', start + head.Length);

        return end > start ? sql[start..end] : sql[start..];
    }

    [Fact]
    public void FullMerge_Filtered_OnKeys_ShouldInjectTargetPredicateIntoOn()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var on = OnSegment(sql);
        on.Should().Contain("target.id = source.id");
        on.Should().Contain("target.tenant_id = @", "the active tenant filter is injected into the MERGE ON");
        sql.Should().NotContain("tenant_id = 1", "the context value is bound, not inlined");
    }

    [Fact]
    public void FullMerge_Filtered_ExplicitOn_ShouldPreserveUserGrouping()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id && (t.Name == s.Name || s.Name == null))
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        var on = OnSegment(sql);
        on.Should().Contain("(target.name = source.name or source.name is null)", "the user ON grouping is preserved");
        on.Should().Contain("target.tenant_id = @");
    }

    [Fact]
    public void FullMerge_Filtered_WhenNotMatchedBySource_ShouldAppendPredicateToTheArm()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .WhenNotMatchedBySource().ThenDelete()
            .ToSql();

        var arm = BySourceArm(sql);
        arm.Should().NotBeEmpty();
        arm.Should().Contain("target.tenant_id = @", "a bare delete arm would otherwise delete hidden rows");
    }

    [Fact]
    public void FullMerge_Filtered_ConditionalBySource_ShouldKeepUserConditionAndPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatchedBySource((t, s) => t.Name != null).ThenDelete()
            .ToSql();

        var arm = BySourceArm(sql);
        arm.Should().Contain("target.name", "the user branch condition is preserved");
        arm.Should().Contain("target.tenant_id = @");
    }

    [Fact]
    public void KeyUpsert_Filtered_ShouldRouteThroughTheMergeOnPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().StartWith("merge into qf_merge_target");
        OnSegment(sql).Should().Contain("target.id = source.id");
        OnSegment(sql).Should().Contain("target.tenant_id = @", "the key upsert is not a second unfiltered mechanism");
    }

    [Fact]
    public void FullMerge_IgnoreFilters_ShouldRenderNativeSqlWithoutPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .IgnoreFilters()
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        sql.Should().NotContain("target.tenant_id = @");
        sql.Should().Contain("on target.id = source.id");
    }

    [Fact]
    public void FullMerge_SelectiveIgnoreByType_ShouldKeepOrDropTheTargetPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var kept = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .IgnoreFilters(typeof(QfMergeOtherEntity))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();
        var dropped = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .IgnoreFilters(typeof(QfMergeTargetEntity))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        OnSegment(kept).Should().Contain("target.tenant_id = @", "ignoring an unrelated type leaves the filter active");
        OnSegment(dropped).Should().NotContain("target.tenant_id = @", "ignoring the target type disables its filter");
    }

    // --- #123 mutant kills: the ON connector between the user condition and the injected filter is a
    // --- literal AND (not OR), and the filter is confined to the ON and WHEN NOT MATCHED BY SOURCE arms.

    [Fact]
    public void FullMerge_Filtered_OnKeys_ShouldAndTargetPredicateToTheKeyCondition()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        OnSegment(sql).Should().Contain(
            "source.id and (target.tenant_id = @",
            "the key predicate and the filter must be joined by AND, not OR");
    }

    [Fact]
    public void FullMerge_Filtered_ExplicitOn_ShouldAndTargetPredicateToTheUserCondition()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .On((t, s) => t.Id == s.Id && (t.Name == s.Name || s.Name == null))
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        OnSegment(sql).Should().Contain(
            ") and (target.tenant_id = @",
            "the user condition group and the filter group must be joined by AND, not OR");
    }

    [Fact]
    public void FullMerge_Filtered_ShouldInjectPredicateOnlyIntoOnAndBySourceArms()
    {
        using var ctx = SqlServerTestContext.Create();
        ConfigureMergeTarget(ctx);

        var sql = ctx.CreateMergeBuilder<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .WhenNotMatchedBySource().ThenDelete()
            .ToSql();

        OnSegment(sql).Should().Contain("target.tenant_id = @", "the ON search condition must be filtered");
        BySourceArm(sql).Should().Contain("target.tenant_id = @", "the by-source delete arm must be filtered");
        ArmBody(sql, "when matched").Should().NotContain("target.tenant_id = @", "the update arm must not be filtered");
        ArmBody(sql, "when not matched ").Should().NotContain("target.tenant_id = @", "the insert arm must not be filtered");
    }
}
