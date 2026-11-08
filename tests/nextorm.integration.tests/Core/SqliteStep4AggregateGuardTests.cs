using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 1 — the deterministic pre-SQL guard surface of
/// <c>src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs</c>.
/// <para>
/// The three restored regressions (<c>EmitApproxPercentile</c>, lines <c>:550</c>/<c>:551</c> of the
/// class) are the declared-15 SQL Server refusal of the two approximate percentiles: Step 3 lost them
/// only because the single container-backed entry point
/// (<c>T348SqlServer2022FunctionsTests</c>) failed in its constructor. The same refusal is reachable
/// without a container by declaring major version 15 on a dedicated context subclass and asking for the
/// SQL text — the guard throws while the command is rendered, so no statement is dispatched.
/// </para>
/// <para>
/// The residual facts exercise the other refusal arms of the same translator on the live SQLite
/// provider: an empty or captured <c>grouping_id</c> argument list and the unsupported typed-NULL
/// operand. Every oracle is the exact type plus the semantic identifier the guard carries.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4AggregateGuardTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    /// <summary>Marker source of the aggregate-guard facts; never seeded, only rendered.</summary>
    [SqlTable("s4_agg_row")]
    public sealed class S4AggRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("value")]
        public int? Value { get; set; }
    }

    /// <summary>A SQL Server 2022 declaration rolled back to major 15, the pre-percentile engine.</summary>
    private sealed class S4Declared15Context()
        : SqlServerDataContext(new Version(15, 0), "Server=127.0.0.1,1;Database=s4;Connect Timeout=1");

    /// <summary>The continuous percentile refuses on a declared-15 engine before any command is dispatched.</summary>
    [Fact]
    public void ApproxPercentileCont_OnDeclared15_ShouldRefuseBeforeSql()
    {
        using var context = new S4Declared15Context();

        var act = () => context.From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_cont(0.5, x.Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*APPROX_PERCENTILE_CONT*major version 16*declared major version: 15*");
    }

    /// <summary>The discrete percentile takes the same name-specific gate and refusal.</summary>
    [Fact]
    public void ApproxPercentileDisc_OnDeclared15_ShouldRefuseBeforeSql()
    {
        using var context = new S4Declared15Context();

        var act = () => context.From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { P = SqlFunctions.SqlServer.approx_percentile_disc(0.5, x.Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*APPROX_PERCENTILE_DISC*major version 16*declared major version: 15*");
    }

    /// <summary>A typed NULL operand outside the closed CLR set is refused by its CLR type name.</summary>
    [Fact]
    public void ApproxCountDistinct_WithUnsupportedTypedNullOperand_ShouldNameTheOperandType()
    {
        using var context = new S4Declared15Context();

        var act = () => context.From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { D = SqlFunctions.SqlServer.approx_count_distinct((string?)null) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*String operand type has no typed NULL*SQL Server*");
    }

    /// <summary>
    /// An empty <c>grouping_id</c> argument list has no GROUP BY expression to match, so the command
    /// boundary refuses it with the grouping-context type (the translator is never reached).
    /// </summary>
    [Fact]
    public void GroupingId_WithoutArguments_ShouldRefuseTheEmptyArgumentList()
    {
        using var context = new S4Declared15Context();

        var act = () => context.From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { G = SqlFunctions.SqlServer.grouping_id() })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*GROUPING_ID*GROUP BY expression*");
    }

    /// <summary>
    /// A captured array cannot be matched against the grouping expressions, so the command boundary
    /// refuses the shape before any SQL is rendered.
    /// </summary>
    [Fact]
    public void GroupingId_WithCapturedArray_ShouldRefuseTheRuntimeArray()
    {
        using var context = new S4Declared15Context();
        var keys = new object?[] { 1, 2 };

        var act = () => context.From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { G = SqlFunctions.SqlServer.grouping_id(keys) })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*GROUPING_ID*GROUP BY expression*");
    }

    /// <summary>The boolean aggregate family is refused on a provider without the capability flag.</summary>
    [Fact]
    public void BooleanAggregates_OnSqlite_ShouldRefuseTheFamily()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Postgres.bool_and(x.Value > 0) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*bool_and/bool_or/every aggregates*not supported*");
    }

    /// <summary>The bitwise aggregate family takes its own refusal marker.</summary>
    [Fact]
    public void BitwiseAggregates_OnSqlite_ShouldRefuseTheFamily()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Postgres.bit_and(x.Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*bit_and/bit_or/bit_xor aggregates*not supported*");
    }

    /// <summary>The statistical aggregate family (corr/covar) takes its own refusal marker.</summary>
    [Fact]
    public void StatisticalAggregates_OnSqlite_ShouldRefuseTheFamily()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Sql.corr(x.Value, x.Grp) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*statistical (corr/covar) aggregates*not supported*");
    }

    /// <summary>The regression aggregate family is refused separately from the statistical one.</summary>
    [Fact]
    public void RegressionAggregates_OnSqlite_ShouldRefuseTheFamily()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Postgres.regr_slope(x.Value, x.Grp) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*regr_* aggregates*not supported*");
    }

    /// <summary>The ordered-set percentile family is refused as a whole on a provider without it.</summary>
    [Fact]
    public void OrderedPercentile_OnSqlite_ShouldRefuseTheFamily()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Postgres.percentile_cont(0.5, () => x.Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ordered-set aggregates (percentile_cont/percentile_disc/mode)*not supported*");
    }

    /// <summary>The arbitrary-value aggregate is gated per provider and refused on SQLite.</summary>
    [Fact]
    public void AnyAggregate_OnSqlite_ShouldRefuseWithItsOwnMarker()
    {
        var act = () => Context().From<S4AggRow>()
            .GroupBy(x => new { x.Grp })
            .Select(x => new { A = SqlFunctions.Sql.any_agg(x.Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*any*not supported*");
    }

    private IDataContext Context() => _sut.DataProvider;
}
