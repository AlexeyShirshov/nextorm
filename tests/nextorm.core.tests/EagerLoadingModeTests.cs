using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// State-machine and API-surface coverage for <see cref="EagerLoadMode"/>: the mode belongs to the whole
/// builder (an explicit choice applies to collections declared earlier too), <c>Default</c> inherits,
/// repeating the same mode is allowed, conflicting explicit modes are rejected, an unknown enum value is
/// rejected, and the old standalone <c>AsSingleQuery</c> API is gone.
/// </summary>
public class EagerLoadingModeTests
{
    public sealed class ModeParent
    {
        public int Id { get; set; }
        public ICollection<ModeChild> Children { get; set; } = new List<ModeChild>();
        public ICollection<ModeNote> Notes { get; set; } = new List<ModeNote>();
    }

    public sealed class ModeChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class ModeNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        // Declare the relationships once, before any data seeding, so JoinInto can resolve the
        // collection navigation from metadata (LoadWith uses its explicit key selectors regardless).
        context.From<ModeParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
        context.From<ModeChild>().WithData(new[]
        {
            new ModeChild { Id = 10, ParentId = 1 },
            new ModeChild { Id = 11, ParentId = 1 },
            new ModeChild { Id = 12, ParentId = 2 },
        });
        context.From<ModeNote>().WithData(new[]
        {
            new ModeNote { Id = 100, ParentId = 1 },
            new ModeNote { Id = 101, ParentId = 2 },
        });
        context.From<ModeParent>().WithData(new[]
        {
            new ModeParent { Id = 1 },
            new ModeParent { Id = 2 },
        });
        return context;
    }

    [Fact]
    public void Default_ThenSingleQuery_AppliesToTheEarlierCollection()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        builder.SingleQuery.Should().BeTrue("the explicit mode applies to the whole builder");

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[1].Notes.Select(n => n.Id).Should().Equal(101);
    }

    [Fact]
    public void SingleQuery_ThenDefault_InheritsTheChoice()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId);

        builder.SingleQuery.Should().BeTrue("Default must inherit the already chosen mode");

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
    }

    [Fact]
    public void ExplicitDefault_WithNoChoice_KeepsSplit()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Default);

        builder.SingleQuery.Should().BeFalse("Default without an earlier choice keeps split loading");
    }

    [Theory]
    [InlineData(EagerLoadMode.SplitQuery)]
    [InlineData(EagerLoadMode.SingleQuery)]
    public void SameExplicitMode_MayBeRepeated(EagerLoadMode mode)
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, mode)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, mode);

        builder.SingleQuery.Should().Be(mode == EagerLoadMode.SingleQuery);
        builder.LoadSpecs.Should().HaveCount(2);
    }

    [Fact]
    public void Omitted_ExplicitDefault_And_SplitQuery_ProduceIdenticalResultsAndCommandCounts()
    {
        static (string Result, int ChildCommands) Run(int variant)
        {
            using var context = CreateContext();
            var childCommands = 0;
            Func<IDataContext, EntityBuilder<ModeChild>> children = c =>
            {
                childCommands++;
                return c.From<ModeChild>();
            };
            Func<IDataContext, EntityBuilder<ModeNote>> notes = c =>
            {
                childCommands++;
                return c.From<ModeNote>();
            };

            var source = context.From<ModeParent>();
            var builder = variant switch
            {
                0 => source.LoadWith(p => p.Children, children, p => p.Id, c => c.ParentId),
                1 => source.LoadWith(p => p.Children, children, p => p.Id, c => c.ParentId, EagerLoadMode.Default),
                2 => source.LoadWith(p => p.Children, children, p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery),
                3 => source
                        .LoadWith(p => p.Children, children, p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                        .LoadWith(p => p.Notes, notes, p => p.Id, n => n.ParentId, EagerLoadMode.Default),
                _ => throw new ArgumentOutOfRangeException(nameof(variant)),
            };

            builder.SingleQuery.Should().BeFalse("every split variant stays split");
            var parents = builder.OrderBy(p => p.Id).ToList();
            var result = string.Join(
                ";",
                parents.Select(p => p.Id + ":" +
                    string.Join("|", p.Children.Select(c => c.Id)) + "/" +
                    string.Join("|", p.Notes.Select(n => n.Id))));
            return (result, childCommands);
        }

        var omitted = Run(0);
        var explicitDefault = Run(1);
        var explicitSplit = Run(2);
        var inheritedSplit = Run(3);

        omitted.Result.Should().Be("1:10|11/;2:12/");
        explicitDefault.Result.Should().Be(omitted.Result, "the explicit Default must behave exactly like the omitted mode");
        explicitSplit.Result.Should().Be(omitted.Result, "the explicit SplitQuery must behave exactly like the omitted mode");

        omitted.ChildCommands.Should().Be(1, "one non-empty chunk issues exactly one child command");
        explicitDefault.ChildCommands.Should().Be(omitted.ChildCommands);
        explicitSplit.ChildCommands.Should().Be(omitted.ChildCommands);

        // SplitQuery -> LoadWith(Default): the later Default inherits the explicit split mode.
        inheritedSplit.Result.Should().Be("1:10|11/100;2:12/101");
        inheritedSplit.ChildCommands.Should().Be(2, "the second collection inherits split and issues its own child command");
    }

    [Fact]
    public void EmptyParentResult_SplitMode_DoesNotExecuteAChildCommand()
    {
        using var context = new InMemoryDataContext();
        context.From<ModeChild>().WithData(Array.Empty<ModeChild>());
        context.From<ModeParent>().WithData(Array.Empty<ModeParent>());

        var childQueryBuilds = 0;
        var parents = context.From<ModeParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryBuilds++;
                    return c.From<ModeChild>();
                },
                p => p.Id,
                c => c.ParentId)
            .ToList();

        parents.Should().BeEmpty();
        // The child query is built once for the preparation-time per-parent top-N validation, but an
        // empty parent result must not execute a second (child) command.
        childQueryBuilds.Should().Be(1, "the child query is built for pre-SQL validation only, never executed for an empty parent result");
    }

    [Fact]
    public void SingleQueryMode_ThenCloneAndModifier_ThenDefaultInheritsAndExecutes()
    {
        using var context = CreateContext();

        var chosen = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);
        var extended = chosen.Clone()
            .Where(p => p.Id == 1)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Default);

        extended.SingleQuery.Should().BeTrue("the later Default inherits SingleQuery through the copy/modifier");
        extended.LoadSpecs.Should().HaveCount(2);

        var parents = extended.OrderBy(p => p.Id).ToList();
        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
    }

    [Fact]
    public void SplitMode_ThenCloneAndModifier_ThenDefaultInheritsAndExecutes()
    {
        using var context = CreateContext();

        var chosen = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery);
        var extended = chosen.Clone()
            .Where(p => p.Id == 2)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Default);

        extended.SingleQuery.Should().BeFalse("the later Default inherits SplitQuery through the copy/modifier");
        extended.LoadSpecs.Should().HaveCount(2);

        var parents = extended.OrderBy(p => p.Id).ToList();
        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(12);
        parents[0].Notes.Select(n => n.Id).Should().Equal(101);
    }

    [Fact]
    public void SuccessfulBranchUse_LeavesTheOriginalBuilderModeAndLoadSpecsUnchanged()
    {
        using var context = CreateContext();

        var original = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);

        var branch = original.Clone()
            .Where(p => p.Id == 1)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        var parents = branch.OrderBy(p => p.Id).ToList();
        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);

        original.SingleQuery.Should().BeTrue("a successful branch must not change the original builder's mode");
        original.LoadSpecs.Should().HaveCount(1, "the branch's extra LoadWith must not leak into the original builder");
        branch.LoadSpecs.Should().HaveCount(2);
    }

    [Fact]
    public void EagerExecution_DoesNotPoisonThePlanCacheOrStickyCacheFlag()
    {
        using var context = CreateContext();

        EntityBuilder<ModeChild>? childBuilder = null;
        context.From<ModeParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childBuilder = c.From<ModeChild>();
                    return childBuilder;
                },
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList()
            .Should().HaveCount(2);

        context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList()
            .Should().HaveCount(2);

        childBuilder.Should().NotBeNull();
        childBuilder!.ToCommand().Cache.Should().BeTrue(
            "selecting a mode and building the eager joins must not clear the sticky command cache flag");

        var command = context.From<ModeParent>().Select(p => p.Id);
        command.Cache.Should().BeTrue("eager execution must not poison the shared command state");

        var cancellationToken = TestContext.Current.CancellationToken;
        var first = context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, cancellationToken);
        var second = context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, cancellationToken);
        second.Should().BeSameAs(first, "the plan cache must still reuse a prepared command after eager execution");
    }

    [Fact]
    public void EagerExecution_DoesNotPoisonTheContextSharedAnyCommand()
    {
        using var context = CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        // (1) Exercise .Any() first so the context allocates and uses its shared AnyCommand.
        context.From<ModeParent>().Where(p => p.Id == 1).Any().Should().BeTrue();
        context.AnyCommand.Should().NotBeNull("the first Any call must allocate the context-shared command");
        var sharedAnyCommand = context.AnyCommand!.Value;

        // (2) Run the eager-loading path with non-empty parents AND children, both modes, on the same context.
        var singleQueryParents = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();
        singleQueryParents.Should().HaveCount(2);
        singleQueryParents.SelectMany(p => p.Children).Should().NotBeEmpty();

        var splitQueryParents = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();
        splitQueryParents.Should().HaveCount(2);
        splitQueryParents.SelectMany(p => p.Children).Should().NotBeEmpty();

        // (3) Exercise .Any() again, on the same shared command instance.
        context.From<ModeParent>().Where(p => p.Id == 1).Any().Should().BeTrue();

        // (4) The SAME shared command instance must remain cache-enabled. This is the assertion the
        // fresh-command test above cannot make: if eager loading set Cache = false (the sticky
        // _dontCache field), the context-shared AnyCommand would poison every later query.
        context.AnyCommand!.Value.Should().BeSameAs(sharedAnyCommand);
        sharedAnyCommand.Cache.Should().BeTrue(
            "eager loading must not set Cache = false on the context-shared AnyCommand");

        var first = context.GetPreparedQueryCommand(sharedAnyCommand, createEnumerator: false, storeInCache: true, cancellationToken);
        var second = context.GetPreparedQueryCommand(sharedAnyCommand, createEnumerator: false, storeInCache: true, cancellationToken);
        second.Should().BeSameAs(first, "the shared AnyCommand's plan must still be reused from the plan cache");
    }

    [Fact]
    public void SingleQuery_ThenSplitQuery_IsRejectedWithoutMutatingTheSource()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);

        Action act = () => builder.LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SplitQuery);

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Be(
            "EagerLoadMode.SingleQuery is already active on this builder and EagerLoadMode.SplitQuery " +
            "conflicts with it: the eager-load mode applies to the whole builder, so all LoadWith calls " +
            "must use the same explicit mode.");
        builder.SingleQuery.Should().BeTrue("a rejected LoadWith must not change the source builder");
        builder.LoadSpecs.Should().HaveCount(1);
    }

    [Fact]
    public void SplitQuery_ThenSingleQuery_IsRejectedWithoutMutatingTheSource()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery);

        Action act = () => builder.LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Be(
            "EagerLoadMode.SplitQuery is already active on this builder and EagerLoadMode.SingleQuery " +
            "conflicts with it: the eager-load mode applies to the whole builder, so all LoadWith calls " +
            "must use the same explicit mode.");
        builder.SingleQuery.Should().BeFalse();
        builder.LoadSpecs.Should().HaveCount(1);
    }

    [Fact]
    public void UnknownModeValue_IsRejectedWithArgumentOutOfRangeWithoutChangingTheBuilder()
    {
        using var context = CreateContext();
        var builder = context.From<ModeParent>();

        Action act = () => builder.LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, (EagerLoadMode)999);

        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("mode");
        builder.SingleQuery.Should().BeFalse();
        builder.LoadSpecs.Should().BeNull("the rejected call must not add a load specification");
    }

    [Fact]
    public void EagerLoadMode_ApiSurface_HasTheExpectedShape()
    {
        ((int)EagerLoadMode.Default).Should().Be(0);
        ((int)EagerLoadMode.SplitQuery).Should().Be(1);
        ((int)EagerLoadMode.SingleQuery).Should().Be(2);

        typeof(EntityBuilder<>).GetMethods().Should().NotContain(m => m.Name == "AsSingleQuery");
        typeof(JoinedEntityBuilder<,>).GetMethods().Should().NotContain(m => m.Name == "AsSingleQuery");

        var loadWith = typeof(EntityBuilder<>).GetMethods().Single(m => m.Name == "LoadWith");
        var parameters = loadWith.GetParameters();
        parameters.Should().HaveCount(5);
        parameters[^1].ParameterType.Should().Be(typeof(EagerLoadMode));
        parameters[^1].IsOptional.Should().BeTrue("mode is an optional parameter");
        parameters[^1].HasDefaultValue.Should().BeTrue();
        parameters[^1].DefaultValue.Should().Be(EagerLoadMode.Default);

        var inherited = typeof(JoinedEntityBuilder<,>).GetMethod("LoadWith");
        inherited.Should().NotBeNull("derived builders inherit LoadWith");
        inherited!.DeclaringType.Should().NotBeNull();
        inherited.DeclaringType!.IsGenericType.Should().BeTrue();
        inherited.DeclaringType!.GetGenericTypeDefinition().Should().Be(
            typeof(EntityBuilder<>),
            "JoinedEntityBuilder must inherit the EntityBuilder LoadWith instead of adding its own overload");
        var inheritedParameters = inherited.GetParameters();
        inheritedParameters.Should().HaveCount(5);
        inheritedParameters[^1].ParameterType.Should().Be(typeof(EagerLoadMode));
        inheritedParameters[^1].IsOptional.Should().BeTrue("the inherited mode parameter stays optional");
        inheritedParameters[^1].HasDefaultValue.Should().BeTrue();
        inheritedParameters[^1].DefaultValue.Should().Be(EagerLoadMode.Default);
        inherited.ReturnType.IsGenericType.Should().BeTrue();
        inherited.ReturnType.GetGenericTypeDefinition().Should().Be(
            typeof(EntityBuilder<>),
            "the inherited LoadWith returns an EntityBuilder<TEntity> copy");
    }

    // --- D209 (#209): context default, conservative Auto dispatch, fail-closed Correlated/top-N, and
    // --- structural plan identity (V01-V13, V15). Context-wide default wiring is exercised in the SQLite
    // --- suite where a real builder/configurable context is available.

    [Theory]
    [InlineData(EagerLoadMode.SplitQuery)]
    [InlineData(EagerLoadMode.SingleQuery)]
    [InlineData(EagerLoadMode.Correlated)]
    [InlineData(EagerLoadMode.Auto)]
    public void UseEagerLoadMode_AcceptsEveryConcreteStrategy(EagerLoadMode mode)
    {
        var builder = new DataContextBuilder();

        builder.UseEagerLoadMode(mode);

        builder.EagerLoadMode.Should().Be(mode);
    }

    [Fact]
    public void UseEagerLoadMode_RejectsDefaultAndUndefinedValuesWithoutChangingTheDefault()
    {
        var builder = new DataContextBuilder();

        Action @default = () => builder.UseEagerLoadMode(EagerLoadMode.Default);
        @default.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("mode");

        Action undefined = () => builder.UseEagerLoadMode((EagerLoadMode)999);
        undefined.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("mode");

        builder.EagerLoadMode.Should().Be(
            EagerLoadMode.SplitQuery,
            "a rejected builder call must not change the context default");
    }

    [Fact]
    public void InMemoryContext_DefaultEagerLoadMode_IsSplitQuery()
    {
        using var context = new InMemoryDataContext();
        IContextEnvironment environment = context;

        environment.EagerLoadMode.Should().Be(EagerLoadMode.SplitQuery, "the legacy default stays split");    }

    [Fact]
    public void Auto_ResolvesToSplitAndProducesTheSameResult()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Auto);

        builder.SingleQuery.Should().BeFalse("the conservative Auto rule selects the split path");

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
    }

    [Fact]
    public void ExplicitCorrelated_FailsBeforeAnyExecution()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated);

        Action act = () => builder.OrderBy(p => p.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Correlated*");
    }

    [Fact]
    public void PerParentTopN_IsRejectedInsteadOfApplyingAGlobalChildLimit()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>().Limit(1), p => p.Id, c => c.ParentId);

        Action act = () => builder.OrderBy(p => p.Id).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Limit/Offset/Page*");
    }

    private QueryCommand<ModeParent> PlannedCommand(
        InMemoryDataContext context,
        EagerLoadMode mode,
        bool children)
    {
        var builder = children
            ? context.From<ModeParent>().LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, mode)
            : context.From<ModeParent>().LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, mode);
        var cmd = builder.ToCommand();
        cmd.PrepareCommand(TestContext.Current.CancellationToken);
        return cmd;
    }

    [Fact]
    public void AutoAndExplicitSplit_WithTheSameResolvedGraph_ReuseOnePlanKey()
    {
        using var context = CreateContext();

        var auto = PlannedCommand(context, EagerLoadMode.Auto, children: true);
        var split = PlannedCommand(context, EagerLoadMode.SplitQuery, children: true);

        var comparer = auto.GetQueryPlanEqualityComparer();
        comparer.Equals(auto, split).Should().BeTrue("Auto and explicit Split resolve to the same executable shape");
        comparer.GetHashCode(auto).Should().Be(comparer.GetHashCode(split));
    }

    [Fact]
    public void DifferentEagerGraph_OrStrategy_IsolatesPlanKeys()
    {
        using var context = CreateContext();

        var childrenSplit = PlannedCommand(context, EagerLoadMode.SplitQuery, children: true);
        var notesSplit = PlannedCommand(context, EagerLoadMode.SplitQuery, children: false);
        var childrenSingle = PlannedCommand(context, EagerLoadMode.SingleQuery, children: true);

        var comparer = childrenSplit.GetQueryPlanEqualityComparer();
        comparer.Equals(childrenSplit, notesSplit).Should().BeFalse("a different load path/collection must not share a plan");
        comparer.Equals(childrenSplit, childrenSingle).Should().BeFalse("a different resolved strategy must not share a plan");
        comparer.Equals(childrenSplit, PlannedCommand(context, EagerLoadMode.SplitQuery, children: true))
            .Should().BeTrue("a repeated equivalent graph reuses the plan");
    }

    [Fact]
    public void DisabledQueryCache_StillResolvesAndExecutesEagerLoading()
    {
        using var context = CreateContext();
        context.QueryCacheEnabled = false;

        var parents = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Auto)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        context.From<ModeParent>().Select(p => p.Id).Cache.Should().BeTrue(
            "a disabled cache resolves eagerly without mutating the sticky Cache flag");
    }

    // --- D209.5: the single central resolver shared by preparation, execution and plan-key construction
    // --- (V02/V03/V05/V10). Context-default wiring end to end is exercised in the SQLite suite.

    [Theory]
    [InlineData(EagerLoadMode.SplitQuery)]
    [InlineData(EagerLoadMode.SingleQuery)]
    public void Resolver_AbsentOverride_InheritsTheContextDefault(EagerLoadMode contextDefault)
        => EagerLoadResolver.Resolve(null, contextDefault).Should().Be(contextDefault);

    [Fact]
    public void Resolver_ContextAuto_DispatchesToSplit()
        => EagerLoadResolver.Resolve(null, EagerLoadMode.Auto).Should().Be(EagerLoadMode.SplitQuery);

    [Fact]
    public void Resolver_QueryOverride_WinsOverTheContextDefault()
    {
        EagerLoadResolver.Resolve(EagerLoadMode.SplitQuery, EagerLoadMode.SingleQuery).Should().Be(EagerLoadMode.SplitQuery);
        EagerLoadResolver.Resolve(EagerLoadMode.SingleQuery, EagerLoadMode.SplitQuery).Should().Be(EagerLoadMode.SingleQuery);
        EagerLoadResolver.Resolve(EagerLoadMode.Auto, EagerLoadMode.SingleQuery).Should().Be(
            EagerLoadMode.SplitQuery,
            "Auto is dispatched by the conservative E1 rule, independent of the context default");
    }

    [Fact]
    public void Resolver_ExplicitAndContextCorrelated_FailClosed()
    {
        Action requested = () => EagerLoadResolver.Resolve(EagerLoadMode.Correlated, EagerLoadMode.SplitQuery);
        requested.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");

        Action context = () => EagerLoadResolver.Resolve(null, EagerLoadMode.Correlated);
        context.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
    }

    [Fact]
    public void Resolver_UndefinedValue_IsRejectedWithTheOffendingSourceName()
    {
        Action requested = () => EagerLoadResolver.Resolve((EagerLoadMode)999, EagerLoadMode.SplitQuery);
        requested.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("requested",
                "an undefined query override must blame the override, not the context default");

        Action context = () => EagerLoadResolver.Resolve(null, (EagerLoadMode)999);
        context.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("contextDefault",
                "an undefined context default with no override must blame the context default");
    }

    [Fact]
    public void DifferentLoadOrder_ProducesDistinctShapesWhileTheModesStillMerge()
    {
        using var context = CreateContext();

        var childrenFirst = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId)
            .ToCommand();
        var notesFirst = context.From<ModeParent>()
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId)
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId)
            .ToCommand();

        var comparer = childrenFirst.GetQueryPlanEqualityComparer();
        comparer.Equals(childrenFirst, notesFirst).Should().BeFalse("a different eager-load order is a different shape");
        comparer.Equals(childrenFirst, childrenFirst.CloneForCache()).Should().BeTrue("the same graph must round-trip through the cache clone");
        comparer.GetHashCode(childrenFirst).Should().Be(comparer.GetHashCode(childrenFirst.CloneForCache()));
    }

    [Fact]
    public void RejectedCorrelatedPreparation_DoesNotMutateTheStickyCacheFlag()
    {
        using var context = CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var command = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated)
            .ToCommand();
        command.Cache.Should().BeTrue();

        Action act = () => command.PrepareCommand(cancellationToken);
        act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");

        command.Cache.Should().BeTrue("a rejected strategy must not clear the sticky cache flag");
        context.From<ModeParent>().Select(p => p.Id).Cache.Should().BeTrue(
            "a rejected preparation must not poison subsequent scalar queries on the context");
    }

    [Fact]
    public void UnCachedPreparation_KeepsTheStickyCacheFlagEnabled()
    {
        using var context = CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var command = context.From<ModeParent>()
            .LoadWith(p => p.Children, c => c.From<ModeChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Auto)
            .ToCommand();

        var prepared = context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, cancellationToken);

        prepared.Should().NotBeNull();
        command.Cache.Should().BeTrue("per-call uncached preparation must use storeInCache=false, never the sticky flag");
        context.From<ModeParent>().Select(p => p.Id).Cache.Should().BeTrue();
    }

    [Fact]
    public void JoinInto_ResolvedStrategy_IsAlwaysTheFixedSinglePath()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .JoinInto(context.From<ModeChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        builder.ResolvedEagerLoadMode.Should().Be(
            EagerLoadMode.SingleQuery,
            "JoinInto is the fixed Single operation and must not inherit the context default");

        var cmd = builder.CreateJoinIntoPairCommand();
        cmd.EagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
        cmd.PrepareCommand(TestContext.Current.CancellationToken);
        cmd.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
    }

    // --- D209 r=1 n=2 (F14): the mixed JoinInto + LoadWith dispatch. A JoinInto keeps its fixed pair
    // --- command plus split child loads when the LoadWith inherits the context default; only an explicit
    // --- SingleQuery merges the two into one denormalized command.

    [Fact]
    public void JoinInto_WithInheritedLoadWith_KeepsTheBaselinePairAndSplitDispatch()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .JoinInto(context.From<ModeChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId);

        builder.SingleQuery.Should().BeFalse(
            "a JoinInto plus an inherited LoadWith keeps the JoinInto pair command and the split child loads");

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents.Should().HaveCount(2);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[1].Notes.Select(n => n.Id).Should().Equal(101);
    }

    [Fact]
    public void JoinInto_WithExplicitSingleQueryLoadWith_StillMerges()
    {
        using var context = CreateContext();

        var builder = context.From<ModeParent>()
            .JoinInto(context.From<ModeChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .LoadWith(p => p.Notes, c => c.From<ModeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        builder.SingleQuery.Should().BeTrue(
            "an explicit SingleQuery LoadWith still merges the JoinInto and the load into one command");

        var parents = builder.OrderBy(p => p.Id).ToList();
        parents.Should().HaveCount(2);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[1].Notes.Select(n => n.Id).Should().Equal(101);
    }

    // --- D209 r=1 n=2 (F13): direct structural identity for EagerLoadShape plus the V12 plan-cache
    // --- contract — a changed captured parameter value alone reuses the plan, a changed child-filter
    // --- structure (which changes the executed command) isolates it.

    [Fact]
    public void EagerLoadShape_EmptyAndDefault_AreEquivalentAndHashToZero()
    {
        var empty = EagerLoadShape.Empty;
        EagerLoadShape @default = default;

        empty.IsEmpty.Should().BeTrue();
        @default.IsEmpty.Should().BeTrue();
        empty.Equals(@default).Should().BeTrue("the empty shape and default(EagerLoadShape) describe the same graph");
        @default.Equals(empty).Should().BeTrue();
        empty.Equals(empty).Should().BeTrue();
        empty.GetHashCode().Should().Be(0);
        @default.GetHashCode().Should().Be(0);
        empty.GetHashCode().Should().Be(@default.GetHashCode());
    }

    [Fact]
    public void EagerLoadShape_IsOrderSensitive()
    {
        var forward = new EagerLoadShape(["P=P;C=C", "P=P;C=N"]);
        var reordered = new EagerLoadShape(["P=P;C=N", "P=P;C=C"]);

        forward.Equals(reordered).Should().BeFalse("the ordered load paths are part of the identity");
        reordered.Equals(forward).Should().BeFalse();
        forward.IsEmpty.Should().BeFalse();
        reordered.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void EagerLoadShape_EqualParts_AreEqualWithEqualHashes()
    {
        var first = new EagerLoadShape(["P=P;C=C", "P=P;C=N"]);
        var second = new EagerLoadShape(["P=P;C=C", "P=P;C=N"]);

        first.Equals(second).Should().BeTrue();
        second.Equals(first).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode(), "equal shapes must produce equal hashes");
    }

    [Fact]
    public void EagerLoadShape_HashIsDeterministic_AndACollisionNeverImpliesEquality()
    {
        var first = new EagerLoadShape(["A", "B"]);
        var same = new EagerLoadShape(["A", "B"]);
        var other = new EagerLoadShape(["A", "C"]);

        first.GetHashCode().Should().Be(first.GetHashCode(), "the structural hash must be deterministic");
        first.Equals(same).Should().BeTrue();
        first.GetHashCode().Should().Be(same.GetHashCode());

        // A hash collision between different shapes, when it happens, must never be treated as equality.
        first.Equals(other).Should().BeFalse();
        if (first.GetHashCode() == other.GetHashCode())
            first.Equals(other).Should().BeFalse("a hash collision alone is never equality");
    }

    [Fact]
    public void EagerPlan_ChildFilterParameterValueReusesThePlan_ButAStructureChangeIsolatesIt()
    {
        using var context = CreateContext();

        static QueryCommand SingleQueryCommand(InMemoryDataContext context, int excludedId, bool extraFilter)
        {
            var builder = context.From<ModeParent>()
                .LoadWith(
                    p => p.Children,
                    c => extraFilter
                        ? c.From<ModeChild>().Where(x => x.Id != excludedId && x.ParentId != 1)
                        : c.From<ModeChild>().Where(x => x.Id != excludedId),
                    p => p.Id,
                    c => c.ParentId,
                    EagerLoadMode.SingleQuery);
            var (specs, joins) = builder.BuildSingleQueryJoins();
            var cmd = builder.CreatePairCommand(specs, joins);
            cmd.PrepareCommand(TestContext.Current.CancellationToken);
            return cmd;
        }

        var valueOne = SingleQueryCommand(context, excludedId: 10, extraFilter: false);
        var valueTwo = SingleQueryCommand(context, excludedId: 11, extraFilter: false);
        var differentStructure = SingleQueryCommand(context, excludedId: 10, extraFilter: true);

        var comparer = valueOne.GetQueryPlanEqualityComparer();
        comparer.Equals(valueOne, valueTwo).Should().BeTrue(
            "a changed captured parameter value alone does not change the executed command's plan shape");
        comparer.GetHashCode(valueOne).Should().Be(comparer.GetHashCode(valueTwo));
        comparer.Equals(valueOne, differentStructure).Should().BeFalse(
            "a changed child-filter structure changes the executed command and must isolate the plan");
    }
}
