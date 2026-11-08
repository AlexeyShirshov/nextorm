using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D16 (F16 Aggregate): the ClickHouse-only aggregate catalogue. The tests pin every vendor
/// spelling and result normalisation of the <see cref="IClickHouseAggregateRenderer"/> arms (the
/// signed narrowing of the unsigned counters, <c>Float64</c> for the approximate quantile
/// algorithms, and the <c>Array/Tuple</c> casts of the composite results), the two-parenthesis
/// grammar and its flattened plural-quantile levels, the captured-value parameter binding, the
/// declared CLR shapes, the deterministic refusals of the state-returning aggregates, the
/// fail-closed provider guard on PostgreSQL and SQL Server, and the window parameter-pair grammar of
/// the window-only <c>exponentialTimeDecayed*</c> family. No connection is opened.
/// </summary>
public class T286bF16AggregateSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f16_entity")]
    internal interface IT286bAggregateEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("g")]
        int G { get; set; }

        [Column("x")]
        int X { get; set; }

        [Column("y")]
        int Y { get; set; }

        [Column("f")]
        double F { get; set; }

        [Column("s")]
        string S { get; set; }

        [Column("ts")]
        DateTime Ts { get; set; }

        [Column("keys")]
        int[] Keys { get; set; }

        [Column("vals")]
        int[] Vals { get; set; }
    }

    /// <summary>
    /// Every delivered render arm, written out literally from the dialect contract: 69 members over
    /// 75 catalog rows (the <c>quantiles</c> and <c>groupArray</c> literals are already covered and
    /// are re-verified through their shipped arms).
    /// </summary>
    private static readonly (string Name, string[]? Parameters, string[] Arguments, Type ResultType, string Expected)[] RenderArms =
    [
        ("entropy", null, ["a"], typeof(double), "entropy(a)"),
        ("first_value", null, ["a"], typeof(int), "first_value(a)"),
        ("last_value", null, ["a"], typeof(int), "last_value(a)"),
        ("max_intersections", null, ["a", "b"], typeof(long), "toInt64(maxIntersections(a, b))"),
        ("max_intersections_position", null, ["a", "b"], typeof(int), "toInt32(maxIntersectionsPosition(a, b))"),
        ("simple_linear_regression", null, ["a", "b"], typeof(Tuple<double, double>), "cast(simpleLinearRegression(a, b) as Tuple(Float64, Float64))"),
        ("delta_sum", null, ["a"], typeof(int), "cast(deltaSum(a) as Int32)"),
        ("delta_sum_timestamp", null, ["a", "b"], typeof(int), "cast(deltaSumTimestamp(a, b) as Int32)"),
        ("gini", null, ["a"], typeof(double), "gini(a)"),
        ("skew_pop", null, ["a"], typeof(double), "skewPop(a)"),
        ("skew_samp", null, ["a"], typeof(double), "skewSamp(a)"),
        ("kurt_pop", null, ["a"], typeof(double), "kurtPop(a)"),
        ("kurt_samp", null, ["a"], typeof(double), "kurtSamp(a)"),
        ("rank_corr", null, ["a", "b"], typeof(double), "rankCorr(a, b)"),
        ("contingency", null, ["a", "b"], typeof(double), "contingency(a, b)"),
        ("cramers_v", null, ["a", "b"], typeof(double), "cramersV(a, b)"),
        ("cramers_v_bias_corrected", null, ["a", "b"], typeof(double), "cramersVBiasCorrected(a, b)"),
        ("theils_u", null, ["a", "b"], typeof(double), "theilsU(a, b)"),
        ("welch_t_test", null, ["a", "b"], typeof(Tuple<double, double>), "cast(welchTTest(a, b) as Tuple(Float64, Float64))"),
        ("student_t_test", null, ["a", "b"], typeof(Tuple<double, double>), "cast(studentTTest(a, b) as Tuple(Float64, Float64))"),
        ("student_t_test_one_sample", null, ["a", "2"], typeof(Tuple<double, double>), "cast(studentTTestOneSample(a, 2) as Tuple(Float64, Float64))"),
        ("mann_whitney_u_test", null, ["a", "b"], typeof(Tuple<double, double>), "cast(mannWhitneyUTest(a, b) as Tuple(Float64, Float64))"),
        ("kolmogorov_smirnov_test", null, ["a", "b"], typeof(Tuple<double, double>), "cast(kolmogorovSmirnovTest(a, b) as Tuple(Float64, Float64))"),
        ("mean_z_test", ["0.5", "0.7", "0.95"], ["a", "b"], typeof(Tuple<double, double, double, double>), "cast(meanZTest(0.5, 0.7, 0.95)(a, b) as Tuple(Float64, Float64, Float64, Float64))"),
        ("analysis_of_variance", null, ["a", "b"], typeof(Tuple<double, double>), "cast(analysisOfVariance(a, b) as Tuple(Float64, Float64))"),
        ("quantile_tdigest", ["0.5"], ["a"], typeof(double?), "toFloat64(quantileTDigest(0.5)(a))"),
        ("quantile_gk", ["100", "0.5"], ["a"], typeof(double?), "toFloat64(quantileGK(100, 0.5)(a))"),
        ("quantile_dd", ["0.01", "0.5"], ["a"], typeof(double?), "quantileDD(0.01, 0.5)(a)"),
        ("quantile_bfloat16", ["0.5"], ["a"], typeof(double?), "quantileBFloat16(0.5)(a)"),
        ("quantile_prometheus_histogram", ["0.5"], ["a", "b"], typeof(double?), "toFloat64(quantilePrometheusHistogram(0.5)(a, b))"),
        ("quantiles_exact", ["0.25", "0.75"], ["a"], typeof(double[]), "cast(quantilesExact(0.25, 0.75)(a) as Array(Float64))"),
        ("quantiles_exact_low", ["0.5"], ["a"], typeof(double[]), "cast(quantilesExactLow(0.5)(a) as Array(Float64))"),
        ("quantiles_exact_high", ["0.5"], ["a"], typeof(double[]), "cast(quantilesExactHigh(0.5)(a) as Array(Float64))"),
        ("quantiles_exact_weighted", ["0.5"], ["a", "w"], typeof(double[]), "cast(quantilesExactWeighted(0.5)(a, w) as Array(Float64))"),
        ("quantiles_timing", ["0.5"], ["a"], typeof(double[]), "cast(quantilesTiming(0.5)(a) as Array(Float64))"),
        ("quantiles_tdigest", ["0.5"], ["a"], typeof(double[]), "cast(quantilesTDigest(0.5)(a) as Array(Float64))"),
        ("quantiles_bfloat16", ["0.5"], ["a"], typeof(double[]), "cast(quantilesBFloat16(0.5)(a) as Array(Float64))"),
        ("quantiles_dd", ["0.01", "0.5", "0.9"], ["a"], typeof(double[]), "cast(quantilesDD(0.01, 0.5, 0.9)(a) as Array(Float64))"),
        ("quantiles_gk", ["100", "0.5", "0.9"], ["a"], typeof(double[]), "cast(quantilesGK(100, 0.5, 0.9)(a) as Array(Float64))"),
        ("quantiles_prometheus_histogram", ["0.5", "0.9"], ["a", "b"], typeof(double[]), "cast(quantilesPrometheusHistogram(0.5, 0.9)(a, b) as Array(Float64))"),
        ("approx_top_k", ["2"], ["a"], typeof(Tuple<int, long, long>[]), "cast(approx_top_k(2)(a) as Array(Tuple(Int32, Int64, Int64)))"),
        ("approx_top_sum", ["2"], ["a", "b"], typeof(Tuple<int, long, long>[]), "cast(approx_top_sum(2)(a, b) as Array(Tuple(Int32, Int64, Int64)))"),
        ("any_heavy", null, ["a"], typeof(int), "anyHeavy(a)"),
        ("max_map", null, ["a", "b"], typeof(Tuple<int[], int[]>), "cast(maxMappedArrays(a, b) as Tuple(Array(Int32), Array(Int32)))"),
        ("min_map", null, ["a", "b"], typeof(Tuple<int[], int[]>), "cast(minMappedArrays(a, b) as Tuple(Array(Int32), Array(Int32)))"),
        ("sum_count", null, ["a"], typeof(Tuple<int, long>), "cast(sumCount(a) as Tuple(Int32, Int64))"),
        ("sum_kahan", null, ["a"], typeof(double), "sumKahan(a)"),
        ("group_array_moving_avg", null, ["a"], typeof(double[]), "cast(groupArrayMovingAvg(a) as Array(Float64))"),
        ("group_array_moving_sum", null, ["a"], typeof(long[]), "cast(groupArrayMovingSum(a) as Array(Int64))"),
        ("group_array_sample", ["2"], ["a"], typeof(int[]), "cast(groupArraySample(2)(a) as Array(Int32))"),
        ("group_array_sorted", ["2"], ["a"], typeof(int[]), "cast(groupArraySorted(2)(a) as Array(Int32))"),
        ("group_array_last", ["2"], ["a"], typeof(int[]), "cast(groupArrayLast(2)(a) as Array(Int32))"),
        ("group_array_insert_at", null, ["a", "1"], typeof(int[]), "cast(groupArrayInsertAt(a, 1) as Array(Int32))"),
        ("group_array_intersect", null, ["a"], typeof(int[]), "cast(groupArrayIntersect(a) as Array(Int32))"),
        ("group_concat", null, ["a"], typeof(string), "groupConcat(a)"),
        ("group_format", ["'CSV'"], ["a", "b"], typeof(string), "groupFormat('CSV')(a, b)"),
        ("histogram", ["3"], ["a"], typeof(Tuple<double, double, double>[]), "cast(histogram(3)(a) as Array(Tuple(Float64, Float64, Float64)))"),
        ("sparkbar", ["3"], ["a", "b"], typeof(string), "sparkbar(3)(a, b)"),
        ("uniq_up_to", ["3"], ["a"], typeof(long), "toInt64(uniqUpTo(3)(a))"),
        ("sequence_count", ["'(?1)(?2)'"], ["a", "(b = 1)", "(b = 2)"], typeof(long), "toInt64(sequenceCount('(?1)(?2)')(a, (b = 1), (b = 2)))"),
        ("sequence_match_events", ["'(?1)(?2)'"], ["a", "(b = 1)", "(b = 2)"], typeof(DateTime[]), "cast(sequenceMatchEvents('(?1)(?2)')(a, (b = 1), (b = 2)) as Array(DateTime))"),
        ("sequence_next_node", ["'forward'", "'head'"], ["a", "s", "(b = 1)", "(b = 1)", "(b = 2)"], typeof(string), "cast(sequenceNextNode('forward', 'head')(a, s, (b = 1), (b = 1), (b = 2)) as Nullable(String))"),
        ("time_series_group_sum", null, ["a", "b"], typeof(Tuple<DateTime, double>[]), "cast(timeSeriesGroupSum(a, b) as Array(Tuple(DateTime, Float64)))"),
        ("time_series_group_rate_sum", null, ["a", "b"], typeof(Tuple<DateTime, double>[]), "cast(timeSeriesGroupRateSum(a, b) as Array(Tuple(DateTime, Float64)))"),
        ("time_series_instant_rate_to_grid", ["0", "210", "15", "45"], ["a", "b"], typeof(double?[]), "cast(timeSeriesInstantRateToGrid(0, 210, 15, 45)(a, b) as Array(Nullable(Float64)))"),
        ("time_series_rate_to_grid", ["0", "210", "15", "45"], ["a", "b"], typeof(double?[]), "cast(timeSeriesRateToGrid(0, 210, 15, 45)(a, b) as Array(Nullable(Float64)))"),
        ("time_series_resample_to_grid_with_staleness", ["0", "210", "15", "45"], ["a", "b"], typeof(double?[]), "cast(timeSeriesResampleToGridWithStaleness(0, 210, 15, 45)(a, b) as Array(Nullable(Float64)))")
    ];

    [Fact]
    public void Render_ShouldEmitTheExactArmForEveryDeliveredMember()
    {
        var aggregates = ClickHouseDialect.Instance.ClickHouseAggregates!;

        using (new AssertionScope())
        {
            RenderArms.Should().HaveCount(67);
            RenderArms.Select(a => a.Name).Distinct().Should().HaveCount(67);

            foreach (var (name, parameters, arguments, resultType, expected) in RenderArms)
            {
                var actual = parameters is null
                    ? aggregates.Render(name, arguments, resultType)
                    : aggregates.RenderParameterised(name, parameters, arguments, resultType);

                actual.Should().Be(expected, "'{0}' is the ClickHouse aggregate arm", name);
            }
        }
    }

    [Fact]
    public void Supports_ShouldAcceptTheWholeCatalogueAndNothingElse()
    {
        var aggregates = ClickHouseDialect.Instance.ClickHouseAggregates!;

        using (new AssertionScope())
        {
            foreach (var (name, _, _, _, _) in RenderArms)
                aggregates.Supports(name).Should().BeTrue("'{0}' is delivered", name);

            aggregates.Supports("not_an_aggregate").Should().BeFalse();
            aggregates.Supports("quantile").Should().BeFalse("the shipped quantile family has its own renderer");
            aggregates.Supports("group_array").Should().BeFalse("the shipped groupArray has its own arm");
        }
    }

    [Fact]
    public void PlainMembers_ShouldRenderTheVendorSpellingOverTheProjectedColumn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            E = SqlFunctions.ClickHouse.entropy(x.X),
            R = SqlFunctions.ClickHouse.rank_corr(x.X, x.Y),
            D = SqlFunctions.ClickHouse.delta_sum(x.X),
            A = SqlFunctions.ClickHouse.any_heavy(x.S),
            S = SqlFunctions.ClickHouse.sum_kahan(x.F)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("entropy(x) as `E`");
            sql.Should().Contain("rankCorr(x, y) as `R`");
            sql.Should().Contain("cast(deltaSum(x) as Int32) as `D`");
            sql.Should().Contain("anyHeavy(s) as `A`");
            sql.Should().Contain("sumKahan(f) as `S`");
        }
    }

    [Fact]
    public void ParameterisedMembers_ShouldUseTheTwoParenthesisGrammar()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.X),
            G = SqlFunctions.ClickHouse.quantile_gk(100, 0.5, x.X),
            D = SqlFunctions.ClickHouse.quantile_dd(0.01, 0.5, x.X),
            K = SqlFunctions.ClickHouse.approx_top_k(2, x.X),
            U = SqlFunctions.ClickHouse.uniq_up_to(3, x.X),
            H = SqlFunctions.ClickHouse.histogram(3, x.X),
            B = SqlFunctions.ClickHouse.sparkbar(3, x.X, x.Y)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toFloat64(quantileTDigest(0.5)(x)) as `Q`");
            sql.Should().Contain("toFloat64(quantileGK(100, 0.5)(x)) as `G`");
            sql.Should().Contain("quantileDD(0.01, 0.5)(x) as `D`");
            sql.Should().Contain("cast(approx_top_k(2)(x) as Array(Tuple(Int32, Int64, Int64))) as `K`");
            sql.Should().Contain("toInt64(uniqUpTo(3)(x)) as `U`");
            sql.Should().Contain("cast(histogram(3)(x) as Array(Tuple(Float64, Float64, Float64))) as `H`");
            sql.Should().Contain("sparkbar(3)(x, y) as `B`");
        }
    }

    [Fact]
    public void PluralQuantiles_ShouldFlattenTheLevelsIntoScalarParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            E = SqlFunctions.ClickHouse.quantiles_exact(new[] { 0.25, 0.5, 0.75 }, x.X),
            T = SqlFunctions.ClickHouse.quantiles_timing(new[] { 0.5, 0.9 }, x.X),
            D = SqlFunctions.ClickHouse.quantiles_dd(0.01, new[] { 0.5, 0.9 }, x.X),
            G = SqlFunctions.ClickHouse.quantiles_gk(100, new[] { 0.5, 0.9 }, x.X),
            W = SqlFunctions.ClickHouse.quantiles_exact_weighted(new[] { 0.5 }, x.X, x.Y)
        }));

        using (new AssertionScope())
        {
            // The levels are separate scalar parameters, never an array literal (the server rejects
            // an array in the parameter list with CANNOT_CONVERT_TYPE).
            sql.Should().Contain("cast(quantilesExact(0.25, 0.5, 0.75)(x) as Array(Float64)) as `E`");
            sql.Should().Contain("cast(quantilesTiming(0.5, 0.9)(x) as Array(Float64)) as `T`");
            sql.Should().Contain("cast(quantilesDD(0.01, 0.5, 0.9)(x) as Array(Float64)) as `D`");
            sql.Should().Contain("cast(quantilesGK(100, 0.5, 0.9)(x) as Array(Float64)) as `G`");
            sql.Should().Contain("cast(quantilesExactWeighted(0.5)(x, y) as Array(Float64)) as `W`");
        }

        var levels = new[] { 0.5 };
        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.quantiles_exact(levels, x.X)
        })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The quantiles levels must be inline expressions, not a captured array.");
    }

    [Fact]
    public void ParameterBinding_ShouldNumberTheParametersInSqlOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();
        var level = 0.5;
        var accuracy = 100L;
        var size = 2L;

        var prepared = Prepare(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.quantile_gk(accuracy, level, x.X),
            G = SqlFunctions.ClickHouse.group_array_sample(size, x.X)
        }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        using (new AssertionScope())
        {
            sql.Should().Contain("toFloat64(quantileGK(@accuracy, @level)(x)) as `Q`");
            sql.Should().Contain("cast(groupArraySample(@size)(x) as Array(Int32)) as `G`");
            prepared.DbCommand.Parameters.Cast<System.Data.Common.DbParameter>().Should().HaveCount(3);
            prepared.DbCommand.Parameters.Cast<System.Data.Common.DbParameter>()
                .Select(p => p.Value)
                .Should().Equal(100L, 0.5, 2L);
        }
    }

    [Fact]
    public void OptionalArity_ShouldPickTheRightVendorForm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C1 = SqlFunctions.ClickHouse.group_concat(x.S),
            C2 = SqlFunctions.ClickHouse.group_concat("-", x.S),
            C3 = SqlFunctions.ClickHouse.group_concat("-", 2, x.S),
            S1 = SqlFunctions.ClickHouse.group_array_sample(2, x.X),
            S2 = SqlFunctions.ClickHouse.group_array_sample(2, 7, x.X),
            I1 = SqlFunctions.ClickHouse.group_array_insert_at(x.X, 1),
            I2 = SqlFunctions.ClickHouse.group_array_insert_at(0, 4, x.X, 1),
            B1 = SqlFunctions.ClickHouse.sparkbar(3, x.X, x.Y),
            B2 = SqlFunctions.ClickHouse.sparkbar(3, 0, 5, x.X, x.Y)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("groupConcat(s) as `C1`");
            sql.Should().Contain("groupConcat('-')(s) as `C2`");
            sql.Should().Contain("groupConcat('-', 2)(s) as `C3`");
            sql.Should().Contain("cast(groupArraySample(2)(x) as Array(Int32)) as `S1`");
            sql.Should().Contain("cast(groupArraySample(2, 7)(x) as Array(Int32)) as `S2`");
            sql.Should().Contain("cast(groupArrayInsertAt(x, 1) as Array(Int32)) as `I1`");
            sql.Should().Contain("cast(groupArrayInsertAt(0, 4)(x, 1) as Array(Int32)) as `I2`");
            sql.Should().Contain("sparkbar(3)(x, y) as `B1`");
            sql.Should().Contain("sparkbar(3, 0, 5)(x, y) as `B2`");
        }
    }

    [Fact]
    public void CompositeResults_ShouldCastToTheDeclaredClrShape()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.ClickHouse.simple_linear_regression(x.X, x.Y),
            T = SqlFunctions.ClickHouse.welch_t_test(x.X, x.Y),
            M = SqlFunctions.ClickHouse.mean_z_test(0.5, 0.7, 0.95, x.F, x.G),
            A = SqlFunctions.ClickHouse.analysis_of_variance(x.F, x.G),
            N = SqlFunctions.ClickHouse.sum_count(x.X),
            K = SqlFunctions.ClickHouse.max_map(x.Keys, x.Vals),
            I = SqlFunctions.ClickHouse.group_array_intersect(x.Keys),
            V = SqlFunctions.ClickHouse.group_array_moving_sum(x.X)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("cast(simpleLinearRegression(x, y) as Tuple(Float64, Float64)) as `L`");
            sql.Should().Contain("cast(welchTTest(x, y) as Tuple(Float64, Float64)) as `T`");
            sql.Should().Contain("cast(meanZTest(0.5, 0.7, 0.95)(f, g) as Tuple(Float64, Float64, Float64, Float64)) as `M`");
            sql.Should().Contain("cast(analysisOfVariance(f, g) as Tuple(Float64, Float64)) as `A`");
            sql.Should().Contain("cast(sumCount(x) as Tuple(Int32, Int64)) as `N`");
            sql.Should().Contain("cast(maxMappedArrays(keys, vals) as Tuple(Array(Int32), Array(Int32))) as `K`");
            sql.Should().Contain("cast(groupArrayIntersect(keys) as Array(Int32)) as `I`");
            sql.Should().Contain("cast(groupArrayMovingSum(x) as Array(Int64)) as `V`");
        }
    }

    [Fact]
    public void SequenceAggregates_ShouldFlattenTheConditionsInOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.ClickHouse.sequence_count("(?1)(?2)", x.Ts, x.X == 1, x.X == 2),
            M = SqlFunctions.ClickHouse.sequence_match_events("(?1)(?2)", x.Ts, x.X == 1, x.X == 2),
            N = SqlFunctions.ClickHouse.sequence_next_node("forward", "head", x.Ts, x.S, x.X == 1, x.X == 1, x.X == 2)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("toInt64(sequenceCount('(?1)(?2)')(ts, (x = 1), (x = 2))) as `C`");
            sql.Should().Contain("cast(sequenceMatchEvents('(?1)(?2)')(ts, (x = 1), (x = 2)) as Array(DateTime)) as `M`");
            sql.Should().Contain("cast(sequenceNextNode('forward', 'head')(ts, s, (x = 1), (x = 1), (x = 2)) as Nullable(String)) as `N`");
        }
    }

    [Fact]
    public void GroupFormat_ShouldFlattenTheValuesIntoTheArgumentList()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.group_format("JSONEachRow", x.Id, x.S)
        }));

        sql.Should().Contain("groupFormat('JSONEachRow')(id, s) as `F`");
    }

    [Fact]
    public void Grouping_ShouldKeepTheAggregatesOverTheGroupedColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e
            .GroupBy(x => new { x.G })
            .Select(x => new
            {
                x.G,
                E = SqlFunctions.ClickHouse.entropy(x.X),
                Q = SqlFunctions.ClickHouse.quantile_tdigest(0.5, x.X)
            }));

        using (new AssertionScope())
        {
            sql.Should().Contain("group by g");
            sql.Should().Contain("entropy(x) as `E`");
            sql.Should().Contain("toFloat64(quantileTDigest(0.5)(x)) as `Q`");
        }
    }

    [Fact]
    public void Refusals_ShouldThrowWithThePreciseReasonOnEveryProvider()
    {
        using var clickHouse = ClickHouseTestContext.Create();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());

        var message =
            "The stochastic_linear_regression aggregate returns an AggregateFunction state (an opaque binary " +
            "serialization) and has no typed CLR result; only the -Merge combinator yields weights, and it " +
            "requires an AggregateFunction-typed column the provider cannot read.";

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(clickHouse, clickHouse.From<IT286bAggregateEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.stochastic_linear_regression(x.X, x.Y) })))
                .Should().Throw<NotSupportedException>().WithMessage(message);

            FluentActions.Invoking(() => SqlOf(postgres, postgres.From<IT286bAggregateEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.stochastic_logistic_regression(x.X, x.Y) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("The stochastic_logistic_regression aggregate returns an AggregateFunction state " +
                    "(an opaque binary serialization) and has no typed CLR result; only the -Merge combinator " +
                    "yields weights, and it requires an AggregateFunction-typed column the provider cannot read.");

            FluentActions.Invoking(() => SqlOf(clickHouse, clickHouse.From<IT286bAggregateEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.exponential_moving_average(10, x.X, x.Ts) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("The exponential_moving_average aggregate returns an AggregateFunction state " +
                    "(an opaque binary serialization) and has no typed CLR result; only the -Merge combinator " +
                    "yields the moving average, and it requires an AggregateFunction-typed column the provider " +
                    "cannot read.");
        }
    }

    [Fact]
    public void ProviderGuard_ShouldFailClosedOnPostgresAndSqlServer()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        foreach (var ctx in new IDataContext[] { postgres, sqlServer })
        {
            using (new AssertionScope())
            {
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bAggregateEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.entropy(x.X) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The entropy aggregate is not supported by this provider.");

                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bAggregateEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.quantiles_exact(new[] { 0.5 }, x.X) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The quantiles_exact aggregate is not supported by this provider.");

                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bAggregateEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.sequence_count("(?1)", x.Ts, x.X == 1) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The sequence_count aggregate is not supported by this provider.");

                // first_value/last_value are aggregates here (the window form is the inherited
                // CommonFunctions member); the bare aggregate call must fail closed, not report the
                // window message.
                FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bAggregateEntity>()
                        .Select(x => new { V = SqlFunctions.ClickHouse.first_value(x.X) })))
                    .Should().Throw<NotSupportedException>()
                    .WithMessage("The first_value aggregate is not supported by this provider.");
            }
        }
    }

    [Fact]
    public void WindowParameterPair_ShouldRenderTheDecayThenTheWindowArguments()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.exponential_time_decayed_sum(2, x.X, x.Ts)
                .Over(SqlFunctions.Sql.asc(() => x.Ts)),
            M = SqlFunctions.ClickHouse.exponential_time_decayed_max(2, x.X, x.Ts)
                .Over(SqlFunctions.Sql.asc(() => x.Ts)),
            C = SqlFunctions.ClickHouse.exponential_time_decayed_count(2, x.Ts)
                .Over(SqlFunctions.Sql.asc(() => x.Ts)),
            A = SqlFunctions.ClickHouse.exponential_time_decayed_avg(2, x.F, x.Ts)
                .Over(SqlFunctions.Sql.asc(() => x.Ts))
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("exponentialTimeDecayedSum(2)(x, ts) over (order by ts) as `S`");
            sql.Should().Contain("exponentialTimeDecayedMax(2)(x, ts) over (order by ts) as `M`");
            sql.Should().Contain("exponentialTimeDecayedCount(2)(ts) over (order by ts) as `C`");
            sql.Should().Contain("exponentialTimeDecayedAvg(2)(f, ts) over (order by ts) as `A`");
        }
    }

    [Fact]
    public void WindowOnlyMembers_ShouldFailClosedOnOtherProviders()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        foreach (var ctx in new IDataContext[] { postgres, sqlServer })
        {
            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bAggregateEntity>()
                    .Select(x => new
                    {
                        V = SqlFunctions.ClickHouse.exponential_time_decayed_sum(2, x.X, x.Ts)
                            .Over(SqlFunctions.Sql.asc(() => x.Ts))
                    })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("The exponentialTimeDecayedSum window function is not supported by this provider.");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheDeclaredClrTypes()
    {
        static MethodInfo Member(string name, int parameters)
            => typeof(ClickHouseFunctions)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Single(m => m.Name == name && m.GetParameters().Length == parameters);

        using (new AssertionScope())
        {
            Member("entropy", 1).ReturnType.Should().Be(typeof(double));
            Member("first_value", 1).ReturnType.IsGenericParameter.Should().BeTrue();
            Member("last_value", 1).ReturnType.IsGenericParameter.Should().BeTrue();
            Member("max_intersections", 2).ReturnType.Should().Be(typeof(long));
            Member("max_intersections_position", 2).ReturnType.Should().Be(typeof(int));
            Member("simple_linear_regression", 2).ReturnType.Should().Be(typeof(Tuple<double, double>));
            Member("delta_sum", 1).ReturnType.IsGenericParameter.Should().BeTrue();
            Member("delta_sum_timestamp", 2).ReturnType.IsGenericParameter.Should().BeTrue();
            Member("rank_corr", 2).ReturnType.Should().Be(typeof(double));
            Member("welch_t_test", 2).ReturnType.Should().Be(typeof(Tuple<double, double>));
            Member("mean_z_test", 5).ReturnType.Should().Be(typeof(Tuple<double, double, double, double>));
            Member("quantile_tdigest", 2).ReturnType.Should().Be(typeof(double?));
            Member("quantile_gk", 3).ReturnType.Should().Be(typeof(double?));
            Member("quantiles_exact", 2).ReturnType.Should().Be(typeof(double[]));
            Member("quantiles_dd", 3).ReturnType.Should().Be(typeof(double[]));
            Member("approx_top_k", 2).ReturnType.IsArray.Should().BeTrue();
            Member("approx_top_k", 2).ReturnType.GetElementType()!.GetGenericTypeDefinition().Should().Be(typeof(Tuple<,,>));
            Member("any_heavy", 1).ReturnType.IsGenericParameter.Should().BeTrue();
            Member("max_map", 2).ReturnType.GetGenericTypeDefinition().Should().Be(typeof(Tuple<,>));
            Member("sum_count", 1).ReturnType.GetGenericTypeDefinition().Should().Be(typeof(Tuple<,>));
            Member("sum_kahan", 1).ReturnType.Should().Be(typeof(double));
            Member("group_array_moving_avg", 1).ReturnType.Should().Be(typeof(double[]));
            Member("group_array_moving_sum", 1).ReturnType.Should().Be(typeof(long[]));
            Member("group_array_sample", 2).ReturnType.IsArray.Should().BeTrue();
            Member("group_array_insert_at", 2).ReturnType.IsArray.Should().BeTrue();
            Member("group_array_intersect", 1).ReturnType.IsArray.Should().BeTrue();
            Member("group_concat", 1).ReturnType.Should().Be(typeof(string));
            Member("group_format", 2).ReturnType.Should().Be(typeof(string));
            Member("histogram", 2).ReturnType.Should().Be(typeof(Tuple<double, double, double>).MakeArrayType());
            Member("sparkbar", 3).ReturnType.Should().Be(typeof(string));
            Member("sparkbar", 5).ReturnType.Should().Be(typeof(string));
            Member("uniq_up_to", 2).ReturnType.Should().Be(typeof(long));
            Member("sequence_count", 3).ReturnType.Should().Be(typeof(long));
            Member("sequence_match_events", 3).ReturnType.Should().Be(typeof(DateTime[]));
            Member("sequence_next_node", 6).ReturnType.Should().Be(typeof(string));
            Member("time_series_group_sum", 2).ReturnType.IsArray.Should().BeTrue();
            Member("time_series_group_sum", 2).ReturnType.GetElementType()!.GetGenericTypeDefinition().Should().Be(typeof(Tuple<,>));
            Member("time_series_rate_to_grid", 6).ReturnType.Should().Be(typeof(double?[]));
            Member("time_series_instant_rate_to_grid", 6).ReturnType.Should().Be(typeof(double?[]));
            Member("time_series_resample_to_grid_with_staleness", 6).ReturnType.Should().Be(typeof(double?[]));
            Member("exponential_time_decayed_sum", 3).ReturnType.Should().Be(typeof(WindowFunction<double>));
            Member("exponential_time_decayed_max", 3).ReturnType.Should().Be(typeof(WindowFunction<double>));
            Member("exponential_time_decayed_count", 2).ReturnType.Should().Be(typeof(WindowFunction<double>));
            Member("exponential_time_decayed_avg", 3).ReturnType.Should().Be(typeof(WindowFunction<double>));

            // The three state-returning members are declared with the object result of the F15 refusal
            // convention; they never reach SQL.
            Member("stochastic_linear_regression", 2).ReturnType.Should().Be(typeof(object));
            Member("stochastic_logistic_regression", 2).ReturnType.Should().Be(typeof(object));
            Member("exponential_moving_average", 3).ReturnType.Should().Be(typeof(object));
        }
    }

    [Fact]
    public void Projection_ShouldRenderEveryFamilyGroupInOneSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bAggregateEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            E = SqlFunctions.ClickHouse.entropy(x.X),
            P = SqlFunctions.ClickHouse.first_value(x.X),
            L = SqlFunctions.ClickHouse.last_value(x.X),
            X = SqlFunctions.ClickHouse.max_intersections(x.X, x.Y),
            Q = SqlFunctions.ClickHouse.quantile_bfloat16(0.5, x.X),
            A = SqlFunctions.ClickHouse.approx_top_sum(2, x.X, x.Y),
            H = SqlFunctions.ClickHouse.histogram(3, x.X),
            G = SqlFunctions.ClickHouse.group_array_sorted(2, x.X),
            K = SqlFunctions.ClickHouse.group_concat("-", x.S),
            U = SqlFunctions.ClickHouse.uniq_up_to(3, x.X),
            T = SqlFunctions.ClickHouse.time_series_rate_to_grid(0, 210, 15, 45, x.Ts, x.F)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("entropy(x) as `E`");
            sql.Should().Contain("first_value(x) as `P`");
            sql.Should().Contain("last_value(x) as `L`");
            sql.Should().Contain("toInt64(maxIntersections(x, y)) as `X`");
            sql.Should().Contain("quantileBFloat16(0.5)(x) as `Q`");
            sql.Should().Contain("cast(approx_top_sum(2)(x, y) as Array(Tuple(Int32, Int64, Int64))) as `A`");
            sql.Should().Contain("cast(histogram(3)(x) as Array(Tuple(Float64, Float64, Float64))) as `H`");
            sql.Should().Contain("cast(groupArraySorted(2)(x) as Array(Int32)) as `G`");
            sql.Should().Contain("groupConcat('-')(s) as `K`");
            sql.Should().Contain("toInt64(uniqUpTo(3)(x)) as `U`");
            sql.Should().Contain("cast(timeSeriesRateToGrid(0, 210, 15, 45)(ts, f) as Array(Nullable(Float64))) as `T`");
        }
    }

    [Fact]
    public void Capability_ShouldBeClickHouseOnly()
    {
        ClickHouseDialect.Instance.ClickHouseAggregates.Should().NotBeNull();
        PostgresDialect.Instance.ClickHouseAggregates.Should().BeNull();
        SqlServerDialect.Instance.ClickHouseAggregates.Should().BeNull();
    }
}
