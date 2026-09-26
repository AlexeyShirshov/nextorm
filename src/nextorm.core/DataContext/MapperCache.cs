using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Key for the compiled row-mapper cache: provider type + result type + generated SQL text + a
/// cheap column signature. The SQL text is already produced when <c>GetMap</c> is called (see
/// <c>GetPreparedQueryCommand</c>), so building the key is ~0.1 us, unlike an expression-tree key
/// that walks the whole expression. The provider is part of the key because the reader accessor
/// depends on the provider's column mapping policy (see <c>DataContext.MapColumnExpression</c>),
/// and two providers can generate identical SQL for the same result type (for example
/// <c>select * from t</c>).
/// </summary>
internal readonly record struct MapperCacheKey(Type ProviderType, Type ResultType, string Sql, int ColumnsSignature, bool OneColumn);

/// <summary>
/// Process-wide cache of compiled row mappers. Mirrors linq2db (materializers are cached in a static
/// <c>MemoryCache&lt;QueryKey, Delegate&gt;</c> where <c>QueryKey</c> includes SQL text + target type)
/// and Dapper (SQL-keyed mapper cache).
///
/// The compiled delegate only reads from <see cref="System.Data.IDataRecord"/>, so it is safe to
/// share across <see cref="DataContext"/> instances of the same provider, and it survives
/// <c>PurgeQueryCache</c> — a cold plan rebuild therefore no longer pays <c>Expression.Compile()</c> (~285 us).
/// </summary>
internal static class MapperCache
{
    // Bound the cache. When full, new shapes aren't cached (still correct, no unbounded growth).
    private const int MaxEntries = 4096;

    private static readonly ConcurrentDictionary<MapperCacheKey, Delegate> _cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(MapperCacheKey key, out Delegate map) => _cache.TryGetValue(key, out map!);

    public static void Add(MapperCacheKey key, Delegate map)
    {
        if (_cache.Count >= MaxEntries) return;
        _cache.TryAdd(key, map);
    }

    public static void Clear() => _cache.Clear();
}
