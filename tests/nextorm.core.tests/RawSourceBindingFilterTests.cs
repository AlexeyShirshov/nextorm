using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D5 task A: the decision matrix and diagnostics data of <c>BindEntity</c> global-filter injection
/// on a raw <c>FromSql</c>/<c>From(string)</c> source. These are provider-free: they run the
/// preparation pipeline over <see cref="InMemoryDataContext"/> and inspect the resolved condition and
/// the collected skip records, never SQL text (that is pinned in the per-provider SQL-generation
/// tests) and never log emission (that is pinned in <c>RawSourceBindingDiagnosticsTests</c> on SQLite,
/// because the in-memory context has no logger factory and the warning is emitted by the SQL planner).
/// </summary>
public class RawSourceBindingFilterTests
{
    private const string TenantKey = "rsb_tenant";

    [SqlTable("rsb_tenant_entity")]
    public sealed class TenantBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("rsb_constant_entity")]
    public sealed class ConstantBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_constant_false_entity")]
    public sealed class ConstantFalseBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_column_dependent_entity")]
    public sealed class ColumnDependentBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_mixed_dependency_entity")]
    public sealed class MixedDependencyBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_opaque_entity")]
    public sealed class OpaqueBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_field_entity")]
    public sealed class FieldBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        public bool Flag;
    }

    [SqlTable("rsb_func_entity")]
    public sealed class FuncBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("rsb_func_violating_entity")]
    public sealed class FuncViolatingBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    // A dedicated entity for the skipped-func test: process-wide metadata is keyed by entity type, so
    // sharing one type across two tests that register different builders would let the first registration
    // win and the second test's delegate would never run.
    [SqlTable("rsb_func_skipped_entity")]
    public sealed class FuncSkippedBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    // A plain mapped left-hand entity for the joined-source matrix.
    [SqlTable("rsb_join_main")]
    public sealed class JoinMainEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    // A filter over a nested member chain: NestedRootEntity maps the nested type as a column
    // (rsb_nested_root), while the value the predicate reads is a column of the nested mapped type
    // (leaf_tenant). Only the leaf is the physical SQL-column dependency.
    [SqlTable("rsb_nested_leaf_entity")]
    public sealed class NestedLeafEntity
    {
        [Column("leaf_tenant")]
        public int TenantId { get; set; }
    }

    [SqlTable("rsb_nested_root_entity")]
    public sealed class NestedRootEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nested_root")]
        public NestedLeafEntity Nested { get; set; } = new();
    }

    // A filter whose predicate reads a *nested lambda's* parameter (a foreign ParameterExpression),
    // i.e. the entity root is not resolvable from that chain.
    [SqlTable("rsb_foreign_param_entity")]
    public sealed class ForeignParamEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("children")]
        public List<ForeignParamChild> Children { get; set; } = [];
    }

    public sealed class ForeignParamChild
    {
        [Column("child_tenant")]
        public int TenantId { get; set; }
    }

    // A transparent Convert around the entity parameter (an upcast to a base type).
    public class BaseNestedEntity
    {
        [Column("base_id")]
        public int BaseId { get; set; }
    }

    [SqlTable("rsb_derived_root_entity")]
    public sealed class DerivedRootEntity : BaseNestedEntity
    {
    }

    private static void ConfigureTenant(InMemoryDataContext ctx)
        => ctx.From<TenantBoundEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter(e => e.Id > 5));

    private static QueryCommand<T> Prepared<T>(IDataContext ctx, EntityBuilder<T> builder)
    {
        var cmd = builder.Select(x => x);
        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        return cmd;
    }

    private static QueryCommand<int> PreparedIds<T>(IDataContext ctx, EntityBuilder<T> builder)
    {
        var cmd = builder.Select(_ => 1);
        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        return cmd;
    }

    private static QueryCommand<int> PreparedJoin<TJoin>(EntityBuilder<Projection<JoinMainEntity, TJoin>> joined)
    {
        var cmd = joined.Select(_ => 1);
        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        return cmd;
    }

    private static void ConfigureConstant(InMemoryDataContext ctx)
        => ctx.From<ConstantBoundEntity>(b => b.HasQueryFilter(e => true));

    private static void ConfigureOpaque(InMemoryDataContext ctx)
        => ctx.From<OpaqueBoundEntity>(b => b.HasQueryFilter(e => e.ToString() == "opaque"));

    private static void ConfigureField(InMemoryDataContext ctx)
        => ctx.From<FieldBoundEntity>(b => b.HasQueryFilter(e => !e.Flag));

    private static void ConfigureFunc(InMemoryDataContext ctx, Func<EntityBuilder<FuncBoundEntity>, IDataContext, EntityBuilder<FuncBoundEntity>> filter)
        => ctx.From<FuncBoundEntity>(b => b.HasQueryFilter(filter));

    private static void ConfigureFuncSkipped(InMemoryDataContext ctx, Func<EntityBuilder<FuncSkippedBoundEntity>, IDataContext, EntityBuilder<FuncSkippedBoundEntity>> filter)
        => ctx.From<FuncSkippedBoundEntity>(b => b.HasQueryFilter(filter));

    private static void ConfigureFuncViolating(InMemoryDataContext ctx)
        => ctx.From<FuncViolatingBoundEntity>(b => b.HasQueryFilter(
            (eb, _) => eb.OrderBy(e => e.Id)));

    private static void ConfigureConstantFalse(InMemoryDataContext ctx)
        => ctx.From<ConstantFalseBoundEntity>(b => b.HasQueryFilter(e => false));

    private static void ConfigureColumnDependent(InMemoryDataContext ctx)
        => ctx.From<ColumnDependentBoundEntity>(b => b.HasQueryFilter(e => e.Id > 0));

    private static void ConfigureMixedDependency(InMemoryDataContext ctx)
        => ctx.From<MixedDependencyBoundEntity>(b => b
            .HasQueryFilter(e => true)
            .HasQueryFilter(e => e.ToString() == "opaque"));

    private static void ConfigureNested(InMemoryDataContext ctx)
        => ctx.From<NestedRootEntity>(b => b.HasQueryFilter(e => e.Nested.TenantId == 1));

    private static void ConfigureForeignParam(InMemoryDataContext ctx)
        => ctx.From<ForeignParamEntity>(b => b.HasQueryFilter(e => e.Children.Any(c => c.TenantId == 1)));

    private static void ConfigureDerivedRoot(InMemoryDataContext ctx)
        => ctx.From<DerivedRootEntity>(b => b.HasQueryFilter(e => ((BaseNestedEntity)e).BaseId == 1));

    // --- Decision matrix -------------------------------------------------------------------------

    [Fact]
    public void Compatible_AllRequiredColumnsDeclared_AppliedWithoutWarning()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = Prepared(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("every filter dependency is declared");
        cmd.PreparedCondition.Should().NotBeNull();
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId").And.Contain("Id");
    }

    [Fact]
    public void PartiallyIncompatible_SkipsMissingFilter_AppliesCompatible()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        var skip = cmd.PendingRawSourceFilterSkips![0];
        skip.FilterKey.Should().Be("tenant");
        skip.Reason.Should().Be("MissingColumns");
        skip.MissingColumns.Should().Be("tenant_id");
        cmd.PreparedCondition.Should().NotBeNull("the compatible anonymous filter is still applied");
        cmd.PreparedCondition!.ToString().Should().NotContain("TenantId");
    }

    [Fact]
    public void FindMissing_IsCaseInsensitive()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["ID", "TENANT_ID"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("column matching is OrdinalIgnoreCase");
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void EmptyColumns_SkipsEveryFilterAsUndetermined()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(Array.Empty<string>());

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().HaveCount(2);
        cmd.PendingRawSourceFilterSkips!.Should().OnlyContain(s => s.Reason == "UndeterminedColumns");
        cmd.PendingRawSourceFilterSkips!.Should().OnlyContain(s => s.SourceOrdinal == 0, "the main source is ordinal 0");
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void EmptyColumns_ConstantTrueApplied()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureConstant(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<ConstantBoundEntity>(Array.Empty<string>());

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("a proven zero-column predicate needs no declared column");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    [Fact]
    public void EmptyColumns_ConstantFalseApplied()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureConstantFalse(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<ConstantFalseBoundEntity>(Array.Empty<string>());

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("a proven zero-column predicate needs no declared column");
        cmd.PreparedCondition.Should().NotBeNull();
        cmd.PreparedCondition!.ToString().Should().Contain("False");
    }

    [Fact]
    public void EmptyColumns_ColumnDependentSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureColumnDependent(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<ColumnDependentBoundEntity>(Array.Empty<string>());

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PendingRawSourceFilterSkips![0].SourceOrdinal.Should().Be(0);
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void EmptyColumns_UndeterminedPredicateSkipped_ProvenEmptyStillApplied()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureMixedDependency(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<MixedDependencyBoundEntity>(Array.Empty<string>());

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle("only the opaque predicate is undetermined");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PreparedCondition.Should().NotBeNull("the proven zero-column predicate is applied");
        cmd.PreparedCondition!.ToString().Should().Contain("True");
    }

    [Fact]
    public void ConstantPredicate_WithDeclaredColumns_AppliesWithoutColumnDependency()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureConstant(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<ConstantBoundEntity>(["id"]);

        var cmd = Prepared(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("a zero-column predicate has no missing dependency");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    [Fact]
    public void OpaqueWholeEntityUsage_IsUndeterminedAndSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureOpaque(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<OpaqueBoundEntity>(["id"]);

        var cmd = Prepared(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void FieldRead_IsUndeterminedAndSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureField(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<FieldBoundEntity>(["id"]);

        var cmd = Prepared(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
    }

    // --- Func filter -----------------------------------------------------------------------------

    [Fact]
    public void FuncFilter_IsInvokedOnce_EvenWhenSkipped()
    {
        using var ctx = new InMemoryDataContext();
        var calls = 0;
        ConfigureFuncSkipped(ctx, (eb, _) =>
        {
            System.Threading.Interlocked.Increment(ref calls);
            return eb.Where(e => e.TenantId == 1);
        });
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<FuncSkippedBoundEntity>(["id"]);

        var cmd = PreparedIds(ctx, bound);

        calls.Should().Be(1, "the builder-function is analyzed once and its predicate is reused");
        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
    }

    [Fact]
    public void FuncFilter_Compatible_IsInvokedOnceAndApplied()
    {
        using var ctx = new InMemoryDataContext();
        var calls = 0;
        ConfigureFunc(ctx, (eb, _) =>
        {
            System.Threading.Interlocked.Increment(ref calls);
            return eb.Where(e => e.TenantId == 1);
        });
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<FuncBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedIds(ctx, bound);

        calls.Should().Be(1);
        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void FuncFilter_ChangingMoreThanWhere_IsRejected()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureFuncViolating(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<FuncViolatingBoundEntity>(["id"]);
        var cmd = bound.Select(_ => 1);

        var act = () => cmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        act.Should().Throw<NotSupportedException>();
    }

    // --- IgnoreFilters ---------------------------------------------------------------------------

    [Fact]
    public void IgnoreFilters_All_AppliesNothingAndWarnsNothing()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(Array.Empty<string>())
            .IgnoreFilters();

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("ignored filters are not analyzed, so they cannot warn");
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void IgnoreFilters_ByKey_LeavesOtherFiltersActiveWithoutWarning()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(["id"])
            .IgnoreFilters(["tenant"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("the ignored tenant filter warned nothing and 'id' satisfies the anonymous filter");
        cmd.PreparedCondition.Should().NotBeNull("the anonymous Id filter is applied");
        cmd.PreparedCondition!.ToString().Should().Contain("Id");
    }

    [Fact]
    public void IgnoreFilters_ByType_AppliesNothingAndWarnsNothing()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(Array.Empty<string>())
            .IgnoreFilters(typeof(TenantBoundEntity));

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void IgnoreFilters_ByKeyAndType_IgnoresKeyedFilterAndLeavesAnonymousMissingColumns()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        // The key+type scope is an intersection: it disables only the filter whose key is "tenant" and
        // whose entity type is TenantBoundEntity. The anonymous Id filter is not keyed, so it is still
        // analyzed; with a declared shape that omits "id" it is skipped as MissingColumns (not by the
        // empty-declaration path).
        var bound = ctx.FromSql("select tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["tenant_id"])
            .IgnoreFilters(["tenant"], typeof(TenantBoundEntity));

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("MissingColumns");
        cmd.PendingRawSourceFilterSkips![0].MissingColumns.Should().Be("id");
        cmd.PreparedCondition.Should().BeNull();
    }

    // --- Joined bound raw sources ----------------------------------------------------------------

    [Fact]
    public void JoinedSource_CompatibleFiltersAppliedToJoinAlias()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().Join(joinedSource, (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.Joins.Should().ContainSingle();
        cmd.Joins![0].JoinCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void JoinedSource_MixedCompatibleIncompatible_SkipsOnlyMissingFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(["id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().Join(joinedSource, (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        var skip = cmd.PendingRawSourceFilterSkips![0];
        skip.EntityType.Should().Be(nameof(TenantBoundEntity));
        skip.Reason.Should().Be("MissingColumns");
        skip.MissingColumns.Should().Be("tenant_id");
        skip.SourceOrdinal.Should().Be(1, "the first join is ordinal 1");
        cmd.Joins![0].JoinCondition!.ToString().Should().NotContain("TenantId");
        cmd.Joins![0].JoinCondition!.ToString().Should().Contain("5", "the compatible anonymous Id filter is still applied");
    }

    [Fact]
    public void JoinedSource_RepeatedAliases_UseEachJoinsOwnBinding()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var incompatible = ctx.FromSql("select id from rsb_first").BindEntity<TenantBoundEntity>(["id"]);
        var compatible = ctx.FromSql("select id, tenant_id from rsb_second").BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedIds(ctx, ctx.From<JoinMainEntity>()
            .Join(incompatible, (a, b) => a.Id == b.Id)
            .Join(compatible, (p, b) => p.Item2.Id == b.Id));

        cmd.Joins.Should().HaveCount(2);
        cmd.Joins![0].JoinCondition!.ToString().Should().NotContain("TenantId");
        cmd.Joins![1].JoinCondition!.ToString().Should().Contain("TenantId");
        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].SourceOrdinal.Should().Be(1);
    }

    [Fact]
    public void JoinedSource_SourceOrdinal_CountsUnboundJoins()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var incompatible = ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(["id"]);

        var cmd = PreparedIds(ctx, ctx.From<JoinMainEntity>()
            .Join(ctx.From<JoinMainEntity>(), (a, b) => a.Id == b.Id)
            .Join(incompatible, (p, b) => p.Item1.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].SourceOrdinal.Should().Be(2, "the unbound join still occupies ordinal 1");
    }

    [Fact]
    public void JoinedSource_OuterJoin_PredicateStaysInOn()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().LeftJoin(joinedSource, (a, b) => a.Id == b.Id));

        cmd.Joins![0].JoinType.Should().Be(JoinType.Left);
        cmd.Joins![0].JoinCondition!.ToString().Should().Contain("TenantId");
        cmd.PreparedCondition.Should().BeNull("an outer-join filter stays in ON and is never moved into WHERE");
    }

    [Fact]
    public void MainSource_BoundRaw_Join_CompatibleFilterAppliedToMainAlias()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);

        var main = ctx.FromSql("select id, tenant_id from rsb_main_table")
            .BindEntity<TenantBoundEntity>(["id", "tenant_id"]);
        var cmd = PreparedIds(ctx, main.Join(ctx.From<JoinMainEntity>(), (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().BeNull("every main-source dependency is declared");
        cmd.PreparedCondition.Should().NotBeNull("main-source filters stay in WHERE");
        var condition = cmd.PreparedCondition!.ToString();
        condition.Should().Contain("Item1", "the filter is re-rooted onto the main source's own alias");
        condition.Should().Contain("TenantId");
        condition.Should().Contain("5");
    }

    [Fact]
    public void MainSource_BoundRaw_Join_MixedFilters_SkipOnlyTheIncompatibleOne()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);

        // The main source declares only "id": the tenant filter must be skipped (ordinal 0) while the
        // compatible anonymous Id filter is still applied — a skipped tenant filter never suppresses it.
        var main = ctx.FromSql("select id from rsb_main_table").BindEntity<TenantBoundEntity>(["id"]);
        var cmd = PreparedIds(ctx, main.Join(ctx.From<JoinMainEntity>(), (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        var skip = cmd.PendingRawSourceFilterSkips![0];
        skip.EntityType.Should().Be(nameof(TenantBoundEntity));
        skip.Reason.Should().Be("MissingColumns");
        skip.MissingColumns.Should().Be("tenant_id");
        skip.SourceOrdinal.Should().Be(0, "the main source is ordinal 0");
        var condition = cmd.PreparedCondition!.ToString();
        condition.Should().Contain("5", "the compatible Id filter is still applied");
        condition.Should().NotContain("TenantId");
    }

    [Fact]
    public void MainSource_BoundRaw_Join_NeverBorrowsJoinBindingOrAlias()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);

        var main = ctx.FromSql("select id from rsb_main_table").BindEntity<TenantBoundEntity>(["id"]);
        var joined = ctx.FromSql("select id, tenant_id from rsb_join_table")
            .BindEntity<TenantBoundEntity>(["id", "tenant_id"]);
        var cmd = PreparedIds(ctx, main.Join(joined, (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].SourceOrdinal.Should().Be(0, "the main source's own binding decides");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("MissingColumns");
        cmd.Joins![0].JoinCondition!.ToString().Should().Contain("TenantId", "the join keeps its own compatible filter");
        cmd.PreparedCondition!.ToString().Should().NotContain("TenantId", "the main alias must not borrow the join binding");
    }

    // --- Bound raw join on CROSS/APPLY (no ON clause) --------------------------------------------

    [Fact]
    public void BoundRawCrossJoin_AppliesFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id, tenant_id from rsb_cross_table")
            .BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossJoin(joinedSource));

        cmd.PendingRawSourceFilterSkips.Should().BeNull("every join dependency is declared");
        cmd.Joins.Should().ContainSingle();
        cmd.Joins![0].JoinCondition.Should().BeNull("a cross join must never fabricate an ON clause");
        cmd.PreparedCondition.Should().NotBeNull("the compatible cross-join filter is placed in WHERE");
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void BoundRawCrossJoin_IncompatibleColumns_EmitsSkipWarning()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id from rsb_cross_table")
            .BindEntity<TenantBoundEntity>(["id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossJoin(joinedSource));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        var skip = cmd.PendingRawSourceFilterSkips![0];
        skip.Reason.Should().Be("MissingColumns");
        skip.MissingColumns.Should().Be("tenant_id");
        skip.SourceOrdinal.Should().Be(1, "the first join is ordinal 1");
        cmd.PreparedCondition.Should().NotBeNull("the compatible anonymous Id filter is still applied");
        cmd.PreparedCondition!.ToString().Should().Contain("5");
    }

    [Fact]
    public void BoundRawApplyJoin_AppliesFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id, tenant_id from rsb_apply_table")
            .BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossApply(joinedSource));

        cmd.PendingRawSourceFilterSkips.Should().BeNull("every apply dependency is declared");
        cmd.Joins.Should().ContainSingle();
        cmd.Joins![0].JoinCondition.Should().BeNull("an apply join must never fabricate an ON clause");
        cmd.PreparedCondition.Should().NotBeNull("the compatible apply-join filter is placed in WHERE");
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void BoundRawApplyJoin_IncompatibleColumns_EmitsSkipWarning()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var joinedSource = ctx.FromSql("select id from rsb_apply_table")
            .BindEntity<TenantBoundEntity>(["id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossApply(joinedSource));

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        var skip = cmd.PendingRawSourceFilterSkips![0];
        skip.Reason.Should().Be("MissingColumns");
        skip.MissingColumns.Should().Be("tenant_id");
        skip.SourceOrdinal.Should().Be(1, "the first join is ordinal 1");
        cmd.PreparedCondition.Should().NotBeNull("the compatible anonymous Id filter is still applied");
    }

    // --- Nested member chains (D4) ---------------------------------------------------------------

    [Fact]
    public void NestedMember_OnlyRootColumnDeclared_IsSkippedForTheDeeperColumn()
    {
        using var ctx = new InMemoryDataContext();
        _ = ctx.From<NestedLeafEntity>(); // publish the nested type's metadata (renderer parity)
        ConfigureNested(ctx);
        var bound = ctx.FromSql("select nested_root from rsb_nested_source")
            .BindEntity<NestedRootEntity>(["nested_root"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle(
            "the physical dependency is the leaf's column, not the declared root's");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("MissingColumns");
        cmd.PendingRawSourceFilterSkips![0].MissingColumns.Should().Be("leaf_tenant");
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void NestedMember_LeafColumnDeclared_IsTrackedAndApplied()
    {
        using var ctx = new InMemoryDataContext();
        _ = ctx.From<NestedLeafEntity>();
        ConfigureNested(ctx);
        var bound = ctx.FromSql("select leaf_tenant from rsb_nested_source")
            .BindEntity<NestedRootEntity>(["leaf_tenant"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("the declared leaf column satisfies the dependency");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    [Fact]
    public void SingleMember_DeclaredColumn_StillApplied()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureColumnDependent(ctx);
        var bound = ctx.FromSql("select id from rsb_single_source").BindEntity<ColumnDependentBoundEntity>(["id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.PreparedCondition.Should().NotBeNull();
    }

    // --- Unresolved parameter roots / transparent conversions (D5) -------------------------------

    [Fact]
    public void ForeignNestedParameter_IsUndeterminedAndSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureForeignParam(ctx);
        var bound = ctx.FromSql("select children from rsb_foreign_source")
            .BindEntity<ForeignParamEntity>(["children"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle(
            "a predicate reading a nested lambda's parameter cannot be proven column-independent");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void TransparentRootConversion_IsNormalizedAndApplied()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureDerivedRoot(ctx);
        var bound = ctx.FromSql("select base_id from rsb_derived_source")
            .BindEntity<DerivedRootEntity>(["base_id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("an upcast Convert around the entity parameter is transparent");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    // --- Immutability ----------------------------------------------------------------------------

    [Fact]
    public void CallerCollectionMutation_AfterBinding_DoesNotChangeTheBoundColumns()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        var columns = new List<string> { "Id", "TenantId" };
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(columns);

        columns.Clear();
        columns.Add("Injected");

        bound.SourceFrom!.SourceBinding!.AvailableColumns.Should().Equal("Id", "TenantId");
    }

    [Fact]
    public void CallerCollectionMutation_AfterBinding_DoesNotChangeThePreparedPlan()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var columns = new List<string> { "id", "tenant_id" };
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(columns);

        columns.Clear();

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull("the plan reads the owned copy, not the caller's collection");
    }

    // --- Comparer laws / plan identity -----------------------------------------------------------

    [Fact]
    public void SameSourceAndBinding_ProduceEqualPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var bound = ctx.FromSql("select id, tenant_id from rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]);

        var first = PreparedIds(ctx, bound);
        var second = PreparedIds(ctx, bound);

        first.GetOrCreatePlanKey(null).Equals(second.GetOrCreatePlanKey(null)).Should().BeTrue();
        first.GetOrCreatePlanKey(null).GetHashCode()
            .Should().Be(second.GetOrCreatePlanKey(null).GetHashCode(), "equal keys must hash equally");
    }

    [Fact]
    public void DifferentDeclaredColumns_ProduceDistinctPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var source = ctx.FromSql("select id, tenant_id from rsb_table");
        var full = PreparedIds(ctx, source.BindEntity<TenantBoundEntity>(["id", "tenant_id"]));
        var partial = PreparedIds(ctx, source.BindEntity<TenantBoundEntity>(["id"]));

        full.GetOrCreatePlanKey(null).Equals(partial.GetOrCreatePlanKey(null)).Should().BeFalse();
    }

    [Fact]
    public void DifferentBoundEntity_ProduceDistinctPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        ConfigureOpaque(ctx);
        var source = ctx.FromSql("select id from rsb_table");
        var tenant = PreparedIds(ctx, source.BindEntity<TenantBoundEntity>(["id"]));
        var opaque = PreparedIds(ctx, source.BindEntity<OpaqueBoundEntity>(["id"]));

        tenant.GetOrCreatePlanKey(null).Equals(opaque.GetOrCreatePlanKey(null)).Should().BeFalse();
    }

    [Fact]
    public void DifferentRawSqlText_ProduceDistinctPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        var first = PreparedIds(ctx, ctx.FromSql("select id from rsb_table").BindEntity<TenantBoundEntity>(["id"]));
        var second = PreparedIds(ctx, ctx.FromSql("select id from rsb_other").BindEntity<TenantBoundEntity>(["id"]));

        first.GetOrCreatePlanKey(null).Equals(second.GetOrCreatePlanKey(null)).Should().BeFalse(
            "a raw fragment is opaque and is identified by reference");
    }

    // --- Binding identity (comparer internals) ---------------------------------------------------

    private static FromExpressionPlanEqualityComparer NewFromComparer()
        => new(new QueryProvider());

    [Fact]
    public void SameSqlAndColumns_DifferentEntityType_NotEqualWithDistinctHash()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        ConfigureOpaque(ctx);
        // Both bindings are copied from the same raw source instance, so the raw-SQL reference is shared
        // and only the entity binding can distinguish the two sources.
        var source = ctx.FromSql("select id from rsb_table");
        var tenant = source.BindEntity<TenantBoundEntity>(["id"]);
        var opaque = source.BindEntity<OpaqueBoundEntity>(["id"]);

        var comparer = NewFromComparer();

        comparer.Equals(tenant.SourceFrom, opaque.SourceFrom).Should().BeFalse(
            "the bound entity type selects different filters");
        comparer.GetHashCode(tenant.SourceFrom).Should().NotBe(
            comparer.GetHashCode(opaque.SourceFrom),
            "different entity types must not collide in the plan key");
    }

    [Fact]
    public void SameTypeSameColumnCount_DifferentColumnNames_NotEqualWithDistinctHash()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        var source = ctx.FromSql("select id, tenant_id from rsb_table");
        var declared = source.BindEntity<TenantBoundEntity>(["id", "tenant_id"]);
        var swapped = source.BindEntity<TenantBoundEntity>(["id", "other"]);

        declared.SourceFrom!.SourceBinding!.AvailableColumns.Should().HaveCount(2);
        swapped.SourceFrom!.SourceBinding!.AvailableColumns.Should().HaveCount(2);

        var comparer = NewFromComparer();

        comparer.Equals(declared.SourceFrom, swapped.SourceFrom).Should().BeFalse(
            "equal column counts with different names declare different filter dependencies");
        comparer.GetHashCode(declared.SourceFrom).Should().NotBe(
            comparer.GetHashCode(swapped.SourceFrom),
            "the declared column names participate in the plan key");
    }

    [Fact]
    public void SameTypeSameColumns_DifferentEffectiveMetadataInstance_NotEqualByHash()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        var entityType = typeof(TenantBoundEntity);
        var firstMetadata = DataContextCache.Metadata[entityType];

        try
        {
            // Build a second mapping instance for the same entity type (same filters, new instance):
            // the comparer keys on the effective metadata instance, not just the CLR type.
            DataContextCache.Metadata.Remove(entityType);
            ConfigureTenant(ctx);
            var secondMetadata = DataContextCache.Metadata[entityType];
            secondMetadata.Should().NotBeSameAs(firstMetadata);

            var source = ctx.FromSql("select id, tenant_id from rsb_table");
            var withFirst = source.BindEntity<TenantBoundEntity>(["id", "tenant_id"]);
            var withSecond = source.BindEntity<TenantBoundEntity>(["id", "tenant_id"]);
            var comparer = NewFromComparer();

            DataContextCache.Metadata[entityType] = firstMetadata;
            var firstHash = comparer.GetHashCode(withFirst.SourceFrom);
            DataContextCache.Metadata[entityType] = secondMetadata;
            var secondHash = comparer.GetHashCode(withSecond.SourceFrom);

            secondHash.Should().NotBe(firstHash,
                "the effective metadata instance governs the injected filters and must key the plan");

            DataContextCache.Metadata[entityType] = firstMetadata;
            comparer.GetHashCode(withSecond.SourceFrom).Should().Be(firstHash,
                "with the same effective metadata instance the two bindings hash equally");
        }
        finally
        {
            DataContextCache.Metadata[entityType] = firstMetadata;
        }
    }

    [Fact]
    public void SameNamedTableAndBinding_ProduceEqualPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var first = PreparedIds(ctx, ctx.From("rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]));
        var second = PreparedIds(ctx, ctx.From("rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]));

        var firstKey = first.GetOrCreatePlanKey(null);
        var secondKey = second.GetOrCreatePlanKey(null);

        firstKey.Equals(secondKey).Should().BeTrue("identically bound named tables key the same plan");
        firstKey.GetHashCode().Should().Be(secondKey.GetHashCode(), "equal plan keys must hash equally");
    }

    [Fact]
    public void SameNamedTable_DifferentBinding_ProduceDistinctPlanKeys()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 7;
        ConfigureTenant(ctx);
        var full = PreparedIds(ctx, ctx.From("rsb_table").BindEntity<TenantBoundEntity>(["id", "tenant_id"]));
        var partial = PreparedIds(ctx, ctx.From("rsb_table").BindEntity<TenantBoundEntity>(["id"]));

        full.GetOrCreatePlanKey(null).Equals(partial.GetOrCreatePlanKey(null)).Should().BeFalse(
            "the named-table short-circuit must still honor the entity binding");
    }

    // --- In-memory rejection ---------------------------------------------------------------------

    [Fact]
    public void InMemory_BoundFromSql_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureTenant(ctx);
        var query = ctx.FromSql("select id from rsb_table")
            .BindEntity<TenantBoundEntity>(["id"])
            .Select(x => x.Id);

        var act = () => query.ToList();

        act.Should().Throw<NotSupportedException>("the in-memory provider cannot execute a raw FROM source");
    }

    [Fact]
    public void InMemory_UnboundFromSql_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        var query = ctx.FromSql("select id from rsb_table").Select(t => t["id"].AsInt);

        var act = () => query.ToList();

        act.Should().Throw<NotSupportedException>("the unbound rejection is unchanged");
    }

    // --- Transparent physical-column wrappers (dependency-walk coverage) -------------------------

    [SqlTable("rsb_wrapper_entity")]
    public sealed class WrapperBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("maybe_id")]
        public int? MaybeId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("created")]
        public DateTime Created { get; set; }
    }

    private static void ConfigureWrappers(InMemoryDataContext ctx)
        => ctx.From<WrapperBoundEntity>(b => b
            .HasQueryFilter(e => e.MaybeId != null && e.MaybeId.Value > 0)
            .HasQueryFilter(e => e.Name.Length > 3)
            .HasQueryFilter(e => e.Created.Year == 2024
                && e.Created.Month == 1 && e.Created.Day == 1
                && e.Created.Hour == 0 && e.Created.Minute == 0
                && e.Created.Second == 0 && e.Created.DayOfYear == 1));

    [Fact]
    public void TransparentWrappers_AllDeclared_AreResolvedToTheirUnderlyingColumns()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureWrappers(ctx);
        var bound = ctx.FromSql("select id, maybe_id, name, created from rsb_wrapper_source")
            .BindEntity<WrapperBoundEntity>(["maybe_id", "name", "created"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull(
            "Nullable.Value, string.Length and the DateTime parts are CLR-only wrappers over the mapped column");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    [Fact]
    public void TransparentWrappers_Undeclared_ReportTheUnderlyingPhysicalColumns()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureWrappers(ctx);
        var bound = ctx.FromSql("select id from rsb_wrapper_source")
            .BindEntity<WrapperBoundEntity>(["id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().HaveCount(3);
        cmd.PendingRawSourceFilterSkips!.Should().OnlyContain(s => s.Reason == "MissingColumns");
        cmd.PendingRawSourceFilterSkips!.Should().Contain(
            s => s.MissingColumns != null && s.MissingColumns.Contains("maybe_id"));
        cmd.PendingRawSourceFilterSkips!.Should().Contain(
            s => s.MissingColumns != null && s.MissingColumns.Contains("name"));
        cmd.PendingRawSourceFilterSkips!.Should().Contain(
            s => s.MissingColumns != null && s.MissingColumns.Contains("created"),
            "the DateTime-part filter depends on the underlying 'created' column");
    }

    // --- Field leaf in a nested chain (unresolvable physical column) ------------------------------

    [SqlTable("rsb_nested_field_leaf")]
    public sealed class NestedFieldLeaf
    {
        [Column("leaf_tenant")]
        public int TenantId { get; set; }

        public int FieldValue;
    }

    [SqlTable("rsb_nested_field_root")]
    public sealed class NestedFieldRoot
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nested_root")]
        public NestedFieldLeaf Nested { get; set; } = new();
    }

    [Fact]
    public void NestedFieldLeaf_IsUndeterminedAndSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<NestedFieldRoot>(b => b.HasQueryFilter(e => e.Nested.FieldValue == 1));
        var bound = ctx.FromSql("select nested_root from rsb_nested_field_source")
            .BindEntity<NestedFieldRoot>(["nested_root"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle(
            "a CLR field is not a mapped column, so the physical dependency cannot be proven");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PreparedCondition.Should().BeNull();
    }

    // --- Interface-typed member (MemberTranslator fallback path) ----------------------------------

    public interface IContractBound
    {
        int ContractTenant { get; }
    }

    [SqlTable("rsb_contract_entity")]
    public sealed class ContractBoundEntity : IContractBound
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        public int ContractTenant => TenantId;
    }

    [Fact]
    public void InterfaceTypedMember_IsUndeterminedAndSkipped()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ContractBoundEntity>(b => b.HasQueryFilter(e => ((IContractBound)e).ContractTenant == 1));
        var bound = ctx.FromSql("select tenant_id from rsb_contract_source")
            .BindEntity<ContractBoundEntity>(["tenant_id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle(
            "an interface-typed member is not the entity's own mapped PropertyInfo, so the column is unprovable");
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("UndeterminedColumns");
        cmd.PreparedCondition.Should().BeNull();
    }

    // --- Naming convention applied to an auto column name -----------------------------------------

    [SqlTable("rsb_conv_entity")]
    public sealed class ConventionBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        public int TenantId { get; set; }
    }

    [Fact]
    public void NamingConvention_DeclaredTranslatedName_IsApplied()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionBoundEntity>(b => b.HasQueryFilter(e => e.TenantId == 1));
        var bound = ctx.FromSql("select tenant_id from rsb_conv_source")
            .BindEntity<ConventionBoundEntity>(["tenant_id"])
            .WithNamingConvention(SnakeCaseNamingConvention.Instance);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().BeNull(
            "the dependency is the naming-convention-translated physical column");
        cmd.PreparedCondition.Should().NotBeNull();
    }

    [Fact]
    public void NamingConvention_UndeclaredTranslatedName_IsMissing()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionBoundEntity>(b => b.HasQueryFilter(e => e.TenantId == 1));
        var bound = ctx.FromSql("select TenantId from rsb_conv_source")
            .BindEntity<ConventionBoundEntity>(["TenantId"])
            .WithNamingConvention(SnakeCaseNamingConvention.Instance);

        var cmd = PreparedIds(ctx, bound);

        cmd.PendingRawSourceFilterSkips.Should().ContainSingle();
        cmd.PendingRawSourceFilterSkips![0].Reason.Should().Be("MissingColumns");
        cmd.PendingRawSourceFilterSkips![0].MissingColumns.Should().Be("tenant_id",
            "the convention translates the auto column name before the compatibility check");
    }

    // --- Null-form filter metadata (TryResolveFilterLambda false arm) -----------------------------

    [SqlTable("rsb_nullform_entity")]
    public sealed class NullFormBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    private sealed class NullFormFilter : IQueryFilterMetadata
    {
        public string Key => "rsb_null_form";

        public LambdaExpression? Lambda => null;

        public Delegate? Func => null;
    }

    private sealed class FilteredMetadata(IEntityMetadata inner, IReadOnlyList<IQueryFilterMetadata> filters) : IEntityMetadata
    {
        public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;

        public string? TableName => inner.TableName;

        public bool IsTableNameAuto => inner.IsTableNameAuto;

        public IPropertyMetadata? DynamicColumnsStore => inner.DynamicColumnsStore;

        public IReadOnlyList<IQueryFilterMetadata> Filters => filters;

        public IReadOnlyList<IRelationshipMetadata> Relationships => inner.Relationships;
    }

    private static IEntityMetadata InjectNullFormFilter<T>(IDataContext ctx)
    {
        _ = ctx.From<T>();
        var original = DataContextCache.Metadata[typeof(T)];
        DataContextCache.Metadata[typeof(T)] = new FilteredMetadata(original, [new NullFormFilter()]);
        return original;
    }

    private static void RestoreMetadata<T>(IEntityMetadata original)
        => DataContextCache.Metadata[typeof(T)] = original;

    [Fact]
    public void UnboundNamedSource_NullFormFilter_IsIgnored()
    {
        using var ctx = new InMemoryDataContext();
        var original = InjectNullFormFilter<NullFormBoundEntity>(ctx);
        try
        {
            var cmd = PreparedIds(ctx, ctx.From<NullFormBoundEntity>());

            cmd.PreparedCondition.Should().BeNull("a filter declaring neither form contributes nothing");
        }
        finally
        {
            RestoreMetadata<NullFormBoundEntity>(original);
        }
    }

    [Fact]
    public void BoundMainSource_NullFormFilter_IsIgnored()
    {
        using var ctx = new InMemoryDataContext();
        var original = InjectNullFormFilter<NullFormBoundEntity>(ctx);
        try
        {
            var bound = ctx.FromSql("select id from rsb_nullform_source")
                .BindEntity<NullFormBoundEntity>(["id"]);

            var cmd = PreparedIds(ctx, bound);

            cmd.PendingRawSourceFilterSkips.Should().BeNull();
            cmd.PreparedCondition.Should().BeNull();
        }
        finally
        {
            RestoreMetadata<NullFormBoundEntity>(original);
        }
    }

    [Fact]
    public void BoundCrossJoin_NullFormFilter_IsIgnored()
    {
        using var ctx = new InMemoryDataContext();
        var original = InjectNullFormFilter<NullFormBoundEntity>(ctx);
        try
        {
            var joined = ctx.FromSql("select id from rsb_nullform_cross")
                .BindEntity<NullFormBoundEntity>(["id"]);

            var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossJoin(joined));

            cmd.PendingRawSourceFilterSkips.Should().BeNull();
            cmd.PreparedCondition.Should().BeNull();
        }
        finally
        {
            RestoreMetadata<NullFormBoundEntity>(original);
        }
    }

    [Fact]
    public void BoundJoin_NullFormFilter_IsIgnored()
    {
        using var ctx = new InMemoryDataContext();
        var original = InjectNullFormFilter<NullFormBoundEntity>(ctx);
        try
        {
            var joined = ctx.FromSql("select id from rsb_nullform_join")
                .BindEntity<NullFormBoundEntity>(["id"]);

            var cmd = PreparedJoin(ctx.From<JoinMainEntity>().Join(joined, (a, b) => a.Id == b.Id));

            cmd.PendingRawSourceFilterSkips.Should().BeNull();
            cmd.Joins![0].JoinCondition.Should().NotBeNull(
                "a filter with neither form leaves the ON clause untouched");
        }
        finally
        {
            RestoreMetadata<NullFormBoundEntity>(original);
        }
    }

    // --- Empty-filter arms (zero-filter joined source) --------------------------------------------

    [Fact]
    public void BoundCrossJoin_ZeroFiltersOnJoinedEntity_NoWarning()
    {
        using var ctx = new InMemoryDataContext();
        var joined = ctx.FromSql("select id from rsb_cross_zero").BindEntity<JoinMainEntity>(["id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().CrossJoin(joined));

        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void BoundJoin_ZeroFiltersOnJoinedEntity_LeavesOnUntouched()
    {
        using var ctx = new InMemoryDataContext();
        var joined = ctx.FromSql("select id from rsb_join_zero").BindEntity<JoinMainEntity>(["id"]);

        var cmd = PreparedJoin(ctx.From<JoinMainEntity>().Join(joined, (a, b) => a.Id == b.Id));

        cmd.PendingRawSourceFilterSkips.Should().BeNull();
        cmd.Joins![0].JoinCondition.Should().NotBeNull();
    }
}
