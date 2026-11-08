using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 12 core coverage through a real file-based SQLite database: the shared query lifecycle
/// (<c>QueryPreparer</c>) and the entity query builder are exercised through production terminals and
/// asserted on the rows they return, never on internals.
///
/// <para>
/// Scope is core behaviour, not provider-specific SQL: preparation and plan reuse, parameter rebinding,
/// plan invalidation, cache opt-out, filtering, projections, ordering, paging, aggregates, distinct and
/// quantifiers. Everything runs against a temporary SQLite file created and seeded by the class.
/// </para>
/// </summary>
public sealed class CorePreparerAndBuilderSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public CorePreparerAndBuilderSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c12.core.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();
        Seed();
    }

    private void Seed()
    {
        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c12_order (
                id integer primary key,
                customer text null,
                amount real not null,
                qty integer not null,
                deleted_at text null);
            insert into c12_order (id, customer, amount, qty, deleted_at) values
                (1, 'alpha', 10.0, 2, null),
                (2, 'alpha', 20.0, 3, null),
                (3, 'beta',  30.0, 1, null),
                (4, 'beta',  40.0, 5, '2024-01-01'),
                (5, null,    50.0, 4, null);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Preparing the same command twice with the cache on reuses one prepared instance.</summary>
    [Fact]
    public void Preparation_ShouldReuseTheCachedPlanForTheSameShape()
    {
        QueryCommand<int> Build() => _db.From<C12Order>().Where(x => x.Qty >= 3).OrderBy(x => x.Id).Select(x => x.Id);

        var first = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);
        var second = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeTrue("the same shape must resolve to the same cached plan");
        Build().ToList().Should().Equal(2, 4, 5);
    }

    /// <summary>A captured value is a bound parameter: the same plan returns different rows after a rebind.</summary>
    [Fact]
    public void CapturedParameter_ShouldRebindWithoutChangingTheStatement()
    {
        var minimum = 3;
        QueryCommand<int> Query() => _db.From<C12Order>().Where(x => x.Qty >= minimum).OrderBy(x => x.Id).Select(x => x.Id);

        Query().ToList().Should().Equal(2, 4, 5);

        minimum = 5;
        Query().ToList().Should().Equal(4);
    }

    /// <summary>Purging the cache forks the plan identity while the rows stay the same.</summary>
    [Fact]
    public void PurgeQueryCache_ShouldForkThePlanAndKeepTheRows()
    {
        QueryCommand<int> Build() => _db.From<C12Order>().Where(x => x.Amount > 15).OrderBy(x => x.Id).Select(x => x.Id);

        var before = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);
        var rowsBefore = Build().ToList();

        _db.PurgeQueryCache();

        var after = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);

        ReferenceEquals(before, after).Should().BeFalse("a purge drops the cached plan, so the next preparation re-prepares");
        Build().ToList().Should().Equal(rowsBefore);
    }

    /// <summary>With the cache disabled each preparation is independent, and both execute correctly.</summary>
    [Fact]
    public void Preparation_WithTheCacheDisabled_ShouldNotReuseTheInstance()
    {
        QueryCommand<int> Build() => _db.From<C12Order>().Where(x => x.Customer == "alpha").OrderBy(x => x.Id).Select(x => x.Id);

        var first = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: false, CancellationToken.None);
        var second = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: false, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeFalse("storeInCache:false prepares a private command");
        Build().ToList().Should().Equal(1, 2);
    }

    /// <summary>Comparison and boolean composition over the builder surface.</summary>
    [Fact]
    public void Filters_ShouldComposeComparisonsAndBooleans()
    {
        var rows = _db.From<C12Order>()
            .Where(x => (x.Qty > 1 && x.Amount <= 30.0) || x.Id == 5)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(1, 2, 5);
    }

    /// <summary>Null checks are two-valued predicates over the nullable text column.</summary>
    [Fact]
    public void NullChecks_ShouldSelectPresentAndAbsentValues()
    {
        _db.From<C12Order>().Where(x => x.DeletedAt == null).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(1, 2, 3, 5);

        _db.From<C12Order>().Where(x => x.DeletedAt != null).Select(x => x.Id)
            .ToList().Should().Equal(4);

        _db.From<C12Order>().Where(x => x.Customer == null).Select(x => x.Id)
            .ToList().Should().Equal(5);
    }

    /// <summary>String predicates translate to SQLite LIKE and match the expected rows.</summary>
    [Fact]
    public void StringPredicates_ShouldMatchTheExpectedRows()
    {
        _db.From<C12Order>().Where(x => x.Customer!.Contains("lph")).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(1, 2);

        _db.From<C12Order>().Where(x => x.Customer!.StartsWith("be")).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(3, 4);
    }

    /// <summary>Anonymous projection with a computed expression over database columns.</summary>
    [Fact]
    public void Projection_ShouldCarryComputedExpressions()
    {
        var rows = _db.From<C12Order>()
            .Where(x => x.Id <= 2)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Total = x.Amount * x.Qty, Doubled = x.Qty * 2 })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Total.Should().Be(20.0);
        rows[1].Doubled.Should().Be(6);
    }

    /// <summary>Ordering with a descending primary key and an ascending tie-breaker.</summary>
    [Fact]
    public void Ordering_ShouldRespectDirectionAndTieBreaker()
    {
        var rows = _db.From<C12Order>()
            .OrderByDescending(x => x.Customer)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(3, 4, 1, 2, 5);
    }

    /// <summary>Paging composes ordering with skip and take.</summary>
    [Fact]
    public void Paging_ShouldReturnTheRequestedWindow()
    {
        var page = _db.From<C12Order>().OrderBy(x => x.Id).Offset(1).Limit(2).Select(x => x.Id).ToList();

        page.Should().Equal(2, 3);
    }

    /// <summary>Scalar aggregates over the seeded rows.</summary>
    [Fact]
    public void Aggregates_ShouldMatchTheSeededValues()
    {
        _db.From<C12Order>().Count().Should().Be(5);
        _db.From<C12Order>().Where(x => x.Qty > 2).Count().Should().Be(3);
        _db.From<C12Order>().Sum(x => x.Qty).Should().Be(15);
        _db.From<C12Order>().Min(x => x.Amount).Should().Be(10.0);
        _db.From<C12Order>().Max(x => x.Amount).Should().Be(50.0);
        _db.From<C12Order>().Avg(x => x.Qty).Should().Be(3.0);
    }

    /// <summary>Grouped projection over a nullable grouping column.</summary>
    [Fact]
    public void GroupBy_ShouldAggregatePerGroup()
    {
        var groups = _db.From<C12Order>()
            .Where(x => x.Customer != null)
            .GroupBy(x => x.Customer)
            .Select(x => new { Customer = x.Customer, Count = SqlFunctions.Sql.count(), Total = SqlFunctions.Sql.sum(x.Qty) })
            .ToList();

        groups.Should().HaveCount(2);
        groups.Single(g => g.Customer == "alpha").Count.Should().Be(2);
        groups.Single(g => g.Customer == "beta").Total.Should().Be(6);
    }

    /// <summary>Distinct projects the cardinality of the selected column.</summary>
    [Fact]
    public void Distinct_ShouldDeduplicateTheProjection()
    {
        var customers = _db.From<C12Order>().Where(x => x.Customer != null).Select(x => x.Customer).Distinct().ToList();

        customers.Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    /// <summary>Quantifiers over a filtered set.</summary>
    [Fact]
    public void Quantifiers_ShouldAnswerOverTheLiveRows()
    {
        _db.From<C12Order>().Where(x => x.Amount > 45.0).Any().Should().BeTrue();
        _db.From<C12Order>().Where(x => x.Amount > 55.0).Any().Should().BeFalse();
        _db.From<C12Order>().Where(x => x.Qty <= 0).Any().Should().BeFalse("every row has a positive quantity");
        _db.From<C12Order>().Where(x => x.Qty <= 1).Count().Should().Be(1, "exactly one row has qty 1");
    }

    /// <summary>A capture list becomes an IN predicate and keeps working after the list changes.</summary>
    [Fact]
    public void CapturedList_ShouldBindAnInPredicateAndRefresh()
    {
        var ids = new List<int> { 1, 3 };

        _db.From<C12Order>().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(1, 3);

        ids = [2, 4];
        _db.From<C12Order>().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(2, 4);
    }

    /// <summary>The first row of an ordered query and the single row of a keyed query.</summary>
    [Fact]
    public void Terminals_ShouldReturnTheFirstAndTheSingleRow()
    {
        _db.From<C12Order>().OrderBy(x => x.Id).Select(x => x.Id).First().Should().Be(1);
        _db.From<C12Order>().Where(x => x.Id == 4).Select(x => x.Customer).Single().Should().Be("beta");
        _db.From<C12Order>().Where(x => x.Id == 99).Select(x => x.Id).FirstOrDefault().Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A locked file is left to the temporary directory; the test outcome is unaffected.
        }
    }

    /// <summary>The seeded order row.</summary>
    [SqlTable("c12_order")]
    public sealed class C12Order
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The nullable customer name.</summary>
        [Column("customer")]
        public string? Customer { get; set; }

        /// <summary>The order amount.</summary>
        [Column("amount")]
        public double Amount { get; set; }

        /// <summary>The quantity.</summary>
        [Column("qty")]
        public int Qty { get; set; }

        /// <summary>The soft-delete marker, null for live rows.</summary>
        [Column("deleted_at")]
        public string? DeletedAt { get; set; }
    }
}
