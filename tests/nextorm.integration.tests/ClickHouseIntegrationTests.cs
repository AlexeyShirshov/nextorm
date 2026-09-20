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
                F = SqlFunctions.ClickHouse.count_if(() => x.Id <= 2)
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
            .Select(x => SqlFunctions.ClickHouse.count_if(() => x.Id <= 2))
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void IfAggregates_ShouldFilterBeforeAggregating()
    {
        // The arguments are cast to the ClickHouse result types (Int64 for the integer sum/min/max,
        // Float64 for the average) so the driver reads them back without a lossy conversion.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                Sum = SqlFunctions.ClickHouse.sum_if((long)x.Id, () => x.Id <= 2),
                Avg = SqlFunctions.ClickHouse.avg_if((double)x.Id, () => x.Id <= 2),
                Min = SqlFunctions.ClickHouse.min_if((long)x.Id, () => x.Id <= 2),
                Max = SqlFunctions.ClickHouse.max_if((long)x.Id, () => x.Id <= 2)
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
}
