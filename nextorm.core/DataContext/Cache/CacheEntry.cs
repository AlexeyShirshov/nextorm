
namespace nextorm.core;

public class CacheEntry
{
    public CacheEntry(object compiledQuery)
    {
        CompiledQuery = compiledQuery;
    }
    public object CompiledQuery { get; set; }
}

// public interface IReplaceParam
// {
//     void ReplaceParams(object[] @params, IDataProvider dataProvider);
// }