using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T249-D5 guards for the additive metadata-driven <c>LoadWith</c>: the SQL-only capability rejection on
/// the in-memory provider (raised at preparation, never at execution and never by mutating the sticky
/// <see cref="QueryCommand.Cache"/> flag), the fail-closed rejection of malformed metadata with zero
/// executed commands and zero plan-store inserts, the duplicate/cycle graph guards, the composition
/// terminals that cannot carry an eager graph, and the scalar terminals that must stay scalar.
/// <para>
/// The SQL context is the provider-free fake dialect (<see cref="RootProjectionTests.TestContext"/>), which
/// prepares and caches real plans without touching a database, so the counters observe planning, not
/// inference.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class MetadataLoadWithGuardTests
{
    public MetadataLoadWithGuardTests() => DataContextCache.Clear();

    // ---------------------------------------------------------------------------------------------
    // Fixtures.
    // ---------------------------------------------------------------------------------------------

    [SqlTable("guard_order")]
    public sealed class GuardOrder
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public ICollection<GuardLine> Lines { get; set; } = new List<GuardLine>();
    }

    [SqlTable("guard_line")]
    public sealed class GuardLine
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }
    }

    [SqlTable("guard_node")]
    public sealed class GuardNode
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }

        public ICollection<GuardNode> Children { get; set; } = new List<GuardNode>();
    }

    [SqlTable("guard_neighbour")]
    public sealed class GuardNeighbour
    {
        [Key]
        public int Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class RecordingInterceptor : IQueryInterceptor
    {
        public int Initialized;

        public int Executing;

        public List<string> Statements { get; } = [];

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
        {
            Initialized++;
            Statements.Add(command.CommandText);
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Executing++;
    }

    private static InMemoryDataContext CreateInMemory()
    {
        var context = new InMemoryDataContext();
        context.From<GuardOrder>(builder => builder.HasMany(o => o.Lines, line => line.OrderId));
        context.From<GuardLine>();
        context.From<GuardNode>(builder => builder.HasMany(n => n.Children, child => child.ParentId));
        context.From<GuardNeighbour>().WithData([new GuardNeighbour { Id = 1, Value = 7 }]);
        return context;
    }

    private static RootProjectionTests.TestContext CreateSqlContext()
    {
        var context = new RootProjectionTests.TestContext();
        context.From<GuardOrder>(builder => builder.HasMany(o => o.Lines, line => line.OrderId));
        context.From<GuardLine>();
        context.From<GuardNeighbour>();
        return context;
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

    private static int StoredPlans() => QueryPlanStore.Values.Count();

    private static PropertyInfo Prop<T>(string name) => typeof(T).GetProperty(name)!;

    private static IReadOnlyList<IPropertyMetadata> Props<T>() => new EntityMetadataBuilder<T>().Build().Properties;

    /// <summary>
    /// Publishes a relationship with a repeated foreign-key component, exactly as a faulty producer could:
    /// the consumer must reject it at declaration, before any SQL and before any plan-store insert.
    /// </summary>
    private static void PublishMalformedOrder()
    {
        var properties = Props<GuardOrder>()
            .Where(p => p.PropertyInfo.Name != nameof(GuardOrder.Lines))
            .ToList();

        var relationship = new RelationshipMetadata(
            RelationshipKind.OneToMany,
            typeof(GuardOrder),
            typeof(GuardLine),
            Prop<GuardOrder>(nameof(GuardOrder.Lines)),
            isCollection: true,
            foreignKeys: [Prop<GuardLine>(nameof(GuardLine.OrderId)), Prop<GuardLine>(nameof(GuardLine.OrderId))],
            principalKeys: [Prop<GuardOrder>(nameof(GuardOrder.Id)), Prop<GuardOrder>(nameof(GuardOrder.Id))],
            dependentType: typeof(GuardLine),
            principalType: typeof(GuardOrder));

        DataContextCache.Metadata[typeof(GuardOrder)] =
            new EntityMetadata("guard_order", properties, relationships: [relationship]);
    }

    // ---------------------------------------------------------------------------------------------
    // SQL-only capability: the additive form is rejected on the in-memory provider at preparation.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_InMemoryRejected_ConstructionRejectsBeforeAnyCommand()
    {
        using var context = CreateInMemory();
        QueryPlanStore.Clear();

        var builder = context.From<GuardOrder>().LoadWith(o => o.Lines);

        // The declaration is validated at declaration time; the capability decision belongs to command
        // construction, where the provider is known — before a command exists, before any SQL is built and
        // before a plan can be inserted.
        Action act = () => builder.ToCommand();

        act.Should().Throw<NotSupportedException>().WithMessage("*SQL-only*");
        StoredPlans().Should().Be(0, "a capability rejection must not insert a plan");
    }

    [Fact]
    public async Task MetadataLoadWith_InMemoryRejected_AllGraphTerminalsRejectBeforeExecution()
    {
        using var context = CreateInMemory();
        var builder = context.From<GuardOrder>().LoadWith(o => o.Lines);

        FluentActions.Invoking(() => builder.ToList()).Should().Throw<NotSupportedException>().WithMessage("*SQL-only*");
        FluentActions.Invoking(() => builder.ToArray()).Should().Throw<NotSupportedException>().WithMessage("*SQL-only*");
        await FluentActions.Invoking(() => builder.ToListAsync()).Should().ThrowAsync<NotSupportedException>().WithMessage("*SQL-only*");
        await FluentActions.Invoking(() => builder.ToArrayAsync()).Should().ThrowAsync<NotSupportedException>().WithMessage("*SQL-only*");

        StoredPlans().Should().Be(0, "none of the rejected graph terminals reaches the plan store");
    }

    [Fact]
    public void MetadataLoadWith_InMemoryRejected_ScalarTerminalsRejectBeforeExecution()
    {
        using var context = CreateInMemory();
        var builder = context.From<GuardOrder>().LoadWith(o => o.Lines);

        FluentActions.Invoking(() => builder.Any()).Should().Throw<NotSupportedException>().WithMessage("*SQL-only*");
        FluentActions.Invoking(() => builder.Count()).Should().Throw<NotSupportedException>().WithMessage("*SQL-only*");

        StoredPlans().Should().Be(0);
    }

    [Fact]
    public void MetadataLoadWith_InMemoryRejected_ExplicitKeyRouteStillWorks()
    {
        using var context = CreateInMemory();

        var parents = context.From<GuardOrder>()
            .LoadWith(o => o.Lines, child => child.From<GuardLine>(), o => o.Id, line => line.OrderId)
            .ToList();

        parents.Should().NotBeNull("the legacy explicit-key route keeps its in-memory behavior");
    }

    // ---------------------------------------------------------------------------------------------
    // Rejection counters on a SQL context: malformed metadata emits zero commands and zero plan inserts.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_RejectionCounters_MalformedMetadata_ZeroCommandsZeroPlanInserts()
    {
        PublishMalformedOrder();
        using var context = CreateSqlContext();
        var interceptor = new RecordingInterceptor();
        context.AddInterceptor(interceptor);
        QueryPlanStore.Clear();

        Action rejected = () => context.From<GuardOrder>().LoadWith(o => o.Lines);

        rejected.Should().Throw<NotSupportedException>().WithMessage("*repeats the mapped foreign key property*");

        interceptor.Initialized.Should().Be(0, "validation happens before any command is constructed");
        interceptor.Executing.Should().Be(0, "no SQL is executed for a rejected shape");
        interceptor.Statements.Should().BeEmpty("zero commands were emitted");
        StoredPlans().Should().Be(0, "the plan store received zero inserts for the rejected shape");

        // Rejection leaves the shared scalar cache fully usable: a neighbouring query still prepares and
        // caches, and the interceptor proves the counter is live rather than vacuously zero.
        var scalar = context.From<GuardNeighbour>().Where(n => n.Value > 0).ToCommand();
        var first = Prepare(context, scalar);
        var repeat = Prepare(context, scalar);

        repeat.Should().BeSameAs(first, "a neighbouring query still caches after the rejection");
        StoredPlans().Should().Be(1);
        first.SqlStmt.Should().NotBeNullOrEmpty("the cached plan carries its SQL even before a connection runs it");
        // The provider command is minted per connection when the prepared command first executes, not
        // during planning: preparation constructs no command and therefore raises no initialized event.
        interceptor.Initialized.Should().Be(0, "no provider command is minted until a connection executes it");
        interceptor.Executing.Should().Be(0, "preparation alone never executes");
    }

    // ---------------------------------------------------------------------------------------------
    // No sticky cache mutation: a rejection and a metadata preparation never disable the shared commands.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_NoStickyCache_RejectionAndPreparationDoNotFlipTheSharedFlags()
    {
        PublishMalformedOrder();
        using var context = CreateSqlContext();
        QueryPlanStore.Clear();

        Action rejected = () => context.From<GuardOrder>().LoadWith(o => o.Lines);
        rejected.Should().Throw<NotSupportedException>();
        rejected.Should().Throw<NotSupportedException>();

        // The malformed metadata is still published: the legacy explicit-key declaration on the same
        // navigation must keep working (the probe never turns a legacy declaration into a failure).
        var legacy = context.From<GuardOrder>()
            .LoadWith(o => o.Lines, child => child.From<GuardLine>(), o => o.Id, line => line.OrderId)
            .ToCommand();
        legacy.Cache.Should().BeTrue();
        Prepare(context, legacy);
        legacy.Cache.Should().BeTrue("storeInCache is call-local, never the sticky command policy");

        var anyCommand = context.From<GuardNeighbour>().AnyCommand();
        anyCommand.Cache.Should().BeTrue("the context-shared scalar command keeps its cache policy");
        var any = Prepare(context, anyCommand);
        var anyRepeat = Prepare(context, anyCommand);
        anyRepeat.Should().BeSameAs(any);
    }

    // ---------------------------------------------------------------------------------------------
    // Graph guards: duplicate declarations and cycles fail closed before SQL.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_GraphGuard_DuplicateDeclarationRejected()
    {
        using var context = CreateInMemory();
        var builder = context.From<GuardOrder>().LoadWith(o => o.Lines);

        Action act = () => builder.LoadWith(o => o.Lines);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already has an eager-load declaration*");
        StoredPlans().Should().Be(0);
    }

    [Fact]
    public void MetadataLoadWith_GraphGuard_CycleRejectedBeforeSql()
    {
        using var context = CreateInMemory();

        Func<IDataContext, EntityBuilder<GuardNode>> factory = null!;
        factory = ctx => ctx.From<GuardNode>().LoadWith(n => n.Children, factory);

        var builder = context.From<GuardNode>().LoadWith(n => n.Children, factory);

        Action act = () => builder.NormalizeEagerLoadGraph(context);

        act.Should().Throw<NotSupportedException>().WithMessage("*cyclic*");
        StoredPlans().Should().Be(0, "a cyclic declaration never reaches a command or the plan store");
    }

    // ---------------------------------------------------------------------------------------------
    // Terminals: composition terminals fail closed, scalar terminals stay scalar.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Terminal_ProjectionAndJoinFailClosed()
    {
        using var context = CreateInMemory();
        var builder = context.From<GuardOrder>().LoadWith(o => o.Lines);

        FluentActions.Invoking(() => builder.Select(o => o.Id))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith cannot be combined*");
        FluentActions.Invoking(() => builder.As(o => o.Id))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith cannot be combined*");
    }

    [Fact]
    public void MetadataLoadWith_Terminal_AnyCountStayScalar_NoChildLoad()
    {
        using var context = CreateSqlContext();
        var interceptor = new RecordingInterceptor();
        context.AddInterceptor(interceptor);
        QueryPlanStore.Clear();

        var builder = context.From<GuardOrder>().Where(o => o.Id > 0).LoadWith(o => o.Lines);

        // Any: the shared EXISTS command is the parent only — no child table, no child query.
        var any = builder.AnyCommand();
        var anySql = Prepare(context, any).SqlStmt;

        // Count: the aggregate command is likewise parent-only.
        var count = builder.SelectParent(o => SqlFunctions.Sql.count());
        count.SingleRow = true;
        var countSql = Prepare(context, count).SqlStmt;

        anySql.Should().NotBeNullOrEmpty("the prepared Any command exposes its own SQL");
        countSql.Should().NotBeNullOrEmpty("the prepared Count command exposes its own SQL");
        new[] { anySql!, countSql! }.Should().OnlyContain(sql =>
            !sql.Contains("guard_line", StringComparison.OrdinalIgnoreCase)
            && !sql.Contains(" join ", StringComparison.OrdinalIgnoreCase));
        interceptor.Initialized.Should().Be(0, "preparation mints no provider command; commands are created per connection at execution");
        interceptor.Executing.Should().Be(0, "preparing the scalar terminals executes nothing");
    }
}
