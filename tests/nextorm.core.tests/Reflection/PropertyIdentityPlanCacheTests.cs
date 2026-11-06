using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace nextorm.core.tests.Reflection;

[Collection("Query cache controls")]
public sealed class PropertyIdentityPlanCacheTests
{
    private class BaseMemberEntity
    {
        public int Id { get; set; }
    }

    private sealed class HiddenMemberEntity : BaseMemberEntity
    {
        public new int Id { get; set; }
    }

    private class BaseEntity
    {
        public int InheritedValue { get; set; }
    }

    private sealed class DerivedEntity : BaseEntity
    {
    }

    private interface IEntityMember
    {
        int Id { get; set; }
    }

    private sealed class ClassMemberEntity : IEntityMember
    {
        public int Id { get; set; }
    }

    [Fact]
    public void EquivalentRepeat_OnWarmCache_ReusesPlanAndBindsFreshValue()
    {
        using var ctx = new NextORM.Core.Tests.ParamRefreshRecipeTests.RecipeTestContext();
        ctx.PurgeQueryCache();
        var entity = ctx.From<CacheEntity>();

        var first = Prepare(ctx, WhereCaptured(entity, 1));
        ExpressionPlanEqualityComparer.ResetCounters();
        var second = Prepare(ctx, WhereCaptured(entity, 2));

        ReferenceEquals(first, second).Should().BeTrue("captured values are not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        second.DbCommandParams[0].Value.Should().Be(2, "a warm hit must bind the new closure value");
    }

    [Fact]
    public void InheritedProperty_OnWarmCache_ReusesPlanBindsFreshValueAndResolvesColumn()
    {
        using var ctx = new NextORM.Core.Tests.ParamRefreshRecipeTests.RecipeTestContext();
        ctx.PurgeQueryCache();
        var entity = ctx.From<DerivedEntity>();

        var first = Prepare(ctx, WhereInheritedCaptured(entity, 1));
        ExpressionPlanEqualityComparer.ResetCounters();
        var second = Prepare(ctx, WhereInheritedCaptured(entity, 2));

        ReferenceEquals(first, second).Should().BeTrue("the inherited property's captured value is not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        second.DbCommandParams[0].Value.Should().Be(2, "the warm plan binds the new closure value");
        second.CommandText.Should().Contain("InheritedValue", "the inherited property must resolve to its mapped column");
    }

    [Fact]
    public void HiddenNewMember_DoesNotCollideWithBaseMember()
    {
        using var ctx = new InMemoryDataContext();
        var hidden = ctx.From<HiddenMemberEntity>().Select(x => x.Id);
        var @base = ctx.From<BaseMemberEntity>().Select(x => x.Id);
        var equivalent = ctx.From<HiddenMemberEntity>().Select(x => x.Id);
        hidden.PrepareCommand(CancellationToken.None);
        @base.PrepareCommand(CancellationToken.None);
        equivalent.PrepareCommand(CancellationToken.None);

        hidden.GetQueryPlanEqualityComparer().Equals(hidden, equivalent).Should().BeTrue("identical shapes are equal");
        hidden.GetQueryPlanEqualityComparer().Equals(hidden, @base).Should().BeFalse("a hidden declaration is a distinct member");
    }

    [Fact]
    public void InterfaceMember_DoesNotCollideWithClassMember()
    {
        using var ctx = new InMemoryDataContext();
        var iface = ctx.From<IEntityMember>().Select(x => x.Id);
        var implementation = ctx.From<ClassMemberEntity>().Select(x => x.Id);
        var equivalent = ctx.From<IEntityMember>().Select(x => x.Id);
        iface.PrepareCommand(CancellationToken.None);
        implementation.PrepareCommand(CancellationToken.None);
        equivalent.PrepareCommand(CancellationToken.None);

        iface.GetQueryPlanEqualityComparer().Equals(iface, equivalent).Should().BeTrue("identical shapes are equal");
        iface.GetQueryPlanEqualityComparer().Equals(iface, implementation).Should().BeFalse("interface and class declarations are distinct members");
    }

    private sealed class CacheEntity
    {
        public int Id { get; set; }
    }

    private static QueryCommand<int> WhereCaptured(EntityBuilder<CacheEntity> entity, int value)
        => entity.Where(x => x.Id > value).Select(x => x.Id);

    private static QueryCommand<int> WhereInheritedCaptured(EntityBuilder<DerivedEntity> entity, int value)
        => entity.Where(x => x.InheritedValue > value).Select(x => x.InheritedValue);

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
}
