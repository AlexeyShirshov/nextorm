using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace nextorm.core.tests;

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
    public void TestDistinct_ReferenceTypeWithoutValueEquality_ShouldThrow()
    {
        // SimpleEntity is a plain reference type without an Equals/GetHashCode override, so the
        // in-memory provider cannot reproduce SQL's value-based DISTINCT and must reject it.
        var act = () => _sut.SimpleEntity.Distinct().ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT*");
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

        planEC.GetHashCode(q1).Should().NotBe(planEC.GetHashCode(q2));
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
        var l = cb.Select(_ => NORM.SQL.exists(cmd)).ToList();
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
            .Select(e => new { Parity = e.Id % 2, count = NORM.SQL.count() })
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
            .Having(e => NORM.SQL.count() > 1)
            .Select(e => new { Parity = e.Id % 2, count = NORM.SQL.count() })
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
            .Having(e => NORM.SQL.count() > 1)
            .Select(e => new { Parity = e.Id % 2, count = NORM.SQL.count() })
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
            .Select(e => new { Parity = e.Id % 2, avg = NORM.SQL.avg(Convert.ToDouble(e.Id)) })
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
            .Select(e => new { Parity = e.Id % 2, count = NORM.SQL.count() })
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
        [SqlTableFunction("simple_tvf")]
        public static IQueryable<SimpleEntity> SimpleTvf() => throw new NotSupportedException();
    }

    [Fact]
    public void TableFunction_ShouldThrowClearNotSupported()
    {
        // The in-memory provider cannot evaluate a database table-valued function, so it must fail
        // loudly instead of silently returning an empty (or otherwise wrong) result set.
        var act = () => _sut.DataProvider
            .FromTableFunction(() => Tvf.SimpleTvf())
            .Select(it => new { it.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }
}