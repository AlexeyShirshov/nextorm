using Microsoft.Extensions.Logging;

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
    public async void Select_ShouldReturnData()
    {
        await foreach(var row in _sut.SimpleEntity.Select(entity=>new {Id=(long)entity.Id}))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
}