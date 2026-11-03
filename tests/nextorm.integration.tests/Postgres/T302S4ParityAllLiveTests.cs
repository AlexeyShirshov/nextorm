using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S4/T302 (AC-S4-5, VM-S4-09, VM-S4-10): collation and floating-point winner-set parity for
/// <c>ExtremeRowTies.All</c> on a live PostgreSQL server. The deep <c>All</c> matrix for this provider
/// already lives in the S1–S3 live classes; this class adds only the parity dimensions.
/// <para>
/// PostgreSQL orders <c>NaN</c> above every other <c>double precision</c> value and treats it as equal to
/// itself, so <c>NaN</c> is a supported non-finite value that the join-back must match like any other
/// winner. The oracle is the raw <c>rank() = 1</c> query over the same live table.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class T302S4ParityAllPostgresTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string TextTableName = "t302_s4_live_text";

    private const string CreateTextSql =
        "create table if not exists t302_s4_live_text (id int not null, val text null, payload int null)";

    private const string FloatTableName = "t302_s4_live_float";

    private const string CreateFloatSql =
        "create table if not exists t302_s4_live_float (id int not null, val double precision null, " +
        "payload int null)";

    [SqlTable("t302_s4_live_text")]
    public sealed class TextRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public string? Val { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    [SqlTable("t302_s4_live_float")]
    public sealed class FloatRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public double? Val { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    private EntityBuilder<TextRow> TextSource() => _sut.DataProvider.From<TextRow>();

    private EntityBuilder<FloatRow> FloatSource() => _sut.DataProvider.From<FloatRow>();

    private void Execute(string sql) => T302S4ParityOracle.Execute(_sut.DataProvider, sql);

    /// <summary>
    /// Collation parity (VM-S4-09): the <c>All</c> winner set equals the raw <c>rank() = 1</c> set on the
    /// same live table, for both directions.
    /// </summary>
    [Fact]
    public void MaxMin_All_Collation_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {TextTableName}");
        Execute(CreateTextSql);
        Execute($"insert into {TextTableName} (id, val, payload) values " +
                "(1, 'a', 10), (2, 'A', 20), (3, '0', 30)");
        try
        {
            var max = TextSource().SelectWhereMax(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, TextTableName, "val", descending: true);

            max.Select(r => r.Id).Should().Equal(maxOracle,
                "All must return exactly the rank() = 1 winners of the same population");
            max.Should().NotBeEmpty();
            max.Should().OnlyContain(r => r.Val == "a" || r.Val == "A");
            max.Should().NotContain(r => r.Id == 3);

            var min = TextSource().SelectWhereMin(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, TextTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(3);
        }
        finally { Execute($"drop table if exists {TextTableName}"); }
    }

    /// <summary>
    /// Finite floating-point parity (VM-S4-10): a unique maximum and a tied minimum must match the raw
    /// <c>rank() = 1</c> winner set; the NULL value key stays out of the population.
    /// </summary>
    [Fact]
    public void MaxMin_All_FloatingPoint_Finite_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {FloatTableName}");
        Execute(CreateFloatSql);
        Execute($"insert into {FloatTableName} (id, val, payload) values " +
                "(1, 1.5, 10), (2, 1.5, 20), (3, 2.25, 30), (4, 3.5, 40), (5, null, 50)");
        try
        {
            var max = FloatSource().SelectWhereMax(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            max.Select(r => r.Id).Should().Equal(maxOracle);
            max.Select(r => r.Id).Should().ContainInOrder(4);

            var min = FloatSource().SelectWhereMin(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(1, 2);
        }
        finally { Execute($"drop table if exists {FloatTableName}"); }
    }

    /// <summary>
    /// Non-finite parity (VM-S4-10): <c>double precision</c> stores ±Infinity and <c>NaN</c>, so their
    /// winner sets must equal the raw <c>rank() = 1</c> oracle. The two populations are checked separately
    /// so a divergence is attributable: ±Infinity first, then the same table plus a <c>NaN</c> row.
    /// </summary>
    [Fact]
    public void MaxMin_All_FloatingPoint_NonFinite_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {FloatTableName}");
        Execute(CreateFloatSql);
        Execute($"insert into {FloatTableName} (id, val, payload) values " +
                "(1, 1.5, 10), (2, -2.5, 20), (3, 'Infinity'::double precision, 30), " +
                "(4, '-Infinity'::double precision, 40), (5, null, 50)");
        try
        {
            var max = FloatSource().SelectWhereMax(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            max.Select(r => r.Id).Should().Equal(maxOracle);
            max.Select(r => r.Id).Should().ContainInOrder(3);

            var min = FloatSource().SelectWhereMin(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(4);

            Execute($"insert into {FloatTableName} (id, val, payload) values " +
                    "(6, 'NaN'::double precision, 60)");

            var nanMax = FloatSource().SelectWhereMax(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var nanMaxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            nanMax.Select(r => r.Id).Should().Equal(nanMaxOracle);

            var nanMin = FloatSource().SelectWhereMin(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var nanMinOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            nanMin.Select(r => r.Id).Should().Equal(nanMinOracle);
        }
        finally { Execute($"drop table if exists {FloatTableName}"); }
    }
}
