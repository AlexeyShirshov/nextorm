using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace nextorm.sqlite.tests;

public class JoinTests
{
    private readonly TestDataRepository _sut;
    private readonly ILogger<JoinTests> _logger;
    public JoinTests(TestDataRepository sut, ILogger<JoinTests> logger)
    {
        _sut = sut;
        _logger = logger;
    }
    [Fact]
    public async void SelectJoin_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().BeGreaterThan(0);
    }
    [Fact]
    public async void Select2Join_ShouldReturnData()
    {
        long idx = 1;
        await foreach (var row in _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Join(_sut.ComplexEntity, (p, c) => p.t2.Id == c.Id + 1)
            .Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().BeGreaterThan(1);
    }
    [Fact]
    public async void SelectJoinWithWhere_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.t2.Boolean ?? false)
            .Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().Be(1);
    }
    [Fact]
    public async void SelectWhereJoin_ShouldReturnData()
    {
        // Func<Task> act = async () =>
        // {
        long idx = 2;
        await foreach (var row in _sut.SimpleEntity
            .Where(it => it.Id > 2)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.t2.RequiredString == "34mfs")
            .Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().Be(3);
        // };

        // await act.Should().ThrowAsync<InvalidOperationException>();
    }
    [Fact]
    public async void SelectJoinSubquery_ShouldReturnData()
    {
        var subQuery = _sut.ComplexEntity.Where(it => it.Id == 3).Select(it => new { it.Id, it.RequiredString, it.Boolean });
        long idx = 2;
        await foreach (var row in _sut.SimpleEntity.Join(subQuery, (s, c) => s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().BeGreaterThan(0);
    }
    [Fact]
    public async void SelectJoinSubqueryWithJoin_ShouldReturnData()
    {
        var subQuery = _sut.ComplexEntity.Join(_sut.ComplexEntity, (c1, c2) => c1.RequiredString == c2.String).Select(p => new { Id1 = p.t1.Id, Id2 = p.t2.Id, Double = p.t1.Double + p.t2.Double });
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Join(subQuery, (s, c) => s.Id == c.Id1 + c.Id2).Select(p => new { p.t1.Id, p.t2.Double }))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}, Double = {d}", row.Id, row.Double);
        }

        idx.Should().Be(0);
    }
}