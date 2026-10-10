using System.Reflection;

namespace NextORM.Core;

/// <summary>The kind of a node in a recursively captured JSON projection shape.</summary>
internal enum JsonShapeNodeKind
{
    /// <summary>A leaf value read from one prepared reader ordinal (or a scalar array element type).</summary>
    Scalar,

    /// <summary>A JSON object built from named members.</summary>
    Object,

    /// <summary>A JSON array built from a declared rank-one array type.</summary>
    Array,
}

/// <summary>How an object node's presence is decided independently of its property values.</summary>
internal enum JsonShapePresenceKind
{
    /// <summary>Explicit construction: the object is always present, even when every property is null.</summary>
    Always,

    /// <summary>An entity slot on a join: absent when every mapped column of the slot reads SQL NULL.</summary>
    AnyColumnNotNull,
}

/// <summary>
/// The explicit reader-ordinal binding of one scalar JSON leaf. The ordinal is assigned by the JSON-only
/// lowering and is never reconstructed from a SQL alias, a CLR property name or traversal order.
/// </summary>
internal readonly struct JsonShapeBinding
{
    /// <summary>Initializes a leaf binding.</summary>
    /// <param name="ordinal">The zero-based prepared reader ordinal.</param>
    /// <param name="nullable">Whether the leaf can read SQL NULL.</param>
    /// <param name="defaultOnNull">Whether SQL NULL means "no row" and must be written as <c>default(T)</c>.</param>
    internal JsonShapeBinding(int ordinal, bool nullable, bool defaultOnNull)
    {
        Ordinal = ordinal;
        Nullable = nullable;
        DefaultOnNull = defaultOnNull;
    }

    /// <summary>The zero-based prepared reader ordinal.</summary>
    public int Ordinal { get; }

    /// <summary>Whether the leaf can read SQL NULL.</summary>
    public bool Nullable { get; }

    /// <summary>Whether SQL NULL means "no row" (a <c>*OrDefault</c> scalar) and must be written as <c>default(T)</c>.</summary>
    public bool DefaultOnNull { get; }
}

/// <summary>
/// The presence rule of an object node. It is intentionally independent of the visible property values so
/// an all-null constructed object is not confused with an absent outer-join entity.
/// </summary>
internal readonly struct JsonShapePresence
{
    private JsonShapePresence(JsonShapePresenceKind kind, int[] ordinals)
    {
        Kind = kind;
        Ordinals = ordinals;
    }

    /// <summary>The presence rule kind.</summary>
    public JsonShapePresenceKind Kind { get; }

    /// <summary>The ordinals whose raw (pre-<c>DefaultOnNull</c>) values feed the predicate; empty for <see cref="JsonShapePresenceKind.Always"/>.</summary>
    public int[] Ordinals { get; }

    /// <summary>An explicit construction: always present.</summary>
    public static JsonShapePresence Always { get; } = new(JsonShapePresenceKind.Always, []);

    /// <summary>An entity slot: present when at least one mapped ordinal is not SQL NULL.</summary>
    /// <param name="ordinals">The slot's mapped reader ordinals.</param>
    public static JsonShapePresence AnyColumnNotNull(int[] ordinals) => new(JsonShapePresenceKind.AnyColumnNotNull, ordinals);
}

/// <summary>
/// A recursively captured JSON projection shape: an object tree with explicit scalar-ordinal bindings, a
/// separately represented object-presence rule, slot grouping for expanded entity items and array element
/// classification. The phase-1 flat writer never consumes a captured shape; the recursive writer
/// (phase 2) consumes the whole tree.
/// </summary>
internal sealed class JsonShapeNode
{
    private JsonShapeNode(
        JsonShapeNodeKind kind,
        string? name,
        Type declaredType,
        JsonShapeBinding? binding,
        JsonShapeNode[] members,
        JsonShapeNode? element,
        JsonShapePresence presence,
        int? slot,
        MemberInfo? member)
    {
        Kind = kind;
        Name = name;
        DeclaredType = declaredType;
        Binding = binding;
        Members = members;
        Element = element;
        Presence = presence;
        Slot = slot;
        Member = member;
    }

    /// <summary>The node kind.</summary>
    public JsonShapeNodeKind Kind { get; }

    /// <summary>The resolved result member name (raw CLR name, before any naming policy), or <see langword="null"/> at the root or for an unbound array element.</summary>
    public string? Name { get; }

    /// <summary>The declared CLR type of the node (the member/parameter type, or the array type for an array node).</summary>
    public Type DeclaredType { get; }

    /// <summary>The reader-ordinal binding for a scalar leaf or an array column; <see langword="null"/> for an unbound array element.</summary>
    public JsonShapeBinding? Binding { get; }

    /// <summary>The object members, in result-set order; empty for scalar and array nodes.</summary>
    public JsonShapeNode[] Members { get; }

    /// <summary>The array element node, or <see langword="null"/> for scalar and object nodes.</summary>
    public JsonShapeNode? Element { get; }

    /// <summary>The object presence rule; <see cref="JsonShapePresence.Always"/> for scalar and array nodes.</summary>
    public JsonShapePresence Presence { get; }

    /// <summary>The zero-based projection item slot for an expanded entity object, or <see langword="null"/>.</summary>
    public int? Slot { get; }

    /// <summary>The projection member an expanded entity object is assigned to, or <see langword="null"/> for a constructor position.</summary>
    public MemberInfo? Member { get; }

    /// <summary>Creates a scalar leaf node.</summary>
    public static JsonShapeNode Scalar(string? name, Type declaredType, JsonShapeBinding? binding)
        => new(JsonShapeNodeKind.Scalar, name, declaredType, binding, [], null, JsonShapePresence.Always, null, null);

    /// <summary>Creates an object node.</summary>
    public static JsonShapeNode Object(
        string? name,
        Type declaredType,
        JsonShapeNode[] members,
        JsonShapePresence presence,
        int? slot,
        MemberInfo? member)
        => new(JsonShapeNodeKind.Object, name, declaredType, null, members, null, presence, slot, member);

    /// <summary>Creates an array node; <paramref name="binding"/> is the whole-column ordinal for a root/member array or <see langword="null"/> for an unbound nested array element.</summary>
    public static JsonShapeNode Array(string? name, Type declaredType, JsonShapeNode element, JsonShapeBinding? binding)
        => new(JsonShapeNodeKind.Array, name, declaredType, binding, [], element, JsonShapePresence.Always, null, null);
}
