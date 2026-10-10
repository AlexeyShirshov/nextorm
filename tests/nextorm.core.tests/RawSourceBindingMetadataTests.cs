using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// P1 regression for #124 D1: <c>BindEntity</c> (and the filter resolver) must resolve
/// <c>TEntity</c>'s metadata through the normal resolution path, so a bound raw source's filters apply
/// on first use and after <see cref="DataContextCache.Clear"/> — without a prior <c>From&lt;T&gt;</c>
/// priming. The attribute filters below are declared on the CLR types, so an auto-resolved mapping
/// carries them even after the process-wide caches are cleared.
/// <para>
/// Runs in the "Query cache controls" collection (serialized, parallelization disabled) because it
/// clears the process-wide <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class RawSourceBindingMetadataTests
{
    [SqlTable("rsb_meta_first_use")]
    [QueryFilter(FilterKey = "attr", FilterLambda = nameof(AttrFilter))]
    public sealed class FirstUseBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        public static Expression<Func<FirstUseBoundEntity, bool>> AttrFilter() => e => e.TenantId > 5;
    }

    [SqlTable("rsb_meta_after_clear")]
    [QueryFilter(FilterKey = "attr", FilterLambda = nameof(AttrFilter))]
    public sealed class AfterClearBoundEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        public static Expression<Func<AfterClearBoundEntity, bool>> AttrFilter() => e => e.TenantId > 5;
    }

    /// <summary>
    /// #185 D185: a type whose attribute-mapped column name differs from any configured name, so a
    /// configured mapping is observably distinct from the auto mapping.
    /// </summary>
    [SqlTable("rsb_meta_configured")]
    public sealed class ConfiguredBoundEntity
    {
        [Column("attr_name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// W2 observation fixture: a type that is registered only by a throw-path <c>BindEntity</c> on an
    /// already-composed receiver, because the registration runs before the composition guard.
    /// </summary>
    [SqlTable("rsb_meta_throwing_receiver")]
    public sealed class ThrowingReceiverEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    private static QueryCommand<int> PreparedIds<T>(IDataContext ctx, EntityBuilder<T> builder)
    {
        var cmd = builder.Select(_ => 1);
        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        return cmd;
    }

    [Fact]
    public void BindEntity_FirstUse_AppliesFilter()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();

        // Never primed through From<TEntity>(): the metadata must be resolved by the binding path.
        var bound = ctx.FromSql("select id, tenant_id from rsb_meta_first_use")
            .BindEntity<FirstUseBoundEntity>(["id", "tenant_id"]);

        var cmd = PreparedIds(ctx, bound);

        cmd.PreparedCondition.Should().NotBeNull("the attribute filter applies without From<T> priming");
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void BindEntity_AfterClear_AppliesFilter()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();

        // The binding is created before the clear and prepared after it: the metadata dropped by
        // Clear() must be re-resolved through the normal path at filter time.
        var bound = ctx.FromSql("select id, tenant_id from rsb_meta_after_clear")
            .BindEntity<AfterClearBoundEntity>(["id", "tenant_id"]);

        DataContextCache.Clear();

        var cmd = PreparedIds(ctx, bound);

        cmd.PreparedCondition.Should().NotBeNull("metadata is re-resolved after Clear()");
        cmd.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void BindEntity_ColdMetadata_RegistersMappingWithoutPriorFrom()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();

        DataContextCache.Metadata.ContainsKey(typeof(FirstUseBoundEntity)).Should().BeFalse(
            "the configured metadata cache starts cold");

        var bound = ctx.FromSql("select id, tenant_id from rsb_meta_first_use")
            .BindEntity<FirstUseBoundEntity>(["id", "tenant_id"]);

        // The bound builder's typed projection must resolve the mapped member from the registration.
        var cmd = bound.Select(x => x.Id);
        cmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        DataContextCache.Metadata.ContainsKey(typeof(FirstUseBoundEntity)).Should().BeTrue(
            "binding registers the mapping through the same path as From<T>()");
        DataContextCache.Metadata[typeof(FirstUseBoundEntity)].Properties
            .Should().Contain(p => p.ColumnName == "id");
    }

    [Fact]
    public void BindEntity_RepeatedBind_DoesNotRebuildTheMapping()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();

        var source = ctx.FromSql("select id, tenant_id from rsb_meta_first_use");
        _ = source.BindEntity<FirstUseBoundEntity>(["id", "tenant_id"]);
        var first = DataContextCache.Metadata[typeof(FirstUseBoundEntity)];

        _ = source.BindEntity<FirstUseBoundEntity>(["id", "tenant_id"]);

        DataContextCache.Metadata[typeof(FirstUseBoundEntity)].Should().BeSameAs(first,
            "a repeated bind is a registration no-op");
    }

    [Fact]
    public void BindEntity_PreConfiguredMapping_IsNotOverwritten()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();
        ctx.From<ConfiguredBoundEntity>(cfg => cfg.Property(x => x.Name!).HasColumnName("configured_name"));
        var configured = DataContextCache.Metadata[typeof(ConfiguredBoundEntity)];
        configured.Properties.Should().ContainSingle(p => p.ColumnName == "configured_name");

        _ = ctx.FromSql("select configured_name from rsb_meta_configured")
            .BindEntity<ConfiguredBoundEntity>(["configured_name"]);

        DataContextCache.Metadata[typeof(ConfiguredBoundEntity)].Should().BeSameAs(configured,
            "a configured mapping wins over the auto mapping and binding never overwrites it");
        DataContextCache.Metadata[typeof(ConfiguredBoundEntity)].Properties
            .Should().NotContain(p => p.ColumnName == "attr_name");
    }

    // W2 (observation, no fix): the registration side-effect in BindEntity runs before
    // BindEntitySource's composition guard. A bind on an already-composed receiver throws
    // InvalidOperationException yet has already populated the process-wide configured Metadata. The
    // frozen rv2 plan does not require a cache rollback on a throwing bind, so this stays an
    // observation for CHECK, not a fixed behavior.
    [Fact]
    public void BindEntity_OnComposedReceiver_ThrowsAndStillRegistersMapping()
    {
        DataContextCache.Clear();
        using var ctx = new InMemoryDataContext();

        DataContextCache.Metadata.ContainsKey(typeof(ThrowingReceiverEntity)).Should().BeFalse(
            "the configured metadata cache starts cold");

        var composed = ctx.From("rsb_meta_throwing_receiver")
            .Where(t => t["id"].AsInt > 0);

        var act = () => composed.BindEntity<ThrowingReceiverEntity>(["id"]);

        act.Should().Throw<InvalidOperationException>(
            "a composed receiver is rejected by the composition guard");
        DataContextCache.Metadata.ContainsKey(typeof(ThrowingReceiverEntity)).Should().BeTrue(
            "observation: registration (EntityBuilderExtensions.cs:78-79) precedes the guard (EntityBuilder.cs:2032-2033)");
    }
}
