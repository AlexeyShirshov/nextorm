using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL generation of global query filters on ClickHouse (#108): the filter reaches the SELECT
/// <c>WHERE</c>, a selective ignore drops only the named predicate, the target filter is ANDed to the
/// <c>ALTER TABLE ... UPDATE/DELETE</c> mutation filter, an INSERT target is never filtered and an
/// INSERT ... SELECT filters only its source. No database connection is opened.
/// </summary>
public class QueryFilterSqlGenerationTests
{
    private const string TenantKey = "qf_tenant_ch_sqlgen";

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
        using var ctx = ClickHouseTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>());

        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = @");
    }

    [Fact]
    public void Select_SelectiveIgnoreByKey_ShouldDropOnlyNamedFilter()
    {
        using var ctx = ClickHouseTestContext.Create();
        ConfigureSelect(ctx);

        var sql = SelectSql(ctx, ctx.From<QfSelectEntity>().IgnoreFilters(["tenant"]));

        sql.Should().Contain("id > 5");
        sql.Should().NotContain("tenant_id = @");
    }

    [Fact]
    public void Update_Where_ShouldAndFilter()
    {
        using var ctx = ClickHouseTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.Update<QfSelectEntity>()
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
        using var ctx = ClickHouseTestContext.Create();
        ConfigureSelect(ctx);

        var sql = ctx.DeleteFrom<QfSelectEntity>()
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
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 1;

        var sql = ctx.InsertInto<QfInsertTargetEntity>(b => b
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
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 1;
        ctx.From<QfInsertSourceEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        var sql = ctx.InsertInto<QfInsertTargetEntity>(b => b
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
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[FuncTenantKey] = 1;
        ctx.From<QfFuncEntity>(b => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[FuncTenantKey])));

        var sql = FuncSelectSql(ctx, ctx.From<QfFuncEntity>());

        sql.Should().Contain("tenant_id = @");
        sql.Should().NotContain("= 1", "the context value is bound, not inlined into the SQL");
    }

    [Fact]
    public void FuncFilter_JoinOn_ShouldRenderBoundParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
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
        using var ctx = ClickHouseTestContext.Create();
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
        using var ctx = ClickHouseTestContext.Create();
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

    // --- #123: ClickHouse has no engine-level DML upsert at all, so a filtered key upsert is refused
    // --- (the same NotSupportedException the unfiltered form already raises). No connection is opened.

    private const string MergeTenantKey = "qf_merge_tenant_ch";

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

    private static void ConfigureMergeTarget(IDataContext ctx)
    {
        ctx.Properties[MergeTenantKey] = 1;
        ctx.From<QfMergeTargetEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[MergeTenantKey]));
    }

    [Fact]
    public void KeyUpsert_Filtered_ShouldThrowNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        ConfigureMergeTarget(ctx);

        var act = () => ctx.MergeInto<QfMergeTargetEntity>()
            .Using(new QfMergeTargetEntity { Id = 1, TenantId = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>("ClickHouse has no DML upsert form, filtered or not");
    }
}
