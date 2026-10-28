using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.ClickHouse;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

public class InMemoryTests
{
    private readonly InMemoryRepository _sut;
    private readonly ILogger<InMemoryTests> _logger;

    public InMemoryTests(InMemoryRepository sut, ILogger<InMemoryTests> logger)
    {
        _sut = sut;
        _logger = logger;

        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public void TestDistinct()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
        });

        var r = _sut.SimpleEntity.Select(it => new { it.Id }).Distinct().ToList();

        r.Should().HaveCount(2);
        r.Should().Contain(x => x.Id == 1);
        r.Should().Contain(x => x.Id == 2);
    }
    [Fact]
    public void TestQueryTag_ShouldNotChangeResult()
    {
        var r = _sut.SimpleEntity.WithTag("app.list").Select(it => new { it.Id }).ToList();

        r.Should().HaveCount(2);
    }
    [Fact]
    public void TestQueryTag_CommandLevel_ShouldNotChangeResult()
    {
        var r = _sut.SimpleEntity.Select(it => new { it.Id }).WithTag("app.list").ToList();

        r.Should().HaveCount(2);
    }
    [Fact]
    public void TestDictionaryLookup()
    {
        // The in-memory provider compiles the predicate, so a captured collection indexed by a column
        // runs as ordinary C# and needs no SQL translation.
        var lookup = new Dictionary<int, string> { [1] = "one", [2] = "two" };

        var r = _sut.SimpleEntity.Where(it => lookup[it.Id] == "one").Select(it => new { it.Id }).ToList();

        r.Should().HaveCount(1);
        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void TestReadOnlyListLookup()
    {
        IReadOnlyList<string> lookup = ["zero", "one", "two"];

        var r = _sut.SimpleEntity.Where(it => lookup[it.Id] == "one").Select(it => new { it.Id }).ToList();

        r.Should().HaveCount(1);
        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void TestReadOnlyDictionaryLookup()
    {
        IReadOnlyDictionary<int, string> lookup = new Dictionary<int, string> { [1] = "one", [2] = "two" };

        var r = _sut.SimpleEntity.Where(it => lookup[it.Id] == "two").Select(it => new { it.Id }).ToList();

        r.Should().HaveCount(1);
        r[0].Id.Should().Be(2);
    }

    [Fact]
    public void TestDistinct_ReferenceTypeWithoutValueEquality_ShouldThrow()
    {
        // SimpleEntity is a plain reference type without an Equals/GetHashCode override, so the
        // in-memory provider cannot reproduce SQL's value-based DISTINCT and must reject it.
        var act = () => _sut.SimpleEntity.Distinct().ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT*");
    }
    [Fact]
    public void TestDistinctOn_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.DistinctOn(it => it.Id).Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT ON*");
    }
    [Fact]
    public void TestTableSample_ShouldThrow()
    {
        var act = () => _sut.DataProvider.From<SimpleEntity>(o => o.TableSample(10)).Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*TABLESAMPLE*");
    }
    [Fact]
    public void TestForJson_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.Select(it => new { it.Id }).ForJson();

        act.Should().Throw<NotSupportedException>().WithMessage("*relational*");
    }
    [Fact]
    public void TestForXml_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.Select(it => new { it.Id }).ForXml();

        act.Should().Throw<NotSupportedException>().WithMessage("*relational*");
    }
    [Fact]
    public async Task TestForJsonAsync_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.Select(it => new { it.Id }).ForJsonAsync(cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*relational*");
    }
    [Fact]
    public void WriteJson_InMemory_ShouldThrowNotSupported()
    {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;
        var length = stream.Length;

        var act = () => _sut.SimpleEntity.Select(it => new { it.Id }).WriteJson(stream);

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(length);
    }
    [Fact]
    public async Task WriteJsonAsync_InMemory_ShouldThrowNotSupported()
    {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;
        var length = stream.Length;

        var act = () => _sut.SimpleEntity.Select(it => new { it.Id }).WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(length);
    }

    // D178 T12: the new enum JSON support must not open an in-memory path; both terminal surfaces stay
    // fail-closed before touching the destination (the in-memory provider has no DbDataReader).
    [Fact]
    public void WriteJson_InMemoryEnum_ShouldStayFailClosed()
    {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => _sut.SimpleEntity
            .Select(it => new { it.Id, State = InMemoryJsonEnum.Active })
            .WriteJson(stream);

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        stream.Position.Should().Be(position);
    }

    [Fact]
    public async Task WriteJsonAsync_InMemoryEnum_ShouldStayFailClosed()
    {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => _sut.SimpleEntity
            .Select(it => new { it.Id, State = InMemoryJsonEnum.Active })
            .WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        stream.Position.Should().Be(position);
    }

    [Fact]
    public async Task WriteJson_InMemoryEnumEntityBuilder_ShouldStayFailClosed()
    {
        var builder = _sut.DataProvider.From<InMemoryEnumEntity>();
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var sync = () => builder.WriteJson(stream);
        var async = () => builder.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        sync.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        await async.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [in-memory]*");
        stream.Position.Should().Be(position);
    }
    [Fact]
    public void TestPivot_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity
            .Pivot(PivotAggregate.Count, it => it.Id, it => it.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT/UNPIVOT*");
    }
    [Fact]
    public void TestWithTies_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.Limit(2).WithTies().Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH TIES*");
    }
    [Fact]
    public void TestForUpdate_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.ForUpdate().Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }
    [Fact]
    public void TestForUpdate_SkipLocked_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.ForUpdate(LockWaitMode.SkipLocked).Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }
    // scenario label: reject/inmemory (V-12): a source-only lock is rejected too (RowLock is null).
    [Fact]
    [Trait("PDCA", "T216")]
    public void TestSourceLock_ShouldThrow()
    {
        var act = () => _sut.DataProvider.From<SimpleEntity>(o => o.ForUpdate()).Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }
    [Fact]
    public void TestForSystemTime_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.ForSystemTime(TemporalClause.All()).Select(it => new { it.Id }).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");
    }
    [Fact]
    public async Task TestWhere()
    {
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
    }
    [Fact]
    public async Task TestWhere_Subquery()
    {
        var subQuery = _sut.SimpleEntity.Where(it => it.Id >= 1).Select(it => new { it.Id });
        var r = await _sut.DataProvider.From(subQuery).Where(it => it.Id == 2).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(2);
    }
    [Fact]
    public async Task TestWhereOnEmpty()
    {
        _sut.SimpleEntity.WithData(null);
        var r = await _sut.SimpleEntity.Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().BeNull();
    }
    [Fact]
    public async Task TestAsync()
    {
        _sut.SimpleEntity.WithAsyncData(GetData());
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);

        static async IAsyncEnumerable<SimpleEntity> GetData()
        {
            yield return new SimpleEntity { Id = 1 };
            await Task.Delay(0);
            yield return new SimpleEntity { Id = 2 };
        }
    }
    [Fact]
    public async Task TestNarrowingCastToQueryCommand()
    {
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
    }
    [Fact]
    public async Task TestTuples()
    {
        // Given
        var query = _sut.SimpleEntity.Select(it => new Tuple<int, int>(it.Id, it.Id + 10));
        //_sut.DataProvider.Compile(query, false, true, CancellationToken.None);
        // When
        await foreach (var item in query.ToAsyncEnumerable())
        {
            _logger.LogInformation("Value is {id}", item.Item2);
        }
        // Then
    }
    [Fact]
    public async Task TestFetch()
    {
        SimpleEntity[] data = new SimpleEntity[100];
        for (var i = 0; i < data.Length; i++)
            data[i] = new SimpleEntity { Id = i };

        var ids = new List<int>();
        await foreach (var row in _sut.SimpleEntity
            .WithData(data)
            .Select(it => new { it.Id }).Pipeline())
        {
            ids.Add(row.Id);
        }

        ids.Should().HaveCount(100);
        ids.Should().BeEquivalentTo(Enumerable.Range(0, 100));
    }
    [Fact]
    public async Task TestFetch_PipelineStopsOnCancellation()
    {
        SimpleEntity[] data = new SimpleEntity[100];
        for (var i = 0; i < data.Length; i++)
            data[i] = new SimpleEntity { Id = i };

        using var cts = new CancellationTokenSource();
        var seen = 0;

        await foreach (var row in _sut.SimpleEntity
            .WithData(data)
            .Select(it => new { it.Id }).Pipeline(cts.Token))
        {
            seen++;
            if (seen == 10) cts.Cancel();
        }

        // The enumerator observes the token before every row, so iteration must stop at the
        // row after which cancellation was requested instead of draining all 100 rows.
        seen.Should().Be(10);
    }
    // [Fact]
    // public void TestQueryCache()
    // {
    //     var id = 2;

    //     var q1 = _sut.SimpleEntity.Where(it => it.Id == id).Select(it => new { it.Id });
    //     q1.PrepareCommand(CancellationToken.None);
    //     var q2 = _sut.SimpleEntity.Where(it2 => it2.Id == id).Select(it3 => new { it3.Id });
    //     q2.PrepareCommand(CancellationToken.None);

    //     var hashCode = q1.GetHashCode();
    //     hashCode.Should().Be(q2.GetHashCode());
    //     q1.Should().Be(q2);

    //     id = 3;

    //     var q3 = _sut.SimpleEntity.Where(it => it.Id == id).Select(it => new { it.Id });
    //     q3.PrepareCommand(CancellationToken.None);

    //     hashCode.Should().NotBe(q3.GetHashCode());
    // }
    [Fact]
    public void TestQueryPlanCache()
    {
        var (id1, id2) = (2, 3);

        var q1 = _sut.SimpleEntity.Where(it => it.Id == id1).Select(it => new { it.Id });
        q1.PrepareCommand(CancellationToken.None);
        var q2 = _sut.SimpleEntity.Where(it2 => it2.Id == id2).Select(it3 => new { it3.Id });
        q2.PrepareCommand(CancellationToken.None);

        // var hashCode = q1.GetHashCode();
        // hashCode.Should().NotBe(q2.GetHashCode());

        var planEC = new QueryPlanEqualityComparer(q1);

        planEC.Equals(q1, q2).Should().BeFalse();
    }
    [Fact]
    public void QueryPlan_HashMustStayStableAcrossGetCacheVersion()
    {
        // Given
        var q = _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id });
        q.PrepareCommand(CancellationToken.None);

        var plan = new QueryPlan(q, null);
        var hashBefore = plan.GetHashCode();

        // When: GetCacheVersion() replaces QueryCommand/_comparer with an equal clone
        var cached = plan.GetCacheVersion();

        // Then: identity (and therefore the hash) must not change, because the plan is a
        // dictionary key and a key's hash must never change once it has been inserted.
        cached.Should().BeSameAs(plan);
        plan.GetHashCode().Should().Be(hashBefore);

        // A freshly built, logically equal plan must still find it.
        var lookup = new QueryPlan(q, null);
        lookup.Equals(plan).Should().BeTrue();
        lookup.GetHashCode().Should().Be(plan.GetHashCode());

        var cache = new Dictionary<QueryPlan, object> { [cached] = 42 };
        cache.TryGetValue(lookup, out var found).Should().BeTrue();
        found.Should().Be(42);
    }
    [Fact]
    public void SelectPrimitive_ShouldReturnData()
    {
        const int limit = 5;
        // Given
        var q1 = _sut.SimpleEntity.Where(it => it.Id < limit).Select(it => it.Id);
        // When
        var r = q1.ToList();
        // Then
        for (var i = 0; i < r.Count; i++)
        {
            r[i].Should().Be(i + 1);
        }
    }
    [Fact]
    public void SelectAny_ShouldReturnData()
    {
        var cb = new EntityBuilder(_sut.DataProvider);
        var cmd = (QueryCommand)_sut.SimpleEntity;
        // Given
        var l = cb.Select(_ => SqlFunctions.Sql.exists(cmd)).ToList();
        // When
        l.Count.Should().Be(1);
        l[0].Should().BeTrue();
    }
    [Fact]
    public void SelectAny2_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Any();

        r.Should().BeTrue();
    }
    [Fact]
    public void Top_ShouldLimitData()
    {
        // Given
        var r = _sut.SimpleEntity.Limit(1).ToList();
        // When
        r.Count.Should().Be(1);
        r[0].Id.Should().Be(1);
    }
    [Fact]
    public void Top_ShouldLimitOffsetData()
    {
        // Given
        var r = _sut.SimpleEntity.Offset(1).ToList();
        // When
        r.Count.Should().Be(1);
        r[0].Id.Should().Be(2);
    }
    [Fact]
    public void First_ShouldReturnFirst()
    {
        // Given
        var r = _sut.SimpleEntity.First();

        // When
        r.Should().NotBeNull();
        r.Id.Should().Be(1);
        // Then
    }
    [Fact]
    public void FirstOffset_ShouldReturnFirst()
    {
        // Given
        var r = _sut.SimpleEntity.Offset(1).First();

        // When
        r.Should().NotBeNull();
        r.Id.Should().Be(2);
        // Then
    }
    [Fact]
    public void FirstOffsetEmpty_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Offset(10).First();
        };

        test.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void FirstOrDefault_ShouldReturnFirst()
    {
        var r = _sut.SimpleEntity.Offset(10).FirstOrDefault();

        r.Should().BeNull();
    }
    [Fact]
    public void Single_ShouldReturnSingle()
    {
        // Given
        var r = _sut.SimpleEntity.Offset(1).Single();

        // When
        r.Should().NotBeNull();
        r.Id.Should().Be(2);
        // Then
    }
    [Fact]
    public void SingleEmpty_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Offset(10).Single();
        };

        test.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void SingleMany_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Single();
        };

        test.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void SingleOrDefault_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Offset(10).SingleOrDefault();

        r.Should().BeNull();
    }
    [Fact]
    public void SingleOrDefaultMany_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.SingleOrDefault();
        };

        test.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void OrderBy_ShouldSortData()
    {
        // Given
        var r = _sut.SimpleEntity.OrderByDescending(it => it.Id).First();
        // When
        r.Id.Should().Be(2);
        // Then
    }
    [Fact]
    public async Task OrderByOverAsyncSource_ShouldSortData()
    {
        _sut.SimpleEntity.WithAsyncData(GetData());

        var r = await _sut.SimpleEntity.OrderByDescending(it => it.Id).Select(it => it.Id).ToArrayAsync();

        r.Should().BeEquivalentTo([2, 1]);

        static async IAsyncEnumerable<SimpleEntity> GetData()
        {
            yield return new SimpleEntity { Id = 1 };
            await Task.Delay(0);
            yield return new SimpleEntity { Id = 2 };
        }
    }
    [Fact]
    public void OrderBy2_ShouldSortData()
    {
        // Given
        var r = _sut.SimpleEntity.OrderBy(_ => 1).OrderByDescending(it => it.Id).First();
        // When
        r.Id.Should().Be(2);
        // Then
    }
    [Fact]
    public void Last_ShouldReturnLastOrderedRow()
    {
        var r = _sut.SimpleEntity.OrderBy(it => it.Id).Last();

        r.Id.Should().Be(2);
    }
    [Fact]
    public void LastOrDefault_ShouldReturnLastOrderedRow()
    {
        var r = _sut.SimpleEntity.OrderByDescending(it => it.Id).LastOrDefault();

        r!.Id.Should().Be(1);
    }
    [Fact]
    public async Task LastAsync_ShouldReturnLastOrderedRow()
    {
        var r = await _sut.SimpleEntity.OrderBy(it => it.Id).LastAsync();

        r.Id.Should().Be(2);
    }
    [Fact]
    public void Last_WithoutOrderBy_ShouldThrow()
    {
        var act = () => _sut.SimpleEntity.Last();

        act.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void ToArray_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Select(it => new { it.Id }).ToArray();

        r.Should().HaveCount(2);
        r.Select(x => x.Id).Should().BeEquivalentTo([1, 2]);
    }
    [Fact]
    public async Task ToArrayAsync_ShouldReturnData()
    {
        var r = await _sut.SimpleEntity.Select(it => new { it.Id }).ToArrayAsync();

        r.Should().HaveCount(2);
        r.Select(x => x.Id).Should().BeEquivalentTo([1, 2]);
    }
    [Fact]
    public void ToHashSet_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Select(it => it.Id).ToHashSet();

        r.Should().BeEquivalentTo([1, 2]);
    }
    [Fact]
    public async Task ToHashSetAsync_ShouldReturnData()
    {
        var r = await _sut.SimpleEntity.Select(it => it.Id).ToHashSetAsync();

        r.Should().BeEquivalentTo([1, 2]);
    }
    [Fact]
    public void ToDictionary_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Select(it => new { it.Id }).ToDictionary(x => x.Id);

        r.Should().HaveCount(2);
        r[1].Id.Should().Be(1);
        r[2].Id.Should().Be(2);
    }
    [Fact]
    public async Task ToDictionaryAsync_ShouldReturnData()
    {
        var r = await _sut.SimpleEntity.Select(it => new { it.Id }).ToDictionaryAsync(x => x.Id);

        r.Should().HaveCount(2);
        r[1].Id.Should().Be(1);
        r[2].Id.Should().Be(2);
    }
    [Fact]
    public void Contains_ShouldFilterData()
    {
        int[] ids = [2, 3];

        var r = _sut.SimpleEntity.Where(it => ids.Contains(it.Id)).Select(it => it.Id).ToArray();

        r.Should().BeEquivalentTo([2]);
    }
    [Fact]
    public void Count_ShouldReturnRowCount()
    {
        _sut.SimpleEntity.Count().Should().Be(2);
    }
    [Fact]
    public async Task CountAsync_ShouldReturnRowCount()
    {
        (await _sut.SimpleEntity.CountAsync()).Should().Be(2);
    }
    [Fact]
    public void Count_WithWhere_ShouldReturnFilteredCount()
    {
        _sut.SimpleEntity.Where(it => it.Id > 1).Count().Should().Be(1);
    }
    [Fact]
    public void Sum_ShouldReturnSum()
    {
        _sut.SimpleEntity.Sum(it => it.Id).Should().Be(3);
    }
    [Fact]
    public void MinMax_ShouldReturnBounds()
    {
        _sut.SimpleEntity.Min(it => it.Id).Should().Be(1);
        _sut.SimpleEntity.Max(it => it.Id).Should().Be(2);
    }
    [Fact]
    public void Avg_ShouldReturnAverage()
    {
        _sut.SimpleEntity.Avg(it => (double)it.Id).Should().Be(1.5);
    }
    [Fact]
    public void GroupBy_ShouldAggregatePerGroup()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .GroupBy(e => new { Parity = e.Id % 2 })
            .Select(e => new { Parity = e.Id % 2, count = SqlFunctions.Sql.count() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().Contain(x => x.Parity == 1 && x.count == 2);
        r.Should().Contain(x => x.Parity == 0 && x.count == 1);
    }
    [Fact]
    public void GroupBy_ScalarKey_ShouldAggregatePerGroup()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .GroupBy(e => e.Id % 2)
            .Select(e => new { Parity = e.Id % 2, count = SqlFunctions.Sql.count() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().Contain(x => x.Parity == 1 && x.count == 2);
        r.Should().Contain(x => x.Parity == 0 && x.count == 1);
    }
    [Fact]
    public void GroupByRollup_ShouldThrow()
    {
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });

        var act = () => _sut.SimpleEntity
            .GroupByRollup(e => new { e.Id })
            .Select(e => new { e.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*ROLLUP/CUBE*");
    }
    [Fact]
    public void GroupByWithTotals_ShouldThrow()
    {
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });

        var act = () => _sut.SimpleEntity
            .GroupBy(e => new { e.Id })
            .WithTotals()
            .Select(e => new { e.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH TOTALS*");
    }
    [Fact]
    public void Sample_WithNonFiniteRatio_ShouldThrow()
    {
        ((Action)(() => _sut.DataProvider.From<SimpleEntity>(o => o.Sample(double.NaN))))
            .Should().Throw<ArgumentOutOfRangeException>();

        ((Action)(() => _sut.DataProvider.From<SimpleEntity>(o => o.Sample(double.PositiveInfinity))))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
    [Fact]
    public void QueryModifiers_ShouldThrow()
    {
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });

        ((Action)(() => _sut.SimpleEntity.Final().Select(e => new { e.Id }).ToList()))
            .Should().Throw<NotSupportedException>().WithMessage("*FINAL/SAMPLE/SETTINGS*");

        ((Action)(() => _sut.SimpleEntity.PreWhere(e => e.Id > 0).Select(e => new { e.Id }).ToList()))
            .Should().Throw<NotSupportedException>().WithMessage("*PREWHERE*");
    }
    [Fact]
    public void LimitBy_ShouldThrow()
    {
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 } });

        var act = () => _sut.SimpleEntity
            .LimitBy(1, e => e.Id)
            .Select(e => new { e.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*LIMIT BY*");
    }
    [Fact]
    public void GroupBy_Having_ShouldFilterGroups()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .GroupBy(e => new { Parity = e.Id % 2 })
            .Having(e => SqlFunctions.Sql.count() > 1)
            .Select(e => new { Parity = e.Id % 2, count = SqlFunctions.Sql.count() })
            .ToList();

        r.Should().ContainSingle();
        r[0].Parity.Should().Be(1);
        r[0].count.Should().Be(2);
    }
    [Fact]
    public void GroupBy_WhereAndHaving_ShouldFilterBeforeGrouping()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .Where(e => e.Id != 2)
            .GroupBy(e => new { Parity = e.Id % 2 })
            .Having(e => SqlFunctions.Sql.count() > 1)
            .Select(e => new { Parity = e.Id % 2, count = SqlFunctions.Sql.count() })
            .ToList();

        r.Should().ContainSingle();
        r[0].Parity.Should().Be(1);
        r[0].count.Should().Be(2);
    }
    [Fact]
    public void GroupBy_Avg_ShouldAggregatePerGroup()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .GroupBy(e => new { Parity = e.Id % 2 })
            .Select(e => new { Parity = e.Id % 2, avg = SqlFunctions.Sql.avg(Convert.ToDouble(e.Id)) })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().Contain(x => x.Parity == 1 && x.avg == 2);
        r.Should().Contain(x => x.Parity == 0 && x.avg == 2);
    }
    [Fact]
    public void GroupBy_OrderByColumn_ShouldSortGroups()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 },
        });

        var r = _sut.SimpleEntity
            .GroupBy(e => new { Parity = e.Id % 2 })
            .Select(e => new { Parity = e.Id % 2, count = SqlFunctions.Sql.count() })
            .OrderBy(2, OrderDirection.Asc)
            .First();

        r.Parity.Should().Be(0);
        r.count.Should().Be(1);
    }
    [Fact]
    public void Aggregate_OnEmptySet_ShouldReturnDefault()
    {
        _sut.SimpleEntity.WithData(Array.Empty<SimpleEntity>());

        _sut.SimpleEntity.Count().Should().Be(0);
        _sut.SimpleEntity.Sum(it => it.Id).Should().Be(0);
        _sut.SimpleEntity.Min(it => it.Id).Should().Be(0);
    }
    [Fact]
    public void Union_ShouldRemoveDuplicates()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Union(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }
    [Fact]
    public void UnionAll_ShouldKeepDuplicates()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .UnionAll(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().HaveCount(13);
    }
    [Fact]
    public void Intersect_ShouldReturnOnlyCommonRows()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Intersect(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo([1, 2, 3]);
    }
    [Fact]
    public void Except_ShouldReturnOnlyLeftRows()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo(Enumerable.Range(4, 7));
    }
    [Fact]
    public void IntersectAll_ShouldReturnCommonRows()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .IntersectAll(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo([1, 2, 3]);
    }
    [Fact]
    public void ExceptAll_ShouldReturnOnlyLeftRows()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .ExceptAll(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo(Enumerable.Range(4, 7));
    }
    [Fact]
    public void SetOperations_WhenChained_ShouldApplyLeftToRight()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id))
            .Intersect(_sut.SimpleEntity.Select(it => it.Id));

        cmd.ToList().Should().BeEquivalentTo(Enumerable.Range(4, 7));
    }

    [Fact]
    public void SubquerySource_SyncToList_ShouldReturnRows()
    {
        Seed1To10();
        var sub = _sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id);

        _sut.DataProvider.From(sub).ToList().Should().BeEquivalentTo([1, 2, 3]);
    }
    [Fact]
    public void SubquerySource_Count_ShouldReturnRowCount()
    {
        Seed1To10();
        var sub = _sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id);

        _sut.DataProvider.From(sub).Count().Should().Be(3);
    }
    [Fact]
    public void SetOperation_AsSubquery_Count_ShouldReturnRowCount()
    {
        Seed1To10();
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.SimpleEntity.Where(it => it.Id <= 3).Select(it => it.Id));

        _sut.DataProvider.From(cmd).Count().Should().Be(7);
    }

    private void Seed1To10() =>
        _sut.SimpleEntity.WithData(Enumerable.Range(1, 10).Select(i => new SimpleEntity { Id = i }));

    private static class Tvf
    {
        public static TvfEntityBuilder<SimpleEntity> SimpleTvf() => new("simple_tvf");
    }

    [Fact]
    public void TableFunction_ShouldThrowClearNotSupported()
    {
        // The in-memory provider cannot evaluate a database table-valued function, so it must fail
        // loudly instead of silently returning an empty (or otherwise wrong) result set.
        var act = () => _sut.DataProvider
            .From(() => Tvf.SimpleTvf())
            .Select(it => new { it.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void CorrelatedApply_ShouldThrowClearNotSupported()
    {
        // The in-memory provider executes joins row-wise and has no correlated APPLY/LATERAL source
        // (only correlated subqueries in SELECT/WHERE/ORDER BY are supported), so it must fail loudly.
        var act = () => _sut.DataProvider
            .From<SimpleEntity>()
            .CrossApply(s => _sut.DataProvider.From<SimpleEntity>().Where(x => x.Id == s.Id))
            .Select(p => new { p.Item1.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void ColumnByName_ShouldThrowClearNotSupported()
    {
        var act = () => _sut.DataProvider
            .From<SimpleEntity>()
            .Select(it => new { R = SqlFunctions.Column<int>(it, "id") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void XmlNodesApply_ShouldThrowClearNotSupported()
    {
        var act = () => _sut.DataProvider
            .From<SimpleEntity>()
            .CrossApply(s => SqlFunctions.SqlServer.xml_nodes(s.Id.ToString(), "/root/item"))
            .Select(p => new { p.Item1.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }

    [Fact]
    public void FromSql_ShouldThrowClearNotSupported()
    {
        var act = () => _sut.DataProvider
            .FromSql("select id from simple_entity")
            .Select(t => new { Id = t["id"].AsInt })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*FromSql*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgresNumericScaleFunctions_ShouldThrow()
    {
        decimal? typedNull = null;

        var scale = () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.scale(12.3400m) }).ToList();
        var minScale = () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.min_scale(12.3400m) }).ToList();
        var trimScale = () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.trim_scale(12.3400m) }).ToList();
        var nullArgument = () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.trim_scale(typedNull) }).ToList();

        scale.Should().Throw<NotSupportedException>().WithMessage("*scale*not supported*");
        minScale.Should().Throw<NotSupportedException>().WithMessage("*min_scale*not supported*");
        trimScale.Should().Throw<NotSupportedException>().WithMessage("*trim_scale*not supported*");
        nullArgument.Should().Throw<NotSupportedException>().WithMessage("*trim_scale*not supported*");
    }

    /// <summary>
    /// The residual PostgreSQL string and numeric families of #281 are SQL-only: the in-memory provider
    /// must refuse every name explicitly (a <see cref="NotSupportedException"/> naming the function, not
    /// a compiled call on the null <see cref="SqlFunctions.Postgres"/> marker surface).
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgresResidualFamilies_ShouldThrow()
    {
        int? nullInt = null;
        Guid uuid = Guid.Empty;
        byte[] bytes = [0xA1];
        TimeSpan interval = TimeSpan.FromHours(25);

        var cases = new (string Name, Action Act)[]
        {
            ("btrim", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.btrim("  ab  ") }).ToList()),
            ("ltrim", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.ltrim("xxab", "x") }).ToList()),
            ("rtrim", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.rtrim("abxx", "x") }).ToList()),
            ("starts_with", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.starts_with("ab", "a") }).ToList()),
            ("parse_ident", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.parse_ident("a.b", false) }).ToList()),
            ("quote_ident", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.quote_ident("a b") }).ToList()),
            ("quote_literal", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.quote_literal("a") }).ToList()),
            ("quote_nullable", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.quote_nullable("a") }).ToList()),
            ("to_ascii", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_ascii("abc", "LATIN1") }).ToList()),
            ("to_ascii#2", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_ascii("abc", 8) }).ToList()),
            ("to_hex", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_hex(nullInt) }).ToList()),
            ("to_oct", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_oct(nullInt) }).ToList()),
            ("to_bin", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_bin(nullInt) }).ToList()),
            ("convert_from", () => _sut.SimpleEntity.Select(it => new
            {
                V = SqlFunctions.Postgres.convert_from(SqlFunctions.Postgres.convert_to("abc", "UTF8"), "UTF8")
            }).ToList()),
            ("convert_to", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.convert_to("abc", "UTF8") }).ToList()),
            ("encode", () => _sut.SimpleEntity.Select(it => new
            {
                V = SqlFunctions.Postgres.encode(SqlFunctions.Postgres.convert_to("abc", "UTF8"), "hex")
            }).ToList()),
            ("decode", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.decode("616263", "hex") }).ToList()),
            ("normalize", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.normalize("abc", "NFC") }).ToList()),
            ("unistr", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.unistr("a\\0441") }).ToList()),
            ("regexp_match", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.regexp_match("ab", "a") }).ToList()),
            ("div", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.div(7m, 2m) }).ToList()),
            ("erf", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.erf(0.5) }).ToList()),
            ("erfc", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.erfc(0.5) }).ToList()),
            ("random_normal", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.random_normal(0.0, 1.0) }).ToList()),
            ("acosd", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.acosd(0.5) }).ToList()),
            ("asind", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.asind(0.5) }).ToList()),
            ("atand", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.atand(0.5) }).ToList()),
            ("atan2d", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.atan2d(1.0, 1.0) }).ToList()),
            ("cosd", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.cosd(60.0) }).ToList()),
            ("cotd", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.cotd(45.0) }).ToList()),
            ("sind", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.sind(30.0) }).ToList()),
            ("tand", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.tand(45.0) }).ToList()),
            ("casefold", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.casefold("A") }).ToList()),
            ("gamma", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.gamma(1.5) }).ToList()),
            ("tgamma", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.tgamma(1.5) }).ToList()),
            ("lgamma", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.lgamma(1.5) }).ToList()),
            ("to_ascii#1", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.to_ascii("abc") }).ToList()),
            ("date_subtract", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.date_subtract(DateTime.UtcNow, interval) }).ToList()),
            ("date_subtract", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.date_subtract(DateTime.UtcNow, interval, "UTC") }).ToList()),
            ("justify_interval", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.justify_interval(interval) }).ToList()),
            ("make_timestamptz", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5) }).ToList()),
            ("make_timestamptz", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5, "UTC") }).ToList()),
            ("clock_timestamp", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.clock_timestamp() }).ToList()),
            ("statement_timestamp", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.statement_timestamp() }).ToList()),
            ("transaction_timestamp", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.transaction_timestamp() }).ToList()),
            ("timeofday", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.timeofday() }).ToList()),
            ("isfinite", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.isfinite(new DateOnly(2024, 1, 1)) }).ToList()),
            ("isfinite", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.isfinite(DateTime.UtcNow) }).ToList()),
            ("isfinite", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.isfinite(interval) }).ToList()),
            ("uuid_extract_timestamp", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.uuid_extract_timestamp(uuid) }).ToList()),
            ("uuid_extract_version", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.uuid_extract_version(uuid) }).ToList()),
            ("get_bit", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.get_bit(bytes, 0L) }).ToList()),
            ("set_bit", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.set_bit(bytes, 0L, 1) }).ToList()),
            ("get_byte", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.get_byte(bytes, 0) }).ToList()),
            ("set_byte", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.set_byte(bytes, 0, 255) }).ToList()),
            ("bit_count", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.bit_count(bytes) }).ToList()),
            ("crc32", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.crc32(bytes) }).ToList()),
            ("crc32c", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.crc32c(bytes) }).ToList()),
            ("current_schemas", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.current_schemas(true) }).ToList()),
            ("pg_postmaster_start_time", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_postmaster_start_time() }).ToList()),
            ("pg_current_logfile", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_current_logfile() }).ToList()),
            ("pg_current_logfile", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_current_logfile("stderr") }).ToList()),
            ("pg_conf_load_time", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_conf_load_time() }).ToList()),
            ("pg_jit_available", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_jit_available() }).ToList()),
            ("pg_my_temp_schema", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_my_temp_schema() }).ToList()),
            ("pg_is_other_temp_schema", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_is_other_temp_schema("0") }).ToList()),
            ("pg_notification_queue_usage", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_notification_queue_usage() }).ToList()),
            ("pg_blocking_pids", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_blocking_pids(nullInt) }).ToList()),
            ("pg_safe_snapshot_blocking_pids", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(nullInt) }).ToList()),
            ("pg_trigger_depth", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_trigger_depth() }).ToList())
,
            ("pg_collation_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_collation_is_visible("0") }).ToList()),
            ("pg_conversion_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_conversion_is_visible("0") }).ToList()),
            ("pg_function_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_function_is_visible("0") }).ToList()),
            ("pg_opclass_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_opclass_is_visible("0") }).ToList()),
            ("pg_operator_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_operator_is_visible("0") }).ToList()),
            ("pg_opfamily_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_opfamily_is_visible("0") }).ToList()),
            ("pg_statistics_obj_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_statistics_obj_is_visible("0") }).ToList()),
            ("pg_table_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_table_is_visible("0") }).ToList()),
            ("pg_ts_config_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_ts_config_is_visible("0") }).ToList()),
            ("pg_ts_dict_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_ts_dict_is_visible("0") }).ToList()),
            ("pg_ts_parser_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_ts_parser_is_visible("0") }).ToList()),
            ("pg_ts_template_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_ts_template_is_visible("0") }).ToList()),
            ("pg_type_is_visible", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_type_is_visible("0") }).ToList()),
            ("pg_char_to_encoding", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_char_to_encoding("UTF8") }).ToList()),
            ("pg_encoding_to_char", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_encoding_to_char(6) }).ToList()),
            ("pg_basetype", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_basetype("integer") }).ToList()),
            ("pg_describe_object", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_describe_object("0", "0", 0) }).ToList()),
            ("pg_tablespace_location", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_tablespace_location("0") }).ToList()),
            ("pg_settings_get_flags", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_settings_get_flags("work_mem") }).ToList()),
            ("pg_index_has_property", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_index_has_property("pg_class_oid_index", "clusterable") }).ToList()),
            ("pg_indexam_has_property", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_indexam_has_property("0", "can_order") }).ToList()),
            ("pg_index_column_has_property", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_index_column_has_property("pg_class_oid_index", 1, "orderable") }).ToList()),
            ("pg_input_is_valid", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_input_is_valid("123", "integer") }).ToList()),
            ("pg_get_serial_sequence", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_get_serial_sequence("pg_class", "oid") }).ToList()),
            ("pg_get_function_arguments", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_get_function_arguments("1") }).ToList()),
            ("pg_get_function_identity_arguments", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_get_function_identity_arguments("1") }).ToList()),
            ("pg_get_function_result", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_get_function_result("1") }).ToList()),
            ("pg_get_functiondef", () => _sut.SimpleEntity.Select(it => new { V = SqlFunctions.Postgres.pg_get_functiondef("1") }).ToList()),
        };

        foreach (var (name, act) in cases)
        {
            var expected = name.Split('#')[0];
            act.Should().Throw<NotSupportedException>().WithMessage($"*{expected}*not supported*");
        }
    }
}

// D178 T12 fixtures: an enum member for the in-memory fail-closed checks.
public enum InMemoryJsonEnum
{
    Unknown = 0,
    Active = 7,
}

public class InMemoryEnumEntity
{
    public int Id { get; set; }
    public InMemoryJsonEnum State { get; set; }
}