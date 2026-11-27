using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Identifies the compiled row mapper by provider policy, SQL, result type and the exact ordered
/// column shape. Identical SQL can require different reader accessors or materializer members.
/// </summary>
internal readonly record struct MapperCacheKey(
    Type ProviderType, Type ResultType, string Sql, MapperColumnShape Columns, bool OneColumn, bool Streaming,
    bool Boxed = false, Type? EntityType = null, object? Dialect = null, PlanMappingIdentity Mapping = default);

/// <summary>
/// The record/composite shape a raw mapper was built for. <see cref="None"/> is every ordinary
/// scalar/entity raw mapping; it is part of the key so adding the raw-row branch never changes an
/// existing key's identity.
/// </summary>
internal enum RawRowKind
{
    /// <summary>An ordinary scalar or mapped-entity raw mapping.</summary>
    None = 0,

    /// <summary>An anonymous PostgreSQL <c>ROW(...)</c> materialized into a <see cref="System.Tuple"/>.</summary>
    AnonymousTuple = 1,

    /// <summary>A caller-registered named composite materialized into its declared CLR type.</summary>
    NamedComposite = 2,
}

/// <summary>
/// Key for a raw-command (<c>ExecuteRaw</c>) row mapper. Unlike <see cref="MapperCacheKey"/> it does
/// <b>not</b> contain the SQL text: raw commands accept arbitrary text, so keying by it would grow the
/// cache without bound. The shape is the ordered reader column names (the entity projection binds by
/// name), the provider type, the result type and whether the projection is a single scalar column.
/// <see cref="RecordKind"/> and <see cref="RecordSignature"/> discriminate a single-record-column raw
/// mapping from an ordinary one for the same column-key: the record shape (tuple arity/item types) is
/// not otherwise represented by the column-name string.
/// </summary>
internal readonly record struct RawMapperCacheKey(
    Type ProviderType,
    Type ResultType,
    bool OneColumn,
    string Columns,
    Type? NamingConventionType,
    RawRowKind RecordKind,
    string? RecordSignature);

/// <summary>
/// Process-wide cache of compiled row mappers. Mirrors linq2db (materializers are cached in a static
/// <c>MemoryCache&lt;QueryKey, Delegate&gt;</c> where <c>QueryKey</c> includes SQL text + target type)
/// and Dapper (SQL-keyed mapper cache).
///
/// SQL mappers survive <c>PurgeQueryCache</c>, so a cold plan rebuild can reuse a compiled delegate.
/// A mapper that closes over column converters is cached only for the lifetime of those converters.
/// </summary>
internal static class MapperCache
{
    // Bound the cache. When full, new shapes aren't cached (still correct, no unbounded growth).
    private const int MaxEntries = 4096;
    // Raw mappers are keyed by result shape, not SQL, but a caller can still vary columns arbitrarily;
    // keep a separate, smaller bound so they cannot evict the LINQ/returning entries.
    private const int MaxRawEntries = 1024;

    private static readonly SqlMappers _sql = new();
    private static readonly ConcurrentDictionary<RawMapperCacheKey, Delegate> _rawCache = new();

    internal static SqlMappers ForColumns(SelectExpression[]? columns)
    {
        var scope = _sql;
        if (columns is not null)
            foreach (var column in columns)
                if (column.Converter is { } converter)
                    scope = scope.ForConverter(converter);
        return scope;
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
        _sql.Clear();
        _rawCache.Clear();
    }

    internal sealed class SqlMappers
    {
        private readonly ConcurrentDictionary<MapperCacheKey, Delegate> _entries = new();
        private ConditionalWeakTable<IPropertyValueConverter, SqlMappers>? _converterScopes;

        // Nest the weak scopes so a mapper with two converters cannot keep the second alive
        // just because another caller still owns the first.
        internal SqlMappers ForConverter(IPropertyValueConverter converter)
            => LazyInitializer.EnsureInitialized(ref _converterScopes)!.GetValue(converter, static _ => new SqlMappers());

        internal bool TryGet(MapperCacheKey key, out Delegate map) => _entries.TryGetValue(key, out map!);

        internal void Add(MapperCacheKey key, Delegate map)
        {
            if (_entries.Count < MaxEntries)
                _entries.TryAdd(key, map);
        }

        internal void Clear()
        {
            _entries.Clear();
            _converterScopes?.Clear();
        }
    }
}
