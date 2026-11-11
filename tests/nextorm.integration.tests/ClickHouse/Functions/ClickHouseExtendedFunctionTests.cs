using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes <see cref="ClickHouseExtendedFunctionTests"/> against every other integration
/// collection: <c>ArrayRewind_ShouldKeepParameterProvenanceAcrossCachedExecutions</c> asserts
/// prepared-command <em>identity</em> across three executions, while a concurrent
/// <see cref="DataContext.PurgeQueryCache"/> anywhere in the process bumps the process-wide
/// <c>QueryPlanStore</c> generation and evicts every thread's thread-local plan dictionary. The
/// second execution then legitimately re-prepares and returns a different
/// <c>DbPreparedQueryCommand</c> — correct product behaviour, but not the identity the regression
/// test is about, so the class must not overlap another collection.
/// </summary>
[CollectionDefinition("T327 array rewind cache", DisableParallelization = true)]
public sealed class T327ArrayRewindCacheCollectionDefinition;

/// <summary>
/// ClickHouse specific surface, third sweep: JSON extraction, the array library (including the
/// higher-order forms), the to* date family, the UTF-8/regexp string forms, the aggregate family
/// and the conditional/window helpers. Each call renders ClickHouse SQL and executes it.
/// </summary>
[Collection("T327 array rewind cache")]
public sealed class ClickHouseExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string Doc = "{\"a\":1,\"b\":\"x\",\"c\":[1,2]}";

    [Fact]
    public async Task JsonExtraction_ShouldExecute()
    {
        var query = _sut.SimpleEntity
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
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

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
    public async Task ArrayLibrary_ShouldExecute()
    {
        var query = _sut.ArrayEntity
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
                Range1 = SqlFunctions.ClickHouse.range(3),
                Range2 = SqlFunctions.ClickHouse.range(1, 4),
                Range3 = SqlFunctions.ClickHouse.range(1, 7, 2),
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
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

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
        r.Range1.Should().BeOfType<long[]>();
        r.Range1.Should().Equal(0L, 1L, 2L);
        r.Range2.Should().BeOfType<long[]>();
        r.Range2.Should().Equal(1L, 2L, 3L);
        r.Range3.Should().BeOfType<long[]>();
        r.Range3.Should().Equal(1L, 3L, 5L);
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

    /// <summary>
    /// #327 DEF-04 regression: the array renderers emit an argument fragment into the shared builder,
    /// truncate it back and re-append it inside the function wrapper. The parameter recorder must follow
    /// that rewind instead of running ahead of the builder, so the immutable template keeps every
    /// occurrence at its real offset. Before the fix the <c>params</c>-array renderer threw
    /// <see cref="ArgumentOutOfRangeException"/> during planning (the D8b failure of
    /// <c>ArrayLibrary_ShouldExecute</c>); now one prepared command serves <c>null → value → null</c>
    /// with byte-identical SQL, the same command instance and correct results.
    /// </summary>
    [Fact]
    [Trait("Issue", "327")]
    public void ArrayRewind_ShouldKeepParameterProvenanceAcrossCachedExecutions()
    {
        var ctx = _sut.DataProvider;
        int? captured = null;
        int[] expectedValue = [1, 2, 2, 9];
        var query = _sut.ComplexEntity
            .Where(x => x.Int == captured)
            .Select(_ => SqlFunctions.ClickHouse.array_concat(new[] { 1, 2 }, new[] { 2, 9 }));

        DbPreparedQueryCommand<int[]>? prepared = null;
        string? canonicalText = null;

        void Execute(string scenario, int? value, int expectedRowCount)
        {
            captured = value;
            var current = (DbPreparedQueryCommand<int[]>)((DataContext)ctx).GetPreparedQueryCommand(
                query,
                createEnumerator: false,
                storeInCache: true,
                TestContext.Current.CancellationToken);

            if (prepared is null)
            {
                prepared = current;
                canonicalText = current.DbCommand.CommandText;
            }
            else
            {
                current.Should().BeSameAs(prepared, "every later execution is a cache hit on the same prepared command");
            }

            current.DbCommand.CommandText.Should().Be(canonicalText, scenario);

            var template = current.ParameterTemplate;
            template.Should().NotBeNull("a rewound fragment must still finalize into a parameter template");
            template!.StatementText.Should().Be(canonicalText, scenario);
            foreach (var occurrence in template.Occurrences)
            {
                template.StatementText.Substring(occurrence.Start, occurrence.Length)
                    .Should().Be("@" + template.Bindings[occurrence.BindingId].Name,
                        "every occurrence span points at the placeholder actually emitted for its binding");
            }

            template.Compose(new string?[template.OccurrenceCount]).Should().Be(canonicalText, scenario);

            var rows = ctx.ToList(current);
            rows.Should().HaveCount(expectedRowCount, scenario);
            foreach (var row in rows)
                row.Should().Equal(expectedValue, scenario);
        }

        Execute("captured null", null, 1);
        Execute("captured 1", 1, 2);
        Execute("captured null again", null, 1);
    }

    [Fact]
    public async Task ToDateFamily_ShouldExecute()
    {
        var value = new DateTime(2023, 5, 17, 13, 45, 30);

        var query = _sut.SimpleEntity
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
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

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
    public async Task Utf8AndRegexpStrings_ShouldExecute()
    {
        var query = _sut.SimpleEntity
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
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

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
    public async Task Aggregates_ShouldExecute()
    {
        var query = _sut.ComplexEntity
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
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

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
    public async Task ConditionalAndWindowHelpers_ShouldExecute()
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

        var query = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Lag = SqlFunctions.ClickHouse.lag_in_frame(x.Id).Over(),
                Lag2 = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1).Over(),
                Lag3 = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1, 0L).Over(),
                Lead = SqlFunctions.ClickHouse.lead_in_frame(x.Id).Over(),
                Lead2 = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1).Over(),
                Lead3 = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1, 0L).Over()
            });

        var lag = await query.ToListAsync(TestContext.Current.CancellationToken);

        lag.Should().HaveCount(3);
        lag[2].Lag.Should().Be(2);
        lag[0].Lead.Should().Be(2);
    }

    [Fact]
    public async Task ArrayAndRankingAggregates_ShouldExecute()
    {
        var query = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .Select(x => new
            {
                Grouped = SqlFunctions.ClickHouse.group_array((int)x.Int!),
                GroupedUniq = SqlFunctions.ClickHouse.group_uniq_array((int)x.Int!),
                TopK = SqlFunctions.ClickHouse.top_k(2, (int)x.Int!),
                TopKWeighted = SqlFunctions.ClickHouse.top_k_weighted(2, (int)x.Int!, x.Id)
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

        // Both non-null rows carry Int == 1, so the unordered groupArray result is [1, 1].
        r.Grouped.Should().Equal(1, 1);
        r.GroupedUniq.Should().Equal(1);
        r.TopK.Should().Equal(1);
        r.TopKWeighted.Should().Equal(1);
    }

    [Fact]
    public async Task StatisticalAndBitAggregates_ShouldExecute()
    {
        var query = _sut.ComplexEntity
            .Select(x => new
            {
                Corr = SqlFunctions.Sql.corr((double)x.Id, (double)x.Id),
                CovarPop = SqlFunctions.Sql.covar_pop((double)x.Id, (double)x.Id),
                CovarSamp = SqlFunctions.Sql.covar_samp((double)x.Id, (double)x.Id),
                BitAnd = SqlFunctions.Postgres.bit_and((int)x.Id),
                BitOr = SqlFunctions.Postgres.bit_or((int)x.Id),
                BitXor = SqlFunctions.Postgres.bit_xor((int)x.Id)
            });

        var r = await query.FirstAsync(TestContext.Current.CancellationToken);

        r.Corr.Should().BeApproximately(1.0, 1e-12);
        r.CovarPop.Should().BeApproximately(2.0 / 3.0, 1e-12);
        r.CovarSamp.Should().BeApproximately(1.0, 1e-12);
        r.BitAnd.Should().Be(0);
        r.BitOr.Should().Be(3);
        r.BitXor.Should().Be(0);
    }

    [Fact]
    public async Task RetentionAggregate_ShouldExecute()
    {
        // retention declares int[] while ClickHouse returns Array(UInt8); the renderer normalises the
        // result to the declared array type, so the mask materialises directly instead of through
        // length(). complex_entity ids are 1..3, so every cumulative condition is satisfied by some row.
        var mask = _sut.ComplexEntity
            .Select(x => SqlFunctions.ClickHouse.retention(x.Id > 0, x.Id > 1, x.Id > 2))
            .First();

        mask.Should().BeOfType<int[]>();
        mask.Should().Equal(1, 1, 1);
    }

    [Fact]
    public void MultiIfGuards_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity.Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.multi_if(
                    SqlFunctions.ClickHouse.when(x.Id == 1, "one"),
                    SqlFunctions.ClickHouse.otherwise("other"),
                    SqlFunctions.ClickHouse.when(x.Id == 2, "two")))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The multiIf otherwise(...) branch must be the last one.");

        FluentActions.Invoking(() => _sut.SimpleEntity.Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.multi_if(
                    SqlFunctions.ClickHouse.when(x.Id == 1, "one")))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("multiIf requires a final otherwise(...) branch.");

        // The branch accessors are query-expression-only markers (they return null at runtime), so the
        // captured-array arm is reached with an already-captured empty array expression instead.
        FluentActions.Invoking(() => _sut.SimpleEntity.Where(x => x.Id == 1)
                .Select(x => SqlFunctions.ClickHouse.multi_if(Array.Empty<ClickHouseFunctions.MultiIfBranch<string>>()))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The multiIf branches must be inline when(...)/otherwise(...) calls.");

        FluentActions.Invoking(() => _sut.SimpleEntity
                .Where(x => SqlFunctions.ClickHouse.otherwise("other") != null)
                .Select(x => x.Id)
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The when/otherwise branches can only be used inside multi_if.");
    }

    [Fact]
    public void AggregateParameterGuards_ShouldBeRejected()
    {
        var levels = new[] { 0.25, 0.75 };
        FluentActions.Invoking(() => _sut.ComplexEntity.Where(x => x.Int != null)
                .Select(x => SqlFunctions.ClickHouse.quantiles(levels, x.Int))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The quantiles levels must be inline expressions, not a captured array.");

        var conditions = new[] { true, true };
        FluentActions.Invoking(() => _sut.ComplexEntity
                .Select(x => SqlFunctions.ClickHouse.window_funnel(3600, x.Datetime, conditions))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The windowFunnel/retention/sequenceMatch conditions must be inline expressions, not a captured array.");
    }

    [Fact]
    public void FullTextPredicates_ShouldBeRejected()
    {
        FluentActions.Invoking(() => _sut.SimpleEntity.Where(x => x.Id == 1)
                .Select(x => SqlFunctions.Sql.contains("abc", "a"))
                .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The full-text predicates (contains/freetext) are not supported by this provider.");
    }

    // --- T277 / issue #277: higher-order array functions over a column array -----------------------------------
    // The reported failure was a missing outer-entity binding when a higher-order array lambda carries an
    // inner parameter (arrayFilter(v -> ..., nums)). The projection here is deliberately the bare call from
    // the issue: no wrapper, no scalar normalization, so the value has to materialize as a real array.

    [Fact]
    public async Task T277_ArrayFilter_Column_BareProjection()
    {
        var query = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums));

        var nums = await query.FirstAsync(TestContext.Current.CancellationToken);

        nums.Should().Equal(3, 2);
    }

    [Fact]
    public async Task T277_ArrayFilter_Column_CorrelatedBody()
    {
        // The lambda body also reads the outer entity column, so the inner parameter and the outer binding
        // have to coexist in one expression tree.
        var query = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_filter(v => v > x.Id, x.Nums));

        var nums = await query.FirstAsync(TestContext.Current.CancellationToken);

        nums.Should().Equal(3, 2);
    }

    [Fact]
    public async Task T277_ArrayFilter_NegativeControls()
    {
        var rejected = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 100, x.Nums))
            .First();

        rejected.Should().BeEmpty("no element of [3, 1, 2] is greater than 100");

        var query = _sut.ArrayEntity
            .Where(x => x.Id == 3)
            .Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums));

        var emptySource = await query.FirstAsync(TestContext.Current.CancellationToken);

        emptySource.Should().BeEmpty("entity 3 carries an empty nums array");
    }

    /// <summary>
    /// T277 source-form matrix: for each higher-order array function, the same expectation is produced from
    /// an entity column, a captured array, an inline array, a body referencing the outer entity column and a
    /// body referencing a captured scalar. Matrix input is <c>[3, 1, 2]</c> on entity <c>Id == 1</c>, the
    /// predicate is <c>v &gt; 1</c> and the map is <c>v + 1</c>; the correlated variants substitute
    /// <c>x.Id</c> (1) and the captured-scalar variants substitute <c>t</c> (1), so every form expects the
    /// same value. Array-returning projections are normalized with <c>length()</c> on purpose: a bare
    /// promoted <c>array_map</c> result hits a separate materialization defect, which T277 does not own.
    /// </summary>
    [Theory]
    [InlineData("array_map", "column", 3L)]
    [InlineData("array_map", "captured-array", 3L)]
    [InlineData("array_map", "inline-array", 3L)]
    [InlineData("array_map", "outer-column", 3L)]
    [InlineData("array_map", "captured-scalar", 3L)]
    [InlineData("array_filter", "column", 2L)]
    [InlineData("array_filter", "captured-array", 2L)]
    [InlineData("array_filter", "inline-array", 2L)]
    [InlineData("array_filter", "outer-column", 2L)]
    [InlineData("array_filter", "captured-scalar", 2L)]
    [InlineData("array_exists", "column", true)]
    [InlineData("array_exists", "captured-array", true)]
    [InlineData("array_exists", "inline-array", true)]
    [InlineData("array_exists", "outer-column", true)]
    [InlineData("array_exists", "captured-scalar", true)]
    [InlineData("array_all", "column", false)]
    [InlineData("array_all", "captured-array", false)]
    [InlineData("array_all", "inline-array", false)]
    [InlineData("array_all", "outer-column", false)]
    [InlineData("array_all", "captured-scalar", false)]
    [InlineData("array_count", "column", 2L)]
    [InlineData("array_count", "captured-array", 2L)]
    [InlineData("array_count", "inline-array", 2L)]
    [InlineData("array_count", "outer-column", 2L)]
    [InlineData("array_count", "captured-scalar", 2L)]
    [InlineData("array_first", "column", 3L)]
    [InlineData("array_first", "captured-array", 3L)]
    [InlineData("array_first", "inline-array", 3L)]
    [InlineData("array_first", "outer-column", 3L)]
    [InlineData("array_first", "captured-scalar", 3L)]
    [InlineData("array_first_index", "column", 1L)]
    [InlineData("array_first_index", "captured-array", 1L)]
    [InlineData("array_first_index", "inline-array", 1L)]
    [InlineData("array_first_index", "outer-column", 1L)]
    [InlineData("array_first_index", "captured-scalar", 1L)]
    [InlineData("array_last", "column", 2L)]
    [InlineData("array_last", "captured-array", 2L)]
    [InlineData("array_last", "inline-array", 2L)]
    [InlineData("array_last", "outer-column", 2L)]
    [InlineData("array_last", "captured-scalar", 2L)]
    [InlineData("array_last_index", "column", 3L)]
    [InlineData("array_last_index", "captured-array", 3L)]
    [InlineData("array_last_index", "inline-array", 3L)]
    [InlineData("array_last_index", "outer-column", 3L)]
    [InlineData("array_last_index", "captured-scalar", 3L)]
    public async Task T277_HigherOrder_SourceMatrix(string function, string variant, object expected)
    {
        var actual = await ExecuteHigherOrderCellAsync(function, variant);

        if (expected is bool expectedBool)
        {
            actual.Should().Be(expectedBool);
            return;
        }

        actual.Should().BeOfType<long>().Which.Should().Be((long)expected);
    }

    /// <summary>
    /// Builds one matrix cell. Every numeric terminal is widened to <c>long</c> so the theory compares a
    /// single numeric type; array-returning functions are measured through <c>length()</c>. Exactly one
    /// cell is read per theory row, and only the <c>long</c> cell is retry-controlled (T328 D4b), which
    /// keeps the one-operation-per-invocation budget.
    /// </summary>
    private async Task<object> ExecuteHigherOrderCellAsync(string function, string variant)
    {
        int[] captured = { 3, 1, 2 };
        int t = 1;

        return function switch
        {
            "array_map" => variant switch
            {
                "column" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_map(v => v + 1, x.Nums))),
                "captured-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_map(v => v + 1, captured))),
                "inline-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_map(v => v + 1, new[] { 3, 1, 2 }))),
                "outer-column" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_map(v => v + x.Id, x.Nums))),
                "captured-scalar" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_map(v => v + t, x.Nums))),
                _ => throw UnknownVariant(function, variant)
            },
            "array_filter" => variant switch
            {
                "column" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums))),
                "captured-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_filter(v => v > 1, captured))),
                "inline-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_filter(v => v > 1, new[] { 3, 1, 2 }))),
                "outer-column" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_filter(v => v > x.Id, x.Nums))),
                "captured-scalar" => await LongCellAsync(x => SqlFunctions.ClickHouse.length(
                    SqlFunctions.ClickHouse.array_filter(v => v > t, x.Nums))),
                _ => throw UnknownVariant(function, variant)
            },
            "array_exists" => variant switch
            {
                "column" => BoolCell(x => SqlFunctions.ClickHouse.array_exists(v => v > 1, x.Nums)),
                "captured-array" => BoolCell(x => SqlFunctions.ClickHouse.array_exists(v => v > 1, captured)),
                "inline-array" => BoolCell(x => SqlFunctions.ClickHouse.array_exists(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => BoolCell(x => SqlFunctions.ClickHouse.array_exists(v => v > x.Id, x.Nums)),
                "captured-scalar" => BoolCell(x => SqlFunctions.ClickHouse.array_exists(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_all" => variant switch
            {
                "column" => BoolCell(x => SqlFunctions.ClickHouse.array_all(v => v > 1, x.Nums)),
                "captured-array" => BoolCell(x => SqlFunctions.ClickHouse.array_all(v => v > 1, captured)),
                "inline-array" => BoolCell(x => SqlFunctions.ClickHouse.array_all(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => BoolCell(x => SqlFunctions.ClickHouse.array_all(v => v > x.Id, x.Nums)),
                "captured-scalar" => BoolCell(x => SqlFunctions.ClickHouse.array_all(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_count" => variant switch
            {
                "column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_count(v => v > 1, x.Nums)),
                "captured-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_count(v => v > 1, captured)),
                "inline-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_count(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_count(v => v > x.Id, x.Nums)),
                "captured-scalar" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_count(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_first" => variant switch
            {
                "column" => IntCell(x => SqlFunctions.ClickHouse.array_first(v => v > 1, x.Nums)),
                "captured-array" => IntCell(x => SqlFunctions.ClickHouse.array_first(v => v > 1, captured)),
                "inline-array" => IntCell(x => SqlFunctions.ClickHouse.array_first(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => IntCell(x => SqlFunctions.ClickHouse.array_first(v => v > x.Id, x.Nums)),
                "captured-scalar" => IntCell(x => SqlFunctions.ClickHouse.array_first(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_first_index" => variant switch
            {
                "column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_first_index(v => v > 1, x.Nums)),
                "captured-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_first_index(v => v > 1, captured)),
                "inline-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_first_index(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_first_index(v => v > x.Id, x.Nums)),
                "captured-scalar" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_first_index(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_last" => variant switch
            {
                "column" => IntCell(x => SqlFunctions.ClickHouse.array_last(v => v > 1, x.Nums)),
                "captured-array" => IntCell(x => SqlFunctions.ClickHouse.array_last(v => v > 1, captured)),
                "inline-array" => IntCell(x => SqlFunctions.ClickHouse.array_last(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => IntCell(x => SqlFunctions.ClickHouse.array_last(v => v > x.Id, x.Nums)),
                "captured-scalar" => IntCell(x => SqlFunctions.ClickHouse.array_last(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            "array_last_index" => variant switch
            {
                "column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_last_index(v => v > 1, x.Nums)),
                "captured-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_last_index(v => v > 1, captured)),
                "inline-array" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_last_index(v => v > 1, new[] { 3, 1, 2 })),
                "outer-column" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_last_index(v => v > x.Id, x.Nums)),
                "captured-scalar" => await LongCellAsync(x => SqlFunctions.ClickHouse.array_last_index(v => v > t, x.Nums)),
                _ => throw UnknownVariant(function, variant)
            },
            _ => throw UnknownVariant(function, variant)
        };
    }

    private Task<long> LongCellAsync(Expression<Func<IArrayEntity, long>> projection)
    {
        var query = _sut.ArrayEntity.Where(x => x.Id == 1).Select(projection);

        return query.FirstAsync(TestContext.Current.CancellationToken);
    }

    private long IntCell(Expression<Func<IArrayEntity, int>> projection) =>
        _sut.ArrayEntity.Where(x => x.Id == 1).Select(projection).First();

    private bool BoolCell(Expression<Func<IArrayEntity, bool>> projection) =>
        _sut.ArrayEntity.Where(x => x.Id == 1).Select(projection).First();

    private static InvalidOperationException UnknownVariant(string function, string variant) =>
        new($"T277 matrix row '{function}'/'{variant}' is not part of the planned provider x form matrix.");
}
