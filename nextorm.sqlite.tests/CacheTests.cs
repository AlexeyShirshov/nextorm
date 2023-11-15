using Microsoft.Extensions.Logging;

namespace nextorm.sqlite.tests;
public class CacheTests
{
    private readonly TestDataContext _sut;
    private readonly ILogger<CacheTests> _logger;
    public CacheTests(TestDataContext sut, ILogger<CacheTests> logger)
    {
        _sut = sut;
        _logger = logger;
    }
    [Fact]
    public async Task TestCache()
    {
        for (var i = 0; i < 2; i++)
        {
            var id = i;
            var row = await _sut.SimpleEntity
                .Where(entity => entity.Id == id)
                .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();
        }
    }
}