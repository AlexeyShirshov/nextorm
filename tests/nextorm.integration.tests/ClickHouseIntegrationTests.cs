using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// End-to-end tests for the ClickHouse specific behaviours, backed by a Testcontainers instance
/// unless <c>NEXTORM_CLICKHOUSE_CONNECTION</c> points at an existing server. Unlike the shared
/// suite these run only against ClickHouse, because the functions under test either have no
/// portable equivalent (the <c>-If</c> combinators, <c>argMin</c>/<c>argMax</c>) or render
/// differently per provider.
/// </summary>
public sealed class ClickHouseIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void Count_ShouldReturn10()
    {
        _sut.SimpleEntity.Select(e => SqlFunctions.Sql.count()).First().Should().Be(10);
    }

    [Fact]
    public void ColumnByName_ShouldReadUnmappedColumn()
    {
        var rows = _sut.DataProvider.From<IWideEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Region = SqlFunctions.Column<ulong>(x, "regionid") })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Region).Should().Equal(10UL, 20UL, 30UL);
    }

    [Fact]
    public void ColumnByName_InWhere_ShouldFilterByUnmappedColumn()
    {
        _sut.DataProvider.From<IWideEntity>()
            .Where(x => SqlFunctions.Column<ulong>(x, "regionid") == 20)
            .Select(x => x.Id)
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void CountAggregates_ShouldCastToInt64InProjection()
    {
        // The native count()/countIf() return UInt64, which the anonymous-projection materializer
        // cannot read back; the dialect casts to the CLR type each function declares (int vs long).
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                N = SqlFunctions.Sql.count(),
                Big = SqlFunctions.Sql.count_big(),
                D = SqlFunctions.Sql.count_distinct(x.Id),
                F = SqlFunctions.Sql.count(() => x.Id <= 2)
            })
            .First();

        r.N.Should().Be(10);
        r.Big.Should().Be(10L);
        r.D.Should().Be(10);
        r.F.Should().Be(2);
    }

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version(),
                U = SqlFunctions.Sql.gen_random_uuid(),
                V7 = SqlFunctions.Sql.uuidv7()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().NotBeNullOrEmpty();
        r.U.Should().NotBe(Guid.Empty);
        r.V7.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UniqAggregates_ShouldCountDistinctValues()
    {
        // simple_entity ids are 1..10, all distinct.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                U = SqlFunctions.ClickHouse.uniq(x.Id),
                E = SqlFunctions.ClickHouse.uniq_exact(x.Id),
                C = SqlFunctions.ClickHouse.uniq_combined(x.Id),
                H = SqlFunctions.ClickHouse.uniq_hll12(x.Id)
            })
            .First();

        r.U.Should().Be(10L);
        r.E.Should().Be(10L);
        r.C.Should().Be(10L);
        r.H.Should().Be(10L);
    }

    [Fact]
    public void JsonExtract_ShouldReadStringJson()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.json_extract_string("{\"s\":\"hi\"}", "s"),
                I = SqlFunctions.ClickHouse.json_extract_int("{\"n\":42}", "n"),
                F = SqlFunctions.ClickHouse.json_extract_float("{\"f\":1.5}", "f"),
                B = SqlFunctions.ClickHouse.json_extract_bool("{\"b\":true}", "b"),
                O = SqlFunctions.ClickHouse.json_extract_raw("{\"o\":{\"x\":1}}", "o"),
                H = SqlFunctions.ClickHouse.json_has("{\"s\":1}", "s"),
                L = SqlFunctions.ClickHouse.json_length("{\"arr\":[1,2,3]}", "arr"),
                T = SqlFunctions.ClickHouse.json_type("{\"s\":\"x\"}", "s")
            })
            .First();

        r.S.Should().Be("hi");
        r.I.Should().Be(42L);
        r.F.Should().Be(1.5);
        r.B.Should().BeTrue();
        r.O.Should().Be("{\"x\":1}");
        r.H.Should().BeTrue();
        r.L.Should().Be(3L);
        r.T.Should().Be("String");
    }

    [Fact]
    public void JsonArrayExtract_ShouldProjectArrays()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                K = SqlFunctions.ClickHouse.json_extract_keys("{\"a\":1,\"b\":2}"),
                KP = SqlFunctions.ClickHouse.json_extract_keys("{\"o\":{\"a\":1}}", "o"),
                A = SqlFunctions.ClickHouse.json_extract_array_raw("[1,2,3]"),
                AP = SqlFunctions.ClickHouse.json_extract_array_raw("{\"o\":[1,2]}", "o"),
                V = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>("{\"a\":1,\"b\":2}"),
                VP = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>("{\"o\":{\"c\":3}}", "o")
            })
            .First();

        r.K.Should().Equal("a", "b");
        r.KP.Should().Equal("a");
        r.A.Should().Equal("1", "2", "3");
        r.AP.Should().Equal("1", "2");
        r.V.Should().BeEquivalentTo(new[] { Tuple.Create("a", 1), Tuple.Create("b", 2) });
        r.VP.Should().Equal(Tuple.Create("c", 3));
    }

    [Fact]
    public void JsonPath_ShouldReadStringJson()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.json_value("{\"a\":\"hi\"}", "$.a"),
                Q = SqlFunctions.ClickHouse.json_query("{\"b\":{\"x\":1}}", "$.b"),
                E = SqlFunctions.ClickHouse.json_exists("{\"c\":1}", "$.c")
            })
            .First();

        r.V.Should().Be("hi");
        r.Q.Should().Be("[{\"x\":1}]");
        r.E.Should().BeTrue();
    }

    [Fact]
    public void VisitParamExtract_ShouldReadFlatJson()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.visit_param_extract_string("{\"s\":\"hi\"}", "s"),
                I = SqlFunctions.ClickHouse.visit_param_extract_int("{\"n\":42}", "n"),
                F = SqlFunctions.ClickHouse.visit_param_extract_float("{\"f\":1.5}", "f"),
                B = SqlFunctions.ClickHouse.visit_param_extract_bool("{\"b\":true}", "b"),
                R = SqlFunctions.ClickHouse.visit_param_extract_raw("{\"o\":{\"x\":1}}", "o")
            })
            .First();

        r.S.Should().Be("hi");
        r.I.Should().Be(42L);
        r.F.Should().Be(1.5);
        r.B.Should().BeTrue();
        r.R.Should().Be("{\"x\":1}");
    }

    [Fact]
    public void AnyAggregates_ShouldReturnRowValue()
    {
        // A single-row source makes any()/anyLast() deterministic.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1L)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.any_agg(x.RequiredString),
                L = SqlFunctions.ClickHouse.any_last(x.RequiredString)
            })
            .First();

        r.F.Should().Be("sdf");
        r.L.Should().Be("sdf");
    }

    [Fact]
    public void GroupByWithTotals_ShouldExecute()
    {
        // The ClickHouse.Driver does not surface the extra totals row (ClickHouse returns it in a
        // separate response block), so only the two groups are observed. The rendered SQL is pinned
        // by the SQL-generation test; here we only confirm the query executes on a real server.
        var r = _sut.ComplexEntity
            .GroupBy(x => new { x.Int })
            .WithTotals()
            .Select(x => new { x.Int })
            .ToList();

        r.Should().HaveCount(2);
    }

    [Fact]
    public void NumbersTableFunction_ShouldReturnThreeRows()
    {
        var rows = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.ClickHouse.numbers(3))
            .Select(r => new { r.Value })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Value).Should().Equal(0L, 1L, 2L);
    }

    [Fact]
    public void ZerosTableFunction_ShouldReturnThreeRows()
    {
        var rows = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
            .Select(r => new { r.Value })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.Value == 0);
    }

    [Fact]
    public void GenerateRandomTableFunction_ShouldReturnRequestedRows()
    {
        var rows = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.ClickHouse.generate_random())
            .Page(3, 0)
            .Select(r => new { r.Id, r.Value, r.Name })
            .ToList();

        rows.Should().HaveCount(3);
    }

    [Fact]
    public void GlobalIn_Subquery_ShouldFilter()
    {
        var ids = _sut.SimpleEntity
            .Where(x => SqlFunctions.ClickHouse.global_in(
                x.Id,
                _sut.SimpleEntity.Where(y => y.Id <= 2).Select(y => y.Id)))
            .Select(x => x.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1, 2);
    }

    [Fact]
    public void GlobalIn_Values_ShouldFilter()
    {
        var ids = _sut.SimpleEntity
            .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, new[] { 1, 2 }))
            .Select(x => x.Id)
            .ToList();

        ids.OrderBy(x => x).Should().Equal(1, 2);
    }

    [Fact]
    public void QueryModifiers_ShouldExecute()
    {
        // FINAL/PREWHERE are not exercised here: the integration tables use the Memory engine, which
        // rejects both ("Storage Memory ... does not support FINAL/PREWHERE"). Their SQL and dialect
        // rendering are pinned by the SQL-generation and dialect tests.
        var settingsRows = _sut.ComplexEntity.Settings(("max_threads", "2")).Select(x => new { x.Id }).ToList();
        settingsRows.Should().HaveCount(3);
    }

    [Fact]
    public void LimitBy_ShouldTakeTopNPerKey()
    {
        // complex_entity has two distinct nullableint values; limit 1 by it yields one row per key.
        var r = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .LimitBy(1, x => x.Int)
            .Select(x => new { x.Id, x.Int })
            .ToList();

        r.Should().HaveCount(2);
        r.Select(x => x.Int).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void QuantileAggregates_ShouldReturnQuantile()
    {
        // complex_entity ids are 1..3 (odd count), so the median is unambiguous: 2.
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                Q = SqlFunctions.ClickHouse.quantile(0.5, x.Id),
                E = SqlFunctions.ClickHouse.quantile_exact(0.5, x.Id),
                M = SqlFunctions.ClickHouse.median(x.Id)
            })
            .First();

        r.E.Should().Be(2.0);
        r.M.Should().Be(2.0);
        r.Q.Should().BeApproximately(2.0, 1e-12);
    }

    [Fact]
    public void TopK_ShouldReturnMostFrequentValues()
    {
        // complex_entity ids are 1..3 (all distinct), so topK(2) returns two of them.
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.ClickHouse.top_k(2, x.Id))
            .First();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(id => id >= 1 && id <= 3);
    }

    [Fact]
    public void TopKWeighted_ShouldReturnWeightedValues()
    {
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.ClickHouse.top_k_weighted(2, x.Id, x.Id))
            .First();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(id => id >= 1 && id <= 3);
    }

    [Fact]
    public void Quantiles_ShouldReturnMultipleQuantiles()
    {
        // ids 1..3: the 0.5 level is the median, so the middle value is 2.
        var r = _sut.ComplexEntity
            .Select(x => SqlFunctions.ClickHouse.quantiles(new[] { 0.25, 0.5, 0.75 }, x.Id))
            .First();

        r.Should().HaveCount(3);
        r.Should().BeInAscendingOrder();
        r[1].Should().BeApproximately(2.0, 1e-12);
    }

    [Fact]
    public void DateTrunc_ShouldTruncateToMonth()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_trunc("month", x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 1));
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
    public void DateTimeAddMonths_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => x.Datetime!.Value.AddMonths(2))
            .First();

        r.Should().Be(new DateTime(2023, 3, 1, 10, 0, 0));
    }

    [Fact]
    public void DateAddHour_ShouldKeepTimeOfDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("hour", 2, x.Datetime))
            .First();

        // A sub-day part is promoted to DateTime so the time of day is kept.
        r.Should().Be(new DateTime(2023, 1, 1, 12, 0, 0));
    }

    [Fact]
    public void DateDiffBig_ShouldReturn64BitSpan()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff_big("milliseconds", new DateTime(1970, 1, 1), x.Datetime))
            .First();

        // ~53 years of milliseconds exceed the 32-bit date_diff.
        r.Should().BeGreaterThan((long)int.MaxValue);
    }

    [Fact]
    public void DurationColumns_ShouldRoundTrip() => CommonTestSuite.DurationRoundTrip(_sut.DataProvider);

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
    public void DateConversionFunctions_ShouldReturnDateParts()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Y = SqlFunctions.ClickHouse.to_year(x.Datetime),
                Q = SqlFunctions.ClickHouse.to_quarter(x.Datetime),
                M = SqlFunctions.ClickHouse.to_month(x.Datetime),
                D = SqlFunctions.ClickHouse.to_day_of_month(x.Datetime),
                DOW = SqlFunctions.ClickHouse.to_day_of_week(x.Datetime),
                DOY = SqlFunctions.ClickHouse.to_day_of_year(x.Datetime),
                H = SqlFunctions.ClickHouse.to_hour(x.Datetime),
                Start = SqlFunctions.ClickHouse.to_start_of_month(x.Datetime),
                Monday = SqlFunctions.ClickHouse.to_monday(x.Datetime),
                YM = SqlFunctions.ClickHouse.to_yyyymm(x.Datetime),
                YMD = SqlFunctions.ClickHouse.to_yyyymmdd(x.Datetime),
                U = SqlFunctions.ClickHouse.to_unix_timestamp(x.Datetime),
                Dt = SqlFunctions.ClickHouse.to_date(x.Datetime),
                Dt32 = SqlFunctions.ClickHouse.to_date32(x.Datetime)
            })
            .First();

        r.Y.Should().Be(2023);
        r.Q.Should().Be(1);
        r.M.Should().Be(1);
        r.D.Should().Be(1);
        r.DOW.Should().Be(7);
        r.DOY.Should().Be(1);
        r.H.Should().Be(10);
        r.Start.Should().Be(new DateTime(2023, 1, 1));
        r.Monday.Should().Be(new DateTime(2022, 12, 26));
        r.YM.Should().Be(202301);
        r.YMD.Should().Be(20230101);
        r.U.Should().BeGreaterThan(0);
        r.Dt.Should().Be(new DateTime(2023, 1, 1));
        r.Dt32.Should().Be(new DateTime(2023, 1, 1));
    }

    [Fact]
    public void Split_ShouldCountParts()
    {
        var count = _sut.SimpleEntity
            .Select(x => SqlFunctions.ClickHouse.length("a,b,c".Split(',')))
            .First();

        count.Should().Be(3);
    }

    [Fact]
    public void BitAggregates_ShouldMatchBitwiseOperations()
    {
        // simple_entity ids are 1..10: AND is 0, OR is 15 and XOR is 11.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                And = SqlFunctions.Postgres.bit_and(x.Id),
                Or = SqlFunctions.Postgres.bit_or(x.Id),
                Xor = SqlFunctions.Postgres.bit_xor(x.Id)
            })
            .First();

        r.And.Should().Be(0);
        r.Or.Should().Be(15);
        r.Xor.Should().Be(11);
    }

    [Fact]
    public void Corr_ShouldReturnOneForIdenticalSeries()
    {
        _sut.ComplexEntity
            .Select(x => SqlFunctions.Sql.corr(x.Id, x.Id))
            .First()
            .Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void CovarPop_ShouldMatchPopulationVariance()
    {
        // ids 1..3: population variance is 2 / 3.
        _sut.ComplexEntity
            .Select(x => SqlFunctions.Sql.covar_pop(x.Id, x.Id))
            .First()
            .Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public void CovarSamp_ShouldMatchSampleVariance()
    {
        // ids 1..3: sample variance is 2 / 2 = 1.
        _sut.ComplexEntity
            .Select(x => SqlFunctions.Sql.covar_samp(x.Id, x.Id))
            .First()
            .Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void ArgMinMax_ShouldReturnValueAtExtremeKey()
    {
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                Min = SqlFunctions.ClickHouse.arg_min(x.RequiredString, x.Id),
                Max = SqlFunctions.ClickHouse.arg_max(x.RequiredString, x.Id)
            })
            .First();

        r.Min.Should().Be("sdf");
        r.Max.Should().Be("34mfs");
    }

    [Fact]
    public void CountIf_ShouldReturnCount()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.count(() => x.Id <= 2))
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void WindowFunnel_ShouldCountConsecutiveConditions()
    {
        // event_entity rows are (1: event 1 @ 00:00, 2: event 2 @ 00:01, 3: event 3 @ 00:02,
        // 4: event 2 @ 00:03). The 600s window contains the whole 1 -> 2 -> 3 chain, so the level is 3.
        var events = _sut.DataProvider.From<IEventEntity>();

        var level = events
            .Select(x => SqlFunctions.ClickHouse.window_funnel(600, x.Timestamp, x.Event == 1, x.Event == 2, x.Event == 3))
            .First();

        level.Should().Be(3);
    }

    [Fact]
    public void SequenceMatch_ShouldMatchPattern()
    {
        var events = _sut.DataProvider.From<IEventEntity>();

        events
            .Select(x => SqlFunctions.ClickHouse.sequence_match("(?1)(?2)(?3)", x.Timestamp, x.Event == 1, x.Event == 2, x.Event == 3))
            .First()
            .Should().Be(1);

        // Reversing the order cannot match: cond2 must occur after cond1.
        events
            .Select(x => SqlFunctions.ClickHouse.sequence_match("(?1)(?2)(?3)", x.Timestamp, x.Event == 3, x.Event == 2, x.Event == 1))
            .First()
            .Should().Be(0);
    }

    [Fact]
    public void Retention_ShouldReturnConditionMask()
    {
        // retention is anchored on the first condition: result[1] = any(event == 1) = 1;
        // result[2] = anchor && any(event == 5) = 0; result[3] = anchor && any(event == 3) = 1.
        var events = _sut.DataProvider.From<IEventEntity>();

        var mask = events
            .Select(x => SqlFunctions.ClickHouse.array_string_concat(
                SqlFunctions.ClickHouse.retention(x.Event == 1, x.Event == 5, x.Event == 3), ","))
            .First();

        mask.Should().Be("1,0,1");
    }

    [Fact]
    public void MultiIf_ShouldReturnMatchedBranch()
    {
        // complex_entity ids are 1..3, so the first two conditions match and the third row takes the else.
        var rows = _sut.ComplexEntity
            .Select(x => new
            {
                x.Id,
                B = SqlFunctions.ClickHouse.multi_if(
                    SqlFunctions.ClickHouse.when(x.Id == 1L, "one"),
                    SqlFunctions.ClickHouse.when(x.Id == 2L, "two"),
                    SqlFunctions.ClickHouse.otherwise("many")),
                N = SqlFunctions.ClickHouse.multi_if(
                    SqlFunctions.ClickHouse.when(x.Id == 1L, 10L),
                    SqlFunctions.ClickHouse.when(x.Id == 2L, 20L),
                    SqlFunctions.ClickHouse.otherwise(30L))
            })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        rows.Select(r => r.B).Should().Equal("one", "two", "many");
        rows.Select(r => r.N).Should().Equal(10L, 20L, 30L);
    }

    [Fact]
    public void InFrameWindowFunctions_ShouldRespectFrame()
    {
        // ClickHouse rejects an explicit frame on lag/lead ("Window function 'lag' does not expect
        // window frame to be explicitly specified"), while lagInFrame/leadInFrame accept and respect
        // it. A frame that starts at the current row has no preceding row, so lagInFrame(x, 1) yields
        // the default, whereas the frame-less lag returns the previous partition row.
        var startingFrame = WindowFrame.Rows(WindowFrameBound.CurrentRow, WindowFrameBound.Following(1));
        var endingFrame = WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow);

        var rows = _sut.SimpleEntity
            .Where(x => x.Id <= 3)
            .Select(x => new
            {
                x.Id,
                Prev = SqlFunctions.Sql.lag(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id)),
                PrevInFrame = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id), startingFrame),
                Next = SqlFunctions.Sql.lead(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id)),
                NextInFrame = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id), endingFrame)
            })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        rows[0].Prev.Should().Be(0);
        rows[1].Prev.Should().Be(1);
        rows[2].Prev.Should().Be(2);
        rows.Should().OnlyContain(r => r.PrevInFrame == 0);

        rows[0].Next.Should().Be(2);
        rows[1].Next.Should().Be(3);
        rows[2].Next.Should().Be(0);
        rows.Should().OnlyContain(r => r.NextInFrame == 0);
    }

    [Fact]
    public void IfAggregates_ShouldFilterBeforeAggregating()
    {
        // The arguments are cast to the ClickHouse result types (Int64 for the integer sum/min/max,
        // Float64 for the average) so the driver reads them back without a lossy conversion.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                Sum = SqlFunctions.Sql.sum((long)x.Id, () => x.Id <= 2),
                Avg = SqlFunctions.Sql.avg((double)x.Id, () => x.Id <= 2),
                Min = SqlFunctions.Sql.min((long)x.Id, () => x.Id <= 2),
                Max = SqlFunctions.Sql.max((long)x.Id, () => x.Id <= 2)
            })
            .First();

        r.Sum.Should().Be(3);
        r.Avg.Should().BeApproximately(1.5, 1e-12);
        r.Min.Should().Be(1);
        r.Max.Should().Be(2);
    }

    [Fact]
    public void AnyStrictness_ShouldKeepSingleMatch()
    {
        // complex_entity has 3 rows sharing small = 3, so a plain left join yields 9 rows while
        // ANY keeps a single matching right row per left row.
        _sut.ComplexEntity
            .LeftJoin(_sut.ComplexEntity, (a, b) => a.SmallInt == b.SmallInt)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id })
            .ToList()
            .Should().HaveCount(9);

        _sut.ComplexEntity
            .LeftJoin(_sut.ComplexEntity, (a, b) => a.SmallInt == b.SmallInt)
            .WithStrictness(JoinStrictness.Any)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id })
            .ToList()
            .Should().HaveCount(3);
    }

    [Fact]
    public void AllStrictness_ShouldKeepEveryMatch()
    {
        _sut.ComplexEntity
            .LeftJoin(_sut.ComplexEntity, (a, b) => a.SmallInt == b.SmallInt)
            .WithStrictness(JoinStrictness.All)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id })
            .ToList()
            .Should().HaveCount(9);
    }

    [Fact]
    public void AsofJoin_ShouldPickClosestMatch()
    {
        // ASOF needs one equi-join column plus one inequality (the latter last). For every left row
        // it keeps the right row with the greatest id not exceeding the left id (ids are unique
        // 1..3), so the result matches the left rows one to one.
        var rows = _sut.ComplexEntity
            .Join(_sut.ComplexEntity, (a, b) => a.Boolean == b.Boolean && a.Id >= b.Id)
            .WithStrictness(JoinStrictness.Asof)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.L == r.R);
    }

    [Fact]
    public void GlobalJoin_ShouldExecute()
    {
        // On a single node GLOBAL JOIN behaves like a regular join, so this only checks that the
        // rendered SQL is accepted and preserves the left-hand rows.
        var rows = _sut.SimpleEntity
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Global()
            .Select(p => new { p.Item1.Id })
            .ToList();

        rows.Should().HaveCount(10);
    }

    [Fact]
    public void SemiJoin_ShouldKeepOnlyMatchingLeftRows()
    {
        // complex_entity has ids 1..3, so exactly the simple rows 1,2,3 have a match.
        _sut.SimpleEntity
            .SemiJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(s => s.Id)
            .ToList()
            .Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void AntiJoin_ShouldKeepOnlyNonMatchingLeftRows()
    {
        _sut.SimpleEntity
            .AntiJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(s => s.Id)
            .ToList()
            .Should().BeEquivalentTo([4, 5, 6, 7, 8, 9, 10]);
    }

    [Fact]
    public void SemiJoin_ShouldNotDuplicateOnMultipleMatches()
    {
        // All three complex rows share small = 3: a plain join yields 9 rows, SEMI keeps each left row once.
        _sut.ComplexEntity
            .SemiJoin(_sut.ComplexEntity, (a, b) => a.SmallInt == b.SmallInt)
            .Select(a => a.Id)
            .ToList()
            .Should().HaveCount(3);
    }

    [Fact]
    public void PasteJoin_ShouldPairByPositionAndUseShorterSide()
    {
        // 10 left rows and 3 right rows, so PASTE yields min(10, 3) = 3 rows carrying both sides.
        var rows = _sut.SimpleEntity
            .PasteJoin(_sut.ComplexEntity)
            .Select(p => new { L = p.Item1.Id, R = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.R).Should().BeEquivalentTo([1L, 2L, 3L]);
        rows.Select(r => r.L).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void ArrayFunctions_ShouldReturnValues()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.ClickHouse.length(x.Nums),
                Has = SqlFunctions.ClickHouse.has(x.Tags, "b"),
                Idx = SqlFunctions.ClickHouse.index_of(x.Tags, "c"),
                S = SqlFunctions.ClickHouse.array_string_concat(x.Tags, ",")
            })
            .First();

        r.L.Should().Be(3);
        r.Has.Should().BeTrue();
        r.Idx.Should().Be(3);
        r.S.Should().Be("a,b,c");
    }

    [Fact]
    public void ArrayStringConcat_WithDefaultDelimiter_ShouldJoinWithoutSeparator()
    {
        var s = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_string_concat(x.Tags))
            .First();

        s.Should().Be("abc");
    }

    [Fact]
    public void ArrayHasAny_WithCapturedArray_ShouldFilterMatchingRows()
    {
        var ids = _sut.ArrayEntity
            .Where(x => SqlFunctions.ClickHouse.has_any(x.Tags, new[] { "c" }))
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1);
    }

    [Fact]
    public void ArrayRelationPredicates_ShouldReturnValues()
    {
        // array_entity id = 1 has nums = [3, 1, 2].
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.starts_with(x.Nums, new[] { 3, 1 }),
                SMiss = SqlFunctions.ClickHouse.starts_with(x.Nums, new[] { 1, 3 }),
                E = SqlFunctions.ClickHouse.ends_with(x.Nums, new[] { 1, 2 }),
                C = SqlFunctions.ClickHouse.has_substr(x.Nums, new[] { 1, 2 }),
                CMiss = SqlFunctions.ClickHouse.has_substr(x.Nums, new[] { 3, 2 })
            })
            .First();

        r.S.Should().BeTrue();
        r.SMiss.Should().BeFalse();
        r.E.Should().BeTrue();
        r.C.Should().BeTrue();
        r.CMiss.Should().BeFalse();
    }

    [Fact]
    public void ArrayJoin_ShouldExpandRows()
    {
        var rows = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new { x.Id, Tag = SqlFunctions.ClickHouse.array_join(x.Tags) })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Tag).Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void ArrayScalarFunctions_ShouldReturnValues()
    {
        // The five functions return arrays, which the row reader cannot materialise yet, so each is
        // wrapped in a scalar-returning array function (length/arrayStringConcat) inside the query.
        // array_entity id = 1 has nums = [3, 1, 2].
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                R = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.range(1, 5)),
                E = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_enumerate(x.Nums)),
                C = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_cum_sum(x.Nums), ","),
                S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_slice(x.Nums, 1, 2), ","),
                P = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_push_back(x.Nums, 4), ",")
            })
            .First();

        r.R.Should().Be(4);
        r.E.Should().Be(3);
        r.C.Should().Be("3,4,6");
        r.S.Should().Be("3,1");
        r.P.Should().Be("3,1,2,4");
    }

    [Fact]
    public void ArrayJoinClause_ShouldExpandRowsAndDropEmptyArrays()
    {
        var ids = _sut.ArrayEntity
            .ArrayJoin(x => x.Tags)
            .Select(x => x.Id)
            .ToList();

        ids.Should().HaveCount(4);
        ids.Should().NotContain(3);
    }

    [Fact]
    public void LeftArrayJoinClause_ShouldKeepEmptyArrays()
    {
        var ids = _sut.ArrayEntity
            .LeftArrayJoin(x => x.Tags)
            .Select(x => x.Id)
            .ToList();

        ids.Should().HaveCount(5);
        ids.Should().Contain(3);
    }

    [Fact]
    public void ArrayJoinElement_ShouldBindExpandedElement()
    {
        var rows = _sut.ArrayEntity
            .ArrayJoinElement(x => x.Tags)
            .Where(p => p.Item1.Id == 1)
            .Select(p => new { p.Item1.Id, Tag = p.Element })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Tag).Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void ArrayJoinElement_ShouldFilterOnElement()
    {
        var rows = _sut.ArrayEntity
            .ArrayJoinElement(x => x.Tags)
            .Where(p => p.Element == "b")
            .Select(p => new { p.Item1.Id, Tag = p.Element })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void LeftArrayJoinElement_ShouldKeepEmptyArrayWithDefaultElement()
    {
        var rows = _sut.ArrayEntity
            .LeftArrayJoinElement(x => x.Tags)
            .Where(p => p.Item1.Id == 3)
            .Select(p => new { p.Item1.Id, Tag = p.Element })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Tag.Should().BeNullOrEmpty();
    }

    [Fact]
    public void UInt64Columns_ShouldMaterializeAsUlong()
    {
        var rows = _sut.DataProvider
            .From<IUInt64Entity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Value })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Id.Should().Be(1UL);
        rows[0].Value.Should().Be(ulong.MaxValue);
        rows[2].Value.Should().Be(42UL);
    }

    [Fact]
    public void UInt64Projection_ShouldMaterializeValueAboveInt64Max()
    {
        var value = _sut.DataProvider
            .From<IUInt64Entity>()
            .OrderByDescending(x => x.Value)
            .Select(x => x.Value)
            .First();

        value.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void NullableUInt64_ShouldMaterializeNullAndValue()
    {
        var rows = _sut.DataProvider
            .From<IUInt64Entity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Maybe })
            .ToList();

        rows[0].Maybe.Should().Be(ulong.MaxValue);
        rows[1].Maybe.Should().BeNull();
        rows[2].Maybe.Should().Be(7UL);
    }

    [Fact]
    public void JsonAllPaths_ShouldProjectNativeJsonPaths()
    {
        var r = _sut.DataProvider
            .From<IJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { Paths = SqlFunctions.ClickHouse.json_all_paths(x.Doc) })
            .First();

        r.Paths.Should().Contain("name").And.Contain("age").And.Contain("nested.x");
    }

    [Fact]
    public void JsonAllPathsWithTypes_ShouldProjectNativeJsonMap()
    {
        var r = _sut.DataProvider
            .From<IJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { Typed = SqlFunctions.ClickHouse.json_all_paths_with_types(x.Doc) })
            .First();

        r.Typed.Should().NotBeEmpty();
        r.Typed.Should().ContainKey("name");
        r.Typed["name"].Should().Be("String");
    }

    [Fact]
    public void ToJsonString_ShouldSerialiseNativeJson()
    {
        var r = _sut.DataProvider
            .From<IJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { Text = SqlFunctions.ClickHouse.to_json_string(x.Doc) })
            .First();

        r.Text.Should().NotBeNull().And.Contain("\"name\"").And.Contain("alice");
    }

    [Fact]
    public void ArrayColumns_ShouldProjectDirectly()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new { x.Tags, x.Nums })
            .First();

        r.Tags.Should().Equal("a", "b", "c");
        r.Nums.Should().Equal(3, 1, 2);
    }

    [Fact]
    public void ArrayExpression_ShouldProjectWithoutWrapper()
    {
        var sorted = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_sort(x.Nums))
            .First();

        sorted.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void NestedArrayExpression_ShouldProjectWithoutWrapper()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_push_back(
                SqlFunctions.ClickHouse.array_reverse(x.Nums), 9))
            .First();

        r.Should().Equal(2, 1, 3, 9);
    }

    [Fact]
    public void GroupArray_ShouldMaterialiseArrayAggregate()
    {
        var ids = _sut.ArrayEntity
            .Select(x => SqlFunctions.ClickHouse.array_sort(
                SqlFunctions.ClickHouse.group_array(x.Id)))
            .First();

        ids.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GroupUniqArray_ShouldMaterialiseDistinctArray()
    {
        var events = _sut.DataProvider
            .From<IEventEntity>()
            .Select(x => SqlFunctions.ClickHouse.array_sort(
                SqlFunctions.ClickHouse.group_uniq_array(x.Event)))
            .First();

        events.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GroupArray_OfArrayColumn_ShouldMaterialiseNestedArray()
    {
        var grouped = _sut.ArrayEntity
            .Select(x => SqlFunctions.ClickHouse.group_array(x.Tags))
            .First();

        grouped.Should().HaveCount(3);
        grouped.SelectMany(x => x).Should().BeEquivalentTo(new[] { "a", "b", "c", "b" });
        grouped.Should().ContainSingle(x => x.Length == 0);
    }

    [Fact]
    public void ArrayMap_ShouldMapElements()
    {
        var mapped = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => -v, x.Nums))
            .First();

        mapped.Should().Equal(-3, -1, -2);
    }

    [Fact]
    public void ArrayFilter_ShouldKeepMatchingElements()
    {
        var filtered = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_sort(
                SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums)))
            .First();

        filtered.Should().Equal(2, 3);
    }

    [Fact]
    public void ArrayExistsAndAll_ShouldReturnBoolean()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Exists = SqlFunctions.ClickHouse.array_exists(v => v == 2, x.Nums),
                All = SqlFunctions.ClickHouse.array_all(v => v > 0, x.Nums)
            })
            .First();

        r.Exists.Should().BeTrue();
        r.All.Should().BeTrue();
    }

    [Fact]
    public void ArrayCount_ShouldCountMatchingElements()
    {
        var count = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_count(v => v > 1, x.Nums))
            .First();

        count.Should().Be(2);
    }

    [Fact]
    public void ArrayFirstAndLast_ShouldReturnElementAndIndex()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                First = SqlFunctions.ClickHouse.array_first(v => v > 1, x.Nums),
                FirstIndex = SqlFunctions.ClickHouse.array_first_index(v => v > 1, x.Nums),
                Last = SqlFunctions.ClickHouse.array_last(v => v > 1, x.Nums),
                LastIndex = SqlFunctions.ClickHouse.array_last_index(v => v > 1, x.Nums)
            })
            .First();

        r.First.Should().Be(3);
        r.FirstIndex.Should().Be(1);
        r.Last.Should().Be(2);
        r.LastIndex.Should().Be(3);
    }

    [Fact]
    public void TupleColumn_ShouldProjectAsSystemTuple()
    {
        var pair = _sut.DataProvider
            .From<ITupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Pair)
            .First();

        pair.Should().Be(Tuple.Create(7, "seven"));
    }

    [Fact]
    public void TupleElementAccess_ShouldReturnValues()
    {
        var r = _sut.DataProvider
            .From<ITupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { A = x.Pair.Item1, B = x.Pair.Item2 })
            .First();

        r.A.Should().Be(7);
        r.B.Should().Be("seven");
    }

    [Fact]
    public void TupleCreate_ShouldMaterialiseTuple()
    {
        var pair = _sut.DataProvider
            .From<ITupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => Tuple.Create(x.Id, x.Pair.Item2))
            .First();

        pair.Should().Be(Tuple.Create(1, "seven"));
    }

    [Fact]
    public void Insert_Value_ShouldPersistRow()
    {
        var ctx = _sut.DataProvider;
        var marker = "ins_" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, marker)
            .Value(x => x.Age, 42)
            .Insert();

        var rows = ctx.From<IInsertEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Age.Should().Be(42);
    }

    [Fact]
    public void ReturningIdentity_ShouldThrowBecauseNotSupported()
    {
        var ctx = _sut.DataProvider;

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "ins_unsupported")
            .ReturningIdentity(x => x.Id)
            .Single();

        act.Should().Throw<NotSupportedException>();
    }
}

[SqlTable("uint64_entity")]
public interface IUInt64Entity
{
    [Key]
    [Column("id")]
    ulong Id { get; set; }
    [Column("value")]
    ulong Value { get; set; }
    [Column("maybe")]
    ulong? Maybe { get; set; }
}

[SqlTable("event_entity")]
public interface IEventEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("ts")]
    DateTime Timestamp { get; set; }
    [Column("event")]
    int Event { get; set; }
}

[SqlTable("json_entity")]
public interface IJsonEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("doc")]
    string Doc { get; set; }
}

[SqlTable("tuple_entity")]
public interface ITupleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("pair")]
    Tuple<int, string> Pair { get; set; }
}

[SqlTable("wide_entity")]
public interface IWideEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
