using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextORM.Core;

/// <summary>
/// Builds the provider-independent semantic structural key of a frozen <see cref="JsonShapePlan"/>.
/// The key describes exactly what the compiled <see cref="JsonRowWriter"/> depends on and nothing
/// else: the ordered per-leaf ordinals and resulting JSON property names, the declared/value/enum
/// types and the classification that follows from the converter attribute, the recursive shape
/// (kind, member order, presence rule, nullable/default handling and direct pass-through), the
/// writer-relevant option values (<see cref="JsonStreamOptions.IgnoreNull"/>) and the naming-policy
/// discriminator. Provider/context identity, runtime reader storage types and the current reader are
/// deliberately absent: the compiled writer inspects the runtime provider field type per read, so the
/// same delegate serves every supported provider storage type.
/// <para>
/// <see cref="JsonShapePlan"/> and <see cref="JsonStreamOptions"/> reference identity is never used as
/// the key. A converter or naming policy that cannot be described by an immutable identity makes
/// <see cref="Build"/> return <see langword="null"/>, which bypasses the cache instead of comparing
/// an approximation.
/// </para>
/// </summary>
internal static class JsonRowWriterKey
{
    private const string NoValue = "none";

    /// <summary>
    /// Builds the semantic key of <paramref name="plan"/>, or <see langword="null"/> when a captured
    /// naming policy or converter has no immutable representation (the caller must then compile without
    /// publishing an entry).
    /// </summary>
    /// <param name="plan">The frozen shape plan the writer will be compiled from.</param>
    /// <returns>The key, or <see langword="null"/> for an unrepresentable configuration.</returns>
    internal static string? Build(JsonShapePlan plan)
    {
        var policy = DescribeNamingPolicy(plan.Options.PropertyNamingPolicy);
        if (policy is null)
            return null;

        var builder = new StringBuilder(256);
        builder.Append(plan.IsScalar ? 'S' : 'O').Append(';');
        AppendBool(builder, plan.Options.IgnoreNull);
        AppendString(builder, policy);

        if (plan.Shape is { } shape)
        {
            builder.Append("recursive;");
            if (!AppendNode(builder, shape, plan.Options))
                return null;
        }
        else
        {
            builder.Append("flat;");
            AppendInt(builder, plan.Columns.Length);
            foreach (var column in plan.Columns)
            {
                if (!AppendColumn(builder, column))
                    return null;
            }
        }

        return builder.ToString();
    }

    // A flat column's writer only touches the ordinal, the resolved name, the value type, the write
    // kind, nullability/default handling and the pre-built enum converter. DeclaredType, ProviderType
    // and IsDirectPassThrough are native-eligibility metadata, never consumed by the writer.
    private static bool AppendColumn(StringBuilder builder, in JsonShapeColumn column)
    {
        AppendInt(builder, column.Ordinal);
        AppendString(builder, column.Name);
        AppendType(builder, column.ValueType);
        AppendInt(builder, (int)column.Kind);
        AppendBool(builder, column.Nullable);
        AppendBool(builder, column.DefaultOnNull);
        AppendTypeOrNone(builder, column.EnumUnderlyingType);
        return AppendConverter(builder, column.EnumStringConverter, column.ValueType);
    }

    // The recursive writer derives its behavior from the node kind, the declared type, the binding
    // (ordinal/nullability/default-on-null), the presence rule and — for object nodes — the ordered,
    // naming-policy-resolved member names. An array node is written from its declared rank-one array
    // type alone (the writer never visits the captured element descriptor), so the element subtree is
    // not part of the key.
    private static bool AppendNode(StringBuilder builder, JsonShapeNode node, JsonStreamOptions options)
    {
        AppendInt(builder, (int)node.Kind);
        AppendString(builder, node.Name);
        AppendType(builder, node.DeclaredType);

        switch (node.Kind)
        {
            case JsonShapeNodeKind.Scalar:
                AppendBinding(builder, node.Binding);
                var (valueType, kind, enumUnderlying, converter) = JsonShapePlan.Classify(node.DeclaredType, node.Name, node.Member);
                AppendType(builder, valueType);
                AppendInt(builder, (int)kind);
                AppendTypeOrNone(builder, enumUnderlying);
                return AppendConverter(builder, converter, valueType);

            case JsonShapeNodeKind.Array:
                AppendBinding(builder, node.Binding);
                return true;

            case JsonShapeNodeKind.Object:
                AppendInt(builder, (int)node.Presence.Kind);
                AppendInt(builder, node.Presence.Ordinals.Length);
                foreach (var ordinal in node.Presence.Ordinals)
                    AppendInt(builder, ordinal);

                AppendInt(builder, node.Members.Length);
                foreach (var member in node.Members)
                {
                    // Resolves the effective name exactly as the writer does (the same helper, so the
                    // same fail-closed validation for an empty name or a null policy result).
                    AppendString(builder, JsonRowWriterFactory.ResolveMemberName(member.Name, options));
                    if (!AppendNode(builder, member, options))
                        return false;
                }

                return true;

            default:
                return false;
        }
    }

    private static void AppendBinding(StringBuilder builder, JsonShapeBinding? binding)
    {
        if (binding is not { } value)
        {
            builder.Append("unbound;");
            return;
        }

        AppendInt(builder, value.Ordinal);
        AppendBool(builder, value.Nullable);
        AppendBool(builder, value.DefaultOnNull);
    }

    /// <summary>
    /// Describes a naming policy only when it has a stable immutable identity. The stock policies are
    /// stateless singletons; any other <see cref="JsonNamingPolicy"/> subclass may carry mutable state,
    /// so it is not representable and the caller bypasses the cache.
    /// </summary>
    private static string? DescribeNamingPolicy(JsonNamingPolicy? policy)
    {
        if (policy is null)
            return NoValue;

        if (ReferenceEquals(policy, JsonNamingPolicy.CamelCase))
            return "camel";

        if (ReferenceEquals(policy, JsonNamingPolicy.SnakeCaseLower))
            return "snake-lower";

        if (ReferenceEquals(policy, JsonNamingPolicy.SnakeCaseUpper))
            return "snake-upper";

        if (ReferenceEquals(policy, JsonNamingPolicy.KebabCaseLower))
            return "kebab-lower";

        if (ReferenceEquals(policy, JsonNamingPolicy.KebabCaseUpper))
            return "kebab-upper";

        return null;
    }

    /// <summary>
    /// Appends the identity of a pre-built string-enum converter. <see cref="JsonShapePlan.Classify"/>
    /// admits only the stock <see cref="JsonStringEnumConverter"/> / <see cref="JsonStringEnumConverter{TEnum}"/>
    /// forms, built with their default configuration for the projected enum type, so the converter adds
    /// no keyable configuration beyond "stock string-enum for this enum type". A converter that is not
    /// closed over the projected value type has no representable identity and makes the key unresolvable
    /// (the caller bypasses the cache).
    /// </summary>
    private static bool AppendConverter(StringBuilder builder, JsonConverter? converter, Type valueType)
    {
        if (converter is null)
        {
            AppendString(builder, NoValue);
            return true;
        }

        var converterType = converter.GetType();
        if (!converterType.IsGenericType || converterType.GetGenericArguments()[0] != valueType)
            return false;

        AppendString(builder, "enum-string");
        AppendType(builder, valueType);
        return true;
    }

    private static void AppendInt(StringBuilder builder, int value)
    {
        builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
    }

    private static void AppendBool(StringBuilder builder, bool value)
    {
        builder.Append(value ? '1' : '0').Append(';');
    }

    // Length-prefixed so a property name containing the separator cannot collide with another part.
    private static void AppendString(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1;");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
    }

    private static void AppendType(StringBuilder builder, Type type)
    {
        var name = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        builder.Append(name.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(name).Append(';');
    }

    private static void AppendTypeOrNone(StringBuilder builder, Type? type)
    {
        if (type is null)
        {
            AppendString(builder, NoValue);
            return;
        }

        AppendType(builder, type);
    }
}

/// <summary>
/// Bounded, context-owned cache of compiled <see cref="JsonRowWriter"/> delegates. One entry is the
/// provider-independent semantic key of a frozen shape plus the immutable delegate built from it; no
/// reader, destination, command, option instance or execution state is retained, so a resident
/// delegate can be invoked concurrently and by any later call with the same shape.
/// <para>
/// Capacity is <see cref="Capacity"/> published entries with LRU eviction. Lookup, cold compilation,
/// publication and eviction run under one lock, so a resident entry is compiled exactly once even
/// under concurrent first use; the returned delegates are invoked outside the lock. Boundedness and
/// isolation are both required: entries live per <see cref="DataContext"/> (two contexts never share),
/// and a process-wide generation in <see cref="DataContextCache"/> invalidates every context's entries
/// when the process-wide caches are cleared.
/// </para>
/// </summary>
internal sealed class JsonRowWriterCache
{
    /// <summary>Maximum number of published writer entries; the least recently used entry is evicted first.</summary>
    internal const int Capacity = 64;

    private sealed class Entry
    {
        internal Entry(string key, JsonRowWriter writer)
        {
            Key = key;
            Writer = writer;
        }

        internal string Key { get; }

        internal JsonRowWriter Writer { get; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private long _generation = DataContextCache.JsonRowWriterCacheGeneration;
    private int _compilationCount;

    /// <summary>The number of published entries (test/evidence observability).</summary>
    internal int EntryCount
    {
        get
        {
            lock (_gate)
                return _index.Count;
        }
    }

    /// <summary>
    /// The number of cold compilations performed under the lock. A warm hit does not increment it, so it
    /// is the observable proof that a resident entry was built once (test/evidence observability).
    /// </summary>
    internal int CompilationCount => Volatile.Read(ref _compilationCount);

    /// <summary>
    /// Returns the resident writer for <paramref name="plan"/>, compiling and publishing it on a miss.
    /// An unrepresentable key bypasses the cache: the writer is compiled but nothing is published.
    /// </summary>
    /// <param name="plan">The frozen shape plan to write rows with.</param>
    /// <returns>The cached or freshly compiled writer.</returns>
    internal JsonRowWriter GetOrBuild(JsonShapePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var key = JsonRowWriterKey.Build(plan);
        if (key is null)
            return JsonRowWriterFactory.Build(plan);

        var generation = DataContextCache.JsonRowWriterCacheGeneration;
        lock (_gate)
        {
            if (_generation != generation)
            {
                _index.Clear();
                _lru.Clear();
                _generation = generation;
            }

            if (_index.TryGetValue(key, out var node))
            {
                if (!ReferenceEquals(node, _lru.First))
                {
                    _lru.Remove(node);
                    _lru.AddFirst(node);
                }

                return node.Value.Writer;
            }

            // Compiled under the lock on purpose: the minimal variant guarantees a resident entry is
            // built once, accepting the cold-compilation contention instead of duplicate publication.
            var writer = JsonRowWriterFactory.Build(plan);
            _compilationCount++;

            var added = _lru.AddFirst(new Entry(key, writer));
            _index.Add(key, added);
            if (_index.Count > Capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _index.Remove(last.Value.Key);
            }

            return writer;
        }
    }

    /// <summary>Drops every entry, releasing the delegates it retained.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _index.Clear();
            _lru.Clear();
        }
    }
}
