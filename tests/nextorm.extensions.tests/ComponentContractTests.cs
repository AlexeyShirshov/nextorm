using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

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
        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score);
        var b2 = b.SelectWhereMaxEx(x => x.Score);

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
        var b = ctx.From<Entity>().SelectWhereMaxEx(x => x.Score);
        var b2 = b.SelectWhereMinEx(x => x.Score);

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
        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score);
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
        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score);
        var cmd = (QueryCommand<Entity>)b;
        cmd.Components.Should().HaveCount(1);

        _ = SqlOf(ctx, cmd);

        cmd.Components.Should().HaveCount(1);
        cmd.From.Should().NotBeNull();
        cmd.From!.SubQuery.Should().BeNull();
    }

    // B6: warm hit does not rebuild
    [Fact]
    public void B6_WarmHit_DoesNotRebuild()
    {
        ExtremeRowComponent<Entity>.ResetBuildCount();
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT);";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score);
        ExtremeRowComponent<Entity>.ResetBuildCount();
        QueryCommand<Entity> cmd = b;
        var prepared = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var r1 = ctx.ToList(prepared);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1);

        var r2 = ctx.ToList(prepared);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1);
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
        var b = ctx.From<Entity>().Where(x => x.Score > threshold).SelectWhereMinEx(x => x.Score);
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

        var b = ctx.From<Entity>().Where(x => x.Score > SqlFunctions.Parameter<int>(0)).SelectWhereMinEx(x => x.Score);
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

        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score, x => new ResultDto { Id = x.Id, Name = x.Name });
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

        var b = ctx.From<AliasEntity>().SelectWhereMinEx(x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn });
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

        var rows = ctx.From<AliasEntity>()
            .SelectWhereMinEx(x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn })
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
        var b = ctx.From<Entity>().SelectWhereMinEx(x => x.Score);
        Action act = () => _ = b.ToList();
        act.Should().Throw<NotSupportedException>();
    }

    // Distinct parity executed semantics
    [Fact]
    public void R1_Distinct_ExecutedSemantics_MatchBuiltIn()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        Exception? builtError = null;
        Exception? externalError = null;
        long[] builtIds = [];
        long[] externalIds = [];

        try
        {
            builtIds = ctx.From<Entity>()
                .SelectWhereMin(x => x.Score, ExtremeRowTies.All)
                .Distinct()
                .Select(x => x.Id)
                .ToList()
                .OrderBy(x => x)
                .ToArray();
        }
        catch (Exception ex)
        {
            builtError = ex;
        }

        try
        {
            externalIds = ctx.From<Entity>()
                .SelectWhereMinEx(x => x.Score, ExtremeRowTies.All)
                .Distinct()
                .Select(x => x.Id)
                .ToList()
                .OrderBy(x => x)
                .ToArray();
        }
        catch (Exception ex)
        {
            externalError = ex;
        }

        (externalError is null).Should().Be(builtError is null, "Distinct must be accepted or rejected by both arms");
        if (builtError is null)
        {
            builtIds.Should().NotBeEmpty("the Distinct arm must return rows, otherwise the comparison is vacuous");
            externalIds.Should().Equal(builtIds);
        }
        else
        {
            externalError!.GetType().Should().Be(builtError.GetType());
        }
    }

    // Warm hit captured/positional counter assertions (BuildQuery exactly 0 on warm hit)
    [Fact]
    public void R3_CapturedWarmHit_DoesNotRebuild()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 0;
        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => x.Score);

        ExtremeRowComponent<Entity>.ResetBuildCount();
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ctx.ToList(prepared);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1);

        threshold = 10;
        ctx.ToList(prepared);
        threshold = 19;
        ctx.ToList(prepared);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1, "a warm hit must not call BuildQuery");
    }

    [Fact]
    public void R4_PositionalWarmHit_DoesNotRebuild()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > SqlFunctions.Parameter<int>(0))
            .SelectWhereMinEx(x => x.Score);

        ExtremeRowComponent<Entity>.ResetBuildCount();
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1);

        var r1 = ctx.ToList(prepared, new object[] { 10 });
        var r2 = ctx.ToList(prepared, new object[] { 19 });
        r1.Should().ContainSingle(x => x.Score == 20);
        r2.Should().ContainSingle(x => x.Score == 20);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(
            1, "the parameter-refresh pass must reuse the built shape instead of rerunning BuildQuery");
    }

    // BuildQuery counter: exactly 1 on cold miss, 0 on confirmed warm hit (captured + positional values changed)
    [Fact]
    public void BuildQueryCounter_ColdMissOne_WarmHitZero_CapturedAndPositional()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 0;
        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => x.Score);

        ExtremeRowComponent<Entity>.ResetBuildCount();
        var prepared = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ctx.ToList(prepared);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1, "cold miss must call BuildQuery exactly once");

        ExtremeRowComponent<Entity>.ResetBuildCount();
        threshold = 10;
        var prepared2 = ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None);
        ctx.ToList(prepared2);
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(0, "warm hit must not call BuildQuery");

        // positional
        var command2 = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > SqlFunctions.Parameter<int>(0))
            .SelectWhereMinEx(x => x.Score);
        ExtremeRowComponent<Entity>.ResetBuildCount();
        var p = ctx.GetPreparedQueryCommand(command2, false, true, CancellationToken.None);
        ctx.ToList(p, new object[] { 5 });
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(1);
        ExtremeRowComponent<Entity>.ResetBuildCount();
        ctx.ToList(p, new object[] { 15 });
        ExtremeRowComponent<Entity>.BuildCount.Should().Be(0);
    }

    // value leakage check between captured calls
    [Fact]
    public void CapturedValue_NoLeakage_BetweenCalls()
    {
        var (conn, ctx) = CreateContext(EntitySeed);
        using var connScope = conn;
        using var ctxScope = ctx;

        int threshold = 5;
        var command = (QueryCommand<Entity>)ctx.From<Entity>()
            .Where(x => x.Score > threshold)
            .SelectWhereMinEx(x => x.Score);

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
        var b = ctx.From<Entity>().SelectWhereMaxEx(x => x.Score);
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
        Action external = () => _ = ctx.From<Entity>().SelectWhereMinEx(x => x.Score).Count();
        external.Should().Throw<BuildSqlCommandException>()
            .Which.Message.Should().Contain("aggregates");
    }


    // Distinct parity already R1; unsupported selector shapes -> handled by compile; skip
}
