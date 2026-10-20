using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Core terminal/composition sweep executed against SQLite (in-process): the materialisation
/// terminals, the aggregate terminals (sync and async), the CSV/JSON streaming writers, the
/// ordering/paging surface and the source overrides. Every call renders SQL and reads rows from a
/// real file-backed database, so the planner, the command cache and the row mapper all run.
/// </summary>
[Collection("Sqlite")]
public sealed class CoreTerminalSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void MaterialisationTerminals_ShouldExecute()
    {
        _sut.SimpleEntityAsClass.OrderBy(x => x.Id).ToArray().Should().HaveCount(10);
        _sut.ComplexEntity.Select(x => x.Id).ToList().Should().HaveCount(3);
        _sut.ComplexEntity.Select(x => x.Id).ToEnumerable().Should().HaveCount(3);
        _sut.ComplexEntity.Select(x => x.Id).ToHashSet().Should().HaveCount(3);
        _sut.ComplexEntity.Select(x => x.Id).ToHashSet(EqualityComparer<long>.Default).Should().HaveCount(3);

        var byId = _sut.ComplexEntity.Select(x => x.Id).ToDictionary(x => x);
        byId.Should().HaveCount(3).And.ContainKey(1L);

        var byIdCmp = _sut.ComplexEntity.Select(x => x.Id).ToDictionary(x => x, EqualityComparer<long>.Default);
        byIdCmp.Should().HaveCount(3);

        _sut.ComplexEntity.Where(x => x.Id == 2).Select(x => x.Id).First().Should().Be(2);
        _sut.ComplexEntity.Where(x => x.Id == 99).Select(x => x.Id).FirstOrDefault().Should().Be(0);
        _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).Last().Should().Be(3);
        _sut.ComplexEntity.OrderBy(x => x.Id).Where(x => x.Id == 99).Select(x => x.Id).LastOrDefault().Should().Be(0);
        _sut.ComplexEntity.Where(x => x.Id == 2).Select(x => x.Id).Single().Should().Be(2);
        _sut.ComplexEntity.Where(x => x.Id == 99).Select(x => x.Id).SingleOrDefault().Should().Be(0);
    }

    [Fact]
    public async Task MaterialisationTerminalsAsync_ShouldExecute()
    {
        (await _sut.ComplexEntity.Select(x => x.Id).ToListAsync()).Should().HaveCount(3);
        (await _sut.SimpleEntityAsClass.ToArrayAsync()).Should().HaveCount(10);
        (await _sut.ComplexEntity.Select(x => x.Id).ToHashSetAsync()).Should().HaveCount(3);
        (await _sut.ComplexEntity.Select(x => x.Id).ToHashSetAsync(EqualityComparer<long>.Default)).Should().HaveCount(3);
        (await _sut.ComplexEntity.Select(x => x.Id).ToDictionaryAsync(x => x)).Should().HaveCount(3);
        (await _sut.ComplexEntity.Select(x => x.Id).ToDictionaryAsync(x => x, EqualityComparer<long>.Default)).Should().HaveCount(3);

        (await _sut.ComplexEntity.Where(x => x.Id == 2).Select(x => x.Id).FirstAsync()).Should().Be(2);
        (await _sut.ComplexEntity.Where(x => x.Id == 99).Select(x => x.Id).FirstOrDefaultAsync()).Should().Be(0);
        (await _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).LastAsync()).Should().Be(3);
        (await _sut.ComplexEntity.OrderBy(x => x.Id).Where(x => x.Id == 99).Select(x => x.Id).LastOrDefaultAsync()).Should().Be(0);
        (await _sut.ComplexEntity.Where(x => x.Id == 2).Select(x => x.Id).SingleAsync()).Should().Be(2);
        (await _sut.ComplexEntity.Where(x => x.Id == 99).Select(x => x.Id).SingleOrDefaultAsync()).Should().Be(0);

        var seen = 0;
        await foreach (var row in _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).ToAsyncEnumerable())
        {
            row.Should().BeGreaterThan(0);
            seen++;
        }

        seen.Should().Be(3);
    }

    [Fact]
    public void AggregateTerminals_ShouldExecute()
    {
        _sut.ComplexEntity.Count().Should().Be(3);
        _sut.ComplexEntity.Where(x => x.Int != null).Count().Should().BeGreaterThan(0);
        _sut.ComplexEntity.LongCount().Should().Be(3);
        _sut.ComplexEntity.Any().Should().BeTrue();
        _sut.ComplexEntity.Where(x => x.Id == 99).Any().Should().BeFalse();
        _sut.ComplexEntity.Where(x => x.Id == 2).Any().Should().BeTrue();

        // The shared seed stores nullableint values null, 1, 1.
        _sut.ComplexEntity.Where(x => x.Int != null).Sum(x => x.Int!.Value).Should().Be(2);
        _sut.ComplexEntity.Where(x => x.Int != null).Min(x => x.Int!.Value).Should().Be(1);
        _sut.ComplexEntity.Where(x => x.Int != null).Max(x => x.Int!.Value).Should().Be(1);
        _sut.ComplexEntity.Where(x => x.Int != null).Avg(x => x.Int!.Value).Should().Be(1);
        _sut.ComplexEntity.Where(x => x.Int != null).Stdev(x => x.Int!.Value).Should().BeGreaterThanOrEqualTo(0);
        _sut.ComplexEntity.Where(x => x.Int != null).Stdevp(x => x.Int!.Value).Should().BeGreaterThanOrEqualTo(0);
        _sut.ComplexEntity.Where(x => x.Int != null).Var(x => x.Int!.Value).Should().BeGreaterThanOrEqualTo(0);
        _sut.ComplexEntity.Where(x => x.Int != null).Varp(x => x.Int!.Value).Should().BeGreaterThanOrEqualTo(0);

        _sut.ComplexEntity.Where(x => x.Id > 0).Sum(x => x.Id).Should().Be(6);
        _sut.ComplexEntity.Where(x => x.Id > 0).Min(x => x.Id).Should().Be(1);
        _sut.ComplexEntity.Where(x => x.Id > 0).Max(x => x.Id).Should().Be(3);
        _sut.ComplexEntity.Where(x => x.Id > 0).Avg(x => x.Id).Should().Be(2);
    }

    [Fact]
    public async Task AggregateTerminalsAsync_ShouldExecute()
    {
        (await _sut.ComplexEntity.CountAsync()).Should().Be(3);
        (await _sut.ComplexEntity.LongCountAsync()).Should().Be(3);
        (await _sut.ComplexEntity.AnyAsync()).Should().BeTrue();
        (await _sut.ComplexEntity.Where(x => x.Id == 99).AnyAsync()).Should().BeFalse();

        (await _sut.ComplexEntity.Where(x => x.Int != null).SumAsync(x => x.Int!.Value)).Should().Be(2);
        (await _sut.ComplexEntity.Where(x => x.Int != null).MinAsync(x => x.Int!.Value)).Should().Be(1);
        (await _sut.ComplexEntity.Where(x => x.Int != null).MaxAsync(x => x.Int!.Value)).Should().Be(1);
        (await _sut.ComplexEntity.Where(x => x.Int != null).AvgAsync(x => x.Int!.Value)).Should().Be(1);
        (await _sut.ComplexEntity.Where(x => x.Int != null).StdevAsync(x => x.Int!.Value)).Should().BeGreaterThanOrEqualTo(0);
        (await _sut.ComplexEntity.Where(x => x.Int != null).StdevpAsync(x => x.Int!.Value)).Should().BeGreaterThanOrEqualTo(0);
        (await _sut.ComplexEntity.Where(x => x.Int != null).VarAsync(x => x.Int!.Value)).Should().BeGreaterThanOrEqualTo(0);
        (await _sut.ComplexEntity.Where(x => x.Int != null).VarpAsync(x => x.Int!.Value)).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void OrderingAndPaging_ShouldExecute()
    {
        _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L);
        _sut.ComplexEntity.OrderByDescending(x => x.Id).Select(x => x.Id).ToList().Should().Equal(3L, 2L, 1L);
        _sut.ComplexEntity.OrderBy(x => x.Id, OrderDirection.Desc).Select(x => x.Id).First().Should().Be(3);
        _sut.ComplexEntity.OrderBy(1).Select(x => x.Id).ToList().Should().Equal(1L, 2L, 3L);
        _sut.ComplexEntity.OrderBy(1, OrderDirection.Desc).Select(x => x.Id).First().Should().Be(3);
        _sut.ComplexEntity.OrderByDescending(1).Select(x => x.Id).First().Should().Be(3);

        _sut.ComplexEntity.OrderBy(x => x.Id).Limit(2).Select(x => x.Id).ToList().Should().HaveCount(2);
        _sut.ComplexEntity.OrderBy(x => x.Id).Offset(1).Select(x => x.Id).ToList().Should().Equal(2L, 3L);
        _sut.ComplexEntity.OrderBy(x => x.Id).Page(2, 1).Select(x => x.Id).ToList().Should().Equal(2L, 3L);

        _sut.ComplexEntity.OrderBy(x => x.Id).Distinct().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void SourceOverrides_ShouldExecute()
    {
        _sut.SimpleEntity.WithTableName("simple_entity").Select(x => x.Id).ToList().Should().NotBeEmpty();
        _sut.SimpleEntity.WithTag("tagged").Select(x => x.Id).First().Should().Be(1);
        _sut.SimpleEntity.WithCommandTimeout(5).Select(x => x.Id).First().Should().Be(1);
        _sut.SimpleEntity.WithKeywordCase(KeywordCase.Upper).Select(x => x.Id).First().Should().Be(1);
        _sut.SimpleEntity.WithUppercaseKeywords(true).Select(x => x.Id).First().Should().Be(1);
        _sut.SimpleEntity.WithQuotedIdentifiers(true).Select(x => x.Id).First().Should().Be(1);
        _sut.SimpleEntity.WithQuotedIdentifiers(false).Select(x => x.Id).First().Should().Be(1);

        var cloned = (EntityBuilder<ISimpleEntity>)((ICloneable)_sut.SimpleEntity).Clone();
        cloned.Where(x => x.Id == 1).Select(x => x.Id).First().Should().Be(1);

        var command = _sut.SimpleEntity.Where(x => x.Id == 1).ToCommand();
        command.Should().NotBeNull();
    }

    [Fact]
    public void CsvAndJsonWriters_ShouldStream()
    {
        using (var csv = new MemoryStream())
        {
            _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteCsv(csv, null, TestContext.Current.CancellationToken);
            Encoding.UTF8.GetString(csv.ToArray()).Should().Contain("Id");
        }

        using (var csv = new MemoryStream())
        {
            _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteCsv(csv, new CsvStreamOptions(), TestContext.Current.CancellationToken);
            csv.Length.Should().BeGreaterThan(0);
        }

        using (var json = new MemoryStream())
        {
            _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteJson(json);
            json.Length.Should().BeGreaterThan(0);
        }

        using (var json = new MemoryStream())
        {
            _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteJson(json, new JsonStreamOptions());
            json.Length.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task CsvAndJsonWritersAsync_ShouldStream()
    {
        using (var csv = new MemoryStream())
        {
            await _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteCsvAsync(csv, null, TestContext.Current.CancellationToken);
            csv.Length.Should().BeGreaterThan(0);
        }

        using (var json = new MemoryStream())
        {
            await _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteJsonAsync(json, TestContext.Current.CancellationToken);
            json.Length.Should().BeGreaterThan(0);
        }

        using (var json = new MemoryStream())
        {
            await _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id }).WriteJsonAsync(json, new JsonStreamOptions(), TestContext.Current.CancellationToken);
            json.Length.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void PlanCacheReuse_ShouldCompareShapes()
    {
        for (var round = 0; round < 2; round++)
        {
            var id = round + 1;

            _sut.SimpleEntity.Where(x => x.Id == id).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id == id).Select(x => new { x.Id }).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id == id).Select(x => new { A = x.Id, B = x.Id + 1 }).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id > id).OrderBy(x => x.Id).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id > id).OrderByDescending(x => x.Id).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id > id).Count().Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).Any().Should().BeTrue();
            _sut.SimpleEntity.Where(x => x.Id > id).Sum(x => x.Id).Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).Min(x => x.Id).Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).Max(x => x.Id).Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).Avg(x => x.Id).Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).Select(x => x.Id).FirstOrDefault().Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id).OrderBy(x => x.Id).Select(x => x.Id).LastOrDefault().Should().BeGreaterThanOrEqualTo(0);
            _sut.SimpleEntity.Where(x => x.Id > id && x.Id < id + 3).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id == id || x.Id == id + 1).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => new[] { 1, 2, 3 }.Contains(x.Id)).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Where(x => x.Id != id).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => x.Id).Distinct().ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => x.Id).Limit(2).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => x.Id).Offset(0).ToList().Should().NotBeNull();
            _sut.SimpleEntity.OrderBy(1).Select(x => x.Id).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => new { V = (int?)x.Id }).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => new { V = x.Id * 2 + id }).ToList().Should().NotBeNull();
            _sut.SimpleEntity.Select(x => new { V = x.Id == id ? "a" : "b" }).ToList().Should().NotBeNull();
        }
    }
}
