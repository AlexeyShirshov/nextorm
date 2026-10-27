using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D3 core-contract tests for the five source-less <c>JoinInto</c> counterparts (#330).
/// <para>
/// The source-less forms remove only the <c>EntityBuilder&lt;TChild&gt;</c> argument and resolve the child
/// source through <c>_dataProvider.From&lt;TChild&gt;()</c>. Every form must therefore (a) materialise the
/// same rows as the explicit bare-source twin, (b) observe a configured child mapping, (c) fail at
/// construction with the same exception as <c>From&lt;TChild&gt;()</c> for a genuinely unresolvable child,
/// (d) forward options exactly once with the sentinel escaping unchanged, and, for <c>AC08</c>, (e) reject a
/// null predicate <b>before</b> child resolution or options execution.
/// </para>
/// <para>
/// The unresolvable fixture is a real metadata failure (<see cref="SlUnresolvableChild"/> declares two
/// <c>[DynamicColumns]</c> stores, which <c>EntityMetadataBuilder&lt;T&gt;.Build</c> rejects), not a mock.
/// </para>
/// </summary>
[Collection("Query cache controls")] // reads and configures the process-wide metadata cache
public class JoinIntoSourceLessTests
{
    public JoinIntoSourceLessTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<SlParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId))
            .WithData([
                new SlParent { Id = 1, Name = "p1" },
                new SlParent { Id = 2, Name = "p2" },
                new SlParent { Id = 3, Name = "p3" },
            ]);
        ctx.From<SlChild>().WithData([
            new SlChild { Id = 10, ParentId = 1, Name = "c1" },
            new SlChild { Id = 11, ParentId = 1, Name = "c2" },
            new SlChild { Id = 12, ParentId = 2, Name = "c3" },
        ]);
        ctx.From<SlDetail>().WithData([
            new SlDetail { Id = 100, ParentId = 1, Name = "d1" },
            new SlDetail { Id = 102, ParentId = 3, Name = "d3" },
        ]);

        return ctx;
    }

    /// <summary>Projection used for deep source-less/explicit parity assertions (order-insensitive).</summary>
    private static object[] Shape(IEnumerable<SlParent> parents)
        => parents
            .OrderBy(p => p.Id)
            .Select(p => (object)new
            {
                p.Id,
                Children = p.Children.Select(c => c.Id).OrderBy(id => id).ToArray(),
                Detail = p.Detail?.Id,
            })
            .ToArray();

    /// <summary>
    /// Observes the real failure of <c>From&lt;SlUnresolvableChild&gt;()</c>: the metadata builder rejects the
    /// two dynamic-columns stores. The observed type/message is returned so the AC08 cases can assert the
    /// <see cref="ArgumentNullException"/> came from the guard, not from resolution.
    /// </summary>
    private static Exception ObserveChildResolutionFailure(InMemoryDataContext ctx)
    {
        var failure = FluentActions.Invoking(() => { _ = ctx.From<SlUnresolvableChild>(); })
            .Should().Throw<Exception>().Which;

        failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("more than one dynamic-columns store");

        return failure;
    }

    // -------------------------------------------------------------------------------------------------
    // AC01 / AC03 / AC06 — positive runtime materialisation and conventional-resolution parity.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AC01_AC06_CollectionDefaultLeft_ShouldMaterialiseLikeTheExplicitBareSource()
    {
        using var sourceLessCtx = CreateContext();
        using var explicitCtx = CreateContext();

        var sourceLess = sourceLessCtx.From<SlParent>()
            .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();
        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto(explicitCtx.From<SlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
        sourceLess.Should().HaveCount(3);
        sourceLess.Single(p => p.Id == 1).Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
        sourceLess.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
        sourceLess.Single(p => p.Id == 3).Children.Should().BeEmpty();
    }

    [Fact]
    public void AC02_AC06_CollectionInner_ShouldMaterialiseLikeTheExplicitBareSource()
    {
        using var sourceLessCtx = CreateContext();
        using var explicitCtx = CreateContext();

        var sourceLess = sourceLessCtx.From<SlParent>()
            .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToList();
        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto(explicitCtx.From<SlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
        sourceLess.Select(p => p.Id).OrderBy(id => id).Should().Equal(1, 2);
        sourceLess.Single(p => p.Id == 1).Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
    }

    [Fact]
    public void AC03_AC06_ReferenceDefaultLeft_ShouldMaterialiseLikeTheExplicitBareSource()
    {
        using var sourceLessCtx = CreateContext();
        using var explicitCtx = CreateContext();

        var sourceLess = sourceLessCtx.From<SlParent>()
            .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail)
            .ToList();
        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto(explicitCtx.From<SlDetail>(), (p, c) => p.Id == c.ParentId, p => p.Detail)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
        sourceLess.Should().HaveCount(3);
        sourceLess.Single(p => p.Id == 1).Detail!.Id.Should().Be(100);
        sourceLess.Single(p => p.Id == 1).Detail!.Name.Should().Be("d1");
        sourceLess.Single(p => p.Id == 2).Detail.Should().BeNull();
        sourceLess.Single(p => p.Id == 3).Detail!.Id.Should().Be(102);
    }

    [Fact]
    public void AC04_AC06_ReferenceInner_ShouldMaterialiseLikeTheExplicitBareSource()
    {
        using var sourceLessCtx = CreateContext();
        using var explicitCtx = CreateContext();

        var sourceLess = sourceLessCtx.From<SlParent>()
            .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner)
            .ToList();
        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto(explicitCtx.From<SlDetail>(), (p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
        sourceLess.Select(p => p.Id).OrderBy(id => id).Should().Equal(1, 3);
        sourceLess.Single(p => p.Id == 1).Detail!.Id.Should().Be(100);
        sourceLess.Single(p => p.Id == 3).Detail!.Id.Should().Be(102);
    }

    [Fact]
    public void AC05_AC06_KeyForm_ShouldMaterialiseLikeTheExplicitBareSource()
    {
        using var sourceLessCtx = CreateContext();
        using var explicitCtx = CreateContext();

        var sourceLess = sourceLessCtx.From<SlParent>()
            .JoinInto<SlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToList();
        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto<SlChild, int>(explicitCtx.From<SlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
        sourceLess.Should().HaveCount(3);
        sourceLess.Single(p => p.Id == 1).Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
        sourceLess.Single(p => p.Id == 3).Children.Should().BeEmpty();
    }

    [Fact]
    public void AC06_ConfiguredChildMapping_ShouldWinOverConvention_ForTheSourceLessResolution()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SlChild>(b => b.Table("sl_child_configured")).WithData([
            new SlChild { Id = 10, ParentId = 1, Name = "c1" },
            new SlChild { Id = 11, ParentId = 1, Name = "c2" },
        ]);
        ctx.From<SlParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId))
            .WithData([
                new SlParent { Id = 1, Name = "p1" },
                new SlParent { Id = 2, Name = "p2" },
            ]);

        var sourceLess = ctx.From<SlParent>()
            .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        DataContextCache.Metadata[typeof(SlChild)].TableName.Should()
            .Be("sl_child_configured", "the source-less form must resolve the already-registered configured mapping");

        // The explicit bare source resolves the same process-wide entry; a fresh context proves the
        // configured mapping (not a convention rebuild) is what both forms observe.
        using var explicitCtx = new InMemoryDataContext();
        explicitCtx.From<SlChild>().WithData([
            new SlChild { Id = 10, ParentId = 1, Name = "c1" },
            new SlChild { Id = 11, ParentId = 1, Name = "c2" },
        ]);
        explicitCtx.From<SlParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId))
            .WithData([
                new SlParent { Id = 1, Name = "p1" },
                new SlParent { Id = 2, Name = "p2" },
            ]);
        DataContextCache.Metadata[typeof(SlChild)].TableName.Should().Be("sl_child_configured");

        var explicitBare = explicitCtx.From<SlParent>()
            .JoinInto(explicitCtx.From<SlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        Shape(sourceLess).Should().BeEquivalentTo(Shape(explicitBare));
    }

    [Fact]
    public void AC06_UnresolvableChild_ShouldThrowTheSameResolutionExceptionAtConstruction()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);

        // Collection and key forms throw while the JoinInto call is being built; no builder is produced,
        // so the failure is not deferred to a list terminal.
        EntityBuilder<SlNullOrderingParent>? builtCollection = null;
        Exception? collectionFailure = null;
        try
        {
            builtCollection = ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>((p, c) => true, p => p.Children);
        }
        catch (Exception ex)
        {
            collectionFailure = ex;
        }

        collectionFailure.Should().NotBeNull();
        collectionFailure!.GetType().Should().BeSameAs(resolutionFailure.GetType());
        collectionFailure.Message.Should().Be(resolutionFailure.Message);
        builtCollection.Should().BeNull("resolution must fail at construction, not at ToList()");

        var referenceFailure = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>((p, c) => true, p => p.Detail))
            .Should().Throw<Exception>().Which;
        referenceFailure.GetType().Should().BeSameAs(resolutionFailure.GetType());
        referenceFailure.Message.Should().Be(resolutionFailure.Message);

        var keyFailure = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild, int>((p, c) => true, p => p.Children, p => p.Id, c => c.Id))
            .Should().Throw<Exception>().Which;
        keyFailure.GetType().Should().BeSameAs(resolutionFailure.GetType());
        keyFailure.Message.Should().Be(resolutionFailure.Message);
    }

    // -------------------------------------------------------------------------------------------------
    // AC08 — null predicate is observed before child resolution and before the options callback.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AC08_Form1_CollectionDefaultLeft_NullPredicate_ShouldWinOverResolutionAndOptions()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("options callback must not run");
        Expression<Func<SlNullOrderingParent, SlUnresolvableChild, bool>> predicate = null!;

        var observed = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>(predicate, p => p.Children, _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("predicate");
        callbackCalls.Should().Be(0, "the options callback runs only after the predicate guard");
        observed.GetType().Should().NotBe(resolutionFailure.GetType());
        observed.Message.Should().Contain("predicate");
    }

    [Fact]
    public void AC08_Form2_CollectionExplicitKind_NullPredicate_ShouldWinOverResolutionAndOptions()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("options callback must not run");
        Expression<Func<SlNullOrderingParent, SlUnresolvableChild, bool>> predicate = null!;

        var observed = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>(predicate, p => p.Children, JoinType.Inner, _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("predicate");
        callbackCalls.Should().Be(0, "the options callback runs only after the predicate guard");
        observed.GetType().Should().NotBe(resolutionFailure.GetType());
        observed.Message.Should().Contain("predicate");
    }

    [Fact]
    public void AC08_Form3_ReferenceDefaultLeft_NullPredicate_ShouldWinOverResolutionAndOptions()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("options callback must not run");
        Expression<Func<SlNullOrderingParent, SlUnresolvableChild, bool>> predicate = null!;

        var observed = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>(predicate, p => p.Detail, _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("predicate");
        callbackCalls.Should().Be(0, "the options callback runs only after the predicate guard");
        observed.GetType().Should().NotBe(resolutionFailure.GetType());
        observed.Message.Should().Contain("predicate");
    }

    [Fact]
    public void AC08_Form4_ReferenceExplicitKind_NullPredicate_ShouldWinOverResolutionAndOptions()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("options callback must not run");
        Expression<Func<SlNullOrderingParent, SlUnresolvableChild, bool>> predicate = null!;

        var observed = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild>(predicate, p => p.Detail, JoinType.Left, _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("predicate");
        callbackCalls.Should().Be(0, "the options callback runs only after the predicate guard");
        observed.GetType().Should().NotBe(resolutionFailure.GetType());
        observed.Message.Should().Contain("predicate");
    }

    [Fact]
    public void AC08_Form5_KeyForm_NullPredicate_ShouldWinOverResolutionAndOptions()
    {
        using var ctx = new InMemoryDataContext();
        var resolutionFailure = ObserveChildResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("options callback must not run");
        Expression<Func<SlNullOrderingParent, SlUnresolvableChild, bool>> predicate = null!;

        var observed = FluentActions.Invoking(() => ctx.From<SlNullOrderingParent>()
                .JoinInto<SlUnresolvableChild, int>(predicate, p => p.Children, p => p.Id, c => c.Id, _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("predicate");
        callbackCalls.Should().Be(0, "the options callback runs only after the predicate guard");
        observed.GetType().Should().NotBe(resolutionFailure.GetType());
        observed.Message.Should().Contain("predicate");
    }

    [Fact]
    public void AC08_Control_ExplicitSourceTwins_WithNullPredicate_ShouldThrowTheSameArgumentNullException()
    {
        using var ctx = CreateContext();
        Expression<Func<SlParent, SlChild, bool>> collectionPredicate = null!;
        Expression<Func<SlParent, SlDetail, bool>> referencePredicate = null!;

        var legacy1 = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto(ctx.From<SlChild>(), collectionPredicate, p => p.Children))
            .Should().Throw<ArgumentNullException>().Which;
        var legacy2 = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto(ctx.From<SlChild>(), collectionPredicate, p => p.Children, JoinType.Left))
            .Should().Throw<ArgumentNullException>().Which;
        var legacy3 = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto(ctx.From<SlDetail>(), referencePredicate, p => p.Detail))
            .Should().Throw<ArgumentNullException>().Which;
        var legacy4 = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto(ctx.From<SlDetail>(), referencePredicate, p => p.Detail, JoinType.Left))
            .Should().Throw<ArgumentNullException>().Which;
        var legacy5 = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>(ctx.From<SlChild>(), collectionPredicate, p => p.Children, p => p.Id, c => c.ParentId))
            .Should().Throw<ArgumentNullException>().Which;

        new[] { legacy1, legacy2, legacy3, legacy4, legacy5 }
            .Select(ex => ex.ParamName)
            .Should().AllBe("predicate");
    }

    // -------------------------------------------------------------------------------------------------
    // AC07 — options forwarding, once-ness, sentinel identity, defaults and rejection parity.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AC07_OptionsCallback_ShouldRunExactlyOnce_ForEverySourceLessForm()
    {
        using var ctx = CreateContext();
        var counts = new int[5];

        ctx.From<SlParent>().JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, _ => counts[0]++);
        ctx.From<SlParent>().JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner, _ => counts[1]++);
        ctx.From<SlParent>().JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, _ => counts[2]++);
        ctx.From<SlParent>().JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner, _ => counts[3]++);
        ctx.From<SlParent>().JoinInto<SlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId, _ => counts[4]++);

        counts.Should().Equal(1, 1, 1, 1, 1);
    }

    [Fact]
    public void AC07_OptionsSentinel_ShouldEscapeUnchanged_ForEverySourceLessForm()
    {
        using var ctx = CreateContext();
        var sentinel = new InvalidOperationException("sentinel-instance");
        var calls = 0;
        Action<JoinOptions> options = _ => { calls++; throw sentinel; };

        FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, options))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
        FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner, options))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
        FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, options))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
        FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner, options))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
        FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId, options))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(sentinel);

        calls.Should().Be(5, "one callback invocation per form, and the sentinel is the same instance");
    }

    [Fact]
    public void AC07_OmittedOrNullOptions_ShouldNotInvokeACallback_AndEveryFormShouldSucceed()
    {
        using var ctx = CreateContext();
        Action<JoinOptions>? noOptions = null;

        Action act = () =>
        {
            _ = ctx.From<SlParent>().JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, noOptions);
            _ = ctx.From<SlParent>().JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner);
            _ = ctx.From<SlParent>().JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, noOptions);
            _ = ctx.From<SlParent>().JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner);
            _ = ctx.From<SlParent>().JoinInto<SlChild, int>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId, noOptions);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AC07_OptionsMutation_ShouldReachTheJoin_ForTheReferenceForm()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<SlUnregisteredParent>().WithData([
            new SlUnregisteredParent { Id = 1, Name = "p1" },
        ]);
        ctx.From<SlDetail>().WithData([
            new SlDetail { Id = 100, ParentId = 1, Name = "d1" },
        ]);

        // Control: without the local configuration the undeclared relationship is rejected.
        Action withoutOptions = () => ctx.From<SlUnregisteredParent>()
            .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail);
        withoutOptions.Should().Throw<NotSupportedException>()
            .WithMessage("*requires an explicitly declared one-to-one relationship*");

        var parents = ctx.From<SlUnregisteredParent>()
            .JoinInto<SlDetail>(
                (p, c) => p.Id == c.ParentId,
                p => p.Detail,
                j => j.OneToOne<SlUnregisteredParent, SlDetail, int>(p => p.Id, c => c.ParentId))
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Detail!.Id.Should().Be(100, "the callback's local relationship configuration must reach the join");
    }

    [Fact]
    public void AC07_UnsupportedJoinKinds_ShouldMatchTheLegacyRejection_ForTheExplicitKindForms()
    {
        using var ctx = CreateContext();

        foreach (var joinType in new[] { JoinType.Right, JoinType.Full })
        {
            var legacyCollection = FluentActions.Invoking(() => ctx.From<SlParent>()
                    .JoinInto(ctx.From<SlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, joinType))
                .Should().Throw<NotSupportedException>().Which;
            var sourceLessCollection = FluentActions.Invoking(() => ctx.From<SlParent>()
                    .JoinInto<SlChild>((p, c) => p.Id == c.ParentId, p => p.Children, joinType))
                .Should().Throw<NotSupportedException>().Which;
            sourceLessCollection.GetType().Should().Be(legacyCollection.GetType());
            sourceLessCollection.Message.Should().Be(legacyCollection.Message);

            var legacyReference = FluentActions.Invoking(() => ctx.From<SlParent>()
                    .JoinInto(ctx.From<SlDetail>(), (p, c) => p.Id == c.ParentId, p => p.Detail, joinType))
                .Should().Throw<NotSupportedException>().Which;
            var sourceLessReference = FluentActions.Invoking(() => ctx.From<SlParent>()
                    .JoinInto<SlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, joinType))
                .Should().Throw<NotSupportedException>().Which;
            sourceLessReference.GetType().Should().Be(legacyReference.GetType());
            sourceLessReference.Message.Should().Be(legacyReference.Message);
        }
    }

    [Fact]
    public void AC05_InvalidKeySelectors_ShouldMatchTheLegacyRejection_ForTheKeyForm()
    {
        using var ctx = new InMemoryDataContext();

        // Unrelated key types pass compilation (int widens to long) but fail the shared key validation.
        var legacyMismatch = FluentActions.Invoking(() => ctx.From<SlKeyParent>()
                .JoinInto<SlKeyChild, long>(ctx.From<SlKeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId))
            .Should().Throw<NotSupportedException>().Which;
        var sourceLessMismatch = FluentActions.Invoking(() => ctx.From<SlKeyParent>()
                .JoinInto<SlKeyChild, long>((p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId))
            .Should().Throw<NotSupportedException>().Which;
        sourceLessMismatch.GetType().Should().Be(legacyMismatch.GetType());
        sourceLessMismatch.Message.Should().Be(legacyMismatch.Message);

        // Null parent key / null child key keep the same parameter name and message as the legacy form.
        Expression<Func<SlParent, SlChild, bool>> predicate = (p, c) => p.Id == c.ParentId;
        Expression<Func<SlParent, int>> nullParentKey = null!;
        Expression<Func<SlChild, int>> childKey = c => c.ParentId;
        var legacyNullParentKey = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>(ctx.From<SlChild>(), predicate, p => p.Children, nullParentKey, childKey))
            .Should().Throw<ArgumentNullException>().Which;
        var sourceLessNullParentKey = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>(predicate, p => p.Children, nullParentKey, childKey))
            .Should().Throw<ArgumentNullException>().Which;
        sourceLessNullParentKey.ParamName.Should().Be("parentKey");
        sourceLessNullParentKey.ParamName.Should().Be(legacyNullParentKey.ParamName);
        sourceLessNullParentKey.Message.Should().Be(legacyNullParentKey.Message);

        Expression<Func<SlParent, int>> parentKey = p => p.Id;
        Expression<Func<SlChild, int>> nullChildKey = null!;
        var legacyNullChildKey = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>(ctx.From<SlChild>(), predicate, p => p.Children, parentKey, nullChildKey))
            .Should().Throw<ArgumentNullException>().Which;
        var sourceLessNullChildKey = FluentActions.Invoking(() => ctx.From<SlParent>()
                .JoinInto<SlChild, int>(predicate, p => p.Children, parentKey, nullChildKey))
            .Should().Throw<ArgumentNullException>().Which;
        sourceLessNullChildKey.ParamName.Should().Be("childKey");
        sourceLessNullChildKey.ParamName.Should().Be(legacyNullChildKey.ParamName);
        sourceLessNullChildKey.Message.Should().Be(legacyNullChildKey.Message);
    }
}

public sealed class SlParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public ICollection<SlChild> Children { get; set; } = new List<SlChild>();
    public SlDetail? Detail { get; set; }
}

public sealed class SlChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class SlDetail
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
}

public sealed class SlKeyParent
{
    public int Id { get; set; }
    public ICollection<SlKeyChild> Children { get; set; } = new List<SlKeyChild>();
}

public sealed class SlKeyChild
{
    public int Id { get; set; }
    public long ParentId { get; set; }
}

/// <summary>
/// Parent used by the AC08 null-ordering cases: the collection/reference selectors are typed over
/// <see cref="SlUnresolvableChild"/>, a child type whose metadata genuinely cannot be built.
/// </summary>
public sealed class SlNullOrderingParent
{
    public int Id { get; set; }
    public ICollection<SlUnresolvableChild> Children { get; set; } = new List<SlUnresolvableChild>();
    public SlUnresolvableChild? Detail { get; set; }
}

/// <summary>
/// A genuinely unresolvable child type: two <c>[DynamicColumns]</c> stores make
/// <c>EntityMetadataBuilder&lt;SlUnresolvableChild&gt;.Build()</c> throw
/// <see cref="InvalidOperationException"/> ("more than one dynamic-columns store"), which is exactly the
/// failure observed through <c>From&lt;SlUnresolvableChild&gt;()</c>.
/// </summary>
public sealed class SlUnresolvableChild
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> First { get; set; } = new();

    [DynamicColumns]
    public Dictionary<string, object?> Second { get; set; } = new();
}

/// <summary>Parent for the AC07 local-configuration case: its reference navigation is not declared.</summary>
public sealed class SlUnregisteredParent
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public SlDetail? Detail { get; set; }
}
