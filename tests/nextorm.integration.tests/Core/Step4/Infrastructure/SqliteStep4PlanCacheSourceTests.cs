using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 2 — plan-cache identity of query sources on live SQLite:
/// <c>src/nextorm.core/Expressions/Comparers/FromExpressionPlanEqualityComparer.cs</c> through the cache lookups a
/// repeated query performs (table name, table function, subquery and raw source arms) plus the
/// <c>Prepare()</c>/<c>storeInCache:false</c> contract of <c>DataContext</c>.
/// <para>
/// The comparer is never called directly: equal and unequal shapes are executed twice and the facts assert
/// the computed rows, so a shared plan must still return the right rows and a distinct plan must not leak
/// the earlier statement's values.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4PlanCacheSourceTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_pc2_row (id integer primary key, grp int null, amount int not null);
        delete from s4_pc2_row;
        insert into s4_pc2_row (id, grp, amount) values
            (9201, 1, 10), (9202, 1, 20), (9203, 2, 30);
        """;

    public SqliteStep4PlanCacheSourceTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Two structurally identical table-function sources share one cached plan and keep their rows.</summary>
    [Fact]
    public void TableFunction_SameArgumentsTwice_ShouldReuseThePlanAndReturnRows()
    {
        List<long> Values() => Context.From(() => JsonEach("[1,2,3]"))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        Values().Should().Equal(1L, 2L, 3L);
        Values().Should().Equal(new[] { 1L, 2L, 3L }, "the second execution resolves through the cached plan");
    }

    /// <summary>A different argument produces a different source identity, so the earlier values cannot leak.</summary>
    [Fact]
    public void TableFunction_DifferentArguments_ShouldNotShareThePlan()
    {
        var three = Context.From(() => JsonEach("[1,2,3]"))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        var two = Context.From(() => JsonEach("[7,8]"))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        three.Should().Equal(1L, 2L, 3L);
        two.Should().Equal(7L, 8L);
    }

    /// <summary>A plain table source is compared by table name, with and without a projection change.</summary>
    [Fact]
    public void TableSource_SameNameTwice_ShouldReturnEachShape()
    {
        Context.From<S4PcRow>().OrderBy(x => x.Id).Select(x => x.Id).ToList()
            .Should().Equal(9201, 9202, 9203);

        var filtered = Context.From<S4PcRow>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Amount })
            .ToList();

        filtered.Should().HaveCount(2);
        filtered.Select(r => r.Amount).Should().Equal(10, 20);
    }

    /// <summary>Two derived sources with the same inner shape each return their own rows.</summary>
    [Fact]
    public void DerivedSource_SameShapeTwice_ShouldReturnEachRows()
    {
        List<int> Amounts(int limit) => Context.From(Context.From<S4PcRow>()
                .Where(x => x.Amount <= limit)
                .Select(x => new { x.Id, x.Amount }))
            .OrderBy(x => x.Id)
            .Select(x => x.Amount)
            .ToList();

        Amounts(20).Should().Equal(10, 20);
        Amounts(30).Should().Equal(10, 20, 30);
    }

    /// <summary>A prepared command serves two parameter values; an uncached preparation leaves the cache intact.</summary>
    [Fact]
    public void Prepare_WithUncachedPreparationInterleaved_ShouldReturnEachRows()
    {
        var prepared = Context.From<S4PcRow>()
            .Where(x => x.Id >= SqlFunctions.Parameter<int>(0))
            .Select(x => x.Id)
            .Prepare(new PrepareOptions(), TestContext.Current.CancellationToken);

        Context.ToList(prepared, new object?[] { 9203 }).Should().Equal(9203);
        Context.ToList(prepared, new object?[] { 9202 }).Should().Equal(9202, 9203);

        var uncached = (DbPreparedQueryCommand<int>)((DataContext)Context)
            .GetPreparedQueryCommand(
                Context.From<S4PcRow>().Where(x => x.Grp == 2).Select(x => x.Amount),
                false,
                storeInCache: false,
                CancellationToken.None);

        Context.ToList(uncached).Should().Equal(30);

        Context.From<S4PcRow>().Where(x => x.Grp == 2).Select(x => x.Amount).ToList()
            .Should().Equal(new[] { 30 }, "the shared plan cache still answers the ordinary query");
    }

    private static TvfEntityBuilder<IS4JsonEachRow> JsonEach(string json) => new("json_each");

    /// <summary>Numeric row shape of <c>json_each</c>.</summary>
    public interface IS4JsonEachRow
    {
        [Column("key")]
        long Key { get; set; }

        [Column("value")]
        long Value { get; set; }
    }

    /// <summary>Marker row of the plan-cache facts.</summary>
    [SqlTable("s4_pc2_row")]
    public sealed class S4PcRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("amount")]
        public int Amount { get; set; }
    }
}
