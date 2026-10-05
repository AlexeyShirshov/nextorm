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
}
