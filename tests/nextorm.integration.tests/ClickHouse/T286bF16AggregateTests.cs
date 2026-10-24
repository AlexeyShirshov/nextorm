using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D16 (F16 Aggregate): live execution of the ClickHouse-only aggregate catalogue against the
/// pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the statistical/probability values, the
/// quantile algorithms and the plural family, the array- and tuple-returning aggregates
/// (<c>groupArray*</c>, <c>histogram</c>, <c>approx_top_*</c>, the mapped-array min/max), the sequence
/// family (including the experimental <c>sequenceNextNode</c> setting), the time-series grids (with the
/// <c>allow_experimental_time_series_aggregate_functions</c> setting), the window-only
/// <c>exponentialTimeDecayed*</c> family, the grouping/empty-input/NULL contracts, the declared CLR
/// shapes, the vendor-absent names on this build, and the three state-returning refusals.
/// </summary>
public sealed class T286bF16AggregateTests : ProviderTestSuite
{
    [SqlTable("t286b_f16_agg")]
    internal interface IT286bAggregateRow
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("g")]
        int G { get; set; }

        [Column("gu")]
        uint Gu { get; set; }

        [Column("x")]
        int X { get; set; }

        [Column("y")]
        int Y { get; set; }

        [Column("f")]
        double F { get; set; }

        [Column("s")]
        string S { get; set; }

        [Column("sn")]
        string? Sn { get; set; }

        [Column("nx")]
        int? Nx { get; set; }

        [Column("ts")]
        DateTime Ts { get; set; }

        [Column("starts")]
        int Starts { get; set; }

        [Column("ends")]
        int Ends { get; set; }

        [Column("keys")]
        int[] Keys { get; set; }

        [Column("vals")]
        int[] Vals { get; set; }

        [Column("pos")]
        uint Pos { get; set; }

        [Column("wu")]
        uint Wu { get; set; }

        [Column("xu")]
        uint Xu { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f16_agg",
                     "create table t286b_f16_agg (id Int32, g Int32, gu UInt32, x Int32, y Int32, " +
                     "f Float64, s String, sn Nullable(String), nx Nullable(Int32), ts DateTime, " +
                     "starts Int32, ends Int32, keys Array(Int32), vals Array(Int32), " +
                     "pos UInt32, wu UInt32, xu UInt32) engine = Memory",
                     "insert into t286b_f16_agg values " +
                     "(1, 0, 0, 1, 2, 1.5, 'a', 'a', 1, '2024-01-01 00:00:01', 1, 4, [1, 2], [1, 2], 0, 1, 1), " +
                     "(2, 0, 0, 2, 4, 2.5, 'b', null, null, '2024-01-01 00:00:02', 2, 5, [1, 2], [2, 4], 1, 2, 2), " +
                     "(3, 0, 0, 3, 6, 3.5, 'c', 'c', 3, '2024-01-01 00:00:03', 3, 6, [1, 2], [3, 6], 2, 3, 3), " +
                     "(4, 0, 0, 4, 8, 4.5, 'd', null, null, '2024-01-01 00:00:04', 4, 7, [1, 2], [4, 8], 3, 4, 4), " +
                     "(5, 1, 1, 5, 10, 5.5, 'e', 'e', 5, '2024-01-01 00:00:05', 1, 2, [1, 2], [5, 10], 4, 5, 5), " +
                     "(6, 1, 1, 6, 12, 6.5, 'f', null, null, '2024-01-01 00:00:06', 1, 2, [1, 2], [6, 12], 5, 6, 6), " +
                     "(7, 1, 1, 7, 14, 7.5, 'g', 'g', 7, '2024-01-01 00:00:07', 1, 2, [1, 2], [7, 14], 6, 7, 7), " +
                     "(8, 1, 1, 8, 16, 8.5, 'h', null, null, '2024-01-01 00:00:08', 1, 2, [1, 2], [8, 16], 7, 8, 8)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void StatisticalAggregates_ShouldMatchThePinnedValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.entropy(x.X),
                En = SqlFunctions.ClickHouse.entropy(x.Nx),
                D = SqlFunctions.ClickHouse.delta_sum(x.X),
                Dt = SqlFunctions.ClickHouse.delta_sum_timestamp(x.X, x.Ts),
                Sp = SqlFunctions.ClickHouse.skew_pop(x.X),
                Ss = SqlFunctions.ClickHouse.skew_samp(x.X),
                Kp = SqlFunctions.ClickHouse.kurt_pop(x.X),
                Ks = SqlFunctions.ClickHouse.kurt_samp(x.X),
                R = SqlFunctions.ClickHouse.rank_corr(x.X, x.Y),
                C = SqlFunctions.ClickHouse.contingency(x.X, x.Y),
                V = SqlFunctions.ClickHouse.cramers_v(x.X, x.Y),
                Vb = SqlFunctions.ClickHouse.cramers_v_bias_corrected(x.X, x.Y),
                U = SqlFunctions.ClickHouse.theils_u(x.X, x.Y),
                Sc = SqlFunctions.ClickHouse.sum_count(x.X),
                Sk = SqlFunctions.ClickHouse.sum_kahan(x.F),
                U3 = SqlFunctions.ClickHouse.uniq_up_to(3, x.G),
                U1 = SqlFunctions.ClickHouse.uniq_up_to(1, x.X)
            })
            .Single();

        using (new AssertionScope())
        {
            // entropy is log2(8) over the non-null values and log2(4) over the four non-null Nx values.
            row.E.Should().Be(3);
            row.En.Should().Be(2);
            row.D.Should().Be(7);
            row.Dt.Should().Be(7);
            row.Sp.Should().Be(0);
            row.Ss.Should().Be(0);
            row.Kp.Should().BeApproximately(1.7619047619047619, 1e-12);
            row.Ks.Should().BeApproximately(1.3489583333333333, 1e-12);
            row.R.Should().Be(1);
            row.C.Should().BeApproximately(0.6585046078685182, 1e-12);
            row.V.Should().BeApproximately(0.9354143466934853, 1e-12);
            row.Vb.Should().Be(double.NaN);
            row.U.Should().Be(-1);
            row.Sc.Should().Be(Tuple.Create(36, 8L));
            row.Sk.Should().Be(40);
            row.U3.Should().Be(2, "g has two distinct values");
            row.U1.Should().Be(2, "uniqUpTo(N) returns N + 1 once the limit is exceeded");
        }
    }

    [Fact]
    public void RegressionAndTwoPopulationTests_ShouldMatchThePinnedValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                L = SqlFunctions.ClickHouse.simple_linear_regression(x.X, x.Y),
                W = SqlFunctions.ClickHouse.welch_t_test(x.F, x.G),
                T = SqlFunctions.ClickHouse.student_t_test(x.F, x.G),
                M = SqlFunctions.ClickHouse.mann_whitney_u_test(x.F, x.G),
                K = SqlFunctions.ClickHouse.kolmogorov_smirnov_test(x.F, x.G),
                Z = SqlFunctions.ClickHouse.mean_z_test(1.0, 1.0, 0.95, x.F, x.G),
                A = SqlFunctions.ClickHouse.analysis_of_variance(x.F, x.Gu)
            })
            .Single();

        using (new AssertionScope())
        {
            row.L.Should().Be(Tuple.Create(2.0, 0.0));
            row.W.Item1.Should().BeApproximately(-4.381780460041329, 1e-9);
            row.W.Item2.Should().BeApproximately(0.004659214943993937, 1e-12);
            row.T.Should().Be(row.W, "with equal variances Student's t equals Welch's t on this sample");
            row.M.Item1.Should().Be(0);
            row.M.Item2.Should().BeApproximately(0.0303828219765776, 1e-12);
            row.K.Item1.Should().Be(1);
            row.K.Item2.Should().BeApproximately(0.02857142857142857, 1e-12);
            row.Z.Item1.Should().BeApproximately(-5.65685424949238, 1e-9);
            row.Z.Item2.Should().BeApproximately(1.5417257900280076e-8, 1e-14);
            row.Z.Item3.Should().BeApproximately(-5.385903824349678, 1e-9);
            row.Z.Item4.Should().BeApproximately(-2.614096175650322, 1e-9);
            row.A.Item1.Should().BeApproximately(19.2, 1e-9);
            row.A.Item2.Should().BeApproximately(0.004659214943993906, 1e-12);
        }
    }

    [Fact]
    public void ExtremeRowAggregates_ShouldSkipNullsAndExposeTheDeclaredShape()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.first_value(x.X),
                L = SqlFunctions.ClickHouse.last_value(x.X),
                Ln = SqlFunctions.ClickHouse.last_value(x.Sn),
                H = SqlFunctions.ClickHouse.any_heavy(x.S)
            })
            .Single();

        using (new AssertionScope())
        {
            row.F.Should().Be(1);
            row.L.Should().Be(8);
            row.Ln.Should().Be("g", "the aggregate forms skip NULL rows");
            row.H.Should().BeOneOf("a", "b", "c", "d", "e", "f", "g", "h");
        }
    }

    [Fact]
    public void IntervalAggregates_ShouldCountTheIntersections()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                M = SqlFunctions.ClickHouse.max_intersections(x.Starts, x.Ends),
                P = SqlFunctions.ClickHouse.max_intersections_position(x.Starts, x.Ends)
            })
            .Single();

        using (new AssertionScope())
        {
            row.M.Should().Be(5);
            row.P.Should().Be(1);
        }
    }

    [Fact]
    public void QuantileAlgorithms_ShouldMatchThePinnedValuesAndShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                T = SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.X),
                G = SqlFunctions.ClickHouse.quantile_gk(100, 0.5, x.X),
                D = SqlFunctions.ClickHouse.quantile_dd(0.01, 0.5, x.X),
                B = SqlFunctions.ClickHouse.quantile_bfloat16(0.5, x.X),
                E = SqlFunctions.ClickHouse.quantiles_exact(new[] { 0.25, 0.75 }, x.X),
                El = SqlFunctions.ClickHouse.quantiles_exact_low(new[] { 0.25, 0.75 }, x.X),
                Eh = SqlFunctions.ClickHouse.quantiles_exact_high(new[] { 0.25, 0.75 }, x.X),
                Ti = SqlFunctions.ClickHouse.quantiles_timing(new[] { 0.25, 0.75 }, x.X),
                Td = SqlFunctions.ClickHouse.quantiles_tdigest(new[] { 0.25, 0.75 }, x.X),
                Bf = SqlFunctions.ClickHouse.quantiles_bfloat16(new[] { 0.25, 0.75 }, x.X),
                Dd = SqlFunctions.ClickHouse.quantiles_dd(0.01, new[] { 0.25, 0.75 }, x.X),
                Gk = SqlFunctions.ClickHouse.quantiles_gk(100, new[] { 0.25, 0.75 }, x.X),
                W = SqlFunctions.ClickHouse.quantiles_exact_weighted(new[] { 0.25, 0.75 }, x.X, x.Wu)
            })
            .Single();

        using (new AssertionScope())
        {
            row.T.Should().Be(4);
            row.G.Should().Be(4);
            row.D.Should().BeApproximately(4.0148353330285875, 1e-9);
            row.B.Should().Be(4);
            row.E.Should().Equal(3.0, 7.0);
            row.El.Should().Equal(3.0, 7.0);
            row.Eh.Should().Equal(3.0, 7.0);
            row.Ti.Should().Equal(3.0, 7.0);
            row.Td.Should().Equal(2.0, 6.0);
            row.Bf.Should().Equal(2.0, 6.0);
            row.Dd[0].Should().BeApproximately(1.9936617014173448, 1e-9);
            row.Dd[1].Should().BeApproximately(5.989510371172621, 1e-9);
            row.Gk.Should().Equal(2.0, 6.0);
            row.W.Should().Equal(4.0, 7.0);
        }
    }

    [Fact]
    public void TopKAndMappedArrayAggregates_ShouldReturnTheirCompositeShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                K = SqlFunctions.ClickHouse.approx_top_k(2, x.X),
                S = SqlFunctions.ClickHouse.approx_top_sum(2, x.X, x.Y),
                Mx = SqlFunctions.ClickHouse.max_map(x.Keys, x.Vals),
                Mn = SqlFunctions.ClickHouse.min_map(x.Keys, x.Vals)
            })
            .Single();

        using (new AssertionScope())
        {
            row.K.Should().Equal(Tuple.Create(1, 1L, 0L), Tuple.Create(2, 1L, 0L));
            row.S.Should().Equal(Tuple.Create(8, 16L, 0L), Tuple.Create(7, 14L, 0L));
            row.Mx.Item1.Should().Equal(1, 2);
            row.Mx.Item2.Should().Equal(8, 16);
            row.Mn.Item1.Should().Equal(1, 2);
            row.Mn.Item2.Should().Equal(1, 2);
        }
    }

    [Fact]
    public void GroupArrayFamily_ShouldKeepTheLimitsAndTheInsertionOrder()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.group_array_moving_avg(x.X),
                S = SqlFunctions.ClickHouse.group_array_moving_sum(x.X),
                So = SqlFunctions.ClickHouse.group_array_sorted(3, x.X),
                La = SqlFunctions.ClickHouse.group_array_last(3, x.X),
                Sa = SqlFunctions.ClickHouse.group_array_sample(3, x.X),
                I = SqlFunctions.ClickHouse.group_array_insert_at(x.X, x.Pos),
                X = SqlFunctions.ClickHouse.group_array_intersect(x.Keys)
            })
            .Single();

        using (new AssertionScope())
        {
            row.A.Should().Equal(0.125, 0.375, 0.75, 1.25, 1.875, 2.625, 3.5, 4.5);
            row.S.Should().Equal(1L, 3L, 6L, 10L, 15L, 21L, 28L, 36L);
            row.So.Should().Equal(1, 2, 3);
            row.La.Should().BeEquivalentTo(new[] { 6, 7, 8 }, "the last-three order inside a block is unspecified");
            row.Sa.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.BeSubsetOf(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            row.I.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
            row.X.Should().Equal(1, 2);
        }
    }

    [Fact]
    public void StringAndBucketAggregates_ShouldMatchThePinnedValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                C = SqlFunctions.ClickHouse.group_concat(x.S),
                Cs = SqlFunctions.ClickHouse.group_concat("-", x.S),
                Cm = SqlFunctions.ClickHouse.group_concat("-", 2, x.S),
                H = SqlFunctions.ClickHouse.histogram(3, x.X),
                B = SqlFunctions.ClickHouse.sparkbar(4, x.Xu, x.Y)
            })
            .Single();

        using (new AssertionScope())
        {
            row.C.Should().HaveLength(8);
            row.C!.OrderBy(c => c).Should().Equal("abcdefgh".ToCharArray());
            row.Cs.Should().HaveLength(15);
            row.Cm!.Split('-').Should().HaveCount(2, "the max-elements parameter caps the concatenation");
            row.H.Should().HaveCount(3);
            row.H.Should().Equal(Tuple.Create(1.0, 4.0, 3.75), Tuple.Create(4.0, 6.5, 2.25), Tuple.Create(6.5, 8.0, 2.0));
            row.B.Should().Be("▂▄▆█", "sparkbar renders the four frequency blocks as a string");
        }
    }

    [Fact]
    public void SequenceAggregates_ShouldMatchThePattern()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Settings(("allow_experimental_funnel_functions", "1"))
            .Select(x => new
            {
                C = SqlFunctions.ClickHouse.sequence_count("(?1)(?2)", x.Ts, x.X == 1, x.X == 2),
                E = SqlFunctions.ClickHouse.sequence_match_events("(?1)(?2)", x.Ts, x.X == 1, x.X == 2),
                N = SqlFunctions.ClickHouse.sequence_next_node("forward", "head", x.Ts, x.S, x.X == 1, x.X == 1, x.X == 2)
            })
            .ToList()
            .Single();

        using (new AssertionScope())
        {
            row.C.Should().Be(1);
            row.E.Should().Equal(new DateTime(2024, 1, 1, 0, 0, 1), new DateTime(2024, 1, 1, 0, 0, 2));
            row.N.Should().Be("c", "the next event after A -> B is C");
        }
    }

    [Fact]
    public void TimeSeriesGrids_ShouldRespectTheExperimentalSetting()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Settings(("allow_experimental_time_series_aggregate_functions", "1"))
            .Select(x => new
            {
                R = SqlFunctions.ClickHouse.time_series_rate_to_grid(1704067201, 1704067208, 1, 5, x.Ts, x.F),
                I = SqlFunctions.ClickHouse.time_series_instant_rate_to_grid(1704067201, 1704067208, 1, 5, x.Ts, x.F),
                S = SqlFunctions.ClickHouse.time_series_resample_to_grid_with_staleness(1704067201, 1704067208, 1, 5, x.Ts, x.F)
            })
            .ToList()
            .Single();

        using (new AssertionScope())
        {
            row.R.Should().Equal(new double?[] { null, 0.3, 0.5, 0.7, 1.0, 1.0, 1.0, 1.0 });
            row.I.Should().Equal(new double?[] { null, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 });
            row.S.Should().Equal(1.5, 2.5, 3.5, 4.5, 5.5, 6.5, 7.5, 8.5);
        }
    }

    [Fact]
    public void EmptyInput_ShouldReturnTheVendorDefaultsAndAGroupingShouldReturnNoRows()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Where(x => x.Id > 100)
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.entropy(x.X),
                D = SqlFunctions.ClickHouse.delta_sum(x.X),
                Sc = SqlFunctions.ClickHouse.sum_count(x.X),
                Q = SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.X),
                A = SqlFunctions.ClickHouse.group_array_sorted(3, x.X),
                U = SqlFunctions.ClickHouse.uniq_up_to(3, x.X),
                M = SqlFunctions.ClickHouse.max_intersections(x.Starts, x.Ends),
                H = SqlFunctions.ClickHouse.histogram(3, x.X),
                L = SqlFunctions.ClickHouse.simple_linear_regression(x.X, x.Y),
                C = SqlFunctions.ClickHouse.group_concat(x.S)
            })
            .Single();

        using (new AssertionScope())
        {
            row.E.Should().Be(0);
            row.D.Should().Be(0);
            row.Sc.Should().Be(Tuple.Create(0, 0L));
            row.Q.Should().Be(double.NaN, "the quantile aggregate has no defined value on an empty sample");
            row.A.Should().BeEmpty();
            row.U.Should().Be(0);
            row.M.Should().Be(0);
            row.H.Should().BeEmpty();
            row.L.Item1.Should().Be(double.NaN);
            row.L.Item2.Should().Be(double.NaN);
            row.C.Should().BeEmpty();
        }

        var groups = ctx.From<IT286bAggregateRow>()
            .Where(x => x.Id > 100)
            .GroupBy(x => new { x.G })
            .Select(x => new { x.G, E = SqlFunctions.ClickHouse.entropy(x.X) })
            .ToList();

        groups.Should().BeEmpty("a grouping over no rows produces no groups, not one empty group");
    }

    [Fact]
    public void Nulls_ShouldBeSkippedAndTheNullableShapesPreserved()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.entropy(x.Nx),
                Q = SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.Nx),
                L = SqlFunctions.ClickHouse.last_value(x.Sn),
                C = SqlFunctions.ClickHouse.group_concat(x.Sn)
            })
            .Single();

        using (new AssertionScope())
        {
            row.E.Should().Be(2, "the four NULL Nx rows are skipped");
            row.Q.Should().Be(3, "the t-digest median is approximate: the four non-NULL values 1, 3, 5, 7 give 3");
            row.L.Should().Be("g");
            row.C.Should().HaveLength(4);
            row.C!.All(c => "aceg".Contains(c)).Should().BeTrue("the NULL rows are skipped");
        }
    }

    [Fact]
    public void Grouping_ShouldComputePerGroupValues()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286bAggregateRow>()
            .GroupBy(x => new { x.G })
            .Select(x => new
            {
                x.G,
                S = SqlFunctions.ClickHouse.sum_kahan(x.F),
                U = SqlFunctions.ClickHouse.uniq_up_to(10, x.X)
            })
            .ToList()
            .OrderBy(x => x.G)
            .ToList();

        using (new AssertionScope())
        {
            rows.Should().HaveCount(2);
            rows[0].S.Should().Be(12);
            rows[0].U.Should().Be(4);
            rows[1].S.Should().Be(28);
            rows[1].U.Should().Be(4);
        }
    }

    [Fact]
    public void WindowOnlyDecayedAggregates_ShouldExecuteWithOver()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                x.Id,
                S = SqlFunctions.ClickHouse.exponential_time_decayed_sum(2, x.X, x.Ts)
                    .Over(SqlFunctions.Sql.asc(() => x.Ts)),
                M = SqlFunctions.ClickHouse.exponential_time_decayed_max(2, x.X, x.Ts)
                    .Over(SqlFunctions.Sql.asc(() => x.Ts)),
                C = SqlFunctions.ClickHouse.exponential_time_decayed_count(2, x.Ts)
                    .Over(SqlFunctions.Sql.asc(() => x.Ts)),
                A = SqlFunctions.ClickHouse.exponential_time_decayed_avg(2, x.X, x.Ts)
                    .Over(SqlFunctions.Sql.asc(() => x.Ts))
            })
            .ToList()
            .OrderBy(x => x.Id)
            .ToList();

        using (new AssertionScope())
        {
            rows.Should().HaveCount(8);
            rows[0].S.Should().Be(1);
            rows[1].S.Should().BeApproximately(2.606530659712633, 1e-12);
            rows[7].S.Should().BeApproximately(16.48600971473543, 1e-9);
            rows.Select(x => x.M).Should().Equal(1.0, 2, 3, 4, 5, 6, 7, 8);
            rows[7].C.Should().BeApproximately(2.4949449946831996, 1e-12);
            rows[7].A.Should().BeApproximately(6.607764800373394, 1e-9);
        }
    }

    [Fact]
    public void VersionAbsentMembers_ShouldBeRejectedByThePinnedServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            Action gini = () => ctx.From<IT286bAggregateRow>()
                .Select(x => new { V = SqlFunctions.ClickHouse.gini(x.X) }).ToList();
            gini.Should().Throw<Exception>().WithMessage("*gini*does not exist*");

            Action student = () => ctx.From<IT286bAggregateRow>()
                .Select(x => new { V = SqlFunctions.ClickHouse.student_t_test_one_sample(x.X, 2) }).ToList();
            student.Should().Throw<Exception>().WithMessage("*studentTTestOneSample*does not exist*");

            Action format = () => ctx.From<IT286bAggregateRow>()
                .Select(x => new { V = SqlFunctions.ClickHouse.group_format("CSV", x.X) }).ToList();
            format.Should().Throw<Exception>().WithMessage("*groupFormat*does not exist*");

            Action prometheus = () => ctx.From<IT286bAggregateRow>()
                .Select(x => new { V = SqlFunctions.ClickHouse.quantile_prometheus_histogram(0.5, x.F, x.Wu) }).ToList();
            prometheus.Should().Throw<Exception>().WithMessage("*quantilePrometheusHistogram*does not exist*");

            Action pluralPrometheus = () => ctx.From<IT286bAggregateRow>()
                .Select(x => new { V = SqlFunctions.ClickHouse.quantiles_prometheus_histogram(new[] { 0.5 }, x.F, x.Wu) }).ToList();
            pluralPrometheus.Should().Throw<Exception>().WithMessage("*quantilesPrometheusHistogram*does not exist*");

            Action groupSum = () => ctx.From<IT286bAggregateRow>()
                .Select(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.time_series_group_sum(x.Ts, x.F))).ToList();
            groupSum.Should().Throw<Exception>().WithMessage("*timeSeriesGroupSum*does not exist*");

            Action groupRateSum = () => ctx.From<IT286bAggregateRow>()
                .Select(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.time_series_group_rate_sum(x.Ts, x.F))).ToList();
            groupRateSum.Should().Throw<Exception>().WithMessage("*timeSeriesGroupRateSum*does not exist*");
        }
    }

    [Fact]
    public void StateReturningAggregates_ShouldBeRefusedBeforeExecution()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => ctx.From<IT286bAggregateRow>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.stochastic_linear_regression(x.X, x.Y) }).ToList())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*stochastic_linear_regression*AggregateFunction state*");

            FluentActions.Invoking(() => ctx.From<IT286bAggregateRow>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.stochastic_logistic_regression(x.X, x.Y) }).ToList())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*stochastic_logistic_regression*AggregateFunction state*");

            FluentActions.Invoking(() => ctx.From<IT286bAggregateRow>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.exponential_moving_average(10, x.X, x.Ts) }).ToList())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*exponential_moving_average*AggregateFunction state*");
        }
    }

    [Fact]
    public void DeclaredResultTypes_ShouldMatchThePinnedServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bAggregateRow>()
            .Select(x => new
            {
                Q = SqlFunctions.ClickHouse.quantile_gk(100, 0.5, x.X),
                Qs = SqlFunctions.ClickHouse.quantiles_gk(100, new[] { 0.5 }, x.X),
                H = SqlFunctions.ClickHouse.histogram(1, x.X),
                K = SqlFunctions.ClickHouse.approx_top_k(1, x.X),
                M = SqlFunctions.ClickHouse.max_map(x.Keys, x.Vals),
                Sc = SqlFunctions.ClickHouse.sum_count(x.X)
            })
            .Single();

        using (new AssertionScope())
        {
            row.Q.HasValue.Should().BeTrue();
            row.Q!.Value.GetType().Should().Be(typeof(double));
            row.Qs.Should().BeOfType<double[]>();
            row.H.Should().BeOfType<Tuple<double, double, double>[]>();
            row.K.Should().BeOfType<Tuple<int, long, long>[]>();
            row.M.Should().BeOfType<Tuple<int[], int[]>>();
            row.Sc.Should().BeOfType<Tuple<int, long>>();
        }
    }
}
