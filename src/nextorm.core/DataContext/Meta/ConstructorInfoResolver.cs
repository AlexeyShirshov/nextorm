using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Resolves and caches constructor metadata for runtime types inspected during query preparation.
/// </summary>
internal static class ConstructorInfoResolver
{
    private static readonly ConcurrentDictionary<Type, Lazy<ConstructorInfo?>> LongestPublicConstructorCache = new();
    private static readonly ConcurrentDictionary<Type, Lazy<ConstructorInfo?>> ParameterlessConstructorCache = new();
    private static readonly ConcurrentDictionary<Type, Lazy<ConstructorInfo?>> RangeConstructorCache = new();

    internal static ConstructorInfo? GetLongestPublicConstructor(Type type)
        => LongestPublicConstructorCache.GetOrAdd(
            type,
            static key => new Lazy<ConstructorInfo?>(() => key.GetConstructors().OrderByDescending(constructor => constructor.GetParameters().Length).FirstOrDefault()))
            .Value;

    internal static ConstructorInfo? GetParameterlessConstructor(Type type)
        => ParameterlessConstructorCache.GetOrAdd(
            type,
            static key => new Lazy<ConstructorInfo?>(() => key.GetConstructor(Type.EmptyTypes)))
            .Value;

    internal static ConstructorInfo? GetRangeConstructor(Type type)
        => RangeConstructorCache.GetOrAdd(
            type,
            static key => new Lazy<ConstructorInfo?>(() => key.GetConstructors().Single(constructor => constructor.GetParameters().Length == 7)))
            .Value;
}
