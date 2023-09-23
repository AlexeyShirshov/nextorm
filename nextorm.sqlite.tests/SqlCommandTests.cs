using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using System.Data.Common;
using System.Linq;

namespace nextorm.sqlite.tests;

public class SqlCommandTests
{
    private readonly TestDataContext _sut;
    private readonly ILogger<SqlCommandTests> _logger;

    public SqlCommandTests(TestDataContext sut, ILogger<SqlCommandTests> logger)
    {
        _sut = sut;
        _logger = logger;
    }

    [Fact]
    public async void SelectEntity_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectEntityToList_ShouldReturnData()
    {
        (await _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).CountAsync()).Should().Be(10);
    }
    [Fact]
    public async Task SelectModifiedEntity_ShouldReturnData()
    {
        long idx = 1;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id + 1 }))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectTable_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new { Id = tbl.Long("id") }))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectSubQuery_ShouldReturnData()
    {
        await foreach (var row in _sut.From(_sut.From("simple_entity").Select(tbl => new { Id = tbl.Long("id") })).Select(subQuery => new { subQuery.Id }))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async Task SelectComplexEntity_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.Datetime, it.RequiredString, it.Boolean, it.Date, it.Double, it.Int, it.Numeric, it.Real, it.SmallInt, it.String, it.TinyInt }))
        {
            _logger.LogInformation("{entity}", row);
        }
    }
    [Fact]
    public async Task SelectEntity_ShouldCancel_WhenCancel()
    {
        CancellationTokenSource tokenSource = new();
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).WithCancellation(tokenSource.Token))
        {
            _logger.LogInformation("{entity}", row);
            tokenSource.Cancel();
            break;
        }
        _logger.LogInformation("Loop cancelled");
    }
    [Fact]
    public async void SelectEntityIntoDTO_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new SimpleEntityDTO(entity.Id)))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectTableIntoDTO_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new SimpleEntityDTO (tbl.Long("id"))))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectEntityIntoTuple_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new Tuple<long>(tbl.Long("id"))))
        {
            _logger.LogInformation("Id = {id}", row.Item1);
        }
    }
}