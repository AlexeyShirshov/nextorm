using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace nextorm.sqlite.tests;

public class SqlCommandTests
{
    private readonly DataContext _sut;
    private readonly ILogger<SqlCommandTests> _logger;

    public SqlCommandTests(DataContext sut, ILogger<SqlCommandTests> logger)
    {
        _sut = sut;
        _logger = logger;
    }

    [Fact]
    public async void SelectEntity_ShouldReturnData()
    {
        long idx=0;
        await foreach(var row in _sut.SimpleEntity.Select(entity=>new {Id=(long)entity.Id}))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }

    [Fact]
    public async Task SelectModifiedEntity_ShouldReturnData()
    {
        long idx=1;
        await foreach(var row in _sut.SimpleEntity.Select(entity=>new {Id=(long)entity.Id+1}))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectTable_ShouldReturnData()
    {
        await foreach(var row in _sut.From("simple_entity").Select(tbl=>new {Id=tbl.Long("id")}))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
}