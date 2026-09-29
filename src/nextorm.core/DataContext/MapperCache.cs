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
/// <c>select * from t</c>). <see cref="Streaming"/> is the streaming discriminator: a projection
/// that contains a streaming LOB accessor must never reuse the buffered mapper compiled for the
/// same SQL shape, and vice versa.
/// </summary>
internal readonly record struct MapperCacheKey(Type ProviderType, Type ResultType, string Sql, int ColumnsSignature, bool OneColumn, bool Streaming);

/// <summary>
/// Key for a raw-command (<c>ExecuteRaw</c>) row mapper. Unlike <see cref="MapperCacheKey"/> it does
/// <b>not</b> contain the SQL text: raw commands accept arbitrary text, so keying by it would grow the
/// cache without bound. The shape is the ordered reader column names (the entity projection binds by
/// name), the provider type, the result type and whether the projection is a single scalar column.
/// </summary>
internal readonly record struct RawMapperCacheKey(Type ProviderType, Type ResultType, bool OneColumn, string Columns, Type? NamingConventionType);

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
    // Raw mappers are keyed by result shape, not SQL, but a caller can still vary columns arbitrarily;
    // keep a separate, smaller bound so they cannot evict the LINQ/returning entries.
    private const int MaxRawEntries = 1024;

    private static readonly ConcurrentDictionary<MapperCacheKey, Delegate> _cache = new();
    private static readonly ConcurrentDictionary<RawMapperCacheKey, Delegate> _rawCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(MapperCacheKey key, out Delegate map) => _cache.TryGetValue(key, out map!);

    public static void Add(MapperCacheKey key, Delegate map)
    {
        if (_cache.Count >= MaxEntries) return;
        _cache.TryAdd(key, map);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetRaw(RawMapperCacheKey key, out Delegate map) => _rawCache.TryGetValue(key, out map!);

    public static void AddRaw(RawMapperCacheKey key, Delegate map)
    {
        if (_rawCache.Count >= MaxRawEntries) return;
        _rawCache.TryAdd(key, map);
    }

    public static void Clear()
    {
        _cache.Clear();
        _rawCache.Clear();
    }
}
