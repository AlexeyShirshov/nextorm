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
/// Thin adapter over the shared <see cref="TerminalPlanCache"/> for the JSON row-writer terminal kind.
/// The bounded LRU, the lock, the generation counter and the eviction logic all live in
/// <see cref="TerminalPlanCache"/>; this adapter only builds the provider-independent semantic key
/// (via <see cref="JsonRowWriterKey"/>) and casts the stored value back to <see cref="JsonRowWriter"/>.
/// An unrepresentable key bypasses the cache: the writer is compiled but nothing is published.
/// </summary>
internal sealed class JsonRowWriterCache
{
    /// <summary>Maximum number of published writer entries; shared with the combined terminal cache.</summary>
    internal const int Capacity = TerminalPlanCache.Capacity;

    private readonly TerminalPlanCache _inner;

    /// <summary>Creates an adapter over a freshly owned terminal cache (isolated, for tests).</summary>
    internal JsonRowWriterCache() : this(new TerminalPlanCache())
    {
    }

    /// <summary>Creates an adapter sharing the supplied terminal cache (one combined store per context).</summary>
    internal JsonRowWriterCache(TerminalPlanCache inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>The number of published entries across all terminal kinds (test/evidence observability).</summary>
    internal int EntryCount => _inner.EntryCount;

    /// <summary>
    /// The number of cold compilations performed under the lock across all terminal kinds. A warm hit
    /// does not increment it (test/evidence observability).
    /// </summary>
    internal int CompilationCount => _inner.CompilationCount;

    /// <summary>
    /// Returns the resident writer for <paramref name="plan"/>, compiling and publishing it on a miss.
    /// </summary>
    /// <param name="plan">The frozen shape plan to write rows with.</param>
    /// <returns>The cached or freshly compiled writer.</returns>
    internal JsonRowWriter GetOrBuild(JsonShapePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var key = JsonRowWriterKey.Build(plan);
        if (key is null)
            return JsonRowWriterFactory.Build(plan);

        return (JsonRowWriter)_inner.GetOrBuild("json:" + key, () => JsonRowWriterFactory.Build(plan));
    }

    /// <summary>Drops every entry of the shared terminal cache, releasing the delegates it retained.</summary>
    internal void Clear() => _inner.Clear();
}
