using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #123 target-filter isolation for the in-memory key upsert. The in-memory provider has no atomic way
/// to keep a filtered-out row out of an upsert, so an active target filter (not disabled with
/// <c>IgnoreFilters</c>) makes the operation refuse with <see cref="NotSupportedException"/> before any
/// state is touched. The no-filter and <c>IgnoreFilters</c> forms keep the previous behavior. No
/// database is involved.
/// </summary>
public class MergeTargetFilterGuardTests
{
    private const string TenantKey = "mtfg_tenant";
    private const string CodeKey = "mtfg_code";

    public sealed class GuardedEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PlainEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class UnrelatedEntity
    {
        public int Id { get; set; }
    }

    /// <summary>A filter over a reference-typed (<see cref="Code"/>) and a value-typed
    /// (<see cref="Rank"/>) column; both mappings must resolve to the same refusal.</summary>
    public sealed class ReferenceValueEntity
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public int Rank { get; set; }
    }

    private static InMemoryDataContext GuardedContext(int tenant, out List<GuardedEntity> rows)
    {
        var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = tenant;
        ctx.From<GuardedEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));
        rows = [];
        ctx.Data[typeof(GuardedEntity)] = rows;
        return ctx;
    }

    private static MergeBuilder<GuardedEntity> KeyUpsert(InMemoryDataContext ctx, GuardedEntity source)
        => ctx.CreateMergeBuilder<GuardedEntity>()
            .Using(source)
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert();

    [Fact]
    public void ActiveFilter_Sync_ShouldRefuseBeforeTouchingState()
    {
        using var ctx = GuardedContext(1, out var rows);

        var act = () => KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 1, Name = "a" }).Merge();

        act.Should().Throw<NotSupportedException>(
            "the in-memory key upsert cannot isolate the write target under an active filter");
        rows.Should().BeEmpty("the refusal happens before any row is added or mutated");
    }

    [Fact]
    public async Task ActiveFilter_Async_ShouldRefuseBeforeTouchingState()
    {
        using var ctx = GuardedContext(1, out var rows);

        var act = async () => await KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 1, Name = "a" })
            .MergeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ActiveFilter_ExistingRow_ShouldNotBeMutated()
    {
        using var ctx = GuardedContext(1, out var rows);
        rows.Add(new GuardedEntity { Id = 1, TenantId = 1, Name = "old" });

        var act = () => KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 1, Name = "new" }).Merge();

        act.Should().Throw<NotSupportedException>();
        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("old", "no update runs when the upsert refuses");
    }

    [Fact]
    public void ActiveFilter_NonPassingSource_ShouldRefuseBeforeSourceValidation()
    {
        using var ctx = GuardedContext(1, out var rows);

        // The source row (TenantId=2) would fail the filter, but the capability refusal has
        // precedence: NotSupportedException, not QueryFilterException, and no state is touched.
        var act = () => KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "b" }).Merge();

        act.Should().Throw<NotSupportedException>("the refusal precedes source-value validation");
        rows.Should().BeEmpty();
    }

    [Fact]
    public void IgnoreFiltersAll_ShouldUpsertAsBefore()
    {
        using var ctx = GuardedContext(1, out var rows);

        var affected = KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "a" })
            .IgnoreFilters()
            .Merge();

        affected.Should().Be(1);
        rows.Should().ContainSingle("IgnoreFilters opts out of the guard and keeps the native upsert");
        rows[0].TenantId.Should().Be(2);
    }

    [Fact]
    public async Task IgnoreFiltersAll_Async_ShouldUpsertAsBefore()
    {
        using var ctx = GuardedContext(1, out var rows);

        var affected = await KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "a" })
            .IgnoreFilters()
            .MergeAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        rows.Should().ContainSingle();
    }

    [Fact]
    public void NoFilterConfigured_ShouldUpsertAsBefore()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<PlainEntity>();
        ctx.Data[typeof(PlainEntity)] = rows;

        var affected = ctx.CreateMergeBuilder<PlainEntity>()
            .Using(new PlainEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task NoFilterConfigured_Async_ShouldUpsertAsBefore()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<PlainEntity>();
        ctx.Data[typeof(PlainEntity)] = rows;

        var affected = await ctx.CreateMergeBuilder<PlainEntity>()
            .Using(new PlainEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        rows.Should().ContainSingle("sync and async agree");
    }

    [Fact]
    public void IgnoreFiltersByName_MatchingKey_ShouldBypass_OtherKey_ShouldRefuse()
    {
        using var ctx = GuardedContext(1, out _);

        var bypassed = KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "a" })
            .IgnoreFilters(["tenant"])
            .Merge();
        bypassed.Should().Be(1);

        var act = () => KeyUpsert(ctx, new GuardedEntity { Id = 2, TenantId = 2, Name = "b" })
            .IgnoreFilters(["other"])
            .Merge();
        act.Should().Throw<NotSupportedException>("a non-matching key leaves the tenant filter active");
    }

    [Fact]
    public void IgnoreFiltersByType_Target_ShouldBypass_Unrelated_ShouldRefuse()
    {
        using var ctx = GuardedContext(1, out _);

        var bypassed = KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "a" })
            .IgnoreFilters(typeof(GuardedEntity))
            .Merge();
        bypassed.Should().Be(1);

        var act = () => KeyUpsert(ctx, new GuardedEntity { Id = 2, TenantId = 2, Name = "b" })
            .IgnoreFilters(typeof(UnrelatedEntity))
            .Merge();
        act.Should().Throw<NotSupportedException>("scoping the disable to another type keeps the filter active");
    }

    [Fact]
    public void IgnoreFilters_ShouldAccumulate()
    {
        using var ctx = GuardedContext(1, out _);

        var affected = KeyUpsert(ctx, new GuardedEntity { Id = 1, TenantId = 2, Name = "a" })
            .IgnoreFilters(["other"])
            .IgnoreFilters(["tenant"])
            .Merge();

        affected.Should().Be(1, "the repeated selective scopes union into a complete bypass");
    }

    [Fact]
    public void ActiveFilter_ReferenceAndValueColumns_ShouldRefuse()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[CodeKey] = "visible";
        ctx.From<ReferenceValueEntity>(b => b
            .HasQueryFilter("code", (e, c) => e.Code == (string)c.Properties[CodeKey]));
        ctx.Data[typeof(ReferenceValueEntity)] = new List<ReferenceValueEntity>();

        var act = () => ctx.CreateMergeBuilder<ReferenceValueEntity>()
            .Using(new ReferenceValueEntity { Id = 1, Code = "visible", Rank = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>("a reference-typed filter column is an active filter too");
    }

    [Fact]
    public void InvalidNullSource_ShouldKeepTheArgumentGuard()
    {
        using var ctx = GuardedContext(1, out _);

        var nullEntity = () => ctx.CreateMergeBuilder<GuardedEntity>().Using((GuardedEntity)null!);
        var nullBatch = () => ctx.CreateMergeBuilder<GuardedEntity>().Using((IEnumerable<GuardedEntity>)null!);
        var emptyBatch = () => ctx.CreateMergeBuilder<GuardedEntity>().Using(Array.Empty<GuardedEntity>());

        nullEntity.Should().Throw<ArgumentNullException>("an active filter must not shadow the source guard");
        nullBatch.Should().Throw<ArgumentNullException>();
        emptyBatch.Should().Throw<ArgumentException>();
    }
}
