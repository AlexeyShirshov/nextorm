using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteStep5ProbeTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private const string Ddl = """
        create table if not exists s5_probe (id integer primary key, name text null, age int not null, flag int not null default 0);
        delete from s5_probe;
        insert into s5_probe (id, name, age, flag) values (1, 'a', 10, 1), (2, 'b', 20, 0), (3, 'c', 30, 1);
        """;

    public SqliteStep5ProbeTests()
    {
        var dc = (DataContext)Context;
        dc.EnsureConnectionOpen();
        using var cmd = dc.GetConnection().CreateCommand();
        cmd.CommandText = Ddl;
        cmd.ExecuteNonQuery();
    }

    /// <summary>P3 — SelectGroupExpressionComparer: two SelectGroup queries (expected U, no flip).</summary>
    [Fact]
    public void P3_SelectGroup_TwoGroups_ShouldExecute()
    {
        var r = Context.From<S5ProbeRow>()
            .SelectGroup(x => new { x.Flag, Cnt = SqlFunctions.Sql.count() })
            .ToList();
        r.Should().NotBeEmpty();
    }

    // P10 (ScanUnkeyedScalarInFrom / ScanUnkeyedTupleInFrom) is NOT probed here: the guard is
    // reached only through a FROM source that carries a rendered subquery with an unkeyed IN list,
    // and the public entry for that shape was not established in this step. Recorded as a barrier
    // in the campaign status instead of a test that would exercise a different arm.

    /// <summary>S2 — multi-level correlated subquery with predicate in nested Any/Where.</summary>
    [Fact]
    public void S2_MultiLevelCorrelation_ShouldExecute()
    {
        var r = Context.From<S5ProbeRow>()
            .Where(x => Context.From<S5ProbeRow>()
                .Where(y => y.Age > x.Age)
                .Any())
            .Select(x => x.Id)
            .ToList();
        r.Should().NotBeNull();
    }

    /// <summary>S2 — member access through navigation-like scalar in correlated subquery.</summary>
    [Fact]
    public void S2_CorrelatedMemberAccess_ShouldExecute()
    {
        var bound = 15;
        var r = Context.From<S5ProbeRow>()
            .Where(x => x.Age > bound && Context.From<S5ProbeRow>().Where(y => y.Id == x.Id).Any())
            .Select(x => x.Id)
            .ToList();
        r.Should().NotBeNull();
    }

    /// <summary>S4 — captured bound slot value in projection (MemberTranslator.TryRenderBoundSlotValue).</summary>
    [Fact]
    public void S4_BoundSlotValue_ShouldExecute()
    {
        var offset = 5;
        var r = Context.From<S5ProbeRow>()
            .Select(x => new { x.Id, Shifted = x.Age + offset })
            .ToList();
        r.Should().NotBeEmpty();
        r.First().Shifted.Should().Be(15);
    }

    /// <summary>S1 — collapsed switch arms via type conversions and casts.</summary>
    [Fact]
    public void S1_TypeConversions_ShouldExecute()
    {
        var r = Context.From<S5ProbeRow>()
            .Select(x => new
            {
                AsDouble = (double)x.Age
            })
            .ToList();
        r.Should().NotBeEmpty();
        r.First().AsDouble.Should().Be(10d);
    }

    /// <summary>Marker entity for the probe table.</summary>
    [SqlTable("s5_probe")]
    public sealed class S5ProbeRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("age")]
        public int Age { get; set; }
        [Column("flag")]
        public int Flag { get; set; }
    }
}