namespace nextorm.integration.tests;

public abstract partial class CommonTestSuite
{
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
