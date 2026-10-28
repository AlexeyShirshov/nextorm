using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the ClickHouse-native function surface against a live server (issue #78). The
/// SQL-shape assertions live in <c>tests/nextorm.clickhouse.tests/Functions</c>; here every
/// rendered call must actually run and return the documented value, which also pins the
/// result types the row reader materialises.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseFunctionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public async Task Utf8StringFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lru = SqlFunctions.Sql.left("Привет", 3),
                Rru = SqlFunctions.Sql.right("Привет", 3),
                Upper = SqlFunctions.ClickHouse.upper_utf8("привет"),
                Lower = SqlFunctions.ClickHouse.lower_utf8("ПРИВЕТ")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.Utf8StringFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.L.Should().Be("abc");
        r.R.Should().Be("def");
        r.Lru.Should().Be("При");
        r.Rru.Should().Be("вет");
        r.Upper.Should().Be("ПРИВЕТ");
        r.Lower.Should().Be("привет");
    }

    [Fact]
    public async Task TrimFunctions_ShouldExecute_WithAndWithoutCharacters()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                TL = SqlFunctions.ClickHouse.trim_left("  x  "),
                TR = SqlFunctions.ClickHouse.trim_right("  x  "),
                TB = SqlFunctions.ClickHouse.trim_both("  x  "),
                TLC = SqlFunctions.ClickHouse.trim_left("xxabc", "x"),
                TRC = SqlFunctions.ClickHouse.trim_right("abcxx", "x"),
                TBC = SqlFunctions.ClickHouse.trim_both("xxabcxx", "x")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.TrimFunctions_ShouldExecute_WithAndWithoutCharacters#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.TL.Should().Be("x  ");
        r.TR.Should().Be("  x");
        r.TB.Should().Be("x");
        r.TLC.Should().Be("abc");
        r.TRC.Should().Be("abc");
        r.TBC.Should().Be("abc");
    }

    [Fact]
    public async Task RegexpFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                One = SqlFunctions.ClickHouse.replace_regexp_one("abcabc", "a", "x"),
                All = SqlFunctions.ClickHouse.replace_regexp_all("abcabc", "a", "x"),
                M = SqlFunctions.ClickHouse.match("abc", "b"),
                E = SqlFunctions.ClickHouse.extract("abc123", "[0-9]+"),
                EA = SqlFunctions.ClickHouse.extract_all("a1b2", "[0-9]")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.RegexpFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.One.Should().Be("xbcabc");
        r.All.Should().Be("xbcxbc");
        r.M.Should().BeTrue();
        r.E.Should().Be("123");
        r.EA.Should().HaveCount(2);
    }

    [Fact]
    public async Task SplitFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.split_by_string(",", "a,b,c"), "|"),
                R = SqlFunctions.ClickHouse.split_by_regexp("[,;]", "a,b;c"),
                W = SqlFunctions.ClickHouse.split_by_whitespace("a b  c"),
                C = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.split_by_string(",", "a,b,c"), string.Empty)
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.SplitFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.S.Should().Be("a|b|c");
        r.R.Should().HaveCount(3);
        r.W.Should().HaveCount(3);
        r.C.Should().Be("abc");
    }

    [Fact]
    public async Task DateTimeFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.format_date_time(
                    SqlFunctions.ClickHouse.parse_date_time("2023-01-02", "%Y-%m-%d"), "%Y/%m/%d"),
                Ftz = SqlFunctions.ClickHouse.format_date_time(
                    SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-01-02", "UTC"), "%Y"),
                P = SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-01-02"),
                N = SqlFunctions.ClickHouse.now(),
                T = SqlFunctions.ClickHouse.today(),
                Y = SqlFunctions.ClickHouse.yesterday()
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.DateTimeFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.F.Should().Be("2023/01/02");
        r.Ftz.Should().Be("2023");
        r.P.Should().NotBeNull();
        r.P!.Value.Year.Should().Be(2023);
        r.N.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Y.Should().Be(r.T!.Value.AddDays(-1));
    }

    [Fact]
    public async Task ArraySetFunctions_ShouldExecute()
    {
        var query = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Concat = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_concat(x.Nums, x.Nums)),
                Uniq = SqlFunctions.ClickHouse.array_uniq(x.Nums),
                Intersect = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_intersect(x.Nums, x.Nums)),
                Union = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_union(x.Nums, x.Nums)),
                Symmetric = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_symmetric_difference(x.Nums, x.Nums)),
                Flatten = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_flatten(new[]
                {
                    new[] { 1L, 2L },
                    new[] { 3L }
                })),
                Sorted = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_sort(x.Nums), ","),
                Reversed = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_reverse(x.Nums), ","),
                Distinct = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_distinct(x.Nums), ",")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.ArraySetFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Concat.Should().Be(6);
        r.Uniq.Should().Be(3);
        r.Intersect.Should().Be(3);
        r.Union.Should().Be(3);
        r.Symmetric.Should().Be(0);
        r.Flatten.Should().Be(3);
        r.Sorted.Should().Be("1,2,3");
        r.Reversed.Should().Be("2,1,3");
        r.Distinct.Should().Be("3,1,2");
    }

    /// <summary>
    /// The <c>range</c> arity arms of the ClickHouse array table: one argument (<c>0..end</c>), two
    /// (<c>start..end</c>) and three (<c>start..end</c> with a step). Each array is joined to a string
    /// so the assertion pins the element order without depending on the array materialiser.
    /// </summary>
    [Fact]
    public async Task ArrayRangeArity_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                One = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(3), ","),
                Two = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(1, 4), ","),
                Three = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.range(1, 7, 2), ",")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.ArrayRangeArity_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.One.Should().Be("0,1,2");
        r.Two.Should().Be("1,2,3");
        r.Three.Should().Be("1,3,5");
    }

    [Fact]
    public async Task MapFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Keys = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L), Tuple.Create("b", 2L)))),
                Values = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_values(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)))),
                ContainsKey = SqlFunctions.ClickHouse.map_contains_key(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)), "a"),
                ContainsValue = SqlFunctions.ClickHouse.map_contains_value(SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1L)), 1L),
                Added = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_add(
                    SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)),
                    SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
                Concatenated = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map_concat(
                    SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)),
                    SqlFunctions.ClickHouse.map(Tuple.Create("b", 2L))))),
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.MapFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Keys.Should().Be(2);
        r.Values.Should().Be(1);
        r.ContainsKey.Should().BeTrue();
        r.ContainsValue.Should().BeTrue();
        r.Added.Should().Be(2);
        r.Concatenated.Should().Be(2);
    }

    // ---------------------------------------------------------------------
    // T257 — ClickHouse map_* higher-order functions (issue #257).
    // The five public helpers must execute against the pinned 25.8 server with their filtering,
    // projection, sorting and predicate semantics intact. The semantic operand throughout is
    // m = map('b', 2, 'a', 1, 'c', -1); its insertion order is observable in the results.
    // map_filter is the one documented return-shape break of this cycle:
    // IDictionary<TKey,TValue> -> Tuple<TKey,TValue>[] (hence the tuple assertions on F).
    // ---------------------------------------------------------------------

    /// <summary>T257 / R-F: positive values survive in insertion order as tuple entries.</summary>
    [Fact]
    public void T257_MapFilter_ShouldExecute_SemanticMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
    }

    /// <summary>T257 / R-A: the lambda result per entry, in map insertion order.</summary>
    [Fact]
    public void T257_MapApply_ShouldExecute_SemanticMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.A.Should().Equal(4, 2, -2);
    }

    /// <summary>T257 / R-S: entries ordered by key, not by insertion order.</summary>
    [Fact]
    public void T257_MapSort_ShouldExecute_SemanticMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.S.Should().Equal(
            Tuple.Create("a", 1), Tuple.Create("b", 2), Tuple.Create("c", -1));
    }

    /// <summary>T257 / R-L: one non-positive value makes the predicate false.</summary>
    [Fact]
    public void T257_MapAll_ShouldExecute_SemanticMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.L.Should().BeFalse();
    }

    /// <summary>T257 / R-L: the all-positive variant stays true.</summary>
    [Fact]
    public void T257_MapAll_ShouldExecute_AllPositiveMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", 1), Tuple.Create("b", 2)))
            })
            .First();

        row.L.Should().BeTrue();
    }

    /// <summary>T257 / R-E: at least one positive value makes the predicate true.</summary>
    [Fact]
    public void T257_MapExists_ShouldExecute_SemanticMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.E.Should().BeTrue();
    }

    /// <summary>T257 / R-E: the all-non-positive variant stays false.</summary>
    [Fact]
    public void T257_MapExists_ShouldExecute_AllNonPositiveMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("a", -1), Tuple.Create("b", 0)))
            })
            .First();

        row.E.Should().BeFalse();
    }

    /// <summary>
    /// T257: a captured <see cref="Dictionary{TKey,TValue}"/> constant is a tested operand source.
    /// This binding is an explicit testable assumption of the plan, not a weakened row: all five
    /// helpers must behave exactly like the inline-map variants.
    /// </summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_CapturedDictionaryConstant()
    {
        var source = new Dictionary<string, int>
        {
            ["b"] = 2,
            ["a"] = 1,
            ["c"] = -1
        };

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, source),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source),
                S = SqlFunctions.ClickHouse.map_sort(source),
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, source),
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, source)
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
        row.A.Should().Equal(4, 2, -2);
        row.S.Should().Equal(
            Tuple.Create("a", 1), Tuple.Create("b", 2), Tuple.Create("c", -1));
        row.L.Should().BeFalse();
        row.E.Should().BeTrue();
    }

    /// <summary>T257: the same matrix with a captured dictionary member (static field) as the operand.</summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_CapturedDictionaryMember()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, MemberMap),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, MemberMap),
                S = SqlFunctions.ClickHouse.map_sort(MemberMap),
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, MemberMap),
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, MemberMap)
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
        row.A.Should().Equal(4, 2, -2);
        row.S.Should().Equal(
            Tuple.Create("a", 1), Tuple.Create("b", 2), Tuple.Create("c", -1));
        row.L.Should().BeFalse();
        row.E.Should().BeTrue();
    }

    /// <summary>T257: an empty captured dictionary stays empty for the array helpers and follows the vacuous predicates.</summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_EmptyCapturedDictionary()
    {
        var source = new Dictionary<string, int>();

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, source),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source),
                S = SqlFunctions.ClickHouse.map_sort(source),
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, source),
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, source)
            })
            .First();

        AssertMapEntries<string, int>(row.F);
        row.A.Should().BeEmpty();
        row.S.Should().BeEmpty();
        row.L.Should().BeTrue("mapAll over an empty map is vacuously true");
        row.E.Should().BeFalse("mapExists over an empty map is false");
    }

    /// <summary>T257: the empty inline constructor yields the same empty/vacuous results.</summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_EmptyInlineMap()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>()),
                A = SqlFunctions.ClickHouse.map_apply<string, int, int>((k, v) => v * 2,
                    SqlFunctions.ClickHouse.map<string, int>()),
                S = SqlFunctions.ClickHouse.map_sort<string, int>(SqlFunctions.ClickHouse.map<string, int>()),
                L = SqlFunctions.ClickHouse.map_all<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>()),
                E = SqlFunctions.ClickHouse.map_exists<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>())
            })
            .First();

        AssertMapEntries<string, int>(row.F);
        row.A.Should().BeEmpty();
        row.S.Should().BeEmpty();
        row.L.Should().BeTrue("mapAll over an empty map is vacuously true");
        row.E.Should().BeFalse("mapExists over an empty map is false");
    }

    /// <summary>T257: a reference-valued apply result materialises the keys, not a hash or an ordinal.</summary>
    [Fact]
    public void T257_MapApply_ShouldExecute_ReferenceValuedResult()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.map_apply((k, v) => k, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.A.Should().Equal("b", "a", "c");
    }

    /// <summary>T257: a default/null apply result must stay null, never become an empty string or a zero.</summary>
    [Fact]
    public void T257_MapApply_ShouldExecute_DefaultResultRetainsNull()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v > 0 ? k : default(string),
                    SqlFunctions.ClickHouse.map(
                        Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        row.A.Should().HaveCount(3);
        row.A[0].Should().Be("b");
        row.A[1].Should().Be("a");
        row.A[2].Should().BeNull("the default branch of the projection must be retained as NULL");
    }

    /// <summary>T257: string-valued maps through filter, apply, predicates and sorting.</summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_StringValuedMaps()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v == "alpha", SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                S = SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                L = SqlFunctions.ClickHouse.map_all((k, v) => k != "", SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v == "alpha", SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha")))
            })
            .First();

        AssertMapEntries(row.F, ("a", "alpha"));
        row.A.Should().Equal("beta", "alpha");
        row.S.Should().Equal(Tuple.Create("a", "alpha"), Tuple.Create("b", "beta"));
        row.L.Should().BeTrue();
        row.E.Should().BeTrue();
    }

    /// <summary>
    /// T257 guard: a null map operand must be refused at translation time, before any SQL reaches
    /// the server, for all five helpers.
    /// </summary>
    [Fact]
    public void T257_MapFunctions_NullMapOperand_ShouldThrowAtTranslation()
    {
        IDictionary<string, int>? source = null;

        var filter = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, source) })
            .First();
        filter.Should().Throw<NotSupportedException>("a null map operand must not reach the server");

        var apply = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source) })
            .First();
        apply.Should().Throw<NotSupportedException>("a null map operand must not reach the server");

        var sort = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_sort(source) })
            .First();
        sort.Should().Throw<NotSupportedException>("a null map operand must not reach the server");

        var all = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, source) })
            .First();
        all.Should().Throw<NotSupportedException>("a null map operand must not reach the server");

        var exists = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, source) })
            .First();
        exists.Should().Throw<NotSupportedException>("a null map operand must not reach the server");
    }

    /// <summary>T257 guard: a null constructor-pair collection is rejected before the server for all five helpers.</summary>
    [Fact]
    public void T257_MapConstructor_NullPairCollection_ShouldThrowAtTranslation()
    {
        Tuple<string, int>[] pairs = null!;

        var keys = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map<string, int>(pairs)) })
            .First();
        keys.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var filter = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_filter<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        filter.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var apply = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_apply<string, int, int>((k, v) => v * 2,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        apply.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var sort = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_sort<string, int>(SqlFunctions.ClickHouse.map<string, int>(pairs)) })
            .First();
        sort.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var all = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_all<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        all.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var exists = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_exists<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        exists.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");
    }

    /// <summary>T257 guard: a captured (non-inline) pair collection is rejected before the server for all five helpers.</summary>
    [Fact]
    public void T257_MapConstructor_CapturedPairCollection_ShouldThrowAtTranslation()
    {
        var pairs = new[] { Tuple.Create("b", 2) };

        var keys = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map<string, int>(pairs)) })
            .First();
        keys.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var filter = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_filter<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        filter.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var apply = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_apply<string, int, int>((k, v) => v * 2,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        apply.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var sort = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.map_sort<string, int>(SqlFunctions.ClickHouse.map<string, int>(pairs)) })
            .First();
        sort.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var all = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_all<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        all.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");

        var exists = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_exists<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>(pairs))
            })
            .First();
        exists.Should().Throw<NotSupportedException>("the map pairs must be inline tuples");
    }

    /// <summary>T257 guard: a non-inline lambda is refused for the four lambda helpers.</summary>
    [Fact]
    public void T257_MapFunctions_NonInlineLambda_ShouldThrowAtTranslation()
    {
        Expression<Func<string, int, bool>> predicate = (k, v) => v > 0;
        Expression<Func<string, int, int>> function = (k, v) => v * 2;

        var filter = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_filter(predicate, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2)))
            })
            .First();
        filter.Should().Throw<NotSupportedException>("the lambda must be inline in the projection");

        var apply = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_apply(function, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2)))
            })
            .First();
        apply.Should().Throw<NotSupportedException>("the lambda must be inline in the projection");

        var all = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_all(predicate, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2)))
            })
            .First();
        all.Should().Throw<NotSupportedException>("the lambda must be inline in the projection");

        var exists = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V = SqlFunctions.ClickHouse.map_exists(predicate, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2)))
            })
            .First();
        exists.Should().Throw<NotSupportedException>("the lambda must be inline in the projection");
    }

    // ---------------------------------------------------------------------
    // T257-TYPE-01 — exact CLR collection element types on the public path.
    // The semantic results above are not enough: a row that carries the right values with the wrong
    // element type (Int16 where Int32 is declared, Int64 where Int32 is declared, DBNull where
    // Tuple<string,int> is declared) is a failure. Every arm below asserts the exact array type the
    // declaration promises *and* the values, so a widened or narrowed materialisation cannot pass.
    // ---------------------------------------------------------------------

    /// <summary>
    /// T257-TYPE-01 / F+S+A: an inline populated map materialises <c>Tuple&lt;string,int&gt;[]</c> for
    /// filter and sort and <c>int[]</c> for apply.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_InlinePopulated_ShouldMaterializeDeclaredElementTypes()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1))),
                S = SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1))),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
        AssertMapEntries(row.S, ("a", 1), ("b", 2), ("c", -1));
        AssertArray(row.A, 4, 2, -2);
    }

    /// <summary>
    /// T257-TYPE-01: a captured dictionary constant is not a width shortcut — the same exact element
    /// types are materialised as for the inline constructor.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_CapturedDictionaryConstant_ShouldMaterializeDeclaredElementTypes()
    {
        var source = new Dictionary<string, int>
        {
            ["b"] = 2,
            ["a"] = 1,
            ["c"] = -1
        };

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, source),
                S = SqlFunctions.ClickHouse.map_sort(source),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source)
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
        AssertMapEntries(row.S, ("a", 1), ("b", 2), ("c", -1));
        AssertArray(row.A, 4, 2, -2);
    }

    /// <summary>T257-TYPE-01: a captured dictionary member (static field) materialises the same exact element types.</summary>
    [Fact]
    public void T257_TypeMatrix_CapturedDictionaryMember_ShouldMaterializeDeclaredElementTypes()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, MemberMap),
                S = SqlFunctions.ClickHouse.map_sort(MemberMap),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, MemberMap)
            })
            .First();

        AssertMapEntries(row.F, ("b", 2), ("a", 1));
        AssertMapEntries(row.S, ("a", 1), ("b", 2), ("c", -1));
        AssertArray(row.A, 4, 2, -2);
    }

    /// <summary>
    /// T257-TYPE-01: an empty inline constructor keeps the declared element type, so the result is an
    /// empty <c>Tuple&lt;string,int&gt;[]</c>/<c>int[]</c> — never <c>DBNull[]</c> and never a
    /// non-empty placeholder.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_EmptyInlineMap_ShouldMaterializeEmptyDeclaredArrays()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>()),
                S = SqlFunctions.ClickHouse.map_sort<string, int>(SqlFunctions.ClickHouse.map<string, int>()),
                A = SqlFunctions.ClickHouse.map_apply<string, int, int>((k, v) => v * 2,
                    SqlFunctions.ClickHouse.map<string, int>()),
                L = SqlFunctions.ClickHouse.map_all<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>()),
                E = SqlFunctions.ClickHouse.map_exists<string, int>((k, v) => v > 0,
                    SqlFunctions.ClickHouse.map<string, int>())
            })
            .First();

        AssertEmptyArray<Tuple<string, int>>(row.F);
        AssertEmptyArray<Tuple<string, int>>(row.S);
        AssertEmptyArray<int>(row.A);
        row.L.Should().BeTrue("mapAll over an empty map is vacuously true");
        row.E.Should().BeFalse("mapExists over an empty map is false");
    }

    /// <summary>T257-TYPE-01: an empty captured dictionary keeps the declared element type too.</summary>
    [Fact]
    public void T257_TypeMatrix_EmptyCapturedDictionary_ShouldMaterializeEmptyDeclaredArrays()
    {
        var source = new Dictionary<string, int>();

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > 0, source),
                S = SqlFunctions.ClickHouse.map_sort(source),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source),
                L = SqlFunctions.ClickHouse.map_all((k, v) => v > 0, source),
                E = SqlFunctions.ClickHouse.map_exists((k, v) => v > 0, source)
            })
            .First();

        AssertEmptyArray<Tuple<string, int>>(row.F);
        AssertEmptyArray<Tuple<string, int>>(row.S);
        AssertEmptyArray<int>(row.A);
        row.L.Should().BeTrue("mapAll over an empty map is vacuously true");
        row.E.Should().BeFalse("mapExists over an empty map is false");
    }

    /// <summary>
    /// T257-TYPE-01: a nullable apply result materialises <c>int?[]</c> and the default branch stays
    /// a null element instead of collapsing to zero.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_ApplyNullableDefault_ShouldMaterializeNullableArray()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.ClickHouse.map_apply<string, int, int?>((k, v) => v > 0 ? v : default(int?),
                    SqlFunctions.ClickHouse.map<string, int>(
                        Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        AssertArray<int?>(row.A, 2, 1, null);
    }

    /// <summary>
    /// T257-TYPE-01: the apply result type is its own contract — a <c>long</c> result over an
    /// <c>int</c>-valued map is <c>long[]</c>, a <c>string</c> result is <c>string[]</c>, and neither
    /// borrows the map value type.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_ApplyResultTypeDiffersFromMapValueType_ShouldMaterializeResultType()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Long = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2L, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1))),
                Text = SqlFunctions.ClickHouse.map_apply((k, v) => v > 0 ? "pos" : "neg",
                    SqlFunctions.ClickHouse.map(
                        Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .First();

        AssertArray(row.Long, 4L, 2L, -2L);
        AssertArray(row.Text, "pos", "pos", "neg");
    }

    /// <summary>
    /// T257-TYPE-01: string-valued maps materialise <c>Tuple&lt;string,string&gt;[]</c> for filter and
    /// sort and <c>string[]</c> for apply.
    /// </summary>
    [Fact]
    public void T257_TypeMatrix_StringValuedMaps_ShouldMaterializeDeclaredElementTypes()
    {
        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v == "alpha", SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                S = SqlFunctions.ClickHouse.map_sort(SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                A = SqlFunctions.ClickHouse.map_apply((k, v) => v, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha"))),
                K = SqlFunctions.ClickHouse.map_apply((k, v) => k, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", "beta"), Tuple.Create("a", "alpha")))
            })
            .First();

        AssertMapEntries(row.F, ("a", "alpha"));
        AssertMapEntries(row.S, ("a", "alpha"), ("b", "beta"));
        AssertArray(row.A, "beta", "alpha");
        AssertArray(row.K, "b", "a");
    }

    /// <summary>
    /// T257 / CHECK #1: a captured local variable inside a <c>map_*</c> lambda body used to throw
    /// <c>NotSupportedException("Cannot clone in param mode")</c> on the parameter-collection pass.
    /// The same query is executed twice on the same <see cref="IDataContext"/>, so the second run is
    /// served by the plan cache and must return the same values and the same exact CLR types.
    /// </summary>
    [Fact]
    public void T257_MapFunctions_ShouldExecute_OnAPlanCacheHitWithCapturedVariables()
    {
        var ctx = _sut.DataProvider;
        var threshold = 0;
        var source = new Dictionary<string, int>
        {
            ["b"] = 2,
            ["a"] = 1,
            ["c"] = -1
        };

        void Run()
        {
            var row = ctx.From<ISimpleEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    F = SqlFunctions.ClickHouse.map_filter((k, v) => v > threshold, source),
                    A = SqlFunctions.ClickHouse.map_apply((k, v) => v * 2, source),
                    S = SqlFunctions.ClickHouse.map_sort(source),
                    L = SqlFunctions.ClickHouse.map_all((k, v) => v > threshold, source),
                    E = SqlFunctions.ClickHouse.map_exists((k, v) => v > threshold, source)
                })
                .First();

            AssertMapEntries(row.F, ("b", 2), ("a", 1));
            AssertArray(row.A, 4, 2, -2);
            AssertMapEntries(row.S, ("a", 1), ("b", 2), ("c", -1));
            row.L.Should().BeFalse();
            row.E.Should().BeTrue();
        }

        Run(); // cold: builds and stores the plan
        Run(); // plan-cache hit: the parameter-collection pass that used to throw
    }

    /// <summary>
    /// T257 / CHECK #1: the same captured-variable lambda inside a typed CTE projection — the shape
    /// that threw on the parameter-collection pass — must execute and materialise the declared
    /// tuple array.
    /// </summary>
    [Fact]
    public void T257_MapFilter_ShouldExecute_InsideACteProjectionWithACapturedVariable()
    {
        var ctx = _sut.DataProvider;
        var threshold = 0;

        var filtered = ctx.From<IComplexEntity>()
            .Where(c => c.Id == 1)
            .Select(c => new
            {
                c.Id,
                F = SqlFunctions.ClickHouse.map_filter((k, v) => v > threshold, SqlFunctions.ClickHouse.map(
                    Tuple.Create("b", 2), Tuple.Create("a", 1), Tuple.Create("c", -1)))
            })
            .AsCte("t257_map_filter_cte");

        var row = ctx.From(filtered).Select(r => new { r.Id, r.F }).First();

        row.Id.Should().Be(1);
        AssertMapEntries(row.F, ("b", 2), ("a", 1));
    }

    /// <summary>
    /// T257 / CHECK #3: an outer higher-order lambda parameter referenced from a <c>map_filter</c>
    /// body nested inside it (the nested-scope defect) must resolve to that outer parameter.
    /// <c>Nums</c> of row 1 is <c>[3, 1, 2]</c> and the map is <c>map('a', 5, 'b', 2)</c>, so the
    /// surviving entry count per outer element is 1, 2, 1.
    /// </summary>
    [Fact]
    public void T257_MapFilter_ShouldResolveAnOuterLambdaParameterInANestedScope()
    {
        var row = _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Counts = SqlFunctions.ClickHouse.array_map(
                    n => SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_filter(
                        (k, v) => v > n,
                        SqlFunctions.ClickHouse.map(Tuple.Create("a", 5), Tuple.Create("b", 2)))),
                    x.Nums)
            })
            .First();

        AssertArray(row.Counts, 1L, 2L, 1L);
    }

    [Fact]
    public async Task HashFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Md5 = SqlFunctions.ClickHouse.md5("abc"),
                Sha1 = SqlFunctions.ClickHouse.sha1("abc"),
                Sha256 = SqlFunctions.ClickHouse.sha256("abc"),
                Sha512 = SqlFunctions.ClickHouse.sha512("abc"),
                Xx32 = SqlFunctions.ClickHouse.xx_hash32("abc"),
                Xx64 = SqlFunctions.ClickHouse.xx_hash64("abc"),
                Xxh3 = SqlFunctions.ClickHouse.xxh3("abc"),
                City = SqlFunctions.ClickHouse.city_hash64("abc"),
                Sip64 = SqlFunctions.ClickHouse.sip_hash64("abc"),
                Sip128 = SqlFunctions.ClickHouse.sip_hash128("abc"),
                Murmur232 = SqlFunctions.ClickHouse.murmur_hash2_32("abc"),
                Murmur264 = SqlFunctions.ClickHouse.murmur_hash2_64("abc"),
                Murmur332 = SqlFunctions.ClickHouse.murmur_hash3_32("abc"),
                Murmur364 = SqlFunctions.ClickHouse.murmur_hash3_64("abc"),
                Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128("abc")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.HashFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        // T254: the six string-returning digests are lowercase hexadecimal, never the raw FixedString
        // bytes: the exact length per function and the lowercase alphabet are the contract.
        r.Md5.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]+$");
        r.Sha1.Should().HaveLength(40).And.MatchRegex("^[0-9a-f]+$");
        r.Sha256.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
        r.Sha512.Should().HaveLength(128).And.MatchRegex("^[0-9a-f]+$");
        r.Xx32.Should().NotBe(0);
        r.Xx64.Should().NotBe(0);
        r.Xxh3.Should().NotBe(0);
        r.City.Should().NotBe(0);
        r.Sip64.Should().NotBe(0);
        r.Sip128.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]+$");
        r.Murmur232.Should().NotBe(0);
        r.Murmur264.Should().NotBe(0);
        r.Murmur332.Should().NotBe(0);
        r.Murmur364.Should().NotBe(0);
        r.Murmur3128.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public async Task UlidAndLengthFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ulid = SqlFunctions.ClickHouse.generate_ulid(),
                Degrees = SqlFunctions.Sql.degrees(3.141592653589793),
                Radians = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi()
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseFunctionExecutionTests.UlidAndLengthFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Ulid.Should().HaveLength(26);
        r.Degrees.Should().BeApproximately(180.0, 1e-9);
        r.Radians.Should().BeApproximately(Math.PI, 1e-9);
        r.Pi.Should().BeApproximately(Math.PI, 1e-9);
    }

    /// <summary>
    /// T254 matrix metadata: the six string-returning binary digests, their hexadecimal length and
    /// the expected value source. The crypto rows are compared with independent .NET hashes over the
    /// UTF-8 bytes; the two 128-bit non-crypto rows with a raw server <c>hex(...)</c> oracle.
    /// </summary>
    private static readonly (string Name, int Length)[] StringDigests =
    [
        ("md5", 32),
        ("sha1", 40),
        ("sha256", 64),
        ("sha512", 128),
        ("sip_hash128", 32),
        ("murmur_hash3_128", 32)
    ];

    /// <summary>The familiar <c>"abc"</c> vectors pinned by the plan, per function.</summary>
    private static readonly Dictionary<string, string> PinnedAbcDigests = new(StringComparer.Ordinal)
    {
        ["md5"] = "900150983cd24fb0d6963f7d28e17f72",
        ["sha1"] = "a9993e364706816aba3e25717850c26c9cd0d89d",
        ["sha256"] = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
        ["sha512"] =
            "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a"
            + "2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f"
    };

    /// <summary>
    /// T254 regression: all six digests materialise as lowercase hexadecimal through an anonymous
    /// LINQ projection. Against the original bare-function renderer the four crypto cases fail the
    /// value/length contract (raw <c>FixedString</c> decoded as UTF-8), which is the red state; the
    /// null case comes from the live <c>Nullable(String)</c> column of <c>complex_entity</c> row 3,
    /// because the optional-argument translator drops a trailing literal <c>null</c>.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("Привет")]
    public void T254_Digests_ShouldMaterializeLowercaseHex_Anonymous(string? input)
    {
        if (input is null)
        {
            var nullRow = _sut.ComplexEntity
                .Where(x => x.Id == 3)
                .Select(x => new
                {
                    Md5 = SqlFunctions.ClickHouse.md5(x.String),
                    Sha1 = SqlFunctions.ClickHouse.sha1(x.String),
                    Sha256 = SqlFunctions.ClickHouse.sha256(x.String),
                    Sha512 = SqlFunctions.ClickHouse.sha512(x.String),
                    Sip128 = SqlFunctions.ClickHouse.sip_hash128(x.String),
                    Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128(x.String)
                })
                .First();

            AssertDigests(input, nullRow.Md5, nullRow.Sha1, nullRow.Sha256, nullRow.Sha512,
                nullRow.Sip128, nullRow.Murmur3128);
            return;
        }

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Md5 = SqlFunctions.ClickHouse.md5(input),
                Sha1 = SqlFunctions.ClickHouse.sha1(input),
                Sha256 = SqlFunctions.ClickHouse.sha256(input),
                Sha512 = SqlFunctions.ClickHouse.sha512(input),
                Sip128 = SqlFunctions.ClickHouse.sip_hash128(input),
                Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128(input)
            })
            .First();

        AssertDigests(input, row.Md5, row.Sha1, row.Sha256, row.Sha512, row.Sip128, row.Murmur3128);
    }

    /// <summary>T254: the same matrix through a typed (member-init) projection with nullable members.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("Привет")]
    public void T254_Digests_ShouldMaterializeLowercaseHex_Typed(string? input)
    {
        if (input is null)
        {
            var nullRow = _sut.ComplexEntity
                .Where(x => x.Id == 3)
                .Select(x => new DigestProjection
                {
                    Md5 = SqlFunctions.ClickHouse.md5(x.String),
                    Sha1 = SqlFunctions.ClickHouse.sha1(x.String),
                    Sha256 = SqlFunctions.ClickHouse.sha256(x.String),
                    Sha512 = SqlFunctions.ClickHouse.sha512(x.String),
                    Sip128 = SqlFunctions.ClickHouse.sip_hash128(x.String),
                    Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128(x.String)
                })
                .First();

            AssertDigests(input, nullRow.Md5, nullRow.Sha1, nullRow.Sha256, nullRow.Sha512,
                nullRow.Sip128, nullRow.Murmur3128);
            return;
        }

        var row = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new DigestProjection
            {
                Md5 = SqlFunctions.ClickHouse.md5(input),
                Sha1 = SqlFunctions.ClickHouse.sha1(input),
                Sha256 = SqlFunctions.ClickHouse.sha256(input),
                Sha512 = SqlFunctions.ClickHouse.sha512(input),
                Sip128 = SqlFunctions.ClickHouse.sip_hash128(input),
                Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128(input)
            })
            .First();

        AssertDigests(input, row.Md5, row.Sha1, row.Sha256, row.Sha512, row.Sip128, row.Murmur3128);
    }

    /// <summary>
    /// T254: the same matrix through the repository's test-local <c>Scalar&lt;string&gt;</c> terminal
    /// (<c>ExecuteRaw(select {renderer sql} as v).Read&lt;string&gt;()</c>). The null case uses a
    /// typed <c>Nullable(String)</c> cast rather than a literal <c>null</c>, so the argument reaches
    /// the renderer-generated wrapper unchanged.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("Привет")]
    public void T254_Digests_ShouldMaterializeLowercaseHex_Scalar(string? input)
    {
        using var context = Provider.CreateContext();

        T Scalar<T>(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<T>().Single();
        }

        var argument = input is null ? "cast(null as Nullable(String))" : SqlStringLiteral(input);

        foreach (var (name, _) in StringDigests)
            AssertDigest(name, Scalar<string>(Render(name, argument)), input);
    }

    /// <summary>Typed projection mirror of the six digest members (T254, E06).</summary>
    public sealed class DigestProjection
    {
        public string? Md5 { get; set; }

        public string? Sha1 { get; set; }

        public string? Sha256 { get; set; }

        public string? Sha512 { get; set; }

        public string? Sip128 { get; set; }

        public string? Murmur3128 { get; set; }
    }

    private void AssertDigests(
        string? input, string? md5, string? sha1, string? sha256, string? sha512, string? sip128, string? murmur3128)
    {
        AssertDigest("md5", md5, input);
        AssertDigest("sha1", sha1, input);
        AssertDigest("sha256", sha256, input);
        AssertDigest("sha512", sha512, input);
        AssertDigest("sip_hash128", sip128, input);
        AssertDigest("murmur_hash3_128", murmur3128, input);
    }

    private void AssertDigest(string name, string? actual, string? input)
    {
        if (input is null)
        {
            actual.Should().BeNull("a NULL input propagates as NULL, never as an empty string");
            actual.Should().NotBe(string.Empty, "NULL and the empty string are distinct inputs");
            return;
        }

        var expected = name is "sip_hash128" or "murmur_hash3_128"
            ? RawHexOracle(name, input)
            : CryptoDigest(name, input);
        var length = StringDigests.Single(d => d.Name == name).Length;

        actual.Should().Be(expected,
            "'{0}' of the UTF-8 bytes of \"{1}\" must be the lowercase digest", name, input);
        actual.Should().NotBeNull();
        actual!.Should().HaveLength(length, "'{0}' is {1} hexadecimal characters", name, length);
        actual.Should().MatchRegex("^[0-9a-f]+$", "'{0}' must stay lowercase hexadecimal", name);

        if (input == "abc" && PinnedAbcDigests.TryGetValue(name, out var pinned))
        {
            expected.Should().Be(pinned, "the pinned .NET vector for '{0}'('abc') is stable", name);
            actual.Should().Be(pinned, "the pinned '{0}' vector of 'abc'", name);
        }
    }

    /// <summary>
    /// Docs conformance for the T257 guide snippets: the four SQL fragments printed in
    /// <c>docs/guide/provider-specific-sql/clickhouse.md</c> are executed verbatim through raw ADO.NET
    /// (independent of the translator), so the documented <c>_t</c> identifiers and cast types are
    /// pinned to the values the pinned 25.8 server actually returns.
    /// </summary>
    [Fact]
    public async Task T257_DocSnippets_ShouldReturnTheDocumentedTypesAndValues()
    {
        await using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        async Task<(string Type, string Value)> ProbeAsync(string expression)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT toTypeName({expression}), toString({expression})";
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            (await reader.ReadAsync(TestContext.Current.CancellationToken))
                .Should().BeTrue("the probe always returns one row");
            return (reader.GetString(0), reader.GetString(1));
        }

        var filter = await ProbeAsync(
            "cast(arrayFilter(_t -> (_t.2 > 0), " +
            "arrayZip(mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))) " +
            "as Array(Tuple(String, Int64)))");
        filter.Should().Be(("Array(Tuple(String, Int64))", "[('a',1)]"));

        var sort = await ProbeAsync(
            "cast(arraySort(_t -> _t.1, " +
            "arrayZip(mapKeys(map('b', 2, 'a', 1)), mapValues(map('b', 2, 'a', 1)))) " +
            "as Array(Tuple(String, Int64)))");
        sort.Should().Be(("Array(Tuple(String, Int64))", "[('a',1),('b',2)]"));

        var applyValue = await ProbeAsync(
            "cast(arrayMap((k, v) -> (v * 2), " +
            "mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1))) as Array(Int64))");
        applyValue.Should().Be(("Array(Int64)", "[2,-2]"));

        var applyReference = await ProbeAsync(
            "cast(arrayMap((k, v) -> k, " +
            "mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1))) as Array(Nullable(String)))");
        applyReference.Should().Be(("Array(Nullable(String))", "['a','b']"));
    }

    /// <summary>An independent .NET digest over the UTF-8 bytes, rendered as lowercase hex.</summary>
    private static string CryptoDigest(string name, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return name switch
        {
            "md5" => Convert.ToHexStringLower(MD5.HashData(bytes)),
            "sha1" => Convert.ToHexStringLower(SHA1.HashData(bytes)),
            "sha256" => Convert.ToHexStringLower(SHA256.HashData(bytes)),
            "sha512" => Convert.ToHexStringLower(SHA512.HashData(bytes)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not a .NET crypto digest")
        };
    }

    /// <summary>
    /// Independent raw-SQL oracle for the two 128-bit non-crypto digests: the server-side
    /// <c>hex(...)</c> result is safe ASCII and is lowercased here. It never routes through the
    /// renderer under test.
    /// </summary>
    private string? RawHexOracle(string name, string input)
    {
        var native = name switch
        {
            "sip_hash128" => "sipHash128",
            "murmur_hash3_128" => "murmurHash3_128",
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not a raw-hex oracle candidate")
        };

        using var context = Provider.CreateContext();
        using var result = context.ExecuteRaw($"select hex({native}({SqlStringLiteral(input)})) as v");
        return result.Read<string>().Single()?.ToLowerInvariant();
    }

    private static string SqlStringLiteral(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string Render(string name, string argument)
        => ClickHouseDialect.Instance.ScalarFunctions!.Render(name, [argument]);

    /// <summary>
    /// #252: <c>array_except</c> is declared for API compatibility, but the native <c>arrayExcept</c>
    /// exists only from ClickHouse 25.9. An undeclared context keeps the pinned-baseline capability
    /// table, so the call must be refused at translation/preparation time on every server — the gate is
    /// the declaration, not the observed server version — instead of reaching the server as an
    /// <c>UNKNOWN_FUNCTION</c>.
    /// </summary>
    [Fact]
    public void ArrayExcept_ShouldRefuseBeforeTouchingTheServer()
    {
        var act = () => _sut.ArrayEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                E = SqlFunctions.ClickHouse.array_except(x.Nums, x.Nums)
            })
            .First();

        act.Should().Throw<NotSupportedException>(
                "an undeclared context keeps the pinned-baseline surface, so the capability gate must refuse early")
            .WithMessage("The array_except function is not supported by this provider.");
    }

    /// <summary>
    /// D0 live server characterization for #252, truthful on both supported servers. Below 25.9 (the
    /// pinned <c>clickhouse/clickhouse-server:25.8-alpine</c>, 25.8.33.6) the server keeps no
    /// <c>arrayExcept</c> entry in <c>system.functions</c> and rejects the raw call with Code 46
    /// (UNKNOWN_FUNCTION). From 25.9 (the external probe server, 25.9.7.56) the function is present and
    /// the raw call returns the documented rows. Raw ADO.NET keeps this oracle independent of the
    /// nextorm translator.
    /// </summary>
    [Fact]
    public async Task ServerCharacterization_ArrayExceptFollowsTheServerVersion()
    {
        await using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        static async Task<object?> ScalarAsync(ClickHouseConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        var version = Convert.ToString(
            await ScalarAsync(connection, "SELECT version()"), CultureInfo.InvariantCulture)!;

        var functions = Convert.ToInt64(
            await ScalarAsync(
                connection,
                "SELECT count() FROM system.functions WHERE name = 'arrayExcept'"),
            CultureInfo.InvariantCulture);

        if (Version.Parse(version.Split(' ')[0]) < new Version(25, 9))
        {
            version.Should().Be("25.8.33.6", "the collection pins clickhouse/clickhouse-server:25.8-alpine");
            functions.Should().Be(
                0, "arrayExcept was introduced in server 25.9 and the pinned server is 25.8");
            Func<Task> act = async () => await ScalarAsync(connection, "SELECT arrayExcept([1], [1])");
            var thrown = await act.Should().ThrowAsync<ClickHouseServerException>(
                "the pinned 25.8 server has no arrayExcept function");
            thrown.Which.Message.Should().Contain("Code: 46")
                .And.Contain("arrayExcept", "the server reports the unknown function by name");
            return;
        }

        version.Should().StartWith("25.9", "the external probe server runs ClickHouse 25.9");
        functions.Should().Be(1, "arrayExcept exists from server 25.9");
        (await ScalarAsync(connection, "SELECT toString(arrayExcept([1, 2, 3], [2]))"))
            .Should().Be("[1,3]", "[1,2,3] except [2] is [1,3]");
        (await ScalarAsync(connection, "SELECT toString(arrayExcept([1, 2, 3, 2], [2]))"))
            .Should().Be("[1,3]", "every occurrence of a subtracted element is removed");
    }

    /// <summary>The captured dictionary member used by the T257 operand-source matrix.</summary>
    private static readonly Dictionary<string, int> MemberMap = new(StringComparer.Ordinal)
    {
        ["b"] = 2,
        ["a"] = 1,
        ["c"] = -1
    };

    /// <summary>
    /// T257: asserts the tuple-array shape (the documented <c>map_filter</c> break) and the exact
    /// entry sequence. The subject is boxed on purpose so the same assertion compiles against the
    /// pre-change <c>IDictionary</c> contract and the post-change tuple contract, while still
    /// failing the type check on the pre-change baseline.
    /// </summary>
    private static void AssertMapEntries<TKey, TValue>(
        object? actual, params (TKey Key, TValue Value)[] expected)
    {
        var entries = actual.Should().BeAssignableTo<Tuple<TKey, TValue>[]>(
            "T257: the map helpers surface Tuple<TKey,TValue>[]").Which;
        entries.Select(entry => (entry.Item1, entry.Item2)).Should().Equal(expected);
    }

    /// <summary>
    /// T257-TYPE-01: asserts the exact CLR array element type the declaration promises and the element
    /// sequence. Correct values in a widened/narrowed array (for example <c>long[]</c> where
    /// <c>int[]</c> is declared) fail the assignability check before the values are compared.
    /// </summary>
    private static void AssertArray<TElement>(object? actual, params TElement[] expected)
    {
        var elements = actual.Should().BeAssignableTo<TElement[]>(
            $"T257-TYPE-01: the declared element type is {typeof(TElement)}").Which;
        elements.Should().Equal(expected);
    }

    /// <summary>
    /// T257-TYPE-01: asserts an empty result is an array of the declared element type with zero
    /// length — an empty enumerable of another type is not accepted.
    /// </summary>
    private static void AssertEmptyArray<TElement>(object? actual)
    {
        var elements = actual.Should().BeAssignableTo<TElement[]>(
            $"T257-TYPE-01: the declared element type is {typeof(TElement)}").Which;
        elements.Should().HaveCount(0);
    }

}
