namespace nextorm.core;

internal class CacheEntry
{
    public CacheEntry(object compiledQuery)
    {
        CompiledQuery = compiledQuery;
    }
    public object CompiledQuery { get; set; }
}
