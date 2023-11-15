using System.Formats.Asn1;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace nextorm.core.tests;

public class InMemoryTests
{
    private readonly InMemoryDataContext _sut;
    private readonly ILogger<InMemoryTests> _logger;

    public InMemoryTests(InMemoryDataContext sut, ILogger<InMemoryTests> logger)
    {
        _sut = sut;
        _logger = logger;

        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
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
        var r = await _sut.From(subQuery).Where(it => it.Id == 2).Select(it => new { it.Id }).SingleOrDefaultAsync();

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
        _sut.DataProvider.Compile(query, false, CancellationToken.None);
        // When
        await foreach (var item in query)
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

        await foreach (var row in _sut.SimpleEntity
            .WithData(data)
            .Select(it => new { it.Id }).Pipeline())
        {
            _logger.LogInformation("Get {id}", row.Id);
        }
    }
    [Fact]
    public void TestQueryCache()
    {
        var id = 2;

        var q1 = _sut.SimpleEntity.Where(it => it.Id == id).Select(it => new { it.Id });
        q1.PrepareCommand(CancellationToken.None);
        var q2 = _sut.SimpleEntity.Where(it2 => it2.Id == id).Select(it3 => new { it3.Id });
        q2.PrepareCommand(CancellationToken.None);

        var hashCode = q1.GetHashCode();
        hashCode.Should().Be(q2.GetHashCode());
        q1.Should().Be(q2);

        id = 3;

        var q3 = _sut.SimpleEntity.Where(it => it.Id == id).Select(it => new { it.Id });
        q3.PrepareCommand(CancellationToken.None);

        hashCode.Should().NotBe(q3.GetHashCode());
    }
    [Fact]
    public void TestQueryPlanCache()
    {
        var (id1, id2) = (2, 3);

        var q1 = _sut.SimpleEntity.Where(it => it.Id == id1).Select(it => new { it.Id });
        q1.PrepareCommand(CancellationToken.None);
        var q2 = _sut.SimpleEntity.Where(it2 => it2.Id == id2).Select(it3 => new { it3.Id });
        q2.PrepareCommand(CancellationToken.None);

        var hashCode = q1.GetHashCode();
        hashCode.Should().NotBe(q2.GetHashCode());

        var planEC = QueryPlanEqualityComparer.Instance;

        planEC.GetHashCode(q1).Should().Be(planEC.GetHashCode(q2));
        planEC.Equals(q1, q2).Should().BeTrue();
    }
}