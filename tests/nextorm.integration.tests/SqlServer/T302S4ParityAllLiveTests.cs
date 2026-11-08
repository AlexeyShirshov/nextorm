using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S4/T302 (AC-S4-5, VM-S4-09, VM-S4-10): collation and finite floating-point winner-set parity for
/// <c>ExtremeRowTies.All</c> on a live SQL Server. The deep <c>All</c> matrix for this provider already
/// lives in the S1–S3 live classes; this class adds only the parity dimensions.
/// <para>
/// The server's default collation is case-insensitive (<c>SQL_Latin1_General_CP1_CI_AS</c>), so
/// <c>'a'</c> and <c>'A'</c> are collation-equivalent at the extremum and the join-back must return both
/// rows — exactly the <c>rank() = 1</c> set. SQL Server <c>float</c> rejects <c>NaN</c> and ±Infinity at
/// insert, so non-finite parity is out of scope here rather than narrowed by this test.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class T302S4ParityAllSqlServerTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private const string TextTableName = "t302_s4_live_text";

    private const string CreateTextSql =
        "create table t302_s4_live_text (id int not null, val nvarchar(10) null, payload int null)";

    private const string FloatTableName = "t302_s4_live_float";

    private const string CreateFloatSql =
        "create table t302_s4_live_float (id int not null, val float null, payload int null)";

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
    /// Collation parity (VM-S4-09): under a case-insensitive collation both collation-equivalent candidates
    /// win, and the <c>All</c> winner set must equal the raw <c>rank() = 1</c> set on the same live table.
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
            var max = TextSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, TextTableName, "val", descending: true);

            max.Select(r => r.Id).Should().Equal(maxOracle,
                "All must return exactly the rank() = 1 winners of the same population");
            max.Should().NotBeEmpty();
            max.Should().OnlyContain(r => r.Val == "a" || r.Val == "A");
            max.Should().NotContain(r => r.Id == 3);

            var min = TextSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
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
            var max = FloatSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            max.Select(r => r.Id).Should().Equal(maxOracle);
            max.Select(r => r.Id).Should().ContainInOrder(4);

            var min = FloatSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(1, 2);
        }
        finally { Execute($"drop table if exists {FloatTableName}"); }
    }
}
