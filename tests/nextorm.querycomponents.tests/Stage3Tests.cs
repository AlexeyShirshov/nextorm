using System.Reflection;
using FluentAssertions;
using System.Linq.Expressions;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.QueryComponents.Tests;

public class Stage3Tests
{
    private sealed class Entity
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
    }

    private static SqliteDataContext Ctx() => new("Data Source=:memory:", new DataContextBuilder());

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).SqlStmt!.Replace("\r\n", "\n");

    [Fact]
    public void A1_MinToMax_ReplacesInPlace_AndSqlUsesDesc()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        var b2 = b.ExternalSelectWhereMax(x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);

        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("desc");
    }

    [Fact]
    public void A1_MaxToMin_ReplacesInPlace_AndSqlHasNoDesc()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>().ExternalSelectWhereMax(x => x.Score);
        var b2 = b.ExternalSelectWhereMin(x => x.Score);

        QueryCommand<Entity> cmd = b2;
        cmd.Components.Should().HaveCount(1);

        var sql = SqlOf(ctx, cmd);
        sql.Should().NotContain(" desc");
    }

    // Chunk A2
    [Fact]
    public void A2_TwoDistinctExclusiveReshapeComponents_ThrowPreparationExceptionNamingSlots()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new ExternalSelectWhereComponent<Entity>(ExternalExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null))
            .WithComponent(new ConflictingReshapeComponent<Entity>());

        QueryCommand<Entity> cmd = b;
        Action act = () => _ = SqlOf(ctx, cmd);
        act.Should().Throw<Exception>()
            .Which.Message.Should().Contain("Multiple distinct exclusive row-source reshape");
    }

    // Chunk A3
    [Fact]
    public void A3_AdditivePredicate_ComposesInsideInnerSource()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>()
            .WithComponent(new AdditivePredicateComponent<Entity>((Expression<Func<Entity, bool>>)(x => x.Score > 5)))
            .WithComponent(new ExternalSelectWhereComponent<Entity>(ExternalExtremeKind.Min, (Expression<Func<Entity, int?>>)(x => x.Score), ExtremeRowTies.One, null));

        QueryCommand<Entity> cmd = b;
        var sql = SqlOf(ctx, cmd);
        sql.Should().Contain("Score > 5");
        // predicate appears in the inner derived table, before rank
        var fromIndex = sql.IndexOf("from Entity", StringComparison.Ordinal);
        var predicateIndex = sql.IndexOf("Score > 5", StringComparison.Ordinal);
        var outerIndex = sql.IndexOf(") as 't1'", StringComparison.Ordinal);
        predicateIndex.Should().BeGreaterThan(fromIndex);
        predicateIndex.Should().BeLessThan(outerIndex);
    }


    // Chunk B4
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

    // Chunk B5
    [Fact]
    public void B5_Clones_CarryComponentsAndPreparedState()
    {
        using var ctx = Ctx();
        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
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
        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        var cmd = (QueryCommand<Entity>)b;
        cmd.Components.Should().HaveCount(1);

        _ = SqlOf(ctx, cmd);

        cmd.Components.Should().HaveCount(1);
        cmd.From.Should().NotBeNull();
        cmd.From!.SubQuery.Should().BeNull();
    }

    // Chunk B6
    [Fact]
    public void B6_WarmHit_DoesNotRebuild()
    {
        ExternalSelectWhereComponent<Entity>.ResetBuildCount();
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT);";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());
        ctx.PurgeQueryCache();

        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        ExternalSelectWhereComponent<Entity>.ResetBuildCount();
        QueryCommand<Entity> cmd = b;
        var prepared = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var r1 = ctx.ToList(prepared);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(1);

        var r2 = ctx.ToList(prepared);
        ExternalSelectWhereComponent<Entity>.BuildCount.Should().Be(1);
    }

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
        var b = ctx.From<Entity>().Where(x => x.Score > threshold).ExternalSelectWhereMin(x => x.Score);
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

        var b = ctx.From<Entity>().Where(x => x.Score > SqlFunctions.Parameter<int>(0)).ExternalSelectWhereMin(x => x.Score);
        var cmd = (QueryCommand<Entity>)b;
        var prepared = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var r1 = ctx.ToList(prepared, new object[] { 10 });
        r1.Should().ContainSingle(x => x.Score == 20);

        var r2 = ctx.ToList(prepared, new object[] { 25 });
        r2.Should().ContainSingle(x => x.Score == 30);
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

    [Fact]
    public void C8_ResultTypePreservation_HiddenRankAbsent()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE Entity (Id INTEGER PRIMARY KEY, Score INTEGER, Name TEXT); INSERT INTO Entity (Id, Score, Name) VALUES (1,10,'a'),(2,20,'b');";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score, x => new ResultDto { Id = x.Id, Name = x.Name });
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

    // Chunk C9
    [Fact]
    public void C9_HiddenAliasCollision_NoLeak()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd0 = conn.CreateCommand();
        cmd0.CommandText = "CREATE TABLE AliasEntity (Id INTEGER PRIMARY KEY, Score INTEGER, rn INTEGER, rank INTEGER, __nextorm_ext_rn INTEGER); INSERT INTO AliasEntity (Id, Score, rn, rank, __nextorm_ext_rn) VALUES (1,10,7,8,9);";
        cmd0.ExecuteNonQuery();
        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        var b = ctx.From<AliasEntity>().ExternalSelectWhereMin(x => x.Score, x => new { x.Id, x.rn, x.rank, x.__nextorm_ext_rn });
        var sql = SqlOf(ctx, b);
        sql.Should().Contain("__nextorm_ext_rn_");
        sql.Should().Contain("rn");
    }

    // Chunk C10
    [Fact]
    public void C10_InMemory_ThrowsNotSupported()
    {
        using var ctx = new NextORM.Core.InMemoryDataContext();
        var b = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score);
        Action act = () => _ = b.ToList();
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void C10_Distinct_ParityWithBuiltIn()
    {
        using var ctx = Ctx();
        var builtIn = ctx.From<Entity>().SelectWhereMin(x => x.Score).Distinct();
        var external = ctx.From<Entity>().ExternalSelectWhereMin(x => x.Score).Distinct();

        Action a1 = () => _ = SqlOf(ctx, (QueryCommand<Entity>)builtIn);
        Action a2 = () => _ = SqlOf(ctx, (QueryCommand<Entity>)external);

        // both should throw (or both should not); assert same outcome
        var t1 = Record.Exception(a1);
        var t2 = Record.Exception(a2);
        (t1 is null).Should().Be(t2 is null);
        if (t1 is not null)
            t2.Should().BeOfType(t1.GetType());
    }
}
