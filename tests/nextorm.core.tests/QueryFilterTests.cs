using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focal coverage for global query filters (#67) on the in-memory provider: the fluent and attribute
/// declarations, the per-context predicate reading <see cref="IDataContext.Properties"/>, joins,
/// <c>IgnoreFilters</c>, the no-filter fast path and the plan-key shape shared between two contexts.
/// </summary>
public class QueryFilterTests
{
    private const string TenantKey = "tenant";

    public sealed class TenantEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class FluentFilteredEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    public sealed class AttributedEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<AttributedEntity, bool>> ActiveOnly => e => !e.IsDeleted;
    }

    public sealed class JoinLeftEntity
    {
        public int Id { get; set; }
    }

    public sealed class JoinRightEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class IgnoredFilterEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class PlainEntity
    {
        public int Id { get; set; }
    }

    public sealed class FirstWinsEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveKeyedEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveTypeLeftEntity
    {
        public int Id { get; set; }
    }

    public sealed class SelectiveTypeRightEntity
    {
        public int Id { get; set; }
    }

    public sealed class SelectiveIntersectionA
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveIntersectionB
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveEmptyScopeEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveOrderEntity
    {
        public int Id { get; set; }
    }

    [QueryFilter(FilterKey = "soft", FilterLambda = nameof(Active))]
    public class SelectiveBaseEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<SelectiveBaseEntity, bool>> Active() => e => !e.IsDeleted;
    }

    public sealed class SelectiveDerivedEntity : SelectiveBaseEntity
    {
    }

    public sealed class SelectiveNullRemoveEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveReplaceEntity
    {
        public int Id { get; set; }
    }

    public sealed class SelectivePlanEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveUnionLeftEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveUnionRightEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class SelectiveAllScopeEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class SelectiveRepeatBuildEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    [QueryFilter(FilterKey = "soft", FilterLambda = nameof(BaseSoft))]
    public class PriorityBaseEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }

        public static Expression<Func<PriorityBaseEntity, bool>> BaseSoft() => e => !e.IsDeleted;
    }

    [QueryFilter(FilterKey = "soft", FilterLambda = nameof(DerivedSoft))]
    public sealed class PriorityDerivedEntity : PriorityBaseEntity
    {
        public static Expression<Func<PriorityDerivedEntity, bool>> DerivedSoft() => e => e.Id != 2;
    }

    [QueryFilter(FilterKey = "soft", FilterLambda = nameof(First))]
    [QueryFilter(FilterKey = "soft", FilterLambda = nameof(Second))]
    public sealed class DeclarationOrderEntity
    {
        public int Id { get; set; }

        public static Expression<Func<DeclarationOrderEntity, bool>> First() => e => e.Id == 1;

        public static Expression<Func<DeclarationOrderEntity, bool>> Second() => e => e.Id == 2;
    }

    // Two anonymous attribute filters on the same type: additive and order-independent, so they must both
    // apply (unlike a repeated named key, which is rejected).
    [QueryFilter(FilterLambda = nameof(GreaterThanZero))]
    [QueryFilter(FilterLambda = nameof(LessThanHundred))]
    public sealed class DuplicateAnonymousAttributeEntity
    {
        public int Id { get; set; }

        public static Expression<Func<DuplicateAnonymousAttributeEntity, bool>> GreaterThanZero() => e => e.Id > 0;

        public static Expression<Func<DuplicateAnonymousAttributeEntity, bool>> LessThanHundred() => e => e.Id < 100;
    }

    public sealed class WhitespaceKeyEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    /// <summary>A source entity for the core INSERT ... SELECT pre-check tests (no filter of its own).</summary>
    public sealed class InsertSelectSourceEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public bool IsDeleted { get; set; }
    }

    /// <summary>An INSERT target carrying two active filters (a keyed one and an anonymous one).</summary>
    public sealed class InsertMultiFilterEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public bool IsDeleted { get; set; }
    }

    /// <summary>The target of the INSERT/MERGE validation tests; shares one tenant filter shape.</summary>
    public sealed class InsertValidatedEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    /// <summary>The target of the anonymous soft-delete validation test.</summary>
    public sealed class InsertSoftDeleteEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    /// <summary>A filter reads a field, which the in-memory column rewrite cannot express.</summary>
    public sealed class InsertFieldFilterEntity
    {
        public int Id { get; set; }

        internal bool Deleted = true;
    }

    /// <summary>A filter calls an instance member, which the in-memory column rewrite cannot express.</summary>
    public sealed class InsertMethodFilterEntity
    {
        public int Id { get; set; }

        public bool IsActive() => true;
    }

    /// <summary>The interface a filter can be declared against while the written entity is concrete.</summary>
    public interface ITenantScopedFilter
    {
        int TenantId { get; }
    }

    /// <summary>
    /// A filter whose lambda parameter is the interface, while the written columns come from the
    /// concrete entity; the written-property index must resolve both to the same member.
    /// </summary>
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(TenantScoped))]
    public sealed class InterfaceFilteredEntity : ITenantScopedFilter
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        public static Expression<Func<ITenantScopedFilter, bool>> TenantScoped() => e => e.TenantId == 1;
    }

    /// <summary>A mapped member declared on a base type; a selector read through the base declaration
    /// must still resolve to the mapped column (the reference-equality identity bug).</summary>
    public class BaseDeclaredIdentityEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class DerivedBaseDeclaredIdentityEntity : BaseDeclaredIdentityEntity
    {
    }

    /// <summary>A base type whose member is hidden with <c>new</c>; the derived declaration is the
    /// mapped column.</summary>
    public class HiddenIdentityBaseEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class HiddenIdentityDerivedEntity : HiddenIdentityBaseEntity
    {
        public new int TenantId { get; set; }
    }

    /// <summary>An inherited attribute filter that reads a member declared on the base type while the
    /// written entity hides it with <c>new</c>; the entity-row and column paths must agree.</summary>
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(TenantScoped))]
    public class HiddenFilteredBaseEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        public static Expression<Func<HiddenFilteredBaseEntity, bool>> TenantScoped() => e => e.TenantId == 1;
    }

    public sealed class HiddenFilteredDerivedEntity : HiddenFilteredBaseEntity
    {
        public new int TenantId { get; set; }
    }

    /// <summary>An entity that maps a public column and also implements the filter's interface member
    /// explicitly, so the interface read resolves to the mapped column through the fallback path.</summary>
    [QueryFilter(FilterKey = "tenant", FilterLambda = nameof(TenantScoped))]
    public sealed class ExplicitInterfaceFilteredEntity : ITenantScopedFilter
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        int ITenantScopedFilter.TenantId => 1;

        public static Expression<Func<ITenantScopedFilter, bool>> TenantScoped() => e => e.TenantId == 1;
    }

    /// <summary>The base-declared counterpart of <see cref="InsertValidatedEntity"/> for the
    /// column-value validation identity tests.</summary>
    public class BaseDeclaredFilteredEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class DerivedBaseDeclaredFilteredEntity : BaseDeclaredFilteredEntity
    {
    }

    private static void ConfigureBaseDeclaredFiltered(EntityMetadataBuilder<DerivedBaseDeclaredFilteredEntity> b)
        => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]);

    private static void ConfigureInsertValidated(EntityMetadataBuilder<InsertValidatedEntity> b)
        => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]);

    private static void ConfigureInsertMultiFilter(EntityMetadataBuilder<InsertMultiFilterEntity> b)
        => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter(e => !e.IsDeleted);

    private static InMemoryDataContext InsertSelectContext(params InsertSelectSourceEntity[] sourceRows)
    {
        var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<InsertSelectSourceEntity>().WithData(sourceRows);
        return ctx;
    }

    private static Expression<Func<TenantEntity, IDataContext, bool>> TenantFilter()
        => (e, ctx) => e.TenantId == (int)ctx.Properties[TenantKey];

    private static InMemoryDataContext TenantContext(int tenant, params TenantEntity[] rows)
    {
        var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = tenant;
        ctx.From<TenantEntity>(b => b.HasQueryFilter(TenantFilter())).WithData(rows);
        return ctx;
    }

    /// <summary>
    /// The discriminating test: two contexts hold different tenant values, run the same query shape
    /// (so the plan key is shared) whose filter reads the per-context value. Each context must return
    /// only its own tenant's rows, and the two commands must describe the same plan.
    /// </summary>
    [Fact]
    public void TwoContexts_DifferentTenant_ShouldReturnOwnRows_AndSharePlan()
    {
        using var ctx1 = TenantContext(1,
            new TenantEntity { Id = 10, TenantId = 1 },
            new TenantEntity { Id = 11, TenantId = 1 },
            new TenantEntity { Id = 20, TenantId = 2 });

        using var ctx2 = TenantContext(2,
            new TenantEntity { Id = 10, TenantId = 1 },
            new TenantEntity { Id = 20, TenantId = 2 });

        QueryCommand<int> cmd1 = ctx1.From<TenantEntity>().Select(x => x.Id);
        QueryCommand<int> cmd2 = ctx2.From<TenantEntity>().Select(x => x.Id);

        cmd1.PrepareCommand(false, TestContext.Current.CancellationToken);
        cmd2.PrepareCommand(false, TestContext.Current.CancellationToken);

        var rows1 = ctx1.From<TenantEntity>().Select(x => x.Id).ToList();
        var rows2 = ctx2.From<TenantEntity>().Select(x => x.Id).ToList();

        rows1.Should().BeEquivalentTo([10, 11]);
        rows2.Should().BeEquivalentTo([20]);

        cmd1.GetOrCreatePlanKey(null).Equals(cmd2.GetOrCreatePlanKey(null))
            .Should().BeTrue("both contexts run the same query shape and must share one cached plan");
    }

    [Fact]
    public void FluentOneArgFilter_ShouldApplyWithoutExplicitWhere()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FluentFilteredEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new FluentFilteredEntity { Id = 1 },
            new FluentFilteredEntity { Id = 2, IsDeleted = true },
        ]);

        var rows = ctx.From<FluentFilteredEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    [Fact]
    public void AttributeFilter_ShouldApply()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<AttributedEntity>().WithData(
        [
            new AttributedEntity { Id = 1 },
            new AttributedEntity { Id = 2, IsDeleted = true },
        ]);

        var rows = ctx.From<AttributedEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    [Fact]
    public void Filter_ShouldApplyToJoinedEntity()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<JoinRightEntity>(b => b.HasQueryFilter((e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .WithData(
            [
                new JoinRightEntity { Id = 1, TenantId = 1 },
                new JoinRightEntity { Id = 2, TenantId = 2 },
            ]);
        ctx.From<JoinLeftEntity>().WithData([new JoinLeftEntity { Id = 1 }, new JoinLeftEntity { Id = 2 }]);

        var rows = ctx.From<JoinLeftEntity>()
            .Join(ctx.From<JoinRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => new { LeftId = p.Item1.Id, RightId = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(1);
        rows[0].LeftId.Should().Be(1);
    }

    [Fact]
    public void IgnoreFilters_ShouldDisableFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<IgnoredFilterEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new IgnoredFilterEntity { Id = 1 },
            new IgnoredFilterEntity { Id = 2, IsDeleted = true },
        ]);

        var filtered = ctx.From<IgnoredFilterEntity>().Select(x => x.Id).ToList();
        var unfiltered = ctx.From<IgnoredFilterEntity>().IgnoreFilters().Select(x => x.Id).ToList();

        filtered.Should().Equal(1);
        unfiltered.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void EntityWithoutFilter_ShouldBeUnchanged()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<PlainEntity>().WithData(
        [
            new PlainEntity { Id = 1 },
            new PlainEntity { Id = 2 },
        ]);

        var rows = ctx.From<PlainEntity>().Select(x => x.Id).ToList();

        rows.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SecondRegistration_ShouldBeIgnored_FirstFilterWins()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FirstWinsEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new FirstWinsEntity { Id = 1 },
            new FirstWinsEntity { Id = 2, IsDeleted = true },
        ]);

        ctx.From<FirstWinsEntity>(b => b.HasQueryFilter(e => e.Id == 999));

        var rows = ctx.From<FirstWinsEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    private static void ConfigureSelectiveKeyed(InMemoryDataContext ctx)
        => ctx.From<SelectiveKeyedEntity>(b => b
                .HasQueryFilter(e => e.Id < 100)
                .HasQueryFilter("soft", (e, _) => !e.IsDeleted))
            .WithData(
            [
                new SelectiveKeyedEntity { Id = 1 },
                new SelectiveKeyedEntity { Id = 2, IsDeleted = true },
                new SelectiveKeyedEntity { Id = 600 },
            ]);

    [Fact]
    public void SelectiveIgnore_ByKey_ShouldDisableOnlyNamedFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var onlyAnonymous = ctx.From<SelectiveKeyedEntity>().IgnoreFilters(["soft"]).Select(x => x.Id).ToList();
        var onlyNamed = ctx.From<SelectiveKeyedEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList();

        onlyAnonymous.Should().BeEquivalentTo([1, 2], "only the named soft-delete filter is disabled");
        onlyNamed.Should().BeEquivalentTo([1, 600], "only the anonymous Id filter is disabled");
    }

    [Fact]
    public void SelectiveIgnore_ByType_ShouldDisableOnlyThatEntityTypesFilters()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveTypeRightEntity>(b => b.HasQueryFilter(e => e.Id > 1)).WithData(
        [
            new SelectiveTypeRightEntity { Id = 1 },
            new SelectiveTypeRightEntity { Id = 2 },
        ]);
        ctx.From<SelectiveTypeLeftEntity>().WithData(
        [
            new SelectiveTypeLeftEntity { Id = 1 },
            new SelectiveTypeLeftEntity { Id = 2 },
        ]);

        var filtered = ctx.From<SelectiveTypeLeftEntity>()
            .Join(ctx.From<SelectiveTypeRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.Id)
            .ToList();
        var unfilteredRight = ctx.From<SelectiveTypeLeftEntity>()
            .IgnoreFilters(typeof(SelectiveTypeRightEntity))
            .Join(ctx.From<SelectiveTypeRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item2.Id)
            .ToList();

        filtered.Should().Equal(2);
        unfilteredRight.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SelectiveIgnore_ByKeyAndType_ShouldTargetIntersection()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveIntersectionA>(b => b.HasQueryFilter("soft", (e, _) => !e.IsDeleted)).WithData(
        [
            new SelectiveIntersectionA { Id = 1 },
            new SelectiveIntersectionA { Id = 2, IsDeleted = true },
        ]);
        ctx.From<SelectiveIntersectionB>(b => b.HasQueryFilter("soft", (e, _) => !e.IsDeleted)).WithData(
        [
            new SelectiveIntersectionB { Id = 1 },
            new SelectiveIntersectionB { Id = 2, IsDeleted = true },
        ]);

        var a = ctx.From<SelectiveIntersectionA>()
            .IgnoreFilters(["soft"], typeof(SelectiveIntersectionA))
            .Select(x => x.Id)
            .ToList();
        var b = ctx.From<SelectiveIntersectionB>()
            .IgnoreFilters(["soft"], typeof(SelectiveIntersectionA))
            .Select(x => x.Id)
            .ToList();

        a.Should().BeEquivalentTo([1, 2], "the named filter is disabled on the listed type");
        b.Should().Equal(1);
    }

    [Fact]
    public void SelectiveIgnore_EmptyScope_ShouldDisableNothing()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveEmptyScopeEntity>(b => b.HasQueryFilter(e => !e.IsDeleted)).WithData(
        [
            new SelectiveEmptyScopeEntity { Id = 1 },
            new SelectiveEmptyScopeEntity { Id = 2, IsDeleted = true },
        ]);

        var emptyKeys = ctx.From<SelectiveEmptyScopeEntity>().IgnoreFilters(Array.Empty<string>()).Select(x => x.Id).ToList();
        var emptyKeysWithType = ctx.From<SelectiveEmptyScopeEntity>()
            .IgnoreFilters(Array.Empty<string>(), typeof(SelectiveEmptyScopeEntity))
            .Select(x => x.Id)
            .ToList();
        var nullKeys = ctx.From<SelectiveEmptyScopeEntity>().IgnoreFilters((IEnumerable<string>)null!).Select(x => x.Id).ToList();

        emptyKeys.Should().Equal(1);
        emptyKeysWithType.Should().Equal(1);
        nullKeys.Should().Equal(1);
    }

    [Fact]
    public void SelectiveIgnore_OrderAndDuplicates_ShouldAccumulate()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveOrderEntity>(b => b
                .HasQueryFilter("lower", (e, _) => e.Id > 1)
                .HasQueryFilter("upper", (e, _) => e.Id < 100))
            .WithData(
            [
                new SelectiveOrderEntity { Id = 1 },
                new SelectiveOrderEntity { Id = 50 },
                new SelectiveOrderEntity { Id = 200 },
            ]);

        var both = ctx.From<SelectiveOrderEntity>().IgnoreFilters(["lower", "upper"]).Select(x => x.Id).ToList();
        var reversedWithDuplicate = ctx.From<SelectiveOrderEntity>()
            .IgnoreFilters(["upper"])
            .IgnoreFilters(["lower", "lower"])
            .Select(x => x.Id)
            .ToList();

        both.Should().BeEquivalentTo([1, 50, 200]);
        reversedWithDuplicate.Should().BeEquivalentTo([1, 50, 200]);
    }

    [Fact]
    public void SelectiveIgnore_AttributeOnBaseType_ShouldApplyToDerived()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveDerivedEntity>().WithData(
        [
            new SelectiveDerivedEntity { Id = 1 },
            new SelectiveDerivedEntity { Id = 2, IsDeleted = true },
        ]);

        var filtered = ctx.From<SelectiveDerivedEntity>().Select(x => x.Id).ToList();
        var ignored = ctx.From<SelectiveDerivedEntity>().IgnoreFilters(["soft"]).Select(x => x.Id).ToList();

        filtered.Should().Equal(1);
        ignored.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SelectiveIgnore_KeyedNullLambda_ShouldRemoveNamedSlot()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveNullRemoveEntity>(b => b
                .HasQueryFilter("soft", (e, _) => !e.IsDeleted)
                .HasQueryFilter("soft", null))
            .WithData(
            [
                new SelectiveNullRemoveEntity { Id = 1 },
                new SelectiveNullRemoveEntity { Id = 2, IsDeleted = true },
            ]);

        var rows = ctx.From<SelectiveNullRemoveEntity>().Select(x => x.Id).ToList();

        rows.Should().BeEquivalentTo([1, 2], "the null lambda removed the named slot");
    }

    [Fact]
    public void KeyedFilter_RepeatedKey_ShouldReplaceEarlierFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SelectiveReplaceEntity>(b => b
                .HasQueryFilter("k", (e, _) => e.Id == 1)
                .HasQueryFilter("k", (e, _) => e.Id == 2))
            .WithData(
            [
                new SelectiveReplaceEntity { Id = 1 },
                new SelectiveReplaceEntity { Id = 2 },
                new SelectiveReplaceEntity { Id = 3 },
            ]);

        var rows = ctx.From<SelectiveReplaceEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(2);
    }

    private static InMemoryDataContext PlanScopeContext(params SelectivePlanEntity[] rows)
    {
        var ctx = new InMemoryDataContext();
        ctx.From<SelectivePlanEntity>(b => b
                .HasQueryFilter("lower", (e, _) => e.Id > 1)
                .HasQueryFilter("upper", (e, _) => e.Id < 100))
            .WithData(rows);
        return ctx;
    }

    [Fact]
    public void SelectiveScope_SameScope_ShouldSharePlan()
    {
        using var ctx = PlanScopeContext(new SelectivePlanEntity { Id = 50 });

        QueryCommand<int> cmd1 = ctx.From<SelectivePlanEntity>().IgnoreFilters(["lower"]).Select(x => x.Id);
        QueryCommand<int> cmd2 = ctx.From<SelectivePlanEntity>().IgnoreFilters(["lower"]).Select(x => x.Id);

        cmd1.PrepareCommand(false, TestContext.Current.CancellationToken);
        cmd2.PrepareCommand(false, TestContext.Current.CancellationToken);

        cmd1.GetOrCreatePlanKey(null).Equals(cmd2.GetOrCreatePlanKey(null))
            .Should().BeTrue("equal selective scopes must share one cached plan");
    }

    [Fact]
    public void SelectiveScope_OrderAndDuplicates_ShouldSharePlan()
    {
        using var ctx = PlanScopeContext(new SelectivePlanEntity { Id = 50 });

        QueryCommand<int> cmd1 = ctx.From<SelectivePlanEntity>().IgnoreFilters(["lower", "upper"]).Select(x => x.Id);
        QueryCommand<int> cmd2 = ctx.From<SelectivePlanEntity>()
            .IgnoreFilters(["upper", "upper"])
            .IgnoreFilters(["lower"])
            .Select(x => x.Id);

        cmd1.PrepareCommand(false, TestContext.Current.CancellationToken);
        cmd2.PrepareCommand(false, TestContext.Current.CancellationToken);

        cmd1.GetOrCreatePlanKey(null).Equals(cmd2.GetOrCreatePlanKey(null))
            .Should().BeTrue("the scope is a set, so order and duplicates do not change the plan");
    }

    [Fact]
    public void SelectiveScope_DifferentScope_ShouldDistinguishPlan()
    {
        using var ctx = PlanScopeContext(new SelectivePlanEntity { Id = 50 });

        QueryCommand<int> onlyLower = ctx.From<SelectivePlanEntity>().IgnoreFilters(["lower"]).Select(x => x.Id);
        QueryCommand<int> onlyUpper = ctx.From<SelectivePlanEntity>().IgnoreFilters(["upper"]).Select(x => x.Id);
        QueryCommand<int> none = ctx.From<SelectivePlanEntity>().Select(x => x.Id);

        onlyLower.PrepareCommand(false, TestContext.Current.CancellationToken);
        onlyUpper.PrepareCommand(false, TestContext.Current.CancellationToken);
        none.PrepareCommand(false, TestContext.Current.CancellationToken);

        onlyLower.GetOrCreatePlanKey(null).Equals(onlyUpper.GetOrCreatePlanKey(null))
            .Should().BeFalse("different scopes inject different predicates");
        onlyLower.GetOrCreatePlanKey(null).Equals(none.GetOrCreatePlanKey(null))
            .Should().BeFalse("a selective scope must not reuse the unfiltered plan");
    }

    [Fact]
    public void SelectiveIgnore_TypeAndKey_ShouldUnionNotIntersect()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<SelectiveUnionRightEntity>(b => b
                .HasQueryFilter("soft", (e, c) => e.TenantId == (int)c.Properties[TenantKey]))
            .WithData(
            [
                new SelectiveUnionRightEntity { Id = 1, TenantId = 1 },
                new SelectiveUnionRightEntity { Id = 2, TenantId = 2 },
            ]);
        ctx.From<SelectiveUnionLeftEntity>(b => b
                .HasQueryFilter(e => e.Id < 100)
                .HasQueryFilter("soft", (e, _) => !e.IsDeleted))
            .WithData(
            [
                new SelectiveUnionLeftEntity { Id = 1 },
                new SelectiveUnionLeftEntity { Id = 2, IsDeleted = true },
                new SelectiveUnionLeftEntity { Id = 600 },
            ]);

        var rows = ctx.From<SelectiveUnionLeftEntity>()
            .IgnoreFilters(typeof(SelectiveUnionLeftEntity))
            .IgnoreFilters(["soft"])
            .Join(ctx.From<SelectiveUnionRightEntity>(), (l, r) => l.Id == r.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        rows.Should().BeEquivalentTo(
            [1, 2],
            "a type-only call and a key-only call accumulate by union (all left filters plus soft on any type)");
    }

    [Fact]
    public void SelectiveIgnore_AllDominatesAnySelectiveScope()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var selectiveThenAll = ctx.From<SelectiveKeyedEntity>().IgnoreFilters(["soft"]).IgnoreFilters().Select(x => x.Id);
        var allThenSelective = ctx.From<SelectiveKeyedEntity>().IgnoreFilters().IgnoreFilters(["soft"]).Select(x => x.Id);

        selectiveThenAll.FilterScope.All.Should().BeTrue("the all-or-nothing form dominates a preceding selective scope");
        allThenSelective.FilterScope.All.Should().BeTrue("a later selective call cannot narrow the all form");
        selectiveThenAll.IgnoreFilters.Should().BeTrue();
    }

    [Fact]
    public void SelectiveIgnore_EmptyAfterSelective_ShouldBeNoOp()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.From<SelectiveKeyedEntity>()
            .IgnoreFilters(["soft"])
            .IgnoreFilters(Array.Empty<string>())
            .IgnoreFilters(Array.Empty<Type>())
            .Select(x => x.Id);

        command.FilterScope.Ignores(typeof(SelectiveKeyedEntity), "soft").Should().BeTrue();
        command.FilterScope.Ignores(typeof(SelectiveKeyedEntity), QueryFilters.AnonymousKey).Should().BeFalse();
        command.FilterScope.All.Should().BeFalse("empty scopes must not widen the accumulated scope");
    }

    [Fact]
    public void RepeatedTerminalBuilds_ShouldNotAccumulateScope()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var builder = ctx.From<SelectiveKeyedEntity>().IgnoreFilters(["soft"]);

        var first = builder.Select(x => x.Id).ToList();
        var second = builder.Select(x => x.Id).ToList();

        first.Should().BeEquivalentTo([1, 2]);
        second.Should().BeEquivalentTo(
            [1, 2],
            "a repeated terminal build re-derives the same selective scope instead of accumulating it");

        var command = builder.Select(x => x.Id);
        command.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.ResetPreparation();
        command.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.FilterScope.Ignores(typeof(SelectiveKeyedEntity), "soft").Should().BeTrue();
        command.FilterScope.Ignores(typeof(SelectiveKeyedEntity), QueryFilters.AnonymousKey).Should().BeFalse();
        command.FilterScope.All.Should().BeFalse("repeated preparation must not turn the selective scope into all");
    }

    [Fact]
    public void CommandIgnoreFiltersGetter_ShouldReportAnyDisabledFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        ctx.From<SelectiveKeyedEntity>().Select(x => x.Id).IgnoreFilters.Should().BeFalse();

        var selective = ctx.From<SelectiveKeyedEntity>().IgnoreFilters(["soft"]).Select(x => x.Id);
        selective.IgnoreFilters.Should().BeTrue("a selective scope still disables at least one filter");

        var all = ctx.From<SelectiveKeyedEntity>().IgnoreFilters().Select(x => x.Id);
        all.IgnoreFilters.Should().BeTrue();

        selective.IgnoreFilters = false;
        selective.IgnoreFilters.Should().BeFalse();
        selective.FilterScope.IsEmpty.Should().BeTrue("the bool setter clears the whole selective scope");
    }

    [Fact]
    public void ReusedCommand_ScopeChange_ShouldInvalidatePreparationAndChangeResult()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.From<SelectiveKeyedEntity>().Select(x => x.Id);
        var filtered = command.ToList();
        command.IsPrepared.Should().BeTrue();

        // The selective setter changes the injected filters, so the prepared condition (and its memoized
        // plan key) must be discarded; otherwise the second call reuses the first scope's plan.
        command.FilterScope = QueryFilterScope.FromKeys(["soft"]);
        command.IsPrepared.Should().BeFalse("a scope change invalidates the prepared plan");
        var softIgnored = command.ToList();

        // The bool setter must invalidate as well.
        command.IgnoreFilters = true;
        command.IsPrepared.Should().BeFalse("the all-or-nothing setter invalidates the prepared plan");
        var allIgnored = command.ToList();

        filtered.Should().Equal([1], "the anonymous Id<100 and the named soft filter both apply");
        softIgnored.Should().BeEquivalentTo([1, 2], "only the named soft filter is disabled");
        allIgnored.Should().BeEquivalentTo([1, 2, 600], "every filter is disabled");
    }

    [Fact]
    public void DuplicateKey_AttributeOnDerivedType_ShouldOverrideBase()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<PriorityDerivedEntity>().WithData(
        [
            new PriorityDerivedEntity { Id = 1, IsDeleted = true },
            new PriorityDerivedEntity { Id = 2, IsDeleted = false },
            new PriorityDerivedEntity { Id = 3, IsDeleted = false },
        ]);

        var rows = ctx.From<PriorityDerivedEntity>().Select(x => x.Id).ToList();

        rows.Should().BeEquivalentTo([1, 3], "the derived declaration replaces the base one for the same key");
    }

    [Fact]
    public void DuplicateKey_OnSameType_ShouldThrowMetadataException()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<DeclarationOrderEntity>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(
                "*more than one*QueryFilterAttribute*soft*",
                "attribute declaration order is not guaranteed, so a repeated key on one type has no deterministic winner");
    }

    [Fact]
    public void DuplicateAnonymousAttributes_ShouldBothApply()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<DuplicateAnonymousAttributeEntity>().WithData(
        [
            new DuplicateAnonymousAttributeEntity { Id = 1 },
            new DuplicateAnonymousAttributeEntity { Id = 200 },
        ]);

        var rows = ctx.From<DuplicateAnonymousAttributeEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal([1], "two anonymous attribute filters are additive and combined with AND");
    }

    [Fact]
    public void WhitespaceFilterKey_ShouldBeTreatedAsAnonymous()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<WhitespaceKeyEntity>(b => b.HasQueryFilter("   ", (e, _) => !e.IsDeleted)).WithData(
        [
            new WhitespaceKeyEntity { Id = 1 },
            new WhitespaceKeyEntity { Id = 2, IsDeleted = true },
        ]);

        var filtered = ctx.From<WhitespaceKeyEntity>().Select(x => x.Id).ToList();
        var ignoreAnonymous = ctx.From<WhitespaceKeyEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList();
        var ignoreWhitespace = ctx.From<WhitespaceKeyEntity>().IgnoreFilters(["   "]).Select(x => x.Id).ToList();

        filtered.Should().Equal(1);
        ignoreAnonymous.Should().BeEquivalentTo([1, 2], "the whitespace key normalised to the anonymous key");
        ignoreWhitespace.Should().Equal([1], "disabling the literal whitespace key must not hit the anonymous slot");
    }

    // --- DML (UPDATE/DELETE): the target filter is injected into the key form's source condition and
    // --- the selective scope is plumbed through the DML builders. The in-memory provider cannot execute
    // --- DML, so these assert the prepared condition/scope rather than rows (SQL text is asserted in
    // --- the provider SQL-generation tests).

    [Fact]
    public void DmlDelete_KeyForm_ShouldCarryTargetFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.DeleteFrom<SelectiveKeyedEntity>().BuildKeyCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Condition.Should().NotBeNull();
        command.Condition!.FilterScope.IsEmpty.Should().BeTrue();
        command.Condition.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Condition.PreparedCondition.Should().NotBeNull("the key form still carries the target filter");
        command.Condition.PreparedCondition!.ToString().Should()
            .Contain("IsDeleted", "the named soft-delete filter is part of the source condition")
            .And.Contain("100", "the anonymous Id filter is part of the source condition");
        command.Condition.Cache.Should().BeTrue("preparing the DML source must not disable the plan cache");
    }

    [Fact]
    public void DmlDelete_KeyForm_IgnoreByKey_ShouldRemoveOnlyNamedFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.DeleteFrom<SelectiveKeyedEntity>()
            .IgnoreFilters(["soft"])
            .BuildKeyCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Condition!.FilterScope.Ignores(typeof(SelectiveKeyedEntity), "soft").Should().BeTrue();
        command.Condition.FilterScope.Ignores(typeof(SelectiveKeyedEntity), QueryFilters.AnonymousKey).Should().BeFalse();
        command.Condition.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Condition.PreparedCondition.Should().NotBeNull("the anonymous Id filter remains");
    }

    [Fact]
    public void DmlDelete_KeyForm_IgnoreAll_ShouldDisableEveryFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.DeleteFrom<SelectiveKeyedEntity>()
            .IgnoreFilters()
            .BuildKeyCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Condition!.FilterScope.All.Should().BeTrue();
        command.Condition.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Condition.PreparedCondition.Should().BeNull();
    }

    [Fact]
    public void DmlUpdate_KeyForm_ShouldCarryTargetFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.Update<SelectiveKeyedEntity>().BuildEntityCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Source.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Source.PreparedCondition.Should().NotBeNull("the key-form update still carries the target filter");
        command.Source.PreparedCondition!.ToString().Should()
            .Contain("IsDeleted", "the named soft-delete filter is part of the source condition")
            .And.Contain("100", "the anonymous Id filter is part of the source condition");
        command.Source.Cache.Should().BeTrue("preparing the DML source must not disable the plan cache");
    }

    [Fact]
    public void DmlUpdate_KeyForm_WithUserWhere_ShouldCarryKeyWhereAndFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.Update<SelectiveKeyedEntity>()
            .Where(x => x.Id > 1000)
            .BuildEntityCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Keys.Should().ContainSingle("the key form carries the declared key equality separately from the condition");
        command.Source.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Source.PreparedCondition!.ToString().Should()
            .Contain("1000", "the user Where predicate is part of the source condition")
            .And.Contain("IsDeleted", "the target's named filter is ANDed to the source condition")
            .And.Contain("100", "the target's anonymous filter is ANDed to the source condition");
    }

    [Fact]
    public void DmlIgnoreFilters_TwiceCalled_ShouldAccumulate()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureSelectiveKeyed(ctx);

        var command = ctx.DeleteFrom<SelectiveKeyedEntity>()
            .IgnoreFilters(["soft"])
            .IgnoreFilters([QueryFilters.AnonymousKey])
            .BuildKeyCommand(new SelectiveKeyedEntity { Id = 2 });

        command.Condition!.FilterScope.Ignores(typeof(SelectiveKeyedEntity), "soft").Should().BeTrue();
        command.Condition.FilterScope.Ignores(typeof(SelectiveKeyedEntity), QueryFilters.AnonymousKey).Should().BeTrue();
        command.Condition.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Condition.PreparedCondition.Should().BeNull();
    }

    // --- INSERT/MERGE validation (PR3): the target is never filtered; the written values are checked
    // --- against the target's active filters before execution. The in-memory provider cannot execute
    // --- DML, so a satisfied filter falls through to NotSupportedException, which is what proves the
    // --- validation ran (and passed) before the executor was requested.

    [Fact]
    public void QueryFilterException_ShouldDeriveFromDataContextException()
    {
        var exception = new QueryFilterException("filter violated");

        exception.Should().BeAssignableTo<DataContextException>();
        exception.Message.Should().Be("filter violated");
    }

    [Fact]
    public void Insert_Entity_SatisfiesFilter_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Values(new InsertValidatedEntity { Id = 1, TenantId = 1 }).Insert();

        act.Should().Throw<NotSupportedException>("the row passes validation, so the read-only context is reached");
    }

    [Fact]
    public void Insert_Entity_ViolatesFilter_ShouldThrowBeforeExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Values(new InsertValidatedEntity { Id = 1, TenantId = 2 }).Insert();

        act.Should().Throw<QueryFilterException>().WithMessage("*tenant*");
    }

    [Fact]
    public void Insert_Entity_IgnoreFilters_ShouldSkipValidation()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .IgnoreFilters()
            .Values(new InsertValidatedEntity { Id = 1, TenantId = 2 })
            .Insert();

        act.Should().Throw<NotSupportedException>("IgnoreFilters disables validation too");
    }

    [Fact]
    public void Insert_Entity_IgnoreByKey_ShouldSkipOnlyNamedFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        // Two active filters: the keyed tenant filter and the anonymous soft-delete filter. Ignoring only
        // "tenant" must leave the anonymous filter active, so the soft-deleted row is still rejected.
        var act = () => ctx.InsertInto<InsertMultiFilterEntity>(ConfigureInsertMultiFilter)
            .IgnoreFilters(["tenant"])
            .Values(new InsertMultiFilterEntity { Id = 1, TenantId = 2, IsDeleted = true })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*anonymous*", "the named tenant filter was skipped but the anonymous one still applies");
    }

    [Fact]
    public void Insert_Entity_IgnoreByKey_SatisfiedOtherFilter_ShouldSkipNamedFilter()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertMultiFilterEntity>(ConfigureInsertMultiFilter)
            .IgnoreFilters(["tenant"])
            .Values(new InsertMultiFilterEntity { Id = 1, TenantId = 2, IsDeleted = false })
            .Insert();

        act.Should().Throw<NotSupportedException>(
            "the tenant filter is skipped and the anonymous soft-delete filter is satisfied");
    }

    [Fact]
    public void Insert_ColumnValue_ViolatesFilter_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Value(x => x.TenantId, 2).Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void Insert_ColumnValue_UnwrittenFilterColumn_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Value(x => x.Id, 5).Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*tenant*")
            .WithMessage(
                "*TenantId*",
                "an omitted column read by an active filter is rejected fail-closed");
    }

    [Fact]
    public void Insert_ColumnValue_AnonymousOmittedColumn_ShouldNameAnonymousFilter()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<InsertSoftDeleteEntity>(b => b.HasQueryFilter(e => !e.IsDeleted))
            .Value(x => x.Id, 1)
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage(
                "*the anonymous filter*",
                "an anonymous key reads as 'the anonymous filter' rather than a blank key");
    }

    [Fact]
    public void Insert_ColumnValue_WrittenNullForValueTypeMember_ShouldThrowQueryFilterException()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Value(x => (int?)x.TenantId, (int?)null)
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage(
                "*TenantId*",
                "a written NULL for a non-nullable value-type column is a controlled rejection, not a NullReferenceException");
    }

    [Fact]
    public void Insert_ColumnValue_FilterReadsField_ShouldThrowFailClosed()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<InsertFieldFilterEntity>(b => b.HasQueryFilter(e => !e.Deleted))
            .Value(x => x.Id, 1)
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*field or calls an instance member*")
            .WithMessage("*fail-closed*", "the filter cannot be evaluated, so the write is rejected");
    }

    [Fact]
    public void Insert_ColumnValue_FilterCallsInstanceMethod_ShouldThrowFailClosed()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<InsertMethodFilterEntity>(b => b.HasQueryFilter(e => e.IsActive()))
            .Value(x => x.Id, 1)
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*field or calls an instance member*")
            .WithMessage("*fail-closed*", "the filter cannot be evaluated, so the write is rejected");
    }

    [Fact]
    public void Insert_ColumnValue_WrittenFilterColumn_SatisfyingFilter_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Value(x => x.Id, 5)
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("the written tenant column satisfies the filter, so execution is reached");
    }

    [Fact]
    public void Insert_ColumnValue_FilterPropertyDeclaredOnInterface_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        // The filter lambda reads TenantId through the interface while the written column is the
        // concrete property. A value that satisfies the filter must not be rejected fail-closed just
        // because the written-property index was keyed by the interface PropertyInfo instance.
        var act = () => ctx.InsertInto<InterfaceFilteredEntity>()
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>(
            "the written concrete column satisfies the interface-declared filter, so execution is reached");
    }

    [Fact]
    public void InsertBuilder_BaseDeclaredMember_SelectorResolvesMappedColumn()
    {
        using var ctx = new InMemoryDataContext();

        // Id/TenantId are declared on the base type but the mapped metadata holds them reflected on the
        // derived type; a selector typed as the derived entity yields the base declaration, so a
        // reference-equality lookup misses the mapped column.
        var act = () => ctx.InsertInto<DerivedBaseDeclaredIdentityEntity>()
            .Value(x => x.Id, 1)
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("every selected base-declared member resolves to its mapped column");
    }

    [Fact]
    public void InsertBuilder_NewHiddenMember_SelectorResolvesDerivedColumn()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<HiddenIdentityDerivedEntity>()
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("the new-hidden member resolves to the derived mapped column");
    }

    [Fact]
    public void Insert_ColumnValue_BaseDeclaredFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<DerivedBaseDeclaredFilteredEntity>(ConfigureBaseDeclaredFiltered)
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("the written base-declared column satisfies the filter, so execution is reached");
    }

    [Fact]
    public void Insert_ColumnValue_BaseDeclaredFilterMember_ViolatingValue_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<DerivedBaseDeclaredFilteredEntity>(ConfigureBaseDeclaredFiltered)
            .Value(x => x.TenantId, 2)
            .Insert();

        act.Should().Throw<QueryFilterException>("the written base-declared column violates the filter");
    }

    [Fact]
    public void Insert_EntityRow_BaseDeclaredFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<DerivedBaseDeclaredFilteredEntity>(ConfigureBaseDeclaredFiltered)
            .Values(new DerivedBaseDeclaredFilteredEntity { Id = 1, TenantId = 1 })
            .Insert();

        act.Should().Throw<NotSupportedException>("the entity-row path reads the same base-declared column");
    }

    [Fact]
    public void Insert_ColumnValue_HiddenFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<HiddenFilteredDerivedEntity>()
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("the hidden derived column satisfies the inherited base filter");
    }

    [Fact]
    public void Insert_ColumnValue_HiddenFilterMember_ViolatingValue_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<HiddenFilteredDerivedEntity>()
            .Value(x => x.TenantId, 2)
            .Insert();

        act.Should().Throw<QueryFilterException>("the hidden derived column violates the inherited base filter");
    }

    [Fact]
    public void Insert_EntityRow_HiddenFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        // The inherited filter reads the base declaration while the object initializer writes the
        // hiding derived property; the entity-row path must resolve the read to the mapped column
        // rather than the base backing field, or a satisfying row is falsely rejected.
        var act = () => ctx.InsertInto<HiddenFilteredDerivedEntity>()
            .Values(new HiddenFilteredDerivedEntity { Id = 1, TenantId = 1 })
            .Insert();

        act.Should().Throw<NotSupportedException>("the entity-row path reads the hidden mapped column");
    }

    [Fact]
    public void Insert_EntityRow_HiddenFilterMember_ViolatingValue_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<HiddenFilteredDerivedEntity>()
            .Values(new HiddenFilteredDerivedEntity { Id = 1, TenantId = 2 })
            .Insert();

        act.Should().Throw<QueryFilterException>("the entity-row path reads the hidden mapped column");
    }

    [Fact]
    public void Insert_EntityRow_InterfaceFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<InterfaceFilteredEntity>()
            .Values(new InterfaceFilteredEntity { Id = 1, TenantId = 1 })
            .Insert();

        act.Should().Throw<NotSupportedException>("the entity-row path resolves the interface member to its implementation");
    }

    [Fact]
    public void Insert_ColumnValue_ExplicitInterfaceFilterMember_SatisfyingValue_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<ExplicitInterfaceFilteredEntity>()
            .Value(x => x.TenantId, 1)
            .Insert();

        act.Should().Throw<NotSupportedException>("the explicit-interface-declared filter resolves to the mapped column");
    }

    [Fact]
    public void Insert_ColumnValue_ExplicitInterfaceFilterMember_ViolatingValue_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<ExplicitInterfaceFilteredEntity>()
            .Value(x => x.TenantId, 2)
            .Insert();

        act.Should().Throw<QueryFilterException>("the explicit-interface-declared filter reads the mapped column");
    }

    [Fact]
    public void Insert_ColumnValue_UnwrittenFilterColumn_IgnoreFilters_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .IgnoreFilters()
            .Value(x => x.Id, 5)
            .Insert();

        act.Should().Throw<NotSupportedException>("IgnoreFilters bypasses the fail-closed omitted-column rejection");
    }

    [Fact]
    public void Insert_Batch_OneRowViolates_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Values(
        [
            new InsertValidatedEntity { Id = 1, TenantId = 1 },
            new InsertValidatedEntity { Id = 2, TenantId = 2 },
        ]).Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void Insert_SoftDeleteFilter_Violates_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.InsertInto<InsertSoftDeleteEntity>(b => b.HasQueryFilter(e => !e.IsDeleted))
            .Values(new InsertSoftDeleteEntity { Id = 1, IsDeleted = true })
            .Insert();

        act.Should().Throw<QueryFilterException>();
    }

    [Fact]
    public void InsertSelect_SourceViolatesSingleFilter_ShouldNameTheKey()
    {
        using var ctx = InsertSelectContext(
            new InsertSelectSourceEntity { Id = 1, TenantId = 1 },
            new InsertSelectSourceEntity { Id = 2, TenantId = 2 });

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new { s.Id, s.TenantId })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*", "a single contributing filter is named in the message");
    }

    [Fact]
    public void InsertSelect_BaseDeclaredFilterMember_SourceViolates_ShouldNameTheKey()
    {
        using var ctx = InsertSelectContext(new InsertSelectSourceEntity { Id = 1, TenantId = 2 });

        // The target filter reads a member declared on the target's base type; the source projection
        // must match it to the mapped column by canonical identity rather than by name, or the
        // pre-check would fail closed with an omitted-column error instead of naming the filter.
        var act = () => ctx.InsertInto<DerivedBaseDeclaredFilteredEntity>(ConfigureBaseDeclaredFiltered)
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new { s.Id, s.TenantId })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*the 'tenant' filter*", "the base-declared target column is resolved on the source projection");
    }

    [Fact]
    public void InsertSelect_SourceViolatesMultipleFilters_ShouldUseGenericMessage()
    {
        using var ctx = InsertSelectContext(new InsertSelectSourceEntity { Id = 1, TenantId = 2, IsDeleted = false });

        var act = () => ctx.InsertInto<InsertMultiFilterEntity>(ConfigureInsertMultiFilter)
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new { s.Id, s.TenantId, s.IsDeleted })
            .Insert();

        var exception = act.Should().Throw<QueryFilterException>().Which;
        exception.Message.Should().Contain("an active filter", "several contributing filters are not attributable to one key");
        exception.Message.Should().NotContain("'tenant'");
    }

    [Fact]
    public void InsertSelect_SourceSatisfiesTargetFilter_ShouldReachExecution()
    {
        using var ctx = InsertSelectContext(new InsertSelectSourceEntity { Id = 1, TenantId = 1 });

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new { s.Id, s.TenantId })
            .Insert();

        act.Should().Throw<NotSupportedException>("the source pre-check passes, so the read-only context is reached");
    }

    [Fact]
    public void InsertSelect_ProjectionOmitsFilterColumn_ShouldThrowFailClosed()
    {
        using var ctx = InsertSelectContext(new InsertSelectSourceEntity { Id = 1, TenantId = 1 });

        var act = () => ctx.InsertInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new InsertValidatedEntity { Id = s.Id })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*TenantId*", "the projection does not expose the column the target filter reads");
    }

    [Fact]
    public void InsertSelect_TargetFilterReadsField_ShouldThrowFailClosed()
    {
        using var ctx = InsertSelectContext(new InsertSelectSourceEntity { Id = 1, TenantId = 1 });

        var act = () => ctx.InsertInto<InsertFieldFilterEntity>(b => b.HasQueryFilter(e => !e.Deleted))
            .Values(
                ctx.From<InsertSelectSourceEntity>(),
                s => new { s.Id })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage(
                "*field or calls an instance member*",
                "the source pre-check cannot rewrite a field read, so the write is rejected fail-closed");
    }

    [Fact]
    public void BulkInsert_ViolatesFilter_ShouldThrowBeforeExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.BulkInsertInto<InsertValidatedEntity>(ConfigureInsertValidated).Values(
        [
            new InsertValidatedEntity { Id = 1, TenantId = 1 },
            new InsertValidatedEntity { Id = 2, TenantId = 2 },
        ]).BulkInsert();

        act.Should().Throw<QueryFilterException>("the synchronous source is validated in full before any batch is sent");
    }

    [Fact]
    public void Merge_ViolatesFilter_ShouldThrowBeforeExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.MergeInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .Using(new InsertValidatedEntity { Id = 1, TenantId = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<QueryFilterException>("the merge's insert branch is validated");
    }

    [Fact]
    public void Merge_IgnoreFilters_ShouldSkipValidation()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        var rows = new List<InsertValidatedEntity>();
        ctx.Data[typeof(InsertValidatedEntity)] = rows;

        var affected = ctx.MergeInto<InsertValidatedEntity>(ConfigureInsertValidated)
            .IgnoreFilters()
            .Using(new InsertValidatedEntity { Id = 1, TenantId = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        rows.Should().ContainSingle();
    }

    // --- PR4: the builder-function (FilterFunc) form. The function is invoked once at plan build with
    // --- the live IDataContext; only the Where predicate it appends is merged, and context reads stay
    // --- bound parameters so the plan key does not change with the value. A function that returns null,
    // --- changes state other than Where, or snapshots a runtime value is rejected with
    // --- NotSupportedException, and a write target with a function filter is rejected fail-closed.

    public sealed class FuncFilterTenantEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFunc))]
    public sealed class FuncAttributeFilteredEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        public static Func<EntityBuilder<FuncAttributeFilteredEntity>, IDataContext, EntityBuilder<FuncAttributeFilteredEntity>> TenantFunc
            => (b, c) => b.Where(e => e.TenantId == (int)c.Properties[TenantKey]);
    }

    public sealed class FuncKeyedEntity
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class FuncMetadataEntity
    {
        public int Id { get; set; }
    }

    public sealed class FuncNullEntity
    {
        public int Id { get; set; }
    }

    public sealed class FuncMutateEntity
    {
        public int Id { get; set; }
    }

    public sealed class FuncCaptureEntity
    {
        public int Id { get; set; }
    }

    public sealed class FuncParameterEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class FuncWriteTargetEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    public sealed class FuncInsertSourceEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private static void ConfigureFuncTenant(EntityMetadataBuilder<FuncFilterTenantEntity> b)
        => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[TenantKey]));

    private static void ConfigureFuncWriteTarget(EntityMetadataBuilder<FuncWriteTargetEntity> b)
        => b.HasQueryFilter("tenant", (eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[TenantKey]));

    private static void ConfigureFuncInsertSource(EntityMetadataBuilder<FuncInsertSourceEntity> b)
        => b.HasQueryFilter("tenant", (eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[TenantKey]));

    [Fact]
    public void FuncFilter_ContextValue_ShouldFilterRows()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FuncFilterTenantEntity>(ConfigureFuncTenant).WithData(
        [
            new FuncFilterTenantEntity { Id = 1, TenantId = 1 },
            new FuncFilterTenantEntity { Id = 2, TenantId = 2 },
        ]);

        var rows = ctx.From<FuncFilterTenantEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    [Fact]
    public void FuncFilter_AttributeDeclared_ShouldFilterRows()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FuncAttributeFilteredEntity>().WithData(
        [
            new FuncAttributeFilteredEntity { Id = 1, TenantId = 1 },
            new FuncAttributeFilteredEntity { Id = 2, TenantId = 2 },
        ]);

        var rows = ctx.From<FuncAttributeFilteredEntity>().Select(x => x.Id).ToList();

        rows.Should().Equal(1);
    }

    private static void ConfigureFuncKeyed(InMemoryDataContext ctx)
        => ctx.From<FuncKeyedEntity>(b => b
                .HasQueryFilter((eb, _) => eb.Where(e => e.Id < 100))
                .HasQueryFilter("soft", (eb, _) => eb.Where(e => !e.IsDeleted)))
            .WithData(
            [
                new FuncKeyedEntity { Id = 1 },
                new FuncKeyedEntity { Id = 2, IsDeleted = true },
                new FuncKeyedEntity { Id = 600 },
            ]);

    [Fact]
    public void FuncFilter_Keyed_SelectiveIgnore_ShouldDisableOnlyNamedFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureFuncKeyed(ctx);

        var withoutSoft = ctx.From<FuncKeyedEntity>().IgnoreFilters(["soft"]).Select(x => x.Id).ToList();
        var withoutAnonymous = ctx.From<FuncKeyedEntity>().IgnoreFilters([QueryFilters.AnonymousKey]).Select(x => x.Id).ToList();

        withoutSoft.Should().BeEquivalentTo([1, 2], "only the named soft-delete filter is disabled");
        withoutAnonymous.Should().BeEquivalentTo([1, 600], "only the anonymous Id filter is disabled");
    }

    [Fact]
    public void FuncFilter_Keyed_IgnoreAll_ShouldDisableEveryFilter()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureFuncKeyed(ctx);

        var rows = ctx.From<FuncKeyedEntity>().IgnoreFilters().Select(x => x.Id).ToList();

        rows.Should().BeEquivalentTo([1, 2, 600]);
    }

    [Fact]
    public void FuncFilter_Keyed_SelectiveIgnore_ShouldAccumulate()
    {
        using var ctx = new InMemoryDataContext();
        ConfigureFuncKeyed(ctx);

        var rows = ctx.From<FuncKeyedEntity>()
            .IgnoreFilters(["soft"])
            .IgnoreFilters([QueryFilters.AnonymousKey])
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([1, 2, 600], "the two selective scopes accumulate");
    }

    [Fact]
    public void FuncFilter_Metadata_ShouldExposeFuncAndNullLambda()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncMetadataEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => e.Id > 0)));

        var filters = DataContextCache.Metadata[typeof(FuncMetadataEntity)].Filters;

        filters.Should().ContainSingle();
        filters[0].Lambda.Should().BeNull("a builder-function filter has no predicate lambda");
        filters[0].Func.Should().NotBeNull("the function declaration is the metadata");
    }

    [Fact]
    public void FuncFilter_ReturningNull_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncNullEntity>(b => b.HasQueryFilter(
            (Func<EntityBuilder<FuncNullEntity>, IDataContext, EntityBuilder<FuncNullEntity>>)((_, _) => null!)));

        var act = () => ctx.From<FuncNullEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*returned a null builder*");
    }

    [Fact]
    public void FuncFilter_ChangingNonWhereState_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncMutateEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => e.Id > 0).Distinct()));

        var act = () => ctx.From<FuncMutateEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*must only add a Where predicate*");
    }

    [Fact]
    public void FuncFilter_CapturingLocal_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        var threshold = 5;
        ctx.From<FuncCaptureEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => e.Id > threshold)));

        var act = () => ctx.From<FuncCaptureEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*captures the runtime value 'threshold'*");
    }

    [Fact]
    public void FuncFilter_ParameterPlaceholder_ShouldFilterInMemory()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncParameterEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => e.TenantId == SqlFunctions.Parameter<int>(0))))
            .WithData(
            [
                new FuncParameterEntity { Id = 1, TenantId = 1 },
                new FuncParameterEntity { Id = 2, TenantId = 2 },
            ]);

        var rows = ctx.From<FuncParameterEntity>().Select(x => x.Id).ToList(1);

        rows.Should().Equal(1);
    }

    [Fact]
    public void Insert_TargetFuncFilter_ShouldThrowQueryFilterFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<FuncWriteTargetEntity>(ConfigureFuncWriteTarget)
            .Values(new FuncWriteTargetEntity { Id = 1, TenantId = 1 })
            .Insert();

        act.Should().Throw<QueryFilterException>()
            .WithMessage("*FilterFunc*", "a builder-function target filter cannot be validated (fail-closed)");
    }

    [Fact]
    public void Insert_TargetFuncFilter_IgnoreFilters_ShouldReachExecution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.InsertInto<FuncWriteTargetEntity>(ConfigureFuncWriteTarget)
            .IgnoreFilters()
            .Values(new FuncWriteTargetEntity { Id = 1, TenantId = 1 })
            .Insert();

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().NotContain("FilterFunc", "IgnoreFilters bypasses the fail-closed write validation");
    }

    [Fact]
    public void Merge_TargetFuncFilter_ShouldThrowQueryFilterFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;

        var act = () => ctx.MergeInto<FuncWriteTargetEntity>(ConfigureFuncWriteTarget)
            .Using(new FuncWriteTargetEntity { Id = 1, TenantId = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<QueryFilterException>().WithMessage("*FilterFunc*");
    }

    [Fact]
    public void Update_KeyForm_FuncFilter_ShouldCarryTargetPredicate()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FuncWriteTargetEntity>(ConfigureFuncWriteTarget);

        var command = ctx.Update<FuncWriteTargetEntity>().BuildEntityCommand(new FuncWriteTargetEntity { Id = 2 });

        command.Source.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Source.PreparedCondition.Should().NotBeNull("the key-form update carries the builder-function filter");
        command.Source.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void Delete_KeyForm_FuncFilter_ShouldCarryTargetPredicate()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FuncWriteTargetEntity>(ConfigureFuncWriteTarget);

        var command = ctx.DeleteFrom<FuncWriteTargetEntity>().BuildKeyCommand(new FuncWriteTargetEntity { Id = 2 });

        command.Condition.Should().NotBeNull();
        command.Condition!.PrepareCommand(false, TestContext.Current.CancellationToken);
        command.Condition.PreparedCondition.Should().NotBeNull("the key-form delete carries the builder-function filter");
        command.Condition.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    [Fact]
    public void InsertSelect_SourceFuncFilter_ShouldFilterTheSource()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Properties[TenantKey] = 1;
        ctx.From<FuncInsertSourceEntity>(ConfigureFuncInsertSource).WithData(
        [
            new FuncInsertSourceEntity { Id = 1, TenantId = 1 },
            new FuncInsertSourceEntity { Id = 2, TenantId = 2 },
        ]);

        var source = ctx.From<FuncInsertSourceEntity>().ToCommand();
        source.PrepareCommand(false, TestContext.Current.CancellationToken);

        source.PreparedCondition.Should().NotBeNull("the INSERT ... SELECT source carries its builder-function filter");
        source.PreparedCondition!.ToString().Should().Contain("TenantId");
    }

    // --- D6 review fixes on the builder-function form: the predicate is bound to the IDataContext the
    // --- function receives (a captured foreign context is rejected), a non-Where mutation applied in
    // --- place before a Where clone is detected, a metadata implementation declaring both forms is
    // --- rejected, and a captured collection stays rejected.

    public sealed class FuncTwoContextEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private static void ConfigureFuncTwoContext(EntityMetadataBuilder<FuncTwoContextEntity> b)
        => b.HasQueryFilter((eb, c) => eb.Where(e => e.TenantId == (int)c.Properties[TenantKey]));

    [Fact]
    public void FuncFilter_TwoContexts_DifferentTenant_ShouldReadPassedContextAndSharePlan()
    {
        using var ctx1 = new InMemoryDataContext();
        ctx1.Properties[TenantKey] = 1;
        ctx1.From<FuncTwoContextEntity>(ConfigureFuncTwoContext).WithData(
        [
            new FuncTwoContextEntity { Id = 10, TenantId = 1 },
            new FuncTwoContextEntity { Id = 20, TenantId = 2 },
        ]);

        using var ctx2 = new InMemoryDataContext();
        ctx2.Properties[TenantKey] = 2;
        ctx2.From<FuncTwoContextEntity>(ConfigureFuncTwoContext).WithData(
        [
            new FuncTwoContextEntity { Id = 10, TenantId = 1 },
            new FuncTwoContextEntity { Id = 20, TenantId = 2 },
        ]);

        var cmd1 = ctx1.From<FuncTwoContextEntity>().Select(x => x.Id);
        var cmd2 = ctx2.From<FuncTwoContextEntity>().Select(x => x.Id);
        cmd1.PrepareCommand(false, TestContext.Current.CancellationToken);
        cmd2.PrepareCommand(false, TestContext.Current.CancellationToken);

        ctx1.From<FuncTwoContextEntity>().Select(x => x.Id).ToList().Should().Equal(10);
        ctx2.From<FuncTwoContextEntity>().Select(x => x.Id).ToList().Should().Equal(20);

        cmd1.GetOrCreatePlanKey(null).Equals(cmd2.GetOrCreatePlanKey(null))
            .Should().BeTrue("the function reads the passed context, whose value stays out of the plan key");
    }

    public sealed class FuncForeignContextEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Fact]
    public void FuncFilter_CapturingForeignContext_ShouldThrowNotSupported()
    {
        using var executing = new InMemoryDataContext();
        executing.Properties[TenantKey] = 1;
        using var foreign = new InMemoryDataContext();
        foreign.Properties[TenantKey] = 2;

        executing.From<FuncForeignContextEntity>(b => b.HasQueryFilter(
            (eb, _) => eb.Where(e => e.TenantId == (int)foreign.Properties[TenantKey])));

        var act = () => executing.From<FuncForeignContextEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*different IDataContext*", "the closure captured a context other than the one passed to the function");
    }

    public sealed class FuncInPlaceMutationEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void FuncFilter_MutatingNonWhereStateInPlace_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncInPlaceMutationEntity>(b => b.HasQueryFilter((eb, _) =>
        {
            // The mutation is applied to the input builder, then Where clones it, so comparing the
            // returned clone against the (already mutated) input would miss it: the snapshot taken before
            // the call is what detects it.
            eb.Paging = new Paging { Limit = 5 };
            return eb.Where(e => e.Id > 0);
        }));

        var act = () => ctx.From<FuncInPlaceMutationEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*must only add a Where predicate*", "an in-place non-Where mutation must be detected");
    }

    public sealed class FuncCollectionCaptureEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void FuncFilter_CapturingCollection_ShouldThrowNotSupported()
    {
        using var ctx = new InMemoryDataContext();
        var ids = new List<int> { 1, 2 };
        ctx.From<FuncCollectionCaptureEntity>(b => b.HasQueryFilter((eb, _) => eb.Where(e => ids.Contains(e.Id))));

        var act = () => ctx.From<FuncCollectionCaptureEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*captures the runtime value 'ids'*", "a captured collection is a runtime value evaluated at plan build");
    }

    public sealed class FuncBothFormsEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void FuncFilter_MetadataDeclaringBothLambdaAndFunc_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<FuncBothFormsEntity>(b => b.HasQueryFilter(e => e.Id > 0));
        var inner = DataContextCache.Metadata[typeof(FuncBothFormsEntity)];
        var lambda = inner.Filters[0].Lambda!;
        Func<EntityBuilder<FuncBothFormsEntity>, IDataContext, EntityBuilder<FuncBothFormsEntity>> func
            = (eb, _) => eb.Where(e => e.Id < 100);

        DataContextCache.Metadata[typeof(FuncBothFormsEntity)] = new EntityMetadata(
            inner.TableName,
            inner.Properties,
            filters: [new QueryFilterMetadata("both", lambda, func)]);

        var act = () => ctx.From<FuncBothFormsEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*both a predicate Lambda and a builder-function Func*", "the two forms are mutually exclusive");
    }
}
