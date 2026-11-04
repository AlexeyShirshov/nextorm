using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 6 of campaign D364: branch coverage for the <c>SelectGroup</c> analyzer and its expression
/// comparer. The facts follow the documented inference rules (maximal row-dependent subexpression,
/// first-appearance order, constants and captured values are not keys, structural duplicate collapse,
/// aggregate opacity) and the refusal paths (identity projection, non-scalar key, HAVING / ORDER BY
/// outside the inferred keys). Each refusal is asserted on the message, never on the exception type
/// alone.
/// </summary>
public sealed class C14SelectGroupExpressionCoverageSqliteTests : IDisposable
{
    private const string Ddl = """
        create table c14_sg (
            id integer primary key,
            grp text null,
            name text null,
            amount real not null,
            flag integer not null);
        insert into c14_sg (id, grp, name, amount, flag) values
            (1, 'a', 'alpha', 10.0, 1),
            (2, 'a', 'ALPHA', 20.0, 1),
            (3, 'b', 'beta', 30.0, 0),
            (4, 'b', 'gamma', 40.0, 0),
            (5, null, null, 50.0, 1);
        """;

    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public C14SelectGroupExpressionCoverageSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c14.sg.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- inference rules

    /// <summary>Two structural duplicates of one key collapse into a single GROUP BY key.</summary>
    [Fact]
    public void DuplicateMember_ShouldCollapseIntoOneKey()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { A = x.Grp, B = x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.C).Sum().Should().Be(5);
        foreach (var row in rows)
        {
            row.A.Should().Be(row.B);
        }
    }

    /// <summary>Two identical scalar-function calls are one key, not two.</summary>
    [Fact]
    public void DuplicateScalarFunction_ShouldCollapseIntoOneKey()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new
            {
                Upper = x.Name!.ToUpper(),
                UpperAgain = x.Name!.ToUpper(),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        rows.Should().HaveCount(4);
        foreach (var row in rows)
        {
            row.Upper.Should().Be(row.UpperAgain);
            row.C.Should().Be(row.Upper == "ALPHA" ? 2 : 1);
        }
    }

    /// <summary>A literal is not a key: it neither groups nor breaks the aggregate.</summary>
    [Fact]
    public void ConstantOutput_ShouldNotBecomeAKey()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { Lit = 7, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Lit.Should().Be(7);
        rows[0].C.Should().Be(5);
    }

    /// <summary>A captured value is not a key either.</summary>
    [Fact]
    public void CapturedValueOutput_ShouldNotBecomeAKey()
    {
        var marker = 3;

        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { Marker = marker, S = SqlFunctions.Sql.sum(x.Amount) })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Marker.Should().Be(3);
        rows[0].S.Should().Be(150.0);
    }

    /// <summary>A composite key keeps first-appearance order and both columns.</summary>
    [Fact]
    public void CompositeKey_ShouldGroupByBothMembers()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { x.Grp, x.Flag, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Single(r => r.Grp == "a").C.Should().Be(2);
    }

    /// <summary>Only the row-dependent part of an aggregate-adjacent expression becomes a key.</summary>
    [Fact]
    public void SubexpressionGranularity_ShouldKeepTheWholeOperand()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new
            {
                Bucket = x.Flag * 2,
                Total = SqlFunctions.Sql.sum(x.Amount) + 1,
            })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Single(r => r.Bucket == 2).Total.Should().Be(81.0);
    }

    /// <summary>Global aggregate: no row-dependent output means no keys at all.</summary>
    [Fact]
    public void GlobalAggregates_ShouldProduceOneRow()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new
            {
                C = SqlFunctions.Sql.count(),
                S = SqlFunctions.Sql.sum(x.Amount),
                Lo = SqlFunctions.Sql.min(x.Amount),
                Hi = SqlFunctions.Sql.max(x.Amount),
                Avg = SqlFunctions.Sql.avg(x.Amount),
            })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].C.Should().Be(5);
        rows[0].Lo.Should().Be(10.0);
        rows[0].Hi.Should().Be(50.0);
        rows[0].Avg.Should().Be(30.0);
    }

    /// <summary>A NULL key is its own group, not folded into a non-null one.</summary>
    [Fact]
    public void NullKey_ShouldFormItsOwnGroup()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Single(r => r.Grp is null).C.Should().Be(1);
    }

    /// <summary>A member-init projection takes the scalar-binding path, not the anonymous one.</summary>
    [Fact]
    public void MemberInitProjection_ShouldGroup()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new C14Bucket
            {
                Grp = x.Grp,
                Total = SqlFunctions.Sql.sum(x.Amount),
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Single(r => r.Grp == "b").Total.Should().Be(70.0);
    }

    /// <summary>A coalesced key is one key (conditional/coalesce arm of the comparer).</summary>
    [Fact]
    public void CoalesceKey_ShouldGroupByTheCoalescedValue()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { Label = x.Name ?? "unknown", C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().ContainSingle(r => r.Label == "unknown");
    }

    /// <summary>A conditional key groups by the evaluated branch value.</summary>
    [Fact]
    public void ConditionalKey_ShouldGroupByTheBranchValue()
    {
        var rows = _db.From<C14Sg>()
            .SelectGroup(x => new { On = x.Flag == 1 ? "yes" : "no", C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Single(r => r.On == "yes").C.Should().Be(3);
    }

    // ---------------------------------------------------------------- HAVING / ORDER BY coverage

    /// <summary>HAVING on an inferred key is accepted and filters groups.</summary>
    [Fact]
    public void HavingOnKey_ShouldFilterGroups()
    {
        var rows = _db.From<C14Sg>()
            .Having(x => x.Grp == "a")
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].C.Should().Be(2);
    }

    /// <summary>HAVING on an aggregate is accepted and filters groups.</summary>
    [Fact]
    public void HavingOnAggregate_ShouldFilterGroups()
    {
        var rows = _db.From<C14Sg>()
            .Having(x => SqlFunctions.Sql.count() > 1)
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.C > 1);
    }

    /// <summary>ORDER BY on an inferred key is accepted.</summary>
    [Fact]
    public void OrderByKey_ShouldSortGroups()
    {
        var rows = _db.From<C14Sg>().Where(x => x.Grp != null)
            .OrderBy(x => x.Grp)
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Grp.Should().Be("a");
    }

    // ---------------------------------------------------------------- refusal paths

    /// <summary>An identity projection has no scalar output and is refused by name.</summary>
    [Fact]
    public void IdentityProjection_ShouldBeRefused()
    {
        var act = () => _db.From<C14Sg>()
            .SelectGroup(x => x)
            .ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*at least one scalar output*");
    }

    /// <summary>A nested object key is not a scalar and is refused by name.</summary>
    [Fact]
    public void NestedObjectKey_ShouldBeRefused()
    {
        var act = () => _db.From<C14Sg>()
            .SelectGroup(x => new { Key = new { x.Grp }, C = SqlFunctions.Sql.count() })
            .ToList();

        act.Should().Throw<Exception>()
            .WithMessage("*non-scalar*");
    }

    /// <summary>HAVING that references a row column outside the inferred keys is refused by name.</summary>
    [Fact]
    public void HavingOutsideKeys_ShouldBeRefused()
    {
        var act = () => _db.From<C14Sg>()
            .Having(x => x.Amount > 10.0)
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*not covered by inferred keys*");
    }

    /// <summary>ORDER BY that references a row column outside the inferred keys is refused by name.</summary>
    [Fact]
    public void OrderByOutsideKeys_ShouldBeRefused()
    {
        var act = () => _db.From<C14Sg>()
            .OrderBy(x => x.Amount)
            .SelectGroup(x => new { x.Grp, C = SqlFunctions.Sql.count() })
            .ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*not covered by inferred keys*");
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // A locked temporary file is left to the OS; the test outcome is unaffected.
        }
    }

    /// <summary>The seeded row.</summary>
    [SqlTable("c14_sg")]
    public sealed class C14Sg
    {
        /// <summary>Primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Nullable group key.</summary>
        [Column("grp")]
        public string? Grp { get; set; }

        /// <summary>Nullable label.</summary>
        [Column("name")]
        public string? Name { get; set; }

        /// <summary>Measured amount.</summary>
        [Column("amount")]
        public double Amount { get; set; }

        /// <summary>Bit flag.</summary>
        [Column("flag")]
        public int Flag { get; set; }
    }

    /// <summary>A member-init projection target.</summary>
    public sealed class C14Bucket
    {
        /// <summary>Group label.</summary>
        public string? Grp { get; set; }

        /// <summary>Group total.</summary>
        public double Total { get; set; }
    }
}
