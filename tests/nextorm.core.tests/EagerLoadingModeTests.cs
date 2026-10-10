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
    public void EmptyParentResult_SplitMode_DoesNotIssueAChildCommand()
    {
        using var context = new InMemoryDataContext();
        context.From<ModeChild>().WithData(Array.Empty<ModeChild>());
        context.From<ModeParent>().WithData(Array.Empty<ModeParent>());

        var childCommands = 0;
        var parents = context.From<ModeParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childCommands++;
                    return c.From<ModeChild>();
                },
                p => p.Id,
                c => c.ParentId)
            .ToList();

        parents.Should().BeEmpty();
        childCommands.Should().Be(0, "an empty parent result must not force a second (child) command in split mode");
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
}
