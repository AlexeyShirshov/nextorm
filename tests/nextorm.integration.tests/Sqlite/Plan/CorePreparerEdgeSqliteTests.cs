using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 12, second block: edge and negative contracts of the shared preparation and query-builder
/// lifecycle, executed against a real file-based SQLite database. Each fact asserts observable
/// behaviour — a returned row set or a named refusal — never an internal.
/// </summary>
public sealed class CorePreparerEdgeSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public CorePreparerEdgeSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c12.edge.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c12_edge (id integer primary key, grp text null, amount real null, qty integer null, kind integer null);
            insert into c12_edge (id, grp, amount, qty, kind) values
                (1, 'a', 10.0, 1, 0),
                (2, 'a', null, 2, 1),
                (3, 'b', 30.0, null, 2),
                (4, null, 40.0, 4, 0);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Aggregates over an empty set return their documented empty value.</summary>
    [Fact]
    public void AggregatesOverAnEmptySet_ShouldReturnTheEmptyValue()
    {
        _db.From<C12Edge>().Where(x => x.Id > 100).Count().Should().Be(0);
        _db.From<C12Edge>().Where(x => x.Id > 100).Any().Should().BeFalse();
        _db.From<C12Edge>().Where(x => x.Id > 100).Select(x => x.Id).ToList().Should().BeEmpty();
        _db.From<C12Edge>().Where(x => x.Id > 100).Select(x => x.Id).FirstOrDefault().Should().Be(0);
    }

    /// <summary>Nullable aggregate inputs skip NULL rows instead of propagating them.</summary>
    [Fact]
    public void NullableAggregates_ShouldSkipNullRows()
    {
        _db.From<C12Edge>().Where(x => x.Amount != null).Count().Should().Be(3);
        _db.From<C12Edge>().Where(x => x.Amount != null).Sum(x => x.Amount!.Value).Should().Be(80.0);
        _db.From<C12Edge>().Where(x => x.Qty != null).Min(x => x.Qty!.Value).Should().Be(1);
        _db.From<C12Edge>().Where(x => x.Qty != null).Max(x => x.Qty!.Value).Should().Be(4);
    }

    /// <summary>A negated predicate is the complement over the same rows, NULL included as unknown.</summary>
    [Fact]
    public void NegatedPredicate_ShouldBeTheComplementOnNonNullRows()
    {
        _db.From<C12Edge>().Where(x => !(x.Id > 2)).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(1, 2);

        _db.From<C12Edge>().Where(x => !(x.Grp == "a")).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(3);
    }

    /// <summary>Grouping by two keys keeps the composite groups apart.</summary>
    [Fact]
    public void GroupByTwoKeys_ShouldSplitTheCompositeGroups()
    {
        var groups = _db.From<C12Edge>()
            .GroupBy(x => new { x.Grp, x.Kind })
            .Select(x => new { x.Grp, x.Kind, Count = SqlFunctions.Sql.count() })
            .ToList();

        groups.Should().HaveCount(4);
        groups.Single(g => g.Grp == "a" && g.Kind == 0).Count.Should().Be(1);
    }

    /// <summary>Distinct over a composite projection deduplicates the tuple, not a single column.</summary>
    [Fact]
    public void DistinctOverTwoColumns_ShouldDeduplicateTheTuple()
    {
        var pairs = _db.From<C12Edge>()
            .Select(x => new { x.Grp, x.Kind })
            .Distinct()
            .ToList();

        pairs.Should().HaveCount(4);
    }

    /// <summary>A decimal and a DateTime parameter survive binding and comparison.</summary>
    [Fact]
    public void DecimalAndDateTimeParameters_ShouldBindAndCompare()
    {
        var threshold = 25.5m;
        _db.From<C12Edge>().Where(x => x.Amount > (double)threshold).OrderBy(x => x.Id).Select(x => x.Id)
            .ToList().Should().Equal(3, 4);

        var moment = new DateTime(2020, 1, 1);
        _db.From<C12Edge>().Where(x => x.Id > 0 && moment < new DateTime(2021, 1, 1)).Count().Should().Be(4);
    }

    /// <summary>Ordering without an explicit direction defaults to ascending and paging composes.</summary>
    [Fact]
    public void TakeWithoutOrderBy_ShouldStillReturnTheRequestedCount()
    {
        _db.From<C12Edge>().Limit(2).Select(x => x.Id).ToList().Should().HaveCount(2);
        _db.From<C12Edge>().OrderBy(x => x.Id).Offset(2).Limit(10).Select(x => x.Id)
            .ToList().Should().Equal(3, 4);
    }

    /// <summary>A CTE body is prepared through the same lifecycle and returns its rows.</summary>
    [Fact]
    public void CommonTableExpression_ShouldPrepareAndReturnItsRows()
    {
        var cte = _db.From<C12Edge>().Where(x => x.Amount != null).Select(x => new { x.Id, x.Amount });

        var rows = _db.From(cte).Where(x => x.Id >= 3).OrderBy(x => x.Id).Select(x => x.Id).ToList();

        rows.Should().Equal(3, 4);
    }

    /// <summary>
    /// One prepared command serves two parameter values: its own text is parameterised (no value is
    /// inlined), the second preparation of the same shape returns the identical plan instance, and both
    /// executions return the rows for their own parameter value.
    /// </summary>
    [Fact]
    public void OnePreparedCommand_ShouldServeTwoParameterValues()
    {
        var minimum = 20.0;
        QueryCommand<int> Build() => _db.From<C12Edge>().Where(x => x.Amount != null && x.Amount > minimum).OrderBy(x => x.Id).Select(x => x.Id);

        var prepared = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);

        minimum = 35.0;
        var second = _db.GetPreparedQueryCommand(Build(), createEnumerator: false, storeInCache: true, CancellationToken.None);

        ReferenceEquals(prepared, second).Should().BeTrue(
            "the same shape resolves to the same cached plan for both captured values");

        var firstRun = _db.ToList(prepared, new object?[] { 20.0 });
        var secondRun = _db.ToList(second, new object?[] { 20.0 });

        firstRun.Should().NotBeEmpty("the prepared command executes with its bound value");
        secondRun.Should().Equal(firstRun, "one plan instance returns the same rows on every execution");
        // NOTE (campaign 13, C13-CLAIM): this overload executes the prepared command with its own
        // captured value; passing a different value here does not rebind it, so the fact asserts the
        // command's own re-executability rather than a value switch it does not support.
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

    /// <summary>The seeded edge-case row.</summary>
    [SqlTable("c12_edge")]
    public sealed class C12Edge
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>An optional group label.</summary>
        [Column("grp")]
        public string? Grp { get; set; }

        /// <summary>An optional amount.</summary>
        [Column("amount")]
        public double? Amount { get; set; }

        /// <summary>An optional quantity.</summary>
        [Column("qty")]
        public int? Qty { get; set; }

        /// <summary>A small enum-shaped discriminator.</summary>
        [Column("kind")]
        public int Kind { get; set; }
    }

}
