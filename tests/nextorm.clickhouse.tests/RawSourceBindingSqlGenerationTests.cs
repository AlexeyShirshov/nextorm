using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// D5 task A: SQL generation for global query filters injected into a raw `FromSql` source that was
/// explicitly bound with `BindEntity`. Covers the compatible/incompatible decision, empty-column
/// constellations, mapped output names, nullable predicates, raw parameter preservation and a bound
/// joined source (outer join: predicates stay in ON). No connection is opened.
/// </summary>
public class RawSourceBindingSqlGenerationTests
{
    private const string TenantKey = "rsb_sql_tenant";

    [SqlTable("rsb_sql_entity")]
    public sealed class RawSqlEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("tenant_id")]
        public int TenantId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("nullable_flag")]
        public bool? NullableFlag { get; set; }
    }

    [SqlTable("rsb_sql_alias_entity")]
    public sealed class RawSqlAliasEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("alt_id")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("alt_tenant")]
        public int TenantId { get; set; }
    }

    [SqlTable("rsb_sql_constant_entity")]
    public sealed class RawSqlConstantEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_sql_column_dependent_entity")]
    public sealed class RawSqlColumnDependentEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_sql_join_main")]
    public sealed class RawSqlJoinMain
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    private static void ConfigureScope(IDataContext ctx)
        => ctx.From<RawSqlEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter(e => !e.IsDeleted)
            .HasQueryFilter(e => e.NullableFlag != true)
            .HasQueryFilter(e => e.Id > 0));

    private static void ConfigureAlias(IDataContext ctx)
        => ctx.From<RawSqlAliasEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

    private static void ConfigureConstant(IDataContext ctx)
        => ctx.From<RawSqlConstantEntity>(b => b.HasQueryFilter(e => true));

    private static void ConfigureColumnDependent(IDataContext ctx)
        => ctx.From<RawSqlColumnDependentEntity>(b => b.HasQueryFilter(e => e.Id > 0));

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

    [Fact]
    public void BoundCompatible_ShouldApplyEveryDeclaredFilter()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id, tenant_id, is_deleted, nullable_flag from rsb_sql_table")
            .BindEntity<RawSqlEntity>(["id", "tenant_id", "is_deleted", "nullable_flag"])
            .Select(x => x.Id));

        sql.Should().Contain("from (select id, tenant_id, is_deleted, nullable_flag from rsb_sql_table)");
        sql.Should().Contain("tenant_id = @");
        sql.Should().Contain("not (");
        sql.Should().Contain("is_deleted");
        sql.Should().Contain("nullable_flag !=");
        sql.Should().Contain("id > 0");
    }

    [Fact]
    public void BoundIncompatible_ShouldSkipMissingFilterAndApplyCompatible()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id, tenant_id, is_deleted from rsb_sql_table")
            .BindEntity<RawSqlEntity>(["id", "tenant_id", "is_deleted"])
            .Select(x => x.Id));

        sql.Should().Contain("tenant_id = @");
        sql.Should().Contain("id > 0");
        sql.Should().NotContain("nullable_flag", "the filter whose column is not declared is skipped");
    }

    [Fact]
    public void EmptyColumns_ConstantPredicate_ShouldApply()
    {
        using var ctx = ClickHouseTestContext.Create();
        ConfigureConstant(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id from rsb_sql_table")
            .BindEntity<RawSqlConstantEntity>(Array.Empty<string>())
            .Select(x => x.Id));

        sql.Should().Contain("from (select id from rsb_sql_table)");
        sql.Should().Contain("where", "a proven zero-column filter is still rendered as a predicate");
    }

    [Fact]
    public void EmptyColumns_ColumnDependentPredicate_ShouldSkip()
    {
        using var ctx = ClickHouseTestContext.Create();
        ConfigureColumnDependent(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id from rsb_sql_table")
            .BindEntity<RawSqlColumnDependentEntity>(Array.Empty<string>())
            .Select(x => x.Id));

        sql.Should().Contain("from (select id from rsb_sql_table)");
        sql.Should().NotContain("where", "the column-dependent filter is skipped against an empty declaration");
    }

    [Fact]
    public void MappedOutputNames_ShouldMatchTheDeclaredAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureAlias(ctx);

        var applied = SqlOf(ctx, ctx.FromSql("select alt_id, alt_tenant from rsb_sql_table")
            .BindEntity<RawSqlAliasEntity>(["alt_id", "alt_tenant"])
            .Select(x => x.Id));
        applied.Should().Contain("alt_tenant =");
        applied.Should().NotContain("tenant_id", "the filter dependency is the mapped column name");

        var skipped = SqlOf(ctx, ctx.FromSql("select alt_id from rsb_sql_table")
            .BindEntity<RawSqlAliasEntity>(["alt_id"])
            .Select(x => x.Id));
        skipped.Should().NotContain("alt_tenant");
    }

    [Fact]
    public void NullablePredicate_ShouldRenderWhenDeclared()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id, nullable_flag from rsb_sql_table")
            .BindEntity<RawSqlEntity>(["id", "nullable_flag"])
            .Select(x => x.Id));

        sql.Should().Contain("nullable_flag");
        sql.Should().Contain("id > 0");
    }

    [Fact]
    public void RawParameterAndFilterParameter_ShouldBothBePreserved()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            ctx.FromSql("select id, tenant_id from rsb_sql_table where id > @min", new { min = 3 })
                .BindEntity<RawSqlEntity>(["id", "tenant_id"])
                .Select(x => x.Id),
            false,
            false,
            CancellationToken.None);

        var sql = Normalize(prepared.DbCommand.CommandText);
        sql.Should().Contain("id > @min", "the raw SQL parameter placeholder is preserved verbatim");
        sql.Should().Contain("tenant_id = @");
        var values = prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();
        values.Should().Contain(3, "the raw parameter value is bound");
        values.Should().Contain(7, "the filter parameter value is bound");
    }

    [Fact]
    public void JoinedBoundSource_MixedFilters_ShouldApplyCompatibleInOnAndKeepOuterJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var bound = ctx.FromSql("select id, tenant_id, is_deleted from rsb_sql_table")
            .BindEntity<RawSqlEntity>(["id", "tenant_id", "is_deleted"]);

        var sql = SqlOf(ctx, ctx.From<RawSqlJoinMain>()
            .LeftJoin(bound, (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        sql.Should().Contain("left join (select id, tenant_id, is_deleted from rsb_sql_table)");
        sql.Should().Contain("t1.id = t2.id");
        sql.Should().Contain("tenant_id =", "the compatible filter is applied to the join alias");
        sql.Should().Contain("is_deleted");
        sql.Should().Contain("not (");
        sql.Should().Contain("id > 0");
        sql.Should().NotContain("nullable_flag", "the incompatible filter is skipped, the compatible ones stay");
        sql.Should().NotContain("where", "an outer-join filter stays in the ON clause");
    }

    [Fact]
    public void MainBoundSource_Joined_ShouldApplyOwnFiltersInWhere()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var main = ctx.FromSql("select id, tenant_id, is_deleted from rsb_sql_table")
            .BindEntity<RawSqlEntity>(["id", "tenant_id", "is_deleted"]);

        var sql = SqlOf(ctx, main
            .Join(ctx.From<RawSqlJoinMain>(), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        sql.Should().Contain("from (select id, tenant_id, is_deleted from rsb_sql_table)");
        sql.Should().Contain("tenant_id =", "the compatible main filter is applied against the main binding");
        sql.Should().Contain("is_deleted");
        sql.Should().Contain("id > 0");
        sql.Should().NotContain("nullable_flag", "an undeclared main dependency is skipped");
        sql.Should().Contain("where", "main-source filters land in WHERE, never in a join ON clause");
    }

    [Fact]
    public void UnboundSource_ShouldRenderWithoutFilters()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.Properties[TenantKey] = 7;
        ConfigureScope(ctx);

        var sql = SqlOf(ctx, ctx.FromSql("select id, tenant_id from rsb_sql_table")
            .Select(x => x["id"].AsInt));

        sql.Should().Contain("from (select id, tenant_id from rsb_sql_table)");
        sql.Should().NotContain("where", "an unbound raw source is never filtered");
        sql.Should().NotContain("tenant_id =");
    }
}

/// <summary>
/// #185 D185: binding a raw source is self-sufficient — it registers the entity mapping through the
/// same path as <c>From&lt;T&gt;()</c>, so a typed member projection works on a genuinely cold
/// <see cref="DataContextCache.Metadata"/> (no prior <c>From&lt;T&gt;</c>). The negative half pins the
/// configured-mapping precedence and that a repeated bind is a registration no-op.
/// <para>
/// Joins the serialized "DataContextCache clear" collection because it clears the process-wide cache.
/// </para>
/// </summary>
[Collection("DataContextCache clear")]
public class RawSourceBindingColdRegistrationTests
{
    [SqlTable("rsb_cold_configured")]
    public sealed class ColdConfiguredEntity
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("attr_name")]
        public string? Name { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    // R01/R02: cold interface + typed projection via the raw-SQL entry point.
    [Fact]
    public void ColdInterfaceTypedProjection_FromSql_ShouldRegisterAndRender()
    {
        DataContextCache.Clear();
        using var ctx = ClickHouseTestContext.Create();

        DataContextCache.Metadata.ContainsKey(typeof(IComplexEntity)).Should().BeFalse("genuinely cold metadata");

        var sql = SqlOf(ctx, ctx.FromSql("select id, somestring from complex_entity")
            .BindEntity<IComplexEntity>(["id", "somestring"])
            .Select(t => t.Id));

        sql.Should().Contain("select id from (select id, somestring from complex_entity)");
        DataContextCache.Metadata.ContainsKey(typeof(IComplexEntity)).Should().BeTrue(
            "binding registers the mapping without a prior From<T>()");
    }

    // R01/R02: the same cold binding through the named-source entry point.
    [Fact]
    public void ColdInterfaceTypedProjection_FromNamedSource_ShouldRegisterAndRender()
    {
        DataContextCache.Clear();
        using var ctx = ClickHouseTestContext.Create();

        DataContextCache.Metadata.ContainsKey(typeof(IComplexEntity)).Should().BeFalse("genuinely cold metadata");

        var sql = SqlOf(ctx, ctx.From("complex_entity")
            .BindEntity<IComplexEntity>(["id", "somestring"])
            .Select(t => t.Id));

        sql.Should().Contain("from complex_entity");
        sql.Should().Contain("id");
        DataContextCache.Metadata.ContainsKey(typeof(IComplexEntity)).Should().BeTrue();
    }

    // R04: a configured From<T>(cfg) registered before binding keeps its configured column name.
    [Fact]
    public void ColdConfiguredMapping_ShouldWinOverAttributeMapping()
    {
        DataContextCache.Clear();
        using var ctx = ClickHouseTestContext.Create();
        ctx.From<ColdConfiguredEntity>(cfg => cfg.Property(x => x.Name!).HasColumnName("configured_name"));

        DataContextCache.Metadata[typeof(ColdConfiguredEntity)].Properties
            .Should().ContainSingle(p => p.ColumnName == "configured_name");

        var sql = SqlOf(ctx, ctx.FromSql("select configured_name from rsb_cold_configured")
            .BindEntity<ColdConfiguredEntity>(["configured_name"])
            .Select(x => x.Name));

        sql.Should().Contain("configured_name");
        sql.Should().NotContain("attr_name", "the configured mapping wins over the attribute mapping");
    }

    // R02: a repeated bind does not change the mapping or the generated SQL.
    [Fact]
    public void RepeatedBind_ShouldNotChangeSqlOrMapping()
    {
        DataContextCache.Clear();
        using var ctx = ClickHouseTestContext.Create();
        var source = ctx.FromSql("select id, somestring from complex_entity");

        var first = source.BindEntity<IComplexEntity>(["id", "somestring"]).Select(t => t.Id);
        var firstMetadata = DataContextCache.Metadata[typeof(IComplexEntity)];
        var second = source.BindEntity<IComplexEntity>(["id", "somestring"]).Select(t => t.Id);

        SqlOf(ctx, first).Should().Be(SqlOf(ctx, second));
        DataContextCache.Metadata[typeof(IComplexEntity)].Should().BeSameAs(firstMetadata,
            "the second bind is a registration no-op");
    }

    // R07: the exact TableAlias guard is unchanged and registers no mapping.
    [Fact]
    public void TableAlias_BindEntity_ShouldStillThrowNotSupported()
    {
        DataContextCache.Clear();
        using var ctx = ClickHouseTestContext.Create();
        var source = ctx.FromSql("select 1 as Id");

        var act = () => source.BindEntity<TableAlias>(["Id"]);

        act.Should().Throw<NotSupportedException>();
        DataContextCache.Metadata.ContainsKey(typeof(TableAlias)).Should().BeFalse(
            "TableAlias is rejected and has no mapping to register");
    }
}

/// <summary>
/// Serializes the tests that clear the process-wide <see cref="DataContextCache"/> with the rest of
/// the suite.
/// </summary>
[CollectionDefinition("DataContextCache clear", DisableParallelization = true)]
public sealed class RawSourceBindingCacheClearCollection;
