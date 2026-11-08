using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T249-D5 preparation and plan identity of the metadata-driven <c>LoadWith</c>. The rows here hold the
/// command shape constant and change exactly one identity dimension: the declaration frontend (explicit
/// keys vs inferred metadata), the relationship components (order, set), the navigation member/scope, the
/// resolved eager-load strategy, or nothing at all (cold/warm/prepared, a captured parameter value). A
/// cached plan may only be shared when every result-affecting piece agrees, and two semantically identical
/// declarations — including the explicit-key and the inferred-metadata form of one 1:N relationship — must
/// key one entry, never two.
/// <para>
/// Everything is prepared through the real planner on a provider-free SQL context (a fake dialect over a
/// fake connection, like <c>RootProjectionTests</c>/<c>T251CompositeCacheTests</c>), so no database is
/// touched and no SQL is executed; the plan store is <see cref="QueryPlanStore"/> itself.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class MetadataLoadWithCacheTests
{
    public MetadataLoadWithCacheTests() => DataContextCache.Clear();

    // ---------------------------------------------------------------------------------------------
    // Fixtures — a single-key 1:N pair (the explicit-key form can express exactly this relationship),
    // a composite pair whose component order/set can be re-published, and a two-navigation pair.
    // ---------------------------------------------------------------------------------------------

    public sealed class Order
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

        public ICollection<OrderLine> AltLines { get; set; } = new List<OrderLine>();
    }

    public sealed class OrderLine
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int AltOrderId { get; set; }
    }

    public sealed class Ledger
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Code { get; set; }

        public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
    }

    public sealed class LedgerEntry
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int OwnerCode { get; set; }
    }

    /// <summary>A plain pair with no declared relationship: the explicit-key identity must stay legacy.</summary>
    public sealed class PlainParent
    {
        [Key]
        public int Id { get; set; }

        public ICollection<PlainChild> Children { get; set; } = new List<PlainChild>();
    }

    public sealed class PlainChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    public sealed class Neighbour
    {
        [Key]
        public int Id { get; set; }

        public int Value { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------

    private static RootProjectionTests.TestContext CreateContext()
    {
        var context = new RootProjectionTests.TestContext();
        context.From<OrderLine>();
        context.From<LedgerEntry>();
        context.From<PlainChild>();
        context.From<Neighbour>();
        return context;
    }

    /// <summary>Publishes the single-key mapping of <see cref="Order"/>/<see cref="OrderLine"/>.</summary>
    private static void PublishDeclaredOrder()
        => DataContextCache.Metadata[typeof(Order)] = new EntityMetadataBuilder<Order>()
            .HasMany(order => order.Lines, line => new { line.OrderId }, order => new { order.Id })
            .HasMany(order => order.AltLines, line => new { line.AltOrderId }, order => new { order.Id })
            .Build();

    /// <summary>
    /// Publishes <see cref="Order"/>/<see cref="OrderLine"/> with a <b>second</b> navigation over the same
    /// foreign key and the same principal key, so the navigation member is the only identity dimension the
    /// two declarations differ in.
    /// </summary>
    private static void PublishTwoNavigationsOverOneKeyPair()
        => DataContextCache.Metadata[typeof(Order)] = new EntityMetadataBuilder<Order>()
            .HasMany(order => order.Lines, line => new { line.OrderId }, order => new { order.Id })
            .HasMany(order => order.AltLines, line => new { line.OrderId }, order => new { order.Id })
            .Build();

    /// <summary>Publishes the composite mapping of <see cref="Ledger"/> with the requested component order.</summary>
    private static void PublishLedger(bool swapped = false, bool singleComponent = false)
    {
        var builder = new EntityMetadataBuilder<Ledger>();
        if (singleComponent)
        {
            builder = builder.HasMany(
                ledger => ledger.Entries,
                entry => new { entry.OwnerCode },
                ledger => new { ledger.Code });
        }
        else if (swapped)
        {
            builder = builder.HasMany(
                ledger => ledger.Entries,
                entry => new { entry.OwnerCode, entry.TenantId },
                ledger => new { ledger.Code, ledger.TenantId });
        }
        else
        {
            builder = builder.HasMany(
                ledger => ledger.Entries,
                entry => new { entry.TenantId, entry.OwnerCode },
                ledger => new { ledger.TenantId, ledger.Code });
        }

        DataContextCache.Metadata[typeof(Ledger)] = builder.Build();
    }

    private static IPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command)
        => context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

    private static string SqlOf<T>(IPreparedQueryCommand<T> prepared)
        => ((DbPreparedQueryCommand<T>)prepared).DbCommand.CommandText.Replace("\r\n", "\n");

    private static int StoredPlans() => QueryPlanStore.Values.Count();

    // ---------------------------------------------------------------------------------------------
    // Cold / warm / prepared on the metadata path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Cache_ColdWarmPrepared_ReusesOnePlanEntry()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var coldCommand = context.From<Order>().Where(o => o.Id > 0).LoadWith(o => o.Lines).ToCommand();
        var cold = Prepare(context, coldCommand);
        StoredPlans().Should().Be(1, "the cold preparation stores exactly one parent plan");
        coldCommand.EagerLoadShape.IsEmpty.Should().BeFalse("the metadata relationship identity is part of the plan key");

        // Warm: the very same prepared command is reused (its plan key is memoized and matches by reference).
        var warm = Prepare(context, coldCommand);
        warm.Should().BeSameAs(cold);
        StoredPlans().Should().Be(1);

        // Prepared: a fresh, structurally identical declaration hits the stored plan instead of storing a new one.
        var preparedCommand = context.From<Order>().Where(o => o.Id > 0).LoadWith(o => o.Lines).ToCommand();
        var prepared = Prepare(context, preparedCommand);
        prepared.Should().BeSameAs(cold, "an equivalent metadata declaration reuses the stored plan");
        StoredPlans().Should().Be(1, "no duplicate entry for an equivalent declaration");
    }

    // ---------------------------------------------------------------------------------------------
    // The headline normalization: explicit keys vs inferred metadata of the same declared 1:N.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Cache_EquivalentExplicitAndInferred_ShareOneShapeAndPlanEntry()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var inferredCommand = context.From<Order>().Where(o => o.Id > 0).LoadWith(o => o.Lines).ToCommand();
        var explicitCommand = context.From<Order>().Where(o => o.Id > 0)
            .LoadWith(o => o.Lines, child => child.From<OrderLine>(), o => o.Id, line => line.OrderId)
            .ToCommand();

        explicitCommand.EagerLoadShape.Equals(inferredCommand.EagerLoadShape)
            .Should().BeTrue("the explicit keys select exactly the declared OneToMany relationship, so both declarations are one graph node");

        var inferred = Prepare(context, inferredCommand);
        var explicitPrepared = Prepare(context, explicitCommand);

        explicitPrepared.Should().BeSameAs(inferred, "both frontends key the same plan entry, not two");
        StoredPlans().Should().Be(1, "no frontend discriminator may split an otherwise-equivalent entry");
        SqlOf(explicitPrepared).Should().Be(SqlOf(inferred), "the shared entry is the same command");
    }

    [Fact]
    public void MetadataLoadWith_Cache_LegacyShape_IsUnchangedWhenNoDeclaredRelationshipMatches()
    {
        using var context = CreateContext();
        var builder = context.From<PlainParent>()
            .LoadWith(p => p.Children, child => child.From<PlainChild>(), p => p.Id, child => child.ParentId);

        var spec = ((IEagerLoadNodeSource)builder).EagerLoadNodeSpecs!.Single();

        spec.PlanShapeKey.Should().Be(spec.ShapeKey, "no declared relationship matches, so the legacy identity is kept verbatim");
        spec.ShapeKey.Should().Contain(";K=").And.Contain(";PK=").And.Contain(";CK=");
        spec.ShapeKey.Should().NotContain(";KY=", "the metadata component list is not part of the legacy fingerprint");
    }

    // ---------------------------------------------------------------------------------------------
    // Result-affecting identity dimensions.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Cache_ComponentOrder_ChangesTheIdentity()
    {
        PublishLedger();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var forwardCommand = context.From<Ledger>().LoadWith(l => l.Entries).ToCommand();
        var forward = Prepare(context, forwardCommand);

        PublishLedger(swapped: true);
        var swappedCommand = context.From<Ledger>().LoadWith(l => l.Entries).ToCommand();
        var swapped = Prepare(context, swappedCommand);

        forwardCommand.EagerLoadShape.Equals(swappedCommand.EagerLoadShape)
            .Should().BeFalse("component order pairs different members and is result-affecting");
        swapped.Should().NotBeSameAs(forward);
        StoredPlans().Should().Be(2, "each component order owns its own plan entry");
    }

    [Fact]
    public void MetadataLoadWith_Cache_ComponentSet_ChangesTheIdentity()
    {
        PublishLedger();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var compositeCommand = context.From<Ledger>().LoadWith(l => l.Entries).ToCommand();
        var composite = Prepare(context, compositeCommand);

        PublishLedger(singleComponent: true);
        var singleCommand = context.From<Ledger>().LoadWith(l => l.Entries).ToCommand();
        var single = Prepare(context, singleCommand);

        singleCommand.EagerLoadShape.Equals(compositeCommand.EagerLoadShape)
            .Should().BeFalse("dropping a key component changes which rows load");
        single.Should().NotBeSameAs(composite);
        StoredPlans().Should().Be(2);
    }

    [Fact]
    public void MetadataLoadWith_Cache_NavigationScope_ChangesTheIdentity()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var linesCommand = context.From<Order>().LoadWith(o => o.Lines).ToCommand();
        var altCommand = context.From<Order>().LoadWith(o => o.AltLines).ToCommand();

        linesCommand.EagerLoadShape.Equals(altCommand.EagerLoadShape)
            .Should().BeFalse("two navigations to the same related type are different graph nodes");

        var lines = Prepare(context, linesCommand);
        var alt = Prepare(context, altCommand);
        alt.Should().NotBeSameAs(lines, "a different navigation must not reuse the other navigation's plan");
        StoredPlans().Should().Be(2);
    }

    [Fact]
    public void MetadataLoadWith_Cache_NavigationMemberAlone_ChangesTheIdentity()
    {
        PublishTwoNavigationsOverOneKeyPair();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        // Same parent, same child, same declared kind/route and the same ordered key pair: only the
        // navigation member differs, so only the ;M= segment can keep the two graphs apart.
        var linesCommand = context.From<Order>().LoadWith(o => o.Lines).ToCommand();
        var altCommand = context.From<Order>().LoadWith(o => o.AltLines).ToCommand();

        linesCommand.EagerLoadShape.Equals(altCommand.EagerLoadShape)
            .Should().BeFalse("two navigation members over one key pair are different graph nodes");

        var lines = Prepare(context, linesCommand);
        var alt = Prepare(context, altCommand);
        alt.Should().NotBeSameAs(lines, "a different navigation member must not reuse the other navigation's plan");
        StoredPlans().Should().Be(2);
    }

    [Fact]
    public void MetadataLoadWith_Cache_ResolvedMode_ChangesTheIdentity()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var splitCommand = context.From<Order>().LoadWith(o => o.Lines).ToCommand();
        var singleCommand = context.From<Order>().LoadWith(o => o.Lines, EagerLoadMode.SingleQuery).ToCommand();

        var split = Prepare(context, splitCommand);
        var single = Prepare(context, singleCommand);

        splitCommand.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);
        singleCommand.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
        single.Should().NotBeSameAs(split, "the resolved strategy is part of plan identity");
        StoredPlans().Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // Isolation: no parameter baking, no captured context, no cross-context default leak.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Cache_CapturedParameterValue_IsNotPartOfTheIdentity()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        static QueryCommand<Order> Build(RootProjectionTests.TestContext ctx, int minimumId)
            => ctx.From<Order>().Where(o => o.Id > minimumId).LoadWith(o => o.Lines).ToCommand();

        var first = Prepare(context, Build(context, 10));
        var second = Prepare(context, Build(context, 20));

        second.Should().BeSameAs(first, "a captured value is parameterized, never baked into the plan");
        StoredPlans().Should().Be(1);
    }

    [Fact]
    public void MetadataLoadWith_Cache_ContextDefault_IsNotSharedAcrossContexts()
    {
        PublishDeclaredOrder();
        QueryPlanStore.Clear();

        using var splitDefault = new RootProjectionTests.TestContext();
        using var singleDefault = new RootProjectionTests.TestContext();
        // A context whose default differs must not adopt the other context's resolved strategy plan.
        using var configured = new NextORM.Sqlite.SqliteDataContext(
            "Data Source=:memory:", new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));

        var splitCommand = splitDefault.From<Order>().LoadWith(o => o.Lines).ToCommand();
        var split = Prepare(splitDefault, splitCommand);

        var configuredCommand = configured.From<Order>().LoadWith(o => o.Lines).ToCommand();
        var configuredPrepared = Prepare(configured, configuredCommand);

        configuredCommand.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
        configuredPrepared.Should().NotBeSameAs(split, "the effective context default resolves a different strategy, hence a different plan");
    }

    /// <summary>
    /// The metadata specification memoizes its child builder per <see cref="IDataContext"/> in a
    /// <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/>, so a spec shared
    /// between contexts resolves each context's own builder and retains none of them.
    /// </summary>
    [Fact]
    public void MetadataLoadWith_Cache_ChildResolution_IsPerContext()
    {
        PublishDeclaredOrder();
        using var first = CreateContext();
        using var second = CreateContext();
        var calls = 0;

        EntityBuilder<Order> Build(IDataContext ctx) => ctx.From<Order>().LoadWith(
            o => o.Lines,
            child => { calls++; return child.From<OrderLine>(); });

        // ToParentCommand validates the declared graph, which resolves each context's child builder once.
        Build(first).ToParentCommand();
        calls.Should().Be(1);
        Build(second).ToParentCommand();
        calls.Should().Be(2, "a second context resolves independently; the memo is weak and per context");
    }

    // ---------------------------------------------------------------------------------------------
    // The shared scalar command/cache is untouched by metadata preparation.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Cache_SharedScalarCache_StaysUsable()
    {
        PublishDeclaredOrder();
        using var context = CreateContext();
        QueryPlanStore.Clear();

        var metadataCommand = context.From<Order>().LoadWith(o => o.Lines).ToCommand();
        metadataCommand.Cache.Should().BeTrue();
        Prepare(context, metadataCommand);
        metadataCommand.Cache.Should().BeTrue("preparation never mutates the sticky QueryCommand.Cache flag");

        // The scalar command shape shares the same context and store: metadata preparation must not have
        // disabled its caching (the `_dontCache` sticky trap) or consumed its plan entry.
        var anyCommand = context.From<Order>().AnyCommand();
        anyCommand.Cache.Should().BeTrue();
        var any = Prepare(context, anyCommand);
        var anyRepeat = Prepare(context, anyCommand);
        anyRepeat.Should().BeSameAs(any, "the shared scalar command still caches after metadata preparation");
    }
}
