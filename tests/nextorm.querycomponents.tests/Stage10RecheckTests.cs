using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

/// <summary>
/// Attempt-2 recheck assertions required by the CHECK report: real Distinct semantics, user values of
/// colliding hidden-alias columns, warm-hit <c>BuildQuery</c> counters under captured and positional
/// rebinding (including the parameter-refresh fallback), public mutation / equality-hash edge cases and
/// the built-in rejection boundaries the external operator used to miss.
/// </summary>
public class Stage10RecheckTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    private sealed class AliasEntity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public int rn { get; set; }
        public int rank { get; set; }
        public int __nextorm_ext_rn { get; set; }
    }

    private static (SqliteConnection Connection, SqliteDataContext Context) CreateContext(string seed)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = seed;
        seedCommand.ExecuteNonQuery();
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        ctx.PurgeQueryCache();
        return (connection, ctx);
    }

    private const string EntitySeed =
        "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER NULL, Name TEXT NULL);" +
        "INSERT INTO Entity (Id, Score, Name) VALUES (1,10,'a'),(2,10,'b'),(3,20,'c'),(4,NULL,'d'),(5,20,'e');";

    private const string AliasSeed =
        "CREATE TABLE AliasEntity (Id INTEGER PRIMARY KEY, Score INTEGER NULL, rn INTEGER, rank INTEGER, __nextorm_ext_rn INTEGER);" +
        "INSERT INTO AliasEntity (Id, Score, rn, rank, __nextorm_ext_rn) VALUES (1,10,7,8,9);";

    // ---- R1: Distinct is executed, not merely compared by "throws or not". ----------------------
    // EntitySeed scores are 10,10,20,NULL,20, so the global minimum 10 is tied between ids 1 and 2.
    [Fact]
    public void R1_Distinct_IsAcceptedAndKeepsTheWinningRows()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var distinctIds = ctx.From<Entity>()
            .ExternalSelectWhereMin(x => x.Score, ExtremeRowTies.All)
            .Distinct()
            .Select(x => x.Id)
            .ToList()
            .OrderBy(x => x)
            .ToArray();

        distinctIds.Should().Equal(1, 2);
    }

    // ---- R2: the user's colliding alias columns keep their own values. --------------------------
    [Fact]
    public void R2_HiddenAliasCollision_UserColumnValuesPreserved()
    {
        var (conn, ctx) = CreateContext(AliasSeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var rows = ctx.From<AliasEntity>()
            .ExternalSelectWhereMin(x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].rn.Should().Be(7);
        rows[0].rank.Should().Be(8);
        rows[0].__nextorm_ext_rn.Should().Be(9);
    }

    // ---- R3/R4: warm hits never rebuild, captured and positional. ------------------------------
    [Fact]
    public void R3_CapturedWarmHit_DoesNotRebuild()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 0;
        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > threshold)
            .ExternalSelectWhereMin(x => x.Score);

        ExternalSelectWhereComponent<Entity>.ResetBuildCount();
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ctx.ToList(prepared);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(1);

        threshold = 10;
        ctx.ToList(prepared);
        threshold = 19;
        ctx.ToList(prepared);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(1, "a warm hit must not call BuildQuery");
    }

    [Fact]
    public void R4_PositionalWarmHit_DoesNotRebuild()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > SqlFunctions.Parameter<int>(0))
            .ExternalSelectWhereMin(x => x.Score);

        ExternalSelectWhereComponent<Entity>.ResetBuildCount();
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(1);

        var r1 = ctx.ToList(prepared, new object[] { 10 });
        var r2 = ctx.ToList(prepared, new object[] { 19 });
        r1.Should().ContainSingle(x => x.Score == 20);
        r2.Should().ContainSingle(x => x.Score == 20);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(
            1, "the parameter-refresh pass must reuse the built shape instead of rerunning BuildQuery");
    }

    // ---- R5/R6/R7: public mutation and equality/hash edges. ------------------------------------
    private sealed class StateComponent : IQueryComponent
    {
        public StateComponent(int state) => State = state;
        public int State { get; }
        public Type Slot => typeof(StateComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) => new StateDescriptor(State);
        public void BuildQuery(IQueryComponentBuildContext context) { }
    }

    private sealed class StateDescriptor : IQueryComponentDescriptor
    {
        public StateDescriptor(int state) => State = state;
        public int State { get; }
        public Type Slot => typeof(StateComponent);
        public Type ComponentType => typeof(StateComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public string Discriminator => "State";
        public int StructuralHash => State;
        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is StateDescriptor rhs && State == rhs.State;
    }

    [Fact]
    public void R5_EmptyPreparedComponents_EqualsAndHashAgree()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        var a = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(new StateComponent(1));
        var b = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(new StateComponent(2));

        a.PreparedComponents = Array.Empty<IQueryComponentDescriptor>();
        b.PreparedComponents = Array.Empty<IQueryComponentDescriptor>();

        var comparer = a.GetQueryPlanEqualityComparer();
        comparer.Equals(a, b).Should().BeFalse(
            "different raw components are not equal just because both prepared lists are empty");
        comparer.GetHashCode(a).Should().NotBe(comparer.GetHashCode(b));
    }

    [Fact]
    public void R6_ComponentsAssignment_InvalidatesPreparedIdentity()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        _ = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        command.PreparedComponents.Should().NotBeNull();

        command.Components = new IQueryComponent[] { new StateComponent(3) };

        command.PreparedComponents.Should().BeNull(
            "an identity change drops the descriptors computed under the old key");

        // The documented way to re-prepare a mutated command; the next preparation rebuilds descriptors
        // from the new list instead of reusing the stale ones.
        command.ResetPreparation();
        _ = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        command.PreparedComponents.Should().ContainSingle()
            .Which.ComponentType.Should().Be(typeof(StateComponent));
    }

    [Fact]
    public void R7_UnpreparedFallback_EqualsAndHashAgree()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        // No preparation: the comparer falls back to the raw component list and GetHashCode(). Two
        // commands carrying the same component instance share that identity.
        var component = new StateComponent(4);
        var a = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(component);
        var b = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(component);

        var comparer = a.GetQueryPlanEqualityComparer();
        comparer.Equals(a, b).Should().BeTrue();
        comparer.GetHashCode(a).Should().Be(comparer.GetHashCode(b));
    }

    // ---- R8: the two uncovered negative guards. ------------------------------------------------
    private sealed class NullDescriptorComponent : IQueryComponent
    {
        public Type Slot => typeof(NullDescriptorComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) => null!;
        public void BuildQuery(IQueryComponentBuildContext context) { }
    }

    private sealed class WrongSlotComponent : IQueryComponent
    {
        public Type Slot => typeof(WrongSlotComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) => new ForeignSlotDescriptor();
        public void BuildQuery(IQueryComponentBuildContext context) { }
    }

    private sealed class ForeignSlotDescriptor : IQueryComponentDescriptor
    {
        public Type Slot => typeof(StateComponent);
        public Type ComponentType => typeof(WrongSlotComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public string Discriminator => "Foreign";
        public int StructuralHash => 0;
        public bool StructurallyEquals(IQueryComponentDescriptor? other) => false;
    }

    [Fact]
    public void R8_NullDescriptor_IsRejected()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(new NullDescriptorComponent());
        Action act = () => _ = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        act.Should().Throw<BuildSqlCommandException>().Which.Message.Should().Contain("null prepared descriptor");
    }

    [Fact]
    public void R8_DescriptorSlotMismatch_IsRejected()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>().WithComponent(new WrongSlotComponent());
        Action act = () => _ = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        act.Should().Throw<BuildSqlCommandException>().Which.Message.Should().Contain("declared slot");
    }

    // ---- R9: the rejection boundary holds for the external operator. ----------------------------
    [Fact]
    public void R9_RowLock_Temporal_NamedWindow_Paging_AreRejected()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        Action rowLock = () => _ = ctx.From<Entity>().ForUpdate().ExternalSelectWhereMin(x => x.Score).ToList();
        Action temporal = () => _ = ctx.From<Entity>().ForSystemTime(TemporalClause.AsOf(new DateTime(2024, 1, 1)))
            .ExternalSelectWhereMin(x => x.Score).ToList();
        Action namedWindow = () => _ = ctx.From<Entity>().Window("w").ExternalSelectWhereMin(x => x.Score).ToList();
        Action paging = () => _ = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score).Limit(1).ToList();

        rowLock.Should().Throw<BuildSqlCommandException>();
        temporal.Should().Throw<BuildSqlCommandException>();
        namedWindow.Should().Throw<BuildSqlCommandException>();
        paging.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void R9_AggregateTerminal_IsRejectedExplicitly()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        // An aggregate terminal cannot reproduce the reshape: the hidden rank column would be appended to
        // the aggregate select list and evaluated over the reshaped rows, so aggregates are refused.
        Action external = () => _ = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score).Count();
        external.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("aggregates");
    }

}
