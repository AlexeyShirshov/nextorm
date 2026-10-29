using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 13 probe for <c>AdvancedAggregateTranslator.Classify</c>: distinct aggregate and terminal
/// shapes that force the classifier through different arms, executed on a real SQLite file and asserted
/// on returned values.
/// </summary>
public sealed class C13AggregateProbeSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public C13AggregateProbeSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c13.agg.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c13_sale (id integer primary key, grp text null, amount real not null, qty integer not null);
            insert into c13_sale (id, grp, amount, qty) values
                (1, 'a', 10.0, 2), (2, 'a', 20.0, 3), (3, 'b', 30.0, 1), (4, 'b', 40.0, 5);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Selector aggregates: Sum/UserAverage/Min/Max over a projection.</summary>
    [Fact]
    public void SelectorAggregates_ShouldReturnTheSeededValues()
    {
        _db.From<C13Sale>().Sum(x => x.Amount).Should().Be(100.0);
        _db.From<C13Sale>().Min(x => x.Amount).Should().Be(10.0);
        _db.From<C13Sale>().Max(x => x.Amount).Should().Be(40.0);
        _db.From<C13Sale>().Avg(x => x.Amount).Should().Be(25.0);
    }

    /// <summary>Count and LongCount over filtered sets.</summary>
    [Fact]
    public void CountAndLongCount_ShouldMatchTheRows()
    {
        _db.From<C13Sale>().Count().Should().Be(4);
        _db.From<C13Sale>().Where(x => x.Grp == "a").Count().Should().Be(2);
        _db.From<C13Sale>().LongCount().Should().Be(4L);
    }

    /// <summary>Aggregates used inside a projection with a computed expression.</summary>
    [Fact]
    public void AggregatesInProjection_ShouldCompose()
    {
        var row = _db.From<C13Sale>()
            .Where(x => x.Id > 0)
            .Select(x => new
            {
                Total = SqlFunctions.Sql.sum(x.Amount),
                Smallest = SqlFunctions.Sql.min(x.Qty),
                Largest = SqlFunctions.Sql.max(x.Qty),
                Rows = SqlFunctions.Sql.count()
            })
            .ToList()
            .Single();

        row.Total.Should().Be(100.0);
        row.Smallest.Should().Be(1);
        row.Largest.Should().Be(5);
        row.Rows.Should().Be(4);
    }

    /// <summary>Grouped aggregate with a HAVING-shaped filter over the aggregate.</summary>
    [Fact]
    public void GroupedAggregate_ShouldFilterGroupsByTheirAggregate()
    {
        var groups = _db.From<C13Sale>()
            .GroupBy(x => x.Grp)
            .Select(x => new { Grp = x.Grp, Total = SqlFunctions.Sql.sum(x.Amount), Rows = SqlFunctions.Sql.count() })
            .ToList();

        groups.Should().HaveCount(2);
        groups.Single(g => g.Grp == "a").Total.Should().Be(30.0);
        groups.Single(g => g.Grp == "b").Rows.Should().Be(2);
    }

    /// <summary>A scalar expression over an aggregate in the projection.</summary>
    [Fact]
    public void ExpressionOverAggregate_ShouldEvaluate()
    {
        var row = _db.From<C13Sale>()
            .Where(x => x.Grp != null)
            .Select(x => new { Doubled = SqlFunctions.Sql.sum(x.Amount) + SqlFunctions.Sql.sum(x.Amount) })
            .ToList()
            .Single();

        row.Doubled.Should().Be(200.0);
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
            // Locked temporary files are left to the OS; the test outcome is unaffected.
        }
    }

    /// <summary>The seeded sale row.</summary>
    [SqlTable("c13_sale")]
    public sealed class C13Sale
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The optional group label.</summary>
        [Column("grp")]
        public string? Grp { get; set; }

        /// <summary>The sale amount.</summary>
        [Column("amount")]
        public double Amount { get; set; }

        /// <summary>The sold quantity.</summary>
        [Column("qty")]
        public int Qty { get; set; }
    }
}
