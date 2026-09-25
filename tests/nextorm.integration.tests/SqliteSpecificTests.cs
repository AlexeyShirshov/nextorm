using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests for behaviour that is specific to the SQLite provider and therefore not part of the
/// shared suite (SQLite has no ANY/ALL subquery support and sorts NULLs first).
/// </summary>
public sealed class SqliteSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public async Task WhereAnySubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == SqlFunctions.Sql.any(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task WhereAllSubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == SqlFunctions.Sql.all(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public void OrderBy2_NullsFirst_ShouldSortData()
    {
        // SQLite orders NULLs first, so the NULL Int group (id = 1) is the leading sort key.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).OrderByDescending(it => it.Id).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
        r[1].Id.Should().Be(3);
        r[2].Id.Should().Be(2);
    }

    [Fact]
    public void InfoFunctions_ShouldReturnSqliteVersion()
    {
        var r = _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.version())
            .First();

        r.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => SqlFunctions.Sql.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void DateProjection_ShouldMaterialiseColumns()
    {
        // Exercises the row mapper (not the scalar path): SQLite returns datetime() as TEXT and the
        // diff as INTEGER, so both have to land in the DateTime?/int? projection members.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = SqlFunctions.Sql.date_add("day", 1, x.Datetime),
                Days = SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)),
                Last = SqlFunctions.Sql.end_of_month(x.Datetime)
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Days.Should().Be(3);
        r.Last.Should().Be(new DateTime(2023, 1, 31));
    }

    /// <summary>
    /// A correlated scalar whose projection is a non-nullable value type cannot yield a value when no
    /// row matches: SQL returns NULL and the reader getter throws. Project as <c>int?</c> (inside the
    /// subquery: <c>Select(s =&gt; (int?)s.Id)</c>) to get <c>null</c> instead. The exception type is
    /// provider-specific, hence this test is not in the shared suite.
    /// </summary>
    [Fact]
    public void CorrelatedScalarFirst_ShouldThrowWhenNoRowMatches()
    {
        var act = () => _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).First()
            })
            .ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// SQLite does not enforce scalar-subquery cardinality (it returns the first row), so a numeric
    /// Single/SingleOrDefault scalar subquery is rendered with a count guard. A single matching row
    /// returns the value.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldReturnTheSingleValue()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).Single()
            })
            .ToList();

        r.Should().ContainSingle();
        r[0].cid.Should().Be(1);
    }

    /// <summary>
    /// More than one matching row must raise through the guard instead of silently returning the first.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenMultipleRowsMatch()
    {
        var act = () => _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Select(c => (int?)c.Id).Single()
            })
            .ToList();

        act.Should().Throw<Microsoft.Data.Sqlite.SqliteException>();
    }

    /// <summary>No matching row yields the default for SingleOrDefault (same as enforcing providers).</summary>
    [Fact]
    public void CorrelatedScalarSingleOrDefault_ShouldYieldDefaultWhenNoRowMatches()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).SingleOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == 0);
    }

    /// <summary>No matching row throws for Single on a non-nullable projection (same as enforcing providers).</summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenNoRowMatches()
    {
        var act = () => _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).Single()
            })
            .ToList();

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Cte_Recursive_WithDistinctUnion_ShouldProduceNumberSeries()
    {
        var ctx = _sut.DataProvider;

        var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CommonTestSuite.CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.Union(step);

        var rows = ctx
            .WithRecursive("nums", body)
            .From("nums")
            .Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt })
            .ToList();

        rows.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    [SqlTable("collation_probe")]
    internal interface ICollationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("binary")]
        string? Name { get; set; }
    }

    [Fact]
    public void ColumnCollation_ShouldApplyDeclaredBinaryCollation()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists collation_probe");
        Execute(ctx, "create table collation_probe (id bigint, name varchar(50))");

        try
        {
            Execute(ctx, "insert into collation_probe (id, name) values (1, 'abc')");
            Execute(ctx, "insert into collation_probe (id, name) values (2, 'ABC')");

            ctx.From<ICollationProbe>().Where(x => x.Name == "abc").Select(x => x.Id).ToList()
                .Should().Equal(1L);
        }
        finally
        {
            Execute(ctx, "drop table if exists collation_probe");
        }
    }

    [Fact]
    public void SqlitePrintfAndHex_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                P = SqlFunctions.Sqlite.printf("%d-%s", 7, "x"),
                F = SqlFunctions.Sqlite.format("%d", 42),
                H = SqlFunctions.Sqlite.hex("AB"),
                O = SqlFunctions.Sqlite.octet_length("AB"),
                U = SqlFunctions.Sqlite.unicode("A"),
                C = SqlFunctions.Sqlite.@char(65, 66),
                T = SqlFunctions.Sqlite.@typeof(1)
            })
            .First();

        r.P.Should().Be("7-x");
        r.F.Should().Be("42");
        r.H.Should().Be("4142");
        r.O.Should().Be(2);
        r.U.Should().Be(65);
        r.C.Should().Be("AB");
        r.T.Should().Be("integer");
    }

    [Fact]
    public void SqliteUnhex_ShouldDecodeBytes()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sqlite.unhex("4142"))
            .First();

        r.Should().Equal(0x41, 0x42);
    }

    [Fact]
    public void SqliteJsonScalars_ShouldRoundTrip()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ex = SqlFunctions.Sqlite.json_extract<string>("{\"a\":\"hello\"}", "$.a"),
                Gt = SqlFunctions.Sqlite.json_get_text("{\"a\":\"hello\"}", "$.a"),
                G = SqlFunctions.Sqlite.json_get("{\"a\":\"hello\"}", "$.a"),
                Set = SqlFunctions.Sqlite.json_set("{\"a\":1}", "$.b", 2),
                Ar = SqlFunctions.Sqlite.json_array(1, 2),
                Ob = SqlFunctions.Sqlite.json_object("a", 1),
                Pa = SqlFunctions.Sqlite.json_patch("{\"a\":1}", "{\"b\":2}"),
                Va = SqlFunctions.Sqlite.json_valid("{\"a\":1}"),
                Ty = SqlFunctions.Sqlite.json_type("{\"a\":1}", "$.a")
            })
            .First();

        r.Ex.Should().Be("hello");
        r.Gt.Should().Be("hello");
        r.G.Should().Be("\"hello\"");
        r.Set.Should().Be("{\"a\":1,\"b\":2}");
        r.Ar.Should().Be("[1,2]");
        r.Ob.Should().Be("{\"a\":1}");
        r.Pa.Should().Be("{\"a\":1,\"b\":2}");
        r.Va.Should().BeTrue();
        r.Ty.Should().Be("integer");
    }

    [Fact]
    public void SqliteJsonGroupArray_ShouldAggregate()
    {
        var r = _sut.SimpleEntity
            .Select(x => SqlFunctions.Sqlite.json_group_array("v"))
            .First();

        r.Should().StartWith("[").And.EndWith("]").And.Contain("\"v\"");
    }

    [Fact]
    public void SqliteDateFunctions_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                U = SqlFunctions.Sqlite.unixepoch(new DateTime(1970, 1, 1, 0, 0, 10, DateTimeKind.Utc)),
                J = SqlFunctions.Sqlite.julianday(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
                T = SqlFunctions.Sqlite.timediff(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            })
            .First();

        r.U.Should().Be(10);
        r.J.Should().BeApproximately(2451545.0, 1e-6);
        r.T.Should().Be("+0000-00-01 00:00:00.000");
    }

    [Fact]
    public void SqliteMathFunctions_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.Sqlite.acos(1.0),
                D = SqlFunctions.Sqlite.degrees(SqlFunctions.Sqlite.pi()),
                L2 = SqlFunctions.Sqlite.log2(8.0),
                M = SqlFunctions.Sqlite.mod(5.0, 2.0),
                R = SqlFunctions.Sqlite.radians(180.0)
            })
            .First();

        r.A.Should().BeApproximately(0.0, 1e-9);
        r.D.Should().BeApproximately(180.0, 1e-9);
        r.L2.Should().BeApproximately(3.0, 1e-9);
        r.M.Should().BeApproximately(1.0, 1e-9);
        r.R.Should().BeApproximately(Math.PI, 1e-9);
    }

    [Fact]
    public void SqliteJsonEachTableFunction_ShouldReturnRows()
    {
        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Sqlite.json_each("[\"a\",\"b\",\"c\"]"))
            .Select(r => r.Value)
            .ToList();

        values.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void SqliteJsonTreeTableFunction_ShouldReturnRows()
    {
        var count = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Sqlite.json_tree("{\"a\":{\"b\":1}}"))
            .Select(r => r.FullKey)
            .ToList();

        count.Should().Contain("$.a");
        count.Should().Contain("$.a.b");
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
