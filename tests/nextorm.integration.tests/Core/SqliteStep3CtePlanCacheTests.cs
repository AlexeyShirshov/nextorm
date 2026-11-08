using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — two high-yield core surfaces left by batch 1 on live SQLite:
/// <list type="bullet">
/// <item>CTE hoisting (<c>src/nextorm.core/Builders/CteHoister.cs</c>): a typed CTE referenced twice by a
/// UNION ALL is declared once, and sibling CTEs joined together keep their declaration order.</item>
/// <item>Plan cache / <c>Prepare()</c> (<c>QueryCommand.QueryPreparer.cs</c> raw-shape capture,
/// <c>DataContext.Cache/**</c>): a prepared command reused with two parameter values, a
/// <c>storeInCache:false</c> preparation that must not poison the context's plan cache, a trivial select
/// followed by a modified form of the same entity, and a global filter observed through both
/// <c>Prepare()</c> and <c>IgnoreFilters()</c>.</item>
/// </list>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3CtePlanCacheTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_pc_row (id integer primary key, grp int null, amount int not null, is_deleted integer not null default 0);
        delete from s3_pc_row;
        insert into s3_pc_row (id, grp, amount, is_deleted) values
            (5101, 1, 10, 0), (5102, 1, 20, 0), (5103, 2, 30, 0), (5104, 2, 40, 1);
        """;

    public SqliteStep3CtePlanCacheTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // CTE hoisting
    // ---------------------------------------------------------------------------------------------

    /// <summary>Two sibling CTEs joined by a raw read keep their order and apply the outer filter.</summary>
    [Fact]
    public void SiblingCtes_JoinedWithFilter_ShouldReturnRows()
    {
        var left = Context.From<S3PcRow>()
            .Where(x => x.Id >= 5101)
            .Select(x => new { x.Id });
        var right = Context.From<S3PcRow>()
            .Where(x => x.Id <= 5102)
            .Select(x => new { x.Id });

        var rows = Context
            .With("s3_pc_left", left)
            .With("s3_pc_right", right)
            .From("s3_pc_left")
            .Join(Context.From("s3_pc_right"), (l, r) => l["id"].AsInt == r["id"].AsInt)
            .Where(p => p.Item1["id"].AsInt > 5101)
            .Select(p => new { Id = p.Item1["id"].AsInt })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(5102);
    }

    // ---------------------------------------------------------------------------------------------
    // Plan cache and Prepare()
    // ---------------------------------------------------------------------------------------------

    /// <summary>One prepared command serves two parameter values and returns each time's rows.</summary>
    [Fact]
    public void Prepare_ReusedWithTwoValues_ShouldReturnEachRows()
    {
        var prepared = Context.From<S3PcRow>()
            .Where(x => x.Id >= SqlFunctions.Parameter<int>(0))
            .Select(x => x.Id)
            .Prepare(new PrepareOptions(), TestContext.Current.CancellationToken);

        Context.ToList(prepared, new object?[] { 5103 }).Should().Equal(5103, 5104);
        Context.ToList(prepared, new object?[] { 5102 }).Should().Equal(5102, 5103, 5104);
    }

    /// <summary>A <c>storeInCache:false</c> preparation leaves the context's shared plan cache intact.</summary>
    [Fact]
    public void StoreInCacheFalse_ShouldNotPoisonTheContextPlanCache()
    {
        var query = Context.From<S3PcRow>()
            .Where(x => x.Id >= 5102)
            .Select(x => x.Id);

        var uncached = (DbPreparedQueryCommand<int>)((DataContext)Context)
            .GetPreparedQueryCommand(query, false, storeInCache: false, CancellationToken.None);

        uncached.SqlStmt.Should().NotBeNullOrEmpty("the plan was rendered without being stored");
        Context.ToList(uncached).Should().Equal(5102, 5103, 5104);

        var first = Context.From<S3PcRow>().Where(x => x.Id >= 5102).Select(x => x.Id).ToList();
        first.Should().Equal(new[] { 5102, 5103, 5104 }, "the shared plan cache still answers the ordinary query correctly");

        var second = Context.From<S3PcRow>().Where(x => x.Id >= 5102).Select(x => x.Id).ToList();
        second.Should().Equal(new[] { 5102, 5103, 5104 }, "the second (cache-hit) execution agrees");
    }

    /// <summary>A trivial entity select and a modified form of the same plan both return their rows.</summary>
    [Fact]
    public void TrivialSelectThenModifiedForm_ShouldBothReturnRows()
    {
        Context.From<S3PcRow>().ToList().Should().HaveCount(4);

        var filtered = Context.From<S3PcRow>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Grp })
            .ToList();

        filtered.Should().HaveCount(2);
        filtered.Select(r => r.Id).Should().Equal(5101, 5102);
    }

    /// <summary>A global filter applies to a prepared query and <c>IgnoreFilters()</c> lifts it.</summary>
    [Fact]
    public void GlobalFilter_PreparedQueryVsIgnoreFilters_ShouldDiffer()
    {
        var prepared = Context.From<S3PcFilteredRow>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Prepare(new PrepareOptions(), TestContext.Current.CancellationToken);

        Context.ToList(prepared).Should().Equal(5101, 5102, 5103);

        Context.From<S3PcFilteredRow>()
            .IgnoreFilters()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(5101, 5102, 5103, 5104);
    }

    /// <summary>Marker table of the CTE and plan-cache facts.</summary>
    [SqlTable("s3_pc_row")]
    public sealed class S3PcRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("amount")]
        public int Amount { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }

    /// <summary>The same table behind an anonymous soft-delete filter.</summary>
    [SqlTable("s3_pc_row")]
    [QueryFilter(FilterLambda = nameof(ActiveOnly))]
    public sealed class S3PcFilteredRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("amount")]
        public int Amount { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        public static Expression<Func<S3PcFilteredRow, bool>> ActiveOnly() => row => !row.IsDeleted;
    }
}
