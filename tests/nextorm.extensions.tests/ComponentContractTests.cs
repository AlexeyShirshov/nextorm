using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

[Collection("Query plan cache")]
public class ComponentContractTests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ResultDto
    {
        public long Id { get; set; }
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

    private static SqliteDataContext Ctx() => new("Data Source=:memory:", new DataContextBuilder());

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).SqlStmt!.Replace("\r\n", "\n");

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

    // A1: Min->Max, Max->Min replace in place
    [Fact]
    public void A1_MinToMax_ReplacesInPlace_AndSqlUsesDesc()
    {
        using var ctx = Ctx();
        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score);
        var b2 = ExtremeRowExtensions.SelectWhereMax(b, x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);
        cmd.Components![0].Slot.Should().Be(typeof(ExtremeRowComponent<Entity>));

        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("desc");
    }

    [Fact]
    public void A1_MaxToMin_ReplacesInPlace_AndSqlHasNoDesc()
    {
        using var ctx = Ctx();
        var b = ExtremeRowExtensions.SelectWhereMax(ctx.From<Entity>(), x => x.Score);
        var b2 = ExtremeRowExtensions.SelectWhereMin(b, x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);
        cmd.Components![0].Slot.Should().Be(typeof(ExtremeRowComponent<Entity>));

        var sql = SqlOf(ctx, cmd);
        sql.Should().NotContain(" desc");
    }

    // A2: conflicting exclusive reshape component rejected at preparation
    [Fact]
    public void A2_TwoDistinctExclusiveReshapeComponents_ThrowPreparationException()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null))
            .WithComponent(new ConflictingReshapeComponent<Entity>());

        QueryCommand<Entity> cmd = b;
        Action act = () => _ = SqlOf(ctx, cmd);
        act.Should().Throw<Exception>()
            .Which.Message.Should().Contain("Multiple distinct exclusive row-source reshape");
    }

    // A3: additive predicate composes
    [Fact]
    public void A3_AdditivePredicate_ComposesInsideInnerSource()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new AdditivePredicateComponent<Entity>((Expression<Func<Entity, bool>>)(x => x.Score > 5)))
            .WithComponent(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));

        QueryCommand<Entity> cmd = b;
        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("Score > 5");
    }

    // two independent components compose: additive predicate fixture + extreme row
    [Fact]
    public void TwoComponents_Compose_AdditivePredicate_Plus_ExtremeRow()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new AdditivePredicateComponent<Entity>((Expression<Func<Entity, bool>>)(x => x.Score > 5)))
            .WithComponent(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));

        QueryCommand<Entity> cmd = b;
        cmd.Components.Should().HaveCount(2);
        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("row_number()");
    }

    // The public untyped constructor rejects a selector written over another entity instead of
    // failing deep inside the parameter replacer later.
    [Fact]
    public void Ctor_RejectsSelectorOverAnotherEntity()
    {
        Action act = () => _ = new ExtremeRowComponent<Entity>(
            ExtremeKind.Min,
            (Expression<Func<ResultDto, long>>)(x => x.Id),
            ExtremeRowTies.One,
            null);

        act.Should().Throw<ArgumentException>().WithParameterName("valueSelector");
    }

    // B4: hash collision discriminated by StructurallyEquals
    private sealed class TestComponent : IQueryComponent
    {
        public int State { get; }
        public TestComponent(int state) => State = state;
        public Type Slot => typeof(TestComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
            => new TestDescriptor(State);
        public void BuildQuery(IQueryComponentBuildContext context) { }
    }

    private sealed class TestDescriptor : IQueryComponentDescriptor
    {
        public int State { get; }
        public TestDescriptor(int state) => State = state;
        public Type Slot => typeof(TestComponent);
        public Type ComponentType => typeof(TestComponent);
        public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
        public string Discriminator => "Test";
        public int StructuralHash => 0; // forced collision
        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is TestDescriptor rhs && State == rhs.State;
    }

    [Fact]
    public void B4_HashCollision_DiscriminatedByEquals()
    {
        using var ctx = Ctx();
        var b1 = ctx.From<Entity>().WithComponent(new TestComponent(1));
        var b2 = ctx.From<Entity>().WithComponent(new TestComponent(2));

        QueryCommand<Entity> c1 = b1;
        QueryCommand<Entity> c2 = b2;
        c1.PrepareCommand(CancellationToken.None);
        c2.PrepareCommand(CancellationToken.None);

        var comparer = c1.GetQueryPlanEqualityComparer();
        comparer.GetHashCode(c1).Should().Be(comparer.GetHashCode(c2));
        comparer.Equals(c1, c2).Should().BeFalse();
    }

    [Fact]
    public void B4_EqualState_SharesKey()
    {
        using var ctx = Ctx();
        var b1 = ctx.From<Entity>().WithComponent(new TestComponent(7));
        var b2 = ctx.From<Entity>().WithComponent(new TestComponent(7));

        QueryCommand<Entity> c1 = b1;
        QueryCommand<Entity> c2 = b2;
        c1.PrepareCommand(CancellationToken.None);
        c2.PrepareCommand(CancellationToken.None);

        var comparer = c1.GetQueryPlanEqualityComparer();
        comparer.Equals(c1, c2).Should().BeTrue();
    }

    // B5: clones carry components and prepared state
    [Fact]
    public void B5_Clones_CarryComponentsAndPreparedState()
    {
        using var ctx = Ctx();
        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score);
        QueryCommand<Entity> cmd = b;
        _ = SqlOf(ctx, cmd); // prepare

        var clone = cmd.Clone();
        clone.Components.Should().HaveCount(1);
        clone.PreparedComponents.Should().NotBeNull();

        var cacheClone = cmd.CloneForCache();
        cacheClone.Components.Should().HaveCount(1);
        cacheClone.PreparedComponents.Should().NotBeNull();

        cmd.ResetPreparation();
        cmd.PreparedComponents.Should().BeNull();

        var sqlAfter = SqlOf(ctx, cmd);
        sqlAfter.Should().Contain("row_number()");
    }

    [Fact]
    public void B5_LiveCommandKeepsOriginalSourceAfterPrepare()
    {
        using var ctx = Ctx();
        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score);
        var cmd = (QueryCommand<Entity>)b;
        cmd.Components.Should().HaveCount(1);

        _ = SqlOf(ctx, cmd);

        cmd.Components.Should().HaveCount(1);
        cmd.From.Should().NotBeNull();
        cmd.From!.SubQuery.Should().BeNull();
    }

    // B6: warm hit reuses the cached plan instead of rebuilding the component contribution
    [Fact]
    public void B6_WarmHit_ReusesThePreparedPlan()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score);
        QueryCommand<Entity> cmd = b;
        var prepared = Prepare(ctx, cmd);
        var r1 = ctx.ToList(prepared);
        r1.Should().ContainSingle().Which.Score.Should().Be(10);

        var warm = Prepare(ctx, cmd);
        // A warm hit returns the cached plan: the SQL text instance is the one built on the miss, so
        // the component's BuildQuery did not run again. (The planner may hand out a different
        // command wrapper when the process-wide plan store is shared, hence the SQL identity.)
        warm.SqlStmt.Should().BeSameAs(prepared.SqlStmt, "a confirmed warm hit must not rebuild the SQL");

        var r2 = ctx.ToList(warm);
        r2.Select(x => x.Id).Should().Equal(r1.Select(x => x.Id));
    }

    // B7: parameter rebinding captured and positional, no leak
    [Fact]
    public void B7_ParameterRebinding_CapturedAndPositional_NoLeak()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT); INSERT INTO Entity (Id, Score, Name) VALUES (1,10,'a'),(2,20,'b'),(3,30,'c');";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        int threshold = 0;
        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>().Where(x => x.Score > threshold), x => x.Score);
        QueryCommand<Entity> cmd = b;

        threshold = 10;
        var prepared1 = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var r1 = ctx.ToList(prepared1);
        r1.Should().ContainSingle(x => x.Score == 20);

        threshold = 25;
        var prepared2 = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var r2 = ctx.ToList(prepared2);
        r2.Should().ContainSingle(x => x.Score == 30);
    }

    [Fact]
    public void B7_PositionalParameter_RebindsOnWarmHit()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT); INSERT INTO Entity (Id, Score, Name) VALUES (1,10,'a'),(2,20,'b'),(3,30,'c');";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>().Where(x => x.Score > SqlFunctions.Parameter<int>(0)), x => x.Score);
        var cmd = (QueryCommand<Entity>)b;
        var prepared = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var r1 = ctx.ToList(prepared, new object[] { 10 });
        r1.Should().ContainSingle(x => x.Score == 20);

        var r2 = ctx.ToList(prepared, new object[] { 25 });
        r2.Should().ContainSingle(x => x.Score == 30);
    }

    // C8: result type preservation, hidden rank absent
    [Fact]
    public void C8_ResultTypePreservation_HiddenRankAbsent()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT); INSERT INTO Entity (Id, Score, Name) VALUES (1,10,'a'),(2,20,'b');";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score, x => new ResultDto { Id = x.Id, Name = x.Name });
        QueryCommand<ResultDto> cmd = b;
        var sql = SqlOf(ctx, cmd);
        var outerSelect = sql[..sql.IndexOf("from (", StringComparison.Ordinal)];
        outerSelect.Should().Contain("Id, Name");
        outerSelect.Should().NotContain("__nextorm_ext_rn");

        var prepared = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var result = ctx.ToList(prepared);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("a");
    }

    // C9: hidden alias collision no leak, user column values preserved
    [Fact]
    public void C9_HiddenAliasCollision_NoLeak()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE AliasEntity (Id INTEGER PRIMARY KEY, Score INTEGER, rn INTEGER, rank INTEGER, __nextorm_ext_rn INTEGER); INSERT INTO AliasEntity (Id, Score, rn, rank, __nextorm_ext_rn) VALUES (1,10,7,8,9);";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<AliasEntity>(), x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn });
        var sql = SqlOf(ctx, b);
        sql.Should().Contain("__nextorm_ext_rn_");
    }

    [Fact]
    public void R2_HiddenAliasCollision_UserColumnValuesPreserved()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE AliasEntity (Id INTEGER PRIMARY KEY, Score INTEGER, rn INTEGER, rank INTEGER, __nextorm_ext_rn INTEGER); INSERT INTO AliasEntity (Id, Score, rn, rank, __nextorm_ext_rn) VALUES (1,10,7,8,9);";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        var rows = ExtremeRowExtensions.SelectWhereMin(ctx.From<AliasEntity>()
, x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].rn.Should().Be(7);
        rows[0].rank.Should().Be(8);
        rows[0].__nextorm_ext_rn.Should().Be(9);
    }

    // C10: in-memory throws
    [Fact]
    public void C10_InMemory_ThrowsNotSupported()
    {
        using var ctx = new NextORM.Core.InMemoryDataContext();
        var b = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score);
        Action act = () => _ = b.ToList();
        act.Should().Throw<NotSupportedException>();
    }

    // DISTINCT is expressible on top of the reshape: the derived source already yields distinct winning
    // rows, so the outer DISTINCT must not change the set (and must not be rejected).
    [Fact]
    public void R1_Distinct_IsAcceptedAndKeepsTheWinningRows()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var plain = ctx.From<Entity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All)
            .Select(x => x.Id)
            .ToList()
            .OrderBy(x => x)
            .ToArray();

        var distinct = ctx.From<Entity>()
            .SelectWhereMin(x => x.Score, ExtremeRowTies.All)
            .Distinct()
            .Select(x => x.Id)
            .ToList()
            .OrderBy(x => x)
            .ToArray();

        plain.Should().NotBeEmpty("otherwise the comparison below is vacuous");
        distinct.Should().Equal(plain);
    }

    // Warm hit assertions for the product component: the cached prepared plan is reused and the
    // rebuilt SQL is never regenerated, so BuildQuery cannot have run again. No product counter.
    [Fact]
    public void R3_CapturedWarmHit_ReusesThePreparedPlan()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 0;
        var command = (QueryCommand<Entity>)ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>()
            .Where(x => x.Score > threshold)
, x => x.Score);

        var prepared = Prepare(ctx, command);
        ctx.ToList(prepared);

        threshold = 10;
        var warm1 = Prepare(ctx, command);
        warm1.SqlStmt.Should().BeSameAs(prepared.SqlStmt, "a warm hit must reuse the built SQL");
        ctx.ToList(warm1).Should().ContainSingle().Which.Score.Should().Be(20);

        threshold = 19;
        var warm2 = Prepare(ctx, command);
        warm2.SqlStmt.Should().BeSameAs(prepared.SqlStmt, "a second warm hit must reuse the same built SQL");
        ctx.ToList(warm2).Should().ContainSingle().Which.Score.Should().Be(20);
    }

    [Fact]
    public void R4_PositionalWarmHit_ReusesThePreparedPlan()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>()
            .Where(x => x.Score > SqlFunctions.Parameter<int>(0))
, x => x.Score);

        var prepared = Prepare(ctx, command);
        var builtSql = prepared.SqlStmt;

        var r1 = ctx.ToList(prepared, new object[] { 10 });
        r1.Should().ContainSingle(x => x.Score == 20);

        var r2 = ctx.ToList(prepared, new object[] { 19 });
        r2.Should().ContainSingle(x => x.Score == 20);

        prepared.SqlStmt.Should().Be(
            builtSql, "the parameter-refresh pass must reuse the built shape instead of rerunning BuildQuery");
    }

    // The engine contract AC4 states, asserted on a test-local component that counts its own
    // BuildQuery calls: exactly 1 on cold miss, 0 on a confirmed warm hit (captured and positional).
    [Fact]
    public void BuildQueryCounter_ColdMissOne_WarmHitZero_CapturedAndPositional()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 0;
        var counting = new CountingComponent<Entity>(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));
        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > threshold)
            .WithComponent(counting);

        Prepare(ctx, command);
        counting.BuildCount.Should().Be(1, "cold miss must call BuildQuery exactly once");

        threshold = 10;
        Prepare(ctx, command);
        counting.BuildCount.Should().Be(1, "warm hit must not call BuildQuery");

        // positional
        var positional = new CountingComponent<Entity>(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));
        var command2 = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > SqlFunctions.Parameter<int>(0))
            .WithComponent(positional);

        var p = Prepare(ctx, command2);
        positional.BuildCount.Should().Be(1, "cold miss must call BuildQuery exactly once");
        ctx.ToList(p, new object[] { 5 });
        Prepare(ctx, command2);
        positional.BuildCount.Should().Be(1);
        ctx.ToList(p, new object[] { 15 });
        positional.BuildCount.Should().Be(1, "a warm positional execution must not call BuildQuery again");
    }

    [Fact]
    public void NoComponentRegression_OrdinaryQueryKeepsItsShapeAndResults()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>().Where(x => x.Score > 10);
        var sql = SqlOf(ctx, command);
        sql.Should().NotContain("row_number()");
        sql.Should().NotContain("__nextorm_ext_");
        ctx.ToList(Prepare(ctx, command)).Select(x => x.Id).Should().Equal(3L, 5L);
    }

    [Fact]
    public void NoComponentRegression_OrdinaryCountRemainsUsable()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        ctx.From<Entity>().Where(x => x.Score != null).Count().Should().Be(4);
    }

    // value leakage check between captured calls
    [Fact]
    public void CapturedValue_NoLeakage_BetweenCalls()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 5;
        var command = (QueryCommand<Entity>)ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>()
            .Where(x => x.Score > threshold)
, x => x.Score);

        threshold = 10;
        var p1 = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        var r1 = ctx.ToList(p1);
        foreach (var r in r1) r.Score.Should().BeGreaterThan(10);

        threshold = 25;
        var p2 = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        var r2 = ctx.ToList(p2);
        foreach (var r in r2) r.Score.Should().BeGreaterThan(25);
    }

    // Clone/CloneForCache/ResetPreparation carry component state (already B5) - duplicate assertion for ExtremeRowComponent
    [Fact]
    public void Clone_CarriesComponentState_ExtremeRow()
    {
        using var ctx = Ctx();
        var b = ExtremeRowExtensions.SelectWhereMax(ctx.From<Entity>(), x => x.Score);
        QueryCommand<Entity> cmd = b;
        _ = SqlOf(ctx, cmd);

        var clone = cmd.Clone();
        clone.Components.Should().HaveCount(1);
        clone.PreparedComponents.Should().NotBeNull();

        var cacheClone = cmd.CloneForCache();
        cacheClone.Components.Should().HaveCount(1);
        cacheClone.PreparedComponents.Should().NotBeNull();

        cmd.ResetPreparation();
        cmd.PreparedComponents.Should().BeNull();
    }

    // foreign exclusive-row-source component in different slot rejected at preparation
    [Fact]
    public void ForeignExclusiveSlot_RejectedAtPreparation()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new ExtremeRowComponent<Entity>(ExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null))
            .WithComponent(new ConflictingReshapeComponent<Entity>());

        QueryCommand<Entity> cmd = b;
        Action act = () => _ = Prepare(ctx, cmd);
        act.Should().Throw<Exception>();
    }

    // rejection boundaries: aggregates rejected explicitly
    [Fact]
    public void Aggregates_RejectedExplicitly()
    {
        using var ctx = Ctx();
        Action external = () => _ = ExtremeRowExtensions.SelectWhereMin(ctx.From<Entity>(), x => x.Score).Count();
        external.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("aggregates");
    }

    [Fact]
    public void SetOperation_RejectedForExtremeRow_WithReason()
    {
        using var ctx = Ctx();
        var left = (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMin(x => x.Score);
        var right = (QueryCommand<Entity>)ctx.From<Entity>().SelectWhereMin(x => x.Score);
        Action act = () => _ = left.Union(right).ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("set operation");
    }


    // Distinct parity already R1; unsupported selector shapes -> handled by compile; skip
}
