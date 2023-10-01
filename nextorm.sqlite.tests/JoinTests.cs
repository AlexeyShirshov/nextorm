using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace nextorm.sqlite.tests;

public class JoinTests
{
    private readonly TestDataContext _sut;
    private readonly ILogger<JoinTests> _logger;
    public JoinTests(TestDataContext sut, ILogger<JoinTests> logger)
    {
        _sut = sut;
        _logger = logger;
    }
    [Fact]
    public async void SelectJoin_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Join(_sut.ComplexEntity, (s,c)=>s.Id == c.Id).Select(p => new { p.t1.Id, p.t2.RequiredString }))
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
            .Join(_sut.ComplexEntity, (s,c)=>s.Id == c.Id)
            .Join(_sut.ComplexEntity, (p,c)=>p.t2.Id == c.Id+1)
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
            .Join(_sut.ComplexEntity, (s,c)=>s.Id == c.Id)
            .Where(p=>p.t2.Boolean??false)
            .Select(p => new { p.t1.Id, p.t2.RequiredString }))
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
            _logger.LogInformation("Id = {id}, String = {str}", row.Id, row.RequiredString);
        }

        idx.Should().Be(1);
    }
}