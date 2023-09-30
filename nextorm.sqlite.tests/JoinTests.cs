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
}