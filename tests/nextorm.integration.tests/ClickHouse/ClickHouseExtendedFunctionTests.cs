using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse specific surface, third sweep: JSON extraction, the array library (including the
/// higher-order forms), the to* date family, the UTF-8/regexp string forms, the aggregate family
/// and the conditional/window helpers. Each call renders ClickHouse SQL and executes it.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string Doc = "{\"a\":1,\"b\":\"x\",\"c\":[1,2]}";

    [Fact]
    public void JsonExtraction_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.json_extract_string(Doc, "b"),
                I = SqlFunctions.ClickHouse.json_extract_int(Doc, "a"),
                F = SqlFunctions.ClickHouse.json_extract_float(Doc, "a"),
                B = SqlFunctions.ClickHouse.json_extract_bool(Doc, "a"),
                Raw = SqlFunctions.ClickHouse.json_extract_raw(Doc, "b"),
                Keys = SqlFunctions.ClickHouse.json_extract_keys(Doc),
                KeysPath = SqlFunctions.ClickHouse.json_extract_keys(Doc, "c"),
                ArrayRaw = SqlFunctions.ClickHouse.json_extract_array_raw(Doc),
                Has = SqlFunctions.ClickHouse.json_has(Doc, "a"),
                Len = SqlFunctions.ClickHouse.json_length(Doc, "c"),
                Type = SqlFunctions.ClickHouse.json_type(Doc, "b"),
                Value = SqlFunctions.ClickHouse.json_value(Doc, "$.b"),
                Query = SqlFunctions.ClickHouse.json_query(Doc, "$.c"),
                Exists = SqlFunctions.ClickHouse.json_exists(Doc, "$.b"),
                ToJson = SqlFunctions.ClickHouse.to_json_string("abc"),
                VisitS = SqlFunctions.ClickHouse.visit_param_extract_string("{\"p\":\"v\"}", "p"),
                VisitI = SqlFunctions.ClickHouse.visit_param_extract_int("{\"p\":1}", "p"),
                VisitF = SqlFunctions.ClickHouse.visit_param_extract_float("{\"p\":1.5}", "p"),
                VisitB = SqlFunctions.ClickHouse.visit_param_extract_bool("{\"p\":true}", "p"),
                VisitRaw = SqlFunctions.ClickHouse.visit_param_extract_raw("{\"p\":\"v\"}", "p")
            })
            .First();

        r.S.Should().Be("x");
        r.I.Should().Be(1);
        r.F.Should().Be(1);
        r.B.Should().BeTrue();
        r.Raw.Should().NotBeNull();
        r.Keys.Should().Contain("a");
        r.KeysPath.Should().NotBeNull();
        r.ArrayRaw.Should().NotBeNull();
        r.Has.Should().BeTrue();
        r.Len.Should().Be(2);
        r.Type.Should().NotBeNullOrEmpty();
        r.Value.Should().NotBeNull();
        r.Query.Should().NotBeNull();
        r.Exists.Should().BeTrue();
        r.ToJson.Should().Contain("abc");
        r.VisitS.Should().Be("v");
        r.VisitI.Should().Be(1);
        r.VisitF.Should().Be(1.5);
        r.VisitB.Should().BeTrue();
        r.VisitRaw.Should().NotBeNull();
    }

    [Fact]
    public void ArrayLibrary_ShouldExecute()
    {
        var r = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Len = SqlFunctions.ClickHouse.length(x.Nums),
                Has = SqlFunctions.ClickHouse.has(x.Nums, 2),
                Idx = SqlFunctions.ClickHouse.index_of(x.Nums, 2),
                HasAny = SqlFunctions.ClickHouse.has_any(x.Nums, new[] { 2, 9 }),
                HasAll = SqlFunctions.ClickHouse.has_all(x.Nums, new[] { 3, 1 }),
                Starts = SqlFunctions.ClickHouse.starts_with(x.Nums, new[] { 3, 1 }),
                Ends = SqlFunctions.ClickHouse.ends_with(x.Nums, new[] { 2 }),
                Substr = SqlFunctions.ClickHouse.has_substr(x.Nums, new[] { 1, 2 }),
                SplitChar = SqlFunctions.ClickHouse.split_by_char(",", "a,b,c"),
                Sorted = SqlFunctions.ClickHouse.array_sort(new[] { 3, 1, 2 }),
                Reversed = SqlFunctions.ClickHouse.array_reverse(new[] { 3, 1, 2 }),
                Distinct = SqlFunctions.ClickHouse.array_distinct(new[] { "a", "b", "c" }),
                //Range1 = SqlFunctions.ClickHouse.range(3),
                //Range2 = SqlFunctions.ClickHouse.range(1, 4),
                //Range3 = SqlFunctions.ClickHouse.range(1, 7, 2),
                Slice1 = SqlFunctions.ClickHouse.array_slice(new[] { 3, 1, 2 }, 1),
                Slice2 = SqlFunctions.ClickHouse.array_slice(new[] { 3, 1, 2 }, 1, 2),
                Pushed = SqlFunctions.ClickHouse.array_push_back(new[] { 3, 1, 2 }, 9),
                Filtered = SqlFunctions.ClickHouse.array_filter(v => v > 1, new[] { 3, 1, 2 }),
                Exists = SqlFunctions.ClickHouse.array_exists(v => v > 2, new[] { 3, 1, 2 }),
                All = SqlFunctions.ClickHouse.array_all(v => v > 0, new[] { 3, 1, 2 }),
                Count = SqlFunctions.ClickHouse.array_count(v => v > 1, new[] { 3, 1, 2 }),
                First = SqlFunctions.ClickHouse.array_first(v => v > 1, new[] { 3, 1, 2 }),
                FirstIdx = SqlFunctions.ClickHouse.array_first_index(v => v > 1, new[] { 3, 1, 2 }),
                Last = SqlFunctions.ClickHouse.array_last(v => v > 1, new[] { 3, 1, 2 }),
                LastIdx = SqlFunctions.ClickHouse.array_last_index(v => v > 1, new[] { 3, 1, 2 }),
                Concat = SqlFunctions.ClickHouse.array_concat(new[] { 3, 1, 2 }, new[] { 1, 2 }),
                Flatten = SqlFunctions.ClickHouse.array_flatten(new[] { new[] { 3, 1 }, new[] { 2 } }),
                Uniq = SqlFunctions.ClickHouse.array_uniq(new[] { 3, 1, 2 }),
                Intersect = SqlFunctions.ClickHouse.array_intersect(new[] { 3, 1, 2 }, new[] { 1, 2, 9 }),
                Union = SqlFunctions.ClickHouse.array_union(new[] { 3, 1, 2 }, new[] { 9 }),
                SymDiff = SqlFunctions.ClickHouse.array_symmetric_difference(new[] { 3, 1, 2 }, new[] { 1, 9 })
            })
            .First();

        r.Len.Should().Be(3);
        r.Has.Should().BeTrue();
        r.Idx.Should().Be(3);
        r.HasAny.Should().BeTrue();
        r.HasAll.Should().BeTrue();
        r.Starts.Should().BeTrue();
        r.Ends.Should().BeTrue();
        r.Substr.Should().BeTrue();
        r.SplitChar.Should().Equal("a", "b", "c");
        r.Sorted.Should().Equal(1, 2, 3);
        r.Reversed.Should().Equal(2, 1, 3);
        r.Distinct.Should().HaveCount(3);
        r.Slice1.Should().Equal(3, 1, 2);
        r.Slice2.Should().Equal(3, 1);
        r.Pushed.Should().Equal(3, 1, 2, 9);
        r.Filtered.Should().Equal(3, 2);
        r.Exists.Should().BeTrue();
        r.All.Should().BeTrue();
        r.Count.Should().Be(2);
        r.First.Should().Be(3);
        r.FirstIdx.Should().Be(1);
        r.Last.Should().Be(2);
        r.LastIdx.Should().Be(3);
        r.Concat.Should().HaveCount(5);
        r.Flatten.Should().HaveCount(3);
        r.Uniq.Should().Be(3);
        r.Intersect.Should().Equal(1, 2);
        r.Union.Should().HaveCount(4);
        r.SymDiff.Should().HaveCount(3);
    }

    [Fact]
    public void ToDateFamily_ShouldExecute()
    {
        var value = new DateTime(2023, 5, 17, 13, 45, 30);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                D = SqlFunctions.ClickHouse.to_date(value),
                Dt = SqlFunctions.ClickHouse.to_date_time(value),
                D32 = SqlFunctions.ClickHouse.to_date32(value),
                Year = SqlFunctions.ClickHouse.to_year(value),
                Quarter = SqlFunctions.ClickHouse.to_quarter(value),
                Month = SqlFunctions.ClickHouse.to_month(value),
                Day = SqlFunctions.ClickHouse.to_day_of_month(value),
                Dow = SqlFunctions.ClickHouse.to_day_of_week(value),
                Doy = SqlFunctions.ClickHouse.to_day_of_year(value),
                Hour = SqlFunctions.ClickHouse.to_hour(value),
                Minute = SqlFunctions.ClickHouse.to_minute(value),
                Second = SqlFunctions.ClickHouse.to_second(value),
                StartYear = SqlFunctions.ClickHouse.to_start_of_year(value),
                StartQuarter = SqlFunctions.ClickHouse.to_start_of_quarter(value),
                StartMonth = SqlFunctions.ClickHouse.to_start_of_month(value),
                StartWeek = SqlFunctions.ClickHouse.to_start_of_week(value),
                StartDay = SqlFunctions.ClickHouse.to_start_of_day(value),
                StartHour = SqlFunctions.ClickHouse.to_start_of_hour(value),
                StartMinute = SqlFunctions.ClickHouse.to_start_of_minute(value),
                Monday = SqlFunctions.ClickHouse.to_monday(value),
                YyyyMm = SqlFunctions.ClickHouse.to_yyyymm(value),
                YyyyMmDd = SqlFunctions.ClickHouse.to_yyyymmdd(value),
                Unix = SqlFunctions.ClickHouse.to_unix_timestamp(value),
                Parsed = SqlFunctions.ClickHouse.parse_date_time("2023-05-17 13:45:30", "%Y-%m-%d %H:%i:%S"),
                Now = SqlFunctions.ClickHouse.now(),
                Today = SqlFunctions.ClickHouse.today(),
                Yesterday = SqlFunctions.ClickHouse.yesterday()
            })
            .First();

        r.D.Should().Be(new DateTime(2023, 5, 17));
        r.Dt.Should().Be(value);
        r.D32.Should().Be(new DateTime(2023, 5, 17));
        r.Year.Should().Be(2023);
        r.Quarter.Should().Be(2);
        r.Month.Should().Be(5);
        r.Day.Should().Be(17);
        r.Dow.Should().BeInRange(1, 7);
        r.Doy.Should().Be(137);
        r.Hour.Should().Be(13);
        r.Minute.Should().Be(45);
        r.Second.Should().Be(30);
        r.StartYear.Should().Be(new DateTime(2023, 1, 1));
        r.StartQuarter.Should().Be(new DateTime(2023, 4, 1));
        r.StartMonth.Should().Be(new DateTime(2023, 5, 1));
        r.StartDay.Should().Be(new DateTime(2023, 5, 17));
        r.StartHour.Should().Be(new DateTime(2023, 5, 17, 13, 0, 0));
        r.StartMinute.Should().Be(new DateTime(2023, 5, 17, 13, 45, 0));
        r.Monday.Should().Be(new DateTime(2023, 5, 15));
        r.YyyyMm.Should().Be(202305);
        r.YyyyMmDd.Should().Be(20230517);
        r.Unix.Should().Be(1684331130);
        r.Parsed.Should().Be(value);
        r.Now.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Today.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Yesterday.Should().BeAfter(new DateTime(2020, 1, 1));
    }

    [Fact]
    public void Utf8AndRegexpStrings_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Lower = SqlFunctions.ClickHouse.lower_utf8("Привет"),
                Upper = SqlFunctions.ClickHouse.upper_utf8("Привет"),
                RepOne = SqlFunctions.ClickHouse.replace_regexp_one("a1b2", "[0-9]", "x"),
                RepAll = SqlFunctions.ClickHouse.replace_regexp_all("a1b2", "[0-9]", "x"),
                Match = SqlFunctions.ClickHouse.match("abc123", "[0-9]+"),
                Extract = SqlFunctions.ClickHouse.extract("abc123", "[0-9]+"),
                ExtractAll = SqlFunctions.ClickHouse.extract_all("a1b2", "[0-9]"),
                SplitStr = SqlFunctions.ClickHouse.split_by_string(",", "a,b,c"),
                SplitRegexp = SqlFunctions.ClickHouse.split_by_regexp("[,;]", "a,b;c"),
                SplitWs = SqlFunctions.ClickHouse.split_by_whitespace("a b  c")
            })
            .First();

        r.Lower.Should().Be("привет");
        r.Upper.Should().Be("ПРИВЕТ");
        r.RepOne.Should().Be("axb2");
        r.RepAll.Should().Be("axbx");
        r.Match.Should().BeTrue();
        r.Extract.Should().Be("123");
        r.ExtractAll.Should().Equal("1", "2");
        r.SplitStr.Should().Equal("a", "b", "c");
        r.SplitRegexp.Should().Equal("a", "b", "c");
        r.SplitWs.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Aggregates_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .Select(x => new
            {
                ArgMin = SqlFunctions.ClickHouse.arg_min(x.Id, x.Int),
                ArgMax = SqlFunctions.ClickHouse.arg_max(x.Id, x.Int),
                Uniq = SqlFunctions.ClickHouse.uniq(x.Int),
                UniqExact = SqlFunctions.ClickHouse.uniq_exact(x.Int),
                UniqCombined = SqlFunctions.ClickHouse.uniq_combined(x.Int),
                UniqHll = SqlFunctions.ClickHouse.uniq_hll12(x.Int),
                Quantile = SqlFunctions.ClickHouse.quantile(0.5, x.Int),
                QuantileExact = SqlFunctions.ClickHouse.quantile_exact(0.5, x.Int),
                QuantileTiming = SqlFunctions.ClickHouse.quantile_timing(0.5, x.Int),
                Median = SqlFunctions.ClickHouse.median(x.Int),
                Quantiles = SqlFunctions.ClickHouse.quantiles(new[] { 0.25, 0.75 }, x.Int),
                AnyLast = SqlFunctions.ClickHouse.any_last(x.Int),
                WindowFunnel = SqlFunctions.ClickHouse.window_funnel(3600, x.Datetime, true, true),
                SequenceMatch = SqlFunctions.ClickHouse.sequence_match("(?1)(?2)", x.Datetime, true, true)
            })
            .First();

        r.ArgMin.Should().BeGreaterThan(0);
        r.ArgMax.Should().BeGreaterThan(0);
        r.Uniq.Should().Be(1);
        r.UniqExact.Should().Be(1);
        r.UniqCombined.Should().Be(1);
        r.UniqHll.Should().Be(1);
        r.Quantile.Should().Be(1);
        r.QuantileExact.Should().Be(1);
        r.QuantileTiming.Should().Be(1);
        r.Median.Should().Be(1);
        r.Quantiles.Should().HaveCount(2);
        r.AnyLast.Should().Be(1);
        r.WindowFunnel.Should().Be(2);
        r.SequenceMatch.Should().Be(1);
    }

    [Fact]
    public void ConditionalAndWindowHelpers_ShouldExecute()
    {
        var conditional = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                MultiIf = SqlFunctions.ClickHouse.multi_if(
                    SqlFunctions.ClickHouse.when(x.Id == 1, "one"),
                    SqlFunctions.ClickHouse.when(x.Id == 2, "two"),
                    SqlFunctions.ClickHouse.otherwise("other")),
                GlobalInValues = SqlFunctions.ClickHouse.global_in(x.Id, 1, 2, 3),
                GlobalInArray = SqlFunctions.ClickHouse.global_in(x.Id, new[] { 1, 2, 3 }),
                Host = SqlFunctions.ClickHouse.host_name(),
                Port = SqlFunctions.ClickHouse.tcp_port()
            })
            .First();

        conditional.MultiIf.Should().Be("one");
        conditional.GlobalInValues.Should().BeTrue();
        conditional.GlobalInArray.Should().BeTrue();
        conditional.Host.Should().NotBeNullOrEmpty();
        conditional.Port.Should().BeGreaterThan(0);

        var lag = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Lag = SqlFunctions.ClickHouse.lag_in_frame(x.Id).Over(),
                Lag2 = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1).Over(),
                Lag3 = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1, 0L).Over(),
                Lead = SqlFunctions.ClickHouse.lead_in_frame(x.Id).Over(),
                Lead2 = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1).Over(),
                Lead3 = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1, 0L).Over()
            })
            .ToList();

        lag.Should().HaveCount(3);
        lag[2].Lag.Should().Be(2);
        lag[0].Lead.Should().Be(2);
    }
}
