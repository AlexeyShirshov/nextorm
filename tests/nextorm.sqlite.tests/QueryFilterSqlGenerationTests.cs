using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation and plan-cache behaviour for global query filters (#67) on SQLite: the filter
/// predicate reaches the <c>WHERE</c>, the per-context tenant value is bound as a parameter (never
/// inlined) and two contexts with different values share one cached plan. The SQL-only test never
/// opens a database; the two-context test uses a temp file database.
/// </summary>
public class QueryFilterSqlGenerationTests
{
    private const string TenantKey = "tenant";

    [SqlTable("filter_tenant_entity")]
    public sealed class FilterTenantEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_tenant_sql_entity")]
    public sealed class FilterTenantSqlEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_join_left_entity")]
    public sealed class FilterJoinLeftEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("filter_join_right_entity")]
    public sealed class FilterJoinRightEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_selective_entity")]
    public sealed class FilterSelectiveEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    [SqlTable("filter_selective_join_left")]
    public sealed class FilterSelectiveJoinLeftEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("filter_selective_join_right")]
    public sealed class FilterSelectiveJoinRightEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    private const string SelectiveTenantKey = "tenant_selective";
    private const string SelectiveJoinTenantKey = "tenant_join_selective";

    private const string JoinTenantKey = "tenant_join";

    private static Expression<Func<FilterTenantEntity, IDataContext, bool>> TenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[TenantKey];

    private static Expression<Func<FilterJoinRightEntity, IDataContext, bool>> JoinTenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[JoinTenantKey];

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

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
    public void FilteredEntity_Sql_ShouldContainPredicateAndBoundParameter()
    {
        // A dedicated entity type and property key keep this SQL-shape test independent of the
        // two-context execution test: the context read folds through the process-wide compiled-value
        // cache, whose key does not include the context instance (see the execution test below).
        using var ctx = SqliteTestContext.Create();
        ctx.Properties["tenant_sqlgen"] = 7;
        ctx.From<FilterTenantSqlEntity>(b => b.HasQueryFilter((e, c) => e.TenantId == (int)c.Properties["tenant_sqlgen"]));

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<FilterTenantSqlEntity>().Select(x => x.Id), false, false, CancellationToken.None);

        var sql = Normalize(prepared.DbCommand.CommandText);
        sql.Should().Contain("tenant_id = $");
        sql.Should().NotContain("= 7");

        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].Value.Should().Be(7);
    }

    [Fact]
    public void TwoContexts_DifferentTenant_ShouldSharePlanKey_AndScopeDistinguishes()
    {
        using var ctx1 = SqliteTestContext.Create();
        using var ctx2 = SqliteTestContext.Create();
        ctx1.Properties[TenantKey] = 1;
        ctx2.Properties[TenantKey] = 2;
        ctx1.From<FilterTenantEntity>(b => b.HasQueryFilter(TenantFilter()));

        var shared1 = ctx1.From<FilterTenantEntity>().Select(x => x.Id);
        var shared2 = ctx2.From<FilterTenantEntity>().Select(x => x.Id);
        shared1.PrepareCommand(false, TestContext.Current.CancellationToken);
        shared2.PrepareCommand(false, TestContext.Current.CancellationToken);

        shared1.GetOrCreatePlanKey(null).Equals(shared2.GetOrCreatePlanKey(null))
            .Should().BeTrue("two contexts with the same shape share the cached plan regardless of the tenant value");

        var unfiltered = ctx1.From<FilterTenantEntity>().IgnoreFilters().Select(x => x.Id);
        var selective = ctx1.From<FilterTenantEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id);
        unfiltered.PrepareCommand(false, TestContext.Current.CancellationToken);
        selective.PrepareCommand(false, TestContext.Current.CancellationToken);

        shared1.GetOrCreatePlanKey(null).Equals(unfiltered.GetOrCreatePlanKey(null))
            .Should().BeFalse("the all-filters-ignored form injects a different condition");
        shared1.GetOrCreatePlanKey(null).Equals(selective.GetOrCreatePlanKey(null))
            .Should().BeFalse("a selective ignore injects a different condition");
    }

    [Fact]
    public void RePreparedJoin_ShouldNotDoubleInjectFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        var first = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(first.DbCommand.CommandText), "tenant_id = ").Should().Be(1);

        cmd.ResetPreparation();

        var second = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(second.DbCommand.CommandText), "tenant_id = ").Should().Be(1);
    }

    [Fact]
    public void DerivedJoin_ShouldApplyFilterOnceInSubquery_NotInOnCondition()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var right = ctx.From<FilterJoinRightEntity>().ToCommand();
        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(right, (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        CountOccurrences(Normalize(prepared.DbCommand.CommandText), "tenant_id = ").Should().Be(1);
    }

    [Fact]
    public void CachedPlanClone_ShouldNotShareJoinInstance()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var cmd = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        var clone = cmd.CloneForCache();

        clone.Joins.Should().NotBeSameAs(cmd.Joins);
        clone.Joins![0].Should().NotBeSameAs(cmd.Joins![0]);
    }

    private static void ConfigureSelective(IDataContext ctx)
    {
        ctx.Properties[SelectiveTenantKey] = 1;
        ctx.From<FilterSelectiveEntity>(b => b
            .HasQueryFilter((e, c) => e.Id > 5)
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[SelectiveTenantKey]));
    }

    private static string PrepareSelectiveSql(IDataContext ctx, EntityBuilder<FilterSelectiveEntity> builder)
    {
        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(x => x.Id), false, false, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

    [Fact]
    public void SelectiveIgnore_ByKey_Sql_ShouldDropOnlyNamedPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = PrepareSelectiveSql(ctx, ctx.From<FilterSelectiveEntity>().IgnoreFilters(["tenant"]));

        sql.Should().Contain("id > 5");
        sql.Should().NotContain("tenant_id = ");
    }

    [Fact]
    public void SelectiveIgnore_ByAnonymousKey_Sql_ShouldDropOnlyAnonymousPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = PrepareSelectiveSql(ctx, ctx.From<FilterSelectiveEntity>().IgnoreFilters([QueryFilters.AnonymousKey]));

        sql.Should().Contain("tenant_id = $");
        sql.Should().NotContain("id > 5");
    }

    [Fact]
    public void SelectiveIgnore_EmptyKeys_Sql_ShouldKeepEveryPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = PrepareSelectiveSql(ctx, ctx.From<FilterSelectiveEntity>().IgnoreFilters(Array.Empty<string>()));

        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = $");
    }

    [Fact]
    public void SelectiveIgnore_ByType_Sql_ShouldDropJoinedTypePredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[SelectiveJoinTenantKey] = 1;
        ctx.From<FilterSelectiveJoinRightEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[SelectiveJoinTenantKey]));

        var filtered = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<FilterSelectiveJoinLeftEntity>()
                .Join(ctx.From<FilterSelectiveJoinRightEntity>(), (l, r) => l.Id == r.Id)
                .Select(p => p.Item2.TenantId),
            false, false, CancellationToken.None);
        Normalize(filtered.DbCommand.CommandText).Should().Contain("tenant_id = ");

        var unfiltered = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<FilterSelectiveJoinLeftEntity>()
                .IgnoreFilters(typeof(FilterSelectiveJoinRightEntity))
                .Join(ctx.From<FilterSelectiveJoinRightEntity>(), (l, r) => l.Id == r.Id)
                .Select(p => p.Item2.TenantId),
            false, false, CancellationToken.None);
        Normalize(unfiltered.DbCommand.CommandText).Should().NotContain("tenant_id = ");
    }

    [Fact]
    public void SharedJoinBuilder_DifferentScopes_ShouldNotLeakFiltersAcrossRenders()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        // One builder lineage: both commands' join arrays hold the same JoinExpression instance, so
        // preparing one must not inject its filter into the other's ON condition.
        var joined = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id);
        var joinedUnfiltered = joined.IgnoreFilters();

        string Render(QueryCommand<int> command, bool cache)
        {
            var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(command, false, cache, CancellationToken.None);
            return Normalize(prepared.DbCommand.CommandText);
        }

        // Without the plan cache: render the filtered command first, then its ignore-all sibling.
        var filtered1 = Render(joined.Select(p => p.Item2.TenantId), cache: false);
        var unfiltered1 = Render(joinedUnfiltered.Select(p => p.Item2.TenantId), cache: false);

        filtered1.Should().Contain("tenant_id = ");
        unfiltered1.Should().NotContain("tenant_id = ", "the sibling's injected filter must not leak into the unfiltered ON condition");

        // Re-render the filtered shape after the sibling: identical SQL, not the sibling's mutation.
        Render(joined.Select(p => p.Item2.TenantId), cache: false).Should().Be(filtered1);

        // The same alternation with the plan cache enabled is stable and plans stay distinct.
        var filteredCached = Render(joined.Select(p => p.Item2.TenantId), cache: true);
        var unfilteredCached = Render(joinedUnfiltered.Select(p => p.Item2.TenantId), cache: true);

        filteredCached.Should().Contain("tenant_id = ");
        unfilteredCached.Should().NotContain("tenant_id = ");
        Render(joined.Select(p => p.Item2.TenantId), cache: true).Should().Be(filteredCached);
    }

    [Fact]
    public void ClonedCommand_DifferentScopes_AlternatingPreparation_ShouldNotLeak()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[JoinTenantKey] = 1;
        ctx.From<FilterJoinRightEntity>(b => b.HasQueryFilter(JoinTenantFilter()));

        var source = ctx.From<FilterJoinLeftEntity>()
            .Join(ctx.From<FilterJoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.TenantId);

        // A Definition-derived clone (the paging clone) used to share the source's `_joins` array, so
        // the source's prepared join was rewritten when the sibling prepared.
        var unfiltered = source.Limit(0);
        unfiltered.IgnoreFilters = true;

        string Render(QueryCommand<int> command, bool cache)
        {
            var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(command, false, cache, CancellationToken.None);
            return Normalize(prepared.DbCommand.CommandText);
        }

        // storeInCache:false re-renders an already-prepared command from its current state, so a shared
        // array is observable as the source's own SQL changing after the sibling prepares.
        var filtered = Render(source, cache: false);
        filtered.Should().Contain("tenant_id = ");
        Render(unfiltered, cache: false).Should().NotContain("tenant_id = ");
        Render(source, cache: false).Should().Be(filtered, "the sibling's preparation must not rewrite the source's join");
        Render(unfiltered, cache: false).Should().NotContain("tenant_id = ");

        // The same alternation with the plan cache enabled stays stable and the plans stay distinct.
        var filteredCached = Render(source, cache: true);
        filteredCached.Should().Contain("tenant_id = ");
        Render(unfiltered, cache: true).Should().NotContain("tenant_id = ");
        Render(source, cache: true).Should().Be(filteredCached);
        Render(source.Limit(0), cache: true).Should().Contain("tenant_id = ", "a fresh sibling re-derived from the source stays filtered");
    }

    [SqlTable("filter_dml_target")]
    public sealed class FilterDmlTargetEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_dml_right")]
    public sealed class FilterDmlRightEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("filter_insert_source")]
    public sealed class FilterInsertSourceEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("filter_insert_all_source")]
    public sealed class FilterInsertAllSourceEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    private const string DmlTenantKey = "tenant_dml";

    private static void ConfigureDmlTarget(IDataContext ctx)
    {
        ctx.Properties[DmlTenantKey] = 1;
        ctx.From<FilterDmlTargetEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[DmlTenantKey]));
    }

    // The internal render seam is the only way to reach the generated SQL of the key form (the public
    // ToSql only exists on the predicate/all builder); the SQL text is what the row asserts.
    private static string RenderMutation(IDataContext ctx, MutationCommand command)
        => ((IMutationExecutor)ctx).Render(command);

    [Fact]
    public void Update_Predicate_FilterIsAppendedToWhere()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(ctx.Update<FilterSelectiveEntity>()
            .Set(x => x.IsDeleted, true)
            .Where(x => x.Id > 10)
            .ToSql());

        sql.Should().Contain("id > 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = ");
    }

    [Fact]
    public void Update_Predicate_SelectiveIgnore_ShouldDropOnlyNamedFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(ctx.Update<FilterSelectiveEntity>()
            .Set(x => x.IsDeleted, true)
            .Where(x => x.Id > 10)
            .IgnoreFilters(["tenant"])
            .ToSql());

        sql.Should().Contain("id > 10");
        sql.Should().Contain("id > 5");
        sql.Should().NotContain("tenant_id = $");
        // The SET list still writes is_deleted; tenant_id is not among the written columns here.
        sql.Should().NotContain("tenant_id");
    }

    [Fact]
    public void Update_KeyForm_FilterIsAndedToKeyPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(RenderMutation(ctx,
            ctx.Update<FilterSelectiveEntity>().BuildEntityCommand(new FilterSelectiveEntity { Id = 3 })));

        sql.Should().Contain("where id = $");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = $");
    }

    [Fact]
    public void Update_KeyForm_WithUserWhere_ShouldAndKeyWhereAndFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(RenderMutation(ctx,
            ctx.Update<FilterSelectiveEntity>()
                .Where(x => x.Id > 10)
                .BuildEntityCommand(new FilterSelectiveEntity { Id = 3 })));

        // The key form always ANDs the declared key and every source predicate: the key equality, the
        // user Where and the two injected global filters coexist in one WHERE.
        sql.Should().Contain("where id = $", "the key equality is rendered");
        sql.Should().Contain("id > 10", "the user Where predicate is ANDed");
        sql.Should().Contain("id > 5", "the target's anonymous filter is ANDed");
        sql.Should().Contain("tenant_id = $", "the target's named filter is ANDed");
    }

    [Fact]
    public void Update_KeyForm_IgnoreAll_ShouldDropTargetFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(RenderMutation(ctx,
            ctx.Update<FilterSelectiveEntity>().IgnoreFilters().BuildEntityCommand(new FilterSelectiveEntity { Id = 3 })));

        sql.Should().Contain("where id = $");
        sql.Should().NotContain("id > 5");
        CountOccurrences(sql, "tenant_id = ").Should().Be(1, "tenant_id appears only in the SET list, not the filter");
    }

    [Fact]
    public void Delete_Predicate_FilterIsAppendedToWhere()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(ctx.DeleteFrom<FilterSelectiveEntity>().Where(x => x.Id > 10).ToSql());

        sql.Should().Contain("id > 10");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = ");
    }

    [Fact]
    public void Delete_KeyForm_FilterIsAndedToKeyPredicate()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(RenderMutation(ctx,
            ctx.DeleteFrom<FilterSelectiveEntity>().BuildKeyCommand(new FilterSelectiveEntity { Id = 3 })));

        sql.Should().Contain("where id = $");
        sql.Should().Contain("id > 5");
        sql.Should().Contain("tenant_id = $");
    }

    [Fact]
    public void Delete_KeyForm_SelectiveIgnore_ShouldDropOnlyNamedFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var named = Normalize(RenderMutation(ctx,
            ctx.DeleteFrom<FilterSelectiveEntity>().IgnoreFilters(["tenant"]).BuildKeyCommand(new FilterSelectiveEntity { Id = 3 })));
        var anonymous = Normalize(RenderMutation(ctx,
            ctx.DeleteFrom<FilterSelectiveEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).BuildKeyCommand(new FilterSelectiveEntity { Id = 3 })));

        named.Should().Contain("id > 5");
        named.Should().NotContain("tenant_id");
        anonymous.Should().Contain("tenant_id = $");
        anonymous.Should().NotContain("id > 5");
    }

    [Fact]
    public void Delete_All_IsExplicitFullTableDelete_ShouldNotApplyFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureSelective(ctx);

        var sql = Normalize(ctx.DeleteFrom<FilterSelectiveEntity>().All().ToSql());

        sql.Should().Be("delete from filter_selective_entity");
    }

    [Fact]
    public void Select_JoinMainSource_FilterShouldApplyToTheTargetTable()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.From<FilterDmlTargetEntity>()
                .Join(ctx.From<FilterDmlRightEntity>(), (l, r) => l.Id == r.Id)
                .Select(p => p.Item1.Id),
            false, false, TestContext.Current.CancellationToken);

        Normalize(prepared.DbCommand.CommandText).Should().Contain("t1.tenant_id = $");
    }

    [Fact]
    public void UpdateJoin_ShouldFilterTargetTable()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);

        var sql = Normalize(ctx.From<FilterDmlTargetEntity>()
            .Join(ctx.From<FilterDmlRightEntity>(), (l, r) => l.Id == r.Id)
            .UpdateJoin()
            .Set(p => p.Item1.TenantId, 9)
            .ToSql());

        CountOccurrences(sql, "tenant_id = ").Should().Be(2, "the SET value and the injected target filter");
        sql.Should().Contain("t1.tenant_id = $");
    }

    [Fact]
    public void UpdateJoin_SelectiveIgnoreByType_ShouldDropTargetFilter()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);

        var sql = Normalize(ctx.From<FilterDmlTargetEntity>()
            .Join(ctx.From<FilterDmlRightEntity>(), (l, r) => l.Id == r.Id)
            .UpdateJoin()
            .IgnoreFilters(typeof(FilterDmlTargetEntity))
            .Set(p => p.Item1.TenantId, 9)
            .ToSql());

        CountOccurrences(sql, "tenant_id = ").Should().Be(1, "only the SET value remains");
    }

    // --- PR3: the write target is never filtered; only the INSERT ... SELECT source is, and the
    // --- inserted rows are validated (pre-checked) separately.

    [Fact]
    public void Insert_Target_IsNotFiltered()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);

        var sql = Normalize(ctx.InsertInto<FilterDmlTargetEntity>()
            .Values(new FilterDmlTargetEntity { Id = 1, TenantId = 1 })
            .ToSql());

        sql.Should().StartWith("insert into filter_dml_target");
        sql.Should().NotContain("where", "the target entity's global filter is never injected into an INSERT");
    }

    [Fact]
    public void InsertSelect_Source_IsFiltered_Target_IsNot()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);
        ctx.From<FilterInsertSourceEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[DmlTenantKey]));

        var sql = Normalize(ctx.InsertInto<FilterDmlTargetEntity>()
            .Values(
                ctx.From<FilterInsertSourceEntity>().Where(x => x.Id > 0),
                s => new FilterDmlTargetEntity { Id = s.Id, TenantId = s.TenantId })
            .ToSql());

        sql.Should().Contain("from filter_insert_source");
        CountOccurrences(sql, "tenant_id = ").Should().Be(1, "only the source filter renders");
    }

    [Fact]
    public void Merge_Target_IsNotFiltered()
    {
        using var ctx = SqliteTestContext.Create();
        ConfigureDmlTarget(ctx);

        var sql = Normalize(ctx.MergeInto<FilterDmlTargetEntity>()
            .Using(new FilterDmlTargetEntity { Id = 1, TenantId = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql());

        CountOccurrences(sql, "tenant_id = ").Should().Be(1, "only the SET value remains; the target filter is not injected");
    }

    [Fact]
    public void InsertSelect_SourceViolatesTargetFilter_ShouldThrowBeforeInsert()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-queryfilter-insertselect-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table filter_insert_all_source (id integer primary key, tenant_id integer not null);" +
                "create table filter_dml_target (id integer primary key, tenant_id integer not null);" +
                "insert into filter_insert_all_source (id, tenant_id) values (1, 1), (2, 2);";
            cmd.ExecuteNonQuery();
        }

        try
        {
            using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
            ctx.EnsureConnectionOpen();
            ConfigureDmlTarget(ctx);

            var act = () => ctx.InsertInto<FilterDmlTargetEntity>()
                .Values(ctx.From<FilterInsertAllSourceEntity>(), s => new FilterDmlTargetEntity { Id = s.Id, TenantId = s.TenantId })
                .Insert();

            act.Should().Throw<QueryFilterException>("the source contains a tenant-2 row the target filter rejects");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
