using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>The label of one canonical token in an expression-shape stream.</summary>
internal enum ShapeTokenKind : byte
{
    /// <summary>The visited node was <see langword="null"/>.</summary>
    Null,
    /// <summary>A non-null expression node: kind + CLR type follow.</summary>
    Node,
    /// <summary>A literal constant whose value affects the rendered SQL; the value is compared exactly.</summary>
    Literal,
    /// <summary>A captured object (a compiler-generated closure); only its type takes part in the shape.</summary>
    Closure,
    /// <summary>A member read from a captured object: the member identity is the shape, the value is not.</summary>
    CapturedMember,
    /// <summary>
    /// A member/method/constructor symbol the node carries (a <see cref="MethodCallExpression"/>'s method,
    /// a <see cref="NewExpression"/>'s constructor, an <see cref="IndexExpression"/>'s indexer, a
    /// <see cref="MemberInitExpression"/>'s bound members, an operator overload). The symbol is the shape:
    /// two calls that differ only in the function they invoke must never share a plan.
    /// </summary>
    Symbol,
    /// <summary>A parameter of the lambda being hashed: only its position in canonical order matters.</summary>
    Parameter,
}

/// <summary>
/// One canonical token of an expression shape. Value types only, so a hit-time comparison walks the
/// fresh expression and compares tokens without allocating: the stored side carries types, members
/// and literal values, never a captured closure instance (design §7: a stored plan must not retain
/// caller closures).
/// </summary>
internal readonly struct ShapeToken : IEquatable<ShapeToken>
{
    public ShapeToken(ShapeTokenKind kind, Type? type = null, object? payload = null, long bits = 0)
    {
        Kind = kind;
        Type = type;
        Payload = payload;
        Bits = bits;
    }

    public ShapeTokenKind Kind { get; }

    /// <summary>The CLR type of the node, member owner, closure or literal.</summary>
    public Type? Type { get; }

    /// <summary>A <see cref="MemberInfo"/>/<see cref="MethodInfo"/> or a reference literal (a string).</summary>
    public object? Payload { get; }

    /// <summary>The bit pattern of a primitive literal.</summary>
    public long Bits { get; }

    /// <inheritdoc/>
    public bool Equals(ShapeToken other) =>
        Kind == other.Kind
        && ReferenceEquals(Type, other.Type)
        && Equals(Payload, other.Payload)
        && Bits == other.Bits;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ShapeToken other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)Kind;
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            hash = hash * 31 + (Payload?.GetHashCode() ?? 0);
            hash = hash * 31 + Bits.GetHashCode();
            return hash;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}:{Type?.Name}:{Payload}:{Bits}";
}

/// <summary>
/// The non-expression dimensions of a native shape: the entity and result types, the provider context
/// the plan belongs to and the terminal that will execute it. Two requests that differ in any of them
/// must never share a plan, so they are the first tokens of every shape.
/// </summary>
internal readonly struct ShapeHeader
{
    public ShapeHeader(Type? entityType, Type? resultType, Type? contextType, int terminal, int renderStamp = 0, int sourceStamp = 0)
    {
        EntityType = entityType;
        ResultType = resultType;
        ContextType = contextType;
        Terminal = terminal;
        RenderStamp = renderStamp;
        SourceStamp = sourceStamp;
    }

    public Type? EntityType { get; }

    public Type? ResultType { get; }

    public Type? ContextType { get; }

    public int Terminal { get; }

    /// <summary>
    /// The identifier-rendering options of the context the plan belongs to (quoting, naming convention,
    /// keyword case). Two contexts of the same type may render different SQL, and the shape carries no
    /// rendered text, so the resolved options are part of the identity — see
    /// <see cref="NativeContextStamp"/>.
    /// </summary>
    public int RenderStamp { get; }

    /// <summary>
    /// The physical source identity (table name, qualifiers, entity binding). A source name is rendered
    /// into the statement but carried outside the expression graphs, so without it <c>From("a")</c> and
    /// <c>From("b")</c> would be one shape — see <see cref="NativeSourceStamp"/>.
    /// </summary>
    public int SourceStamp { get; }
}

/// <summary>One captured value slot of a shape: the member path from the closure to the value.</summary>
internal sealed class ShapeCapture
{
    public ShapeCapture(Type closureType, Type valueType, MemberInfo[] path, string name)
    {
        ClosureType = closureType;
        ValueType = valueType;
        Path = path;
        Name = name;
    }

    /// <summary>The compiler-generated closure type the value is read from.</summary>
    public Type ClosureType { get; }

    /// <summary>The captured value's declared type.</summary>
    public Type ValueType { get; }

    /// <summary>The member chain from the closure instance to the value (the path the binder walks).</summary>
    public MemberInfo[] Path { get; }

    /// <summary>The leaf member name, which is the parameter name the miss-time render produced.</summary>
    public string Name { get; }
}

/// <summary>
/// The published identity of one raw shape: a hash for indexing, the canonical token stream for
/// exact structural comparison, and the capture slots the hit path binds.
/// </summary>
internal sealed class ShapeKey
{
    public ShapeKey(ShapeToken[] tokens, ShapeCapture[] captures, int hash)
    {
        Tokens = tokens;
        Captures = captures;
        Hash = hash;
    }

    /// <summary>The canonical token stream (types, members, literal values; never a closure).</summary>
    public ShapeToken[] Tokens { get; }

    /// <summary>The capture slots, in canonical first-appearance order.</summary>
    public ShapeCapture[] Captures { get; }

    /// <summary>The structural hash used to index candidates.</summary>
    public int Hash { get; }
}

/// <summary>
/// Binds the current closure values of a fresh graph into the cached plan's parameters, without
/// building a command. Built once on the miss from the published capture slots and the parameter
/// names the render produced; every entry is a compiled writer, so a hit reads the closure and
/// assigns the parameter without reflection or allocation.
/// </summary>
internal sealed class NativeBinder
{
    private readonly int[] _slotStarts;
    private readonly int[] _parameterIndices;
    private readonly Action<object?, DbParameter>[] _writers;

    private NativeBinder(int[] slotStarts, int[] parameterIndices, Action<object?, DbParameter>[] writers, int parameterCount)
    {
        _slotStarts = slotStarts;
        _parameterIndices = parameterIndices;
        _writers = writers;
        ParameterCount = parameterCount;
    }

    /// <summary>The number of parameters the cached statement declares.</summary>
    public int ParameterCount { get; }

    /// <summary>The number of capture slots the binder covers.</summary>
    public int SlotCount => _slotStarts.Length - 1;

    /// <summary>
    /// Creates the binder for a published shape and the parameter names of the compiled statement, or
    /// returns <see langword="null"/> when the mapping is not a provable bijection (a runtime
    /// placeholder, a stable parameter, an unmatched or duplicated name).
    /// </summary>
    public static NativeBinder? TryCreate(ShapeKey shape, DbParameterCollection parameters)
    {
        if (shape.Captures.Length == 0)
            return parameters.Count == 0 ? new NativeBinder([0], [], [], 0) : null;

        if (parameters.Count == 0)
            return null;

        var start = new int[shape.Captures.Length + 1];
        var parameterIndices = new List<int>(parameters.Count);
        var writers = new List<Action<object?, DbParameter>>(parameters.Count);

        for (var slot = 0; slot < shape.Captures.Length; slot++)
        {
            start[slot] = parameterIndices.Count;
            var capture = shape.Captures[slot];
            var matched = false;

            for (var i = 0; i < parameters.Count; i++)
            {
                if (!string.Equals(parameters[i].ParameterName, capture.Name, StringComparison.Ordinal))
                    continue;

                var writer = CompileWriter(capture);
                if (writer is null)
                    return null;

                parameterIndices.Add(i);
                writers.Add(writer);
                matched = true;
            }

            if (!matched)
                return null;
        }

        start[shape.Captures.Length] = parameterIndices.Count;

        // Every declared parameter must be explained by exactly one capture slot.
        if (parameterIndices.Count != parameters.Count)
            return null;

        return new NativeBinder(start, parameterIndices.ToArray(), writers.ToArray(), parameters.Count);
    }

    /// <summary>
    /// Writes the value of <paramref name="slot"/> (read from <paramref name="closure"/>) into every
    /// parameter bound to it. Allocation-free.
    /// </summary>
    public void Write(int slot, object? closure, DbParameterCollection parameters)
    {
        var from = _slotStarts[slot];
        var to = _slotStarts[slot + 1];
        for (var i = from; i < to; i++)
            _writers[i](closure, parameters[_parameterIndices[i]]);
    }

    private static Action<object?, DbParameter>? CompileWriter(ShapeCapture capture)
    {
        try
        {
            var closure = Expression.Parameter(typeof(object), "closure");
            Expression value = Expression.Convert(closure, capture.ClosureType);
            foreach (var member in capture.Path)
                value = Expression.MakeMemberAccess(value, member);

            var parameter = Expression.Parameter(typeof(DbParameter), "parameter");
            var boxed = Expression.Convert(value, typeof(object));
            var normalized = Expression.Coalesce(boxed, Expression.Constant(DBNull.Value, typeof(object)));
            var assign = Expression.Assign(
                Expression.Property(parameter, nameof(DbParameter.Value)),
                normalized);
            return Expression.Lambda<Action<object?, DbParameter>>(assign, closure, parameter).Compile();
        }
        catch (Exception)
        {
            // A path whose members are not readable (or a type shape the compiler rejects) is not
            // provable; the caller keeps the legacy route.
            return null;
        }
    }
}

/// <summary>
/// Canonical, value-agnostic identity of a raw expression graph.
/// </summary>
/// <remarks>
/// <para>
/// The plan key of the native route cannot be the normalized <see cref="QueryPlan"/> key: that key
/// hashes <c>ConstantExpression.Value</c>, so every fresh display class (one per call in the common
/// "captured local" shape) produces a different key and the native path could never hit. Design §14
/// Q6 forbids keying closure identities as a substitute for binding, so this identity keeps every
/// SQL-affecting dimension (node kinds, CLR types, members, methods, <b>literal</b> values and the
/// canonical capture order) and replaces a captured member with a typed slot. Captured values are
/// bound per execution; the identity never sees them. Every symbol a node carries (a method, a
/// constructor, a member, an indexer, an operator overload, a list-initializer <c>Add</c>) is a token of
/// its own, and a captured read contributes one token per member of its path: two expressions that
/// differ only in <i>which</i> function or column they name are different statements and must never
/// share a plan.
/// </para>
/// <para>
/// The hash indexes candidates only: <see cref="Matches(ShapeKey, in ShapeHeader, LambdaExpression?)"/> compares the fresh graph against the
/// stored token stream, so a hash collision can never reuse a wrong plan. Both directions are
/// allocation-free (thread-static visitor, value-type tokens), which is what keeps the hit path
/// inside its budget.
/// </para>
/// <para>
/// Fail closed: a graph the identity cannot prove (a captured collection, a nested command, an outer
/// reference, an unsupported node payload) yields no identity, and the caller keeps the legacy route.
/// </para>
/// </remarks>
internal static class ShapeIdentity
{
    [ThreadStatic] private static Visitor? _visitor;

    /// <summary>
    /// Computes the structural hash of a fresh graph without materialising the token stream or the
    /// capture slots. This is the index probe: the candidate is then verified by
    /// <see cref="Matches(ShapeKey, in ShapeHeader, LambdaExpression?)"/>, so a hash collision cannot
    /// select a wrong plan.
    /// </summary>
    /// <param name="header">The non-expression dimensions (types, context, terminal).</param>
    /// <param name="root">The fresh expression graph.</param>
    /// <param name="hash">The structural hash.</param>
    /// <returns><see langword="false"/> when the graph is not keyable (fail closed).</returns>
    public static bool TryHash(in ShapeHeader header, LambdaExpression? root, out int hash)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginHash(header);
        var ok = ScanRoot(visitor, root);
        hash = ok ? visitor.Hash : 0;
        return ok;
    }

    /// <summary>Creates the identity of the given roots under <paramref name="header"/>.</summary>
    /// <param name="header">The non-expression dimensions (types, context, terminal).</param>
    /// <param name="roots">The expression graphs that make up the raw shape, in canonical order.</param>
    /// <returns>The identity, or <see langword="null"/> for a graph that must stay on the legacy route.</returns>
    public static ShapeKey? TryCreate(in ShapeHeader header, params LambdaExpression?[] roots)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginCreate(header);

        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i] is not { } root)
            {
                visitor.Add(ShapeTokenKind.Null);
                continue;
            }

            if (!visitor.Scan(root))
                return null;
        }

        return visitor.ToKey();
    }

    /// <summary>
    /// Whether the fresh graphs have the same shape as <paramref name="key"/>. Allocation-free; the
    /// published side holds no closure instance.
    /// </summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="roots">The fresh expression graphs, in canonical order.</param>
    /// <returns><see langword="true"/> when the shapes are identical, capture count included.</returns>
    public static bool Matches(ShapeKey key, in ShapeHeader header, params LambdaExpression?[] roots)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginCompare(key, header);
        try
        {
            for (var i = 0; i < roots.Length; i++)
            {
                if (!ScanRoot(visitor, roots[i]))
                    return false;
            }

            return !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    /// <summary>
    /// Single-graph overload that takes no <c>params</c> array, so the hit path allocates nothing.
    /// </summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="root">The fresh expression graph.</param>
    /// <returns><see langword="true"/> when the shape is identical, capture count included.</returns>
    public static bool Matches(ShapeKey key, in ShapeHeader header, LambdaExpression? root)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginCompare(key, header);
        try
        {
            return ScanRoot(visitor, root) && !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    /// <summary>
    /// Three-graph overload that takes no <c>params</c> array, so the hit path allocates nothing. The
    /// roots are the filter, the grouping key and the projection, in that canonical order.
    /// </summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="root0">The filter graph, or <see langword="null"/>.</param>
    /// <param name="root1">The grouping graph, or <see langword="null"/>.</param>
    /// <param name="root2">The projection graph, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the shape is identical, capture count included.</returns>
    public static bool Matches(
        ShapeKey key,
        in ShapeHeader header,
        LambdaExpression? root0,
        LambdaExpression? root1,
        LambdaExpression? root2)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginCompare(key, header);
        try
        {
            return ScanRoots(visitor, root0, root1, root2) && !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    /// <summary>Three-graph hash probe; no <c>params</c> array (see the single-graph overload).</summary>
    /// <param name="header">The non-expression dimensions (types, context, terminal).</param>
    /// <param name="root0">The filter graph, or <see langword="null"/>.</param>
    /// <param name="root1">The grouping graph, or <see langword="null"/>.</param>
    /// <param name="root2">The projection graph, or <see langword="null"/>.</param>
    /// <param name="hash">The structural hash.</param>
    /// <returns><see langword="false"/> when a graph is not keyable (fail closed).</returns>
    public static bool TryHash(
        in ShapeHeader header,
        LambdaExpression? root0,
        LambdaExpression? root1,
        LambdaExpression? root2,
        out int hash)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginHash(header);
        var ok = ScanRoots(visitor, root0, root1, root2);
        hash = ok ? visitor.Hash : 0;
        return ok;
    }

    /// <summary>Creates the identity of three graphs; no <c>params</c> array.</summary>
    /// <param name="header">The non-expression dimensions (types, context, terminal).</param>
    /// <param name="root0">The filter graph, or <see langword="null"/>.</param>
    /// <param name="root1">The grouping graph, or <see langword="null"/>.</param>
    /// <param name="root2">The projection graph, or <see langword="null"/>.</param>
    /// <returns>The identity, or <see langword="null"/> for a graph that must stay on the legacy route.</returns>
    public static ShapeKey? TryCreate(
        in ShapeHeader header,
        LambdaExpression? root0,
        LambdaExpression? root1,
        LambdaExpression? root2)
    {
        var visitor = _visitor ??= new Visitor();
        visitor.BeginCreate(header);
        return ScanRoots(visitor, root0, root1, root2) ? visitor.ToKey() : null;
    }

    /// <summary>Three-graph bind; no <c>params</c> array.</summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="binder">The published binder.</param>
    /// <param name="parameters">The cached statement's parameter collection.</param>
    /// <param name="root0">The filter graph, or <see langword="null"/>.</param>
    /// <param name="root1">The grouping graph, or <see langword="null"/>.</param>
    /// <param name="root2">The projection graph, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every value was written.</returns>
    public static bool TryBind(
        ShapeKey key,
        in ShapeHeader header,
        NativeBinder binder,
        DbParameterCollection parameters,
        LambdaExpression? root0,
        LambdaExpression? root1,
        LambdaExpression? root2)
    {
        if (parameters.Count != binder.ParameterCount || binder.SlotCount != key.Captures.Length)
            return false;

        var visitor = _visitor ??= new Visitor();
        visitor.BeginBind(key, header, binder, parameters);
        try
        {
            return ScanRoots(visitor, root0, root1, root2) && !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    private static bool ScanRoots(Visitor visitor, LambdaExpression? root0, LambdaExpression? root1, LambdaExpression? root2)
        => ScanRoot(visitor, root0) && ScanRoot(visitor, root1) && ScanRoot(visitor, root2);

    private static bool ScanRoot(Visitor visitor, LambdaExpression? root)
    {
        if (root is { } expression)
            return visitor.Scan(expression);

        visitor.Add(ShapeTokenKind.Null);
        return true;
    }

    /// <summary>
    /// Binds the current closure values of the fresh graph into the cached parameters. The caller must
    /// have established <see cref="Matches(ShapeKey, in ShapeHeader, LambdaExpression?)"/> first; the walk is the same, so the slots line up. All
    /// parameter writes happen only after the whole walk matched a token (a mismatch rejects before any
    /// closure is read).
    /// </summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="binder">The published binder.</param>
    /// <param name="parameters">The cached statement's parameter collection.</param>
    /// <param name="roots">The fresh expression graphs, in canonical order.</param>
    /// <returns><see langword="true"/> when every value was written.</returns>
    public static bool TryBind(
        ShapeKey key,
        in ShapeHeader header,
        NativeBinder binder,
        DbParameterCollection parameters,
        params LambdaExpression?[] roots)
    {
        if (parameters.Count != binder.ParameterCount || binder.SlotCount != key.Captures.Length)
            return false;

        var visitor = _visitor ??= new Visitor();
        visitor.BeginBind(key, header, binder, parameters);
        try
        {
            for (var i = 0; i < roots.Length; i++)
            {
                if (!ScanRoot(visitor, roots[i]))
                    return false;
            }

            return !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    /// <summary>Single-graph overload that takes no <c>params</c> array (allocation-free hit).</summary>
    /// <param name="key">The published identity.</param>
    /// <param name="header">The non-expression dimensions of the fresh request.</param>
    /// <param name="binder">The published binder.</param>
    /// <param name="parameters">The cached statement's parameter collection.</param>
    /// <param name="root">The fresh expression graph.</param>
    /// <returns><see langword="true"/> when every value was written.</returns>
    public static bool TryBind(
        ShapeKey key,
        in ShapeHeader header,
        NativeBinder binder,
        DbParameterCollection parameters,
        LambdaExpression? root)
    {
        if (parameters.Count != binder.ParameterCount || binder.SlotCount != key.Captures.Length)
            return false;

        var visitor = _visitor ??= new Visitor();
        visitor.BeginBind(key, header, binder, parameters);
        try
        {
            return ScanRoot(visitor, root) && !visitor.CompareMismatch && visitor.Complete;
        }
        finally
        {
            visitor.EndCompare();
        }
    }

    private sealed class Visitor : ExpressionVisitor
    {
        private readonly List<ShapeToken> _tokens = new();
        private readonly List<ShapeCapture> _captures = new();
        private readonly List<MemberInfo> _path = new();
        private XxHash32 _hash;

        // Compare / bind mode
        private ShapeKey? _expected;
        private bool _hashOnly;
        private int _cursor;
        private bool _mismatch;
        private int _slot;
        private NativeBinder? _binder;
        private DbParameterCollection? _parameters;

        private bool Comparing => _expected is not null;

        public bool CompareMismatch => _mismatch;

        public bool Complete => _expected is not null && _cursor == _expected.Tokens.Length;

        public void BeginCreate(in ShapeHeader header)
        {
            _tokens.Clear();
            _captures.Clear();
            _hash = new XxHash32();
            _expected = null;
            _hashOnly = false;
            _cursor = 0;
            _mismatch = false;
            _slot = 0;
            _binder = null;
            _parameters = null;
            AddHeader(header);
        }

        public void BeginHash(in ShapeHeader header)
        {
            _hash = new XxHash32();
            _expected = null;
            _hashOnly = true;
            _cursor = 0;
            _mismatch = false;
            _slot = 0;
            _binder = null;
            _parameters = null;
            AddHeader(header);
        }

        public int Hash => _hash.ToHashCode();

        public void BeginCompare(ShapeKey key, in ShapeHeader header)
        {
            _expected = key;
            _cursor = 0;
            _mismatch = false;
            _slot = 0;
            _binder = null;
            _parameters = null;
            AddHeader(header);
        }

        public void BeginBind(ShapeKey key, in ShapeHeader header, NativeBinder binder, DbParameterCollection parameters)
        {
            _expected = key;
            _cursor = 0;
            _mismatch = false;
            _slot = 0;
            _binder = binder;
            _parameters = parameters;
            AddHeader(header);
        }

        public void EndCompare()
        {
            _expected = null;
            _binder = null;
            _parameters = null;
        }

        private void AddHeader(in ShapeHeader header)
        {
            Add(ShapeTokenKind.Literal, header.EntityType);
            Add(ShapeTokenKind.Literal, header.ResultType);
            Add(ShapeTokenKind.Literal, header.ContextType);
            Add(ShapeTokenKind.Literal, typeof(int), null, header.Terminal);
            Add(ShapeTokenKind.Literal, typeof(int), null, header.RenderStamp);
            Add(ShapeTokenKind.Literal, typeof(int), null, header.SourceStamp);
        }

        public bool Scan(Expression root)
        {
            try
            {
                Visit(root);
                return true;
            }
            catch (UnsupportedShapeException)
            {
                return false;
            }
        }

        public ShapeKey? ToKey()
            => _expected is not null || _hashOnly ? null : new ShapeKey(_tokens.ToArray(), _captures.ToArray(), _hash.ToHashCode());

        public void Add(ShapeTokenKind kind, Type? type = null, object? payload = null, long bits = 0)
            => Add(new ShapeToken(kind, type, payload, bits));

        public void Add(ShapeToken token)
        {
            if (_expected is not null)
            {
                if (_cursor >= _expected.Tokens.Length || !_expected.Tokens[_cursor].Equals(token))
                    _mismatch = true;

                _cursor++;
                return;
            }

            if (!_hashOnly)
                _tokens.Add(token);

            _hash.Add((int)token.Kind);
            _hash.Add(token.Type is null ? 0 : RuntimeHelpers.GetHashCode(token.Type));
            // Value equality for literals and reference identity for members/methods: Payload.Equals
            // and GetHashCode are the same contract ShapeToken.Equals uses.
            _hash.Add(token.Payload?.GetHashCode() ?? 0);
            _hash.Add(token.Bits);
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                Add(ShapeTokenKind.Null);
                return null;
            }

            Add(ShapeTokenKind.Node, node.Type, null, (int)node.NodeType);
            return base.Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Add(ShapeTokenKind.Parameter, node.Type, node.Name);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            AddConstant(node);
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // A value read from a captured object is a bind slot, not part of the shape's identity.
            if (TryFindClosureRoot(node, out var closure))
            {
                // A captured command / queryable changes the rendered statement by its own graph, and a
                // captured collection is the uncacheable shape the baseline keeps on the legacy route.
                if (typeof(QueryCommand).IsAssignableFrom(node.Type) || typeof(IQueryable).IsAssignableFrom(node.Type))
                    throw new UnsupportedShapeException();

                if (node.Type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(node.Type))
                    throw new UnsupportedShapeException();

                // The closure *type* is part of the shape: the binder reads the member from an
                // instance of exactly that type, so a capture from another call site's display class
                // must not share the plan even when the member name and value type coincide.
                Add(ShapeTokenKind.Closure, closure.Type);

                // The whole member path from the closure to the value is part of the shape: `holder.First.X`
                // and `holder.Second.X` (same leaf name and type) are different columns.
                AddCapturePath(node);

                Add(ShapeTokenKind.CapturedMember, node.Type, node.Member);

                if (_binder is { } binder && _parameters is { } parameters)
                {
                    if (_slot >= binder.SlotCount)
                        throw new UnsupportedShapeException();

                    binder.Write(_slot, closure.Value, parameters);
                }

                if (!Comparing)
                    _captures.Add(new ShapeCapture(closure.Type, node.Type, BuildPath(node), node.Member.Name));

                _slot++;
                return node;
            }

            // A column/entity member access is the column the statement reads: the member identity must be
            // part of the shape, or `x.Id` and `x.Value` would be indistinguishable.
            Add(ShapeTokenKind.Symbol, node.Member.DeclaringType, node.Member);
            return base.VisitMember(node);
        }

        /// <summary>
        /// Emits one symbol token per member of the captured path, outermost first. The list is reused, so
        /// the hit path stays allocation-free.
        /// </summary>
        private void AddCapturePath(MemberExpression node)
        {
            _path.Clear();
            Expression? current = node;
            while (current is MemberExpression member && member.Expression is not null)
            {
                _path.Add(member.Member);
                current = member.Expression;
            }

            for (var i = _path.Count - 1; i >= 0; i--)
                Add(ShapeTokenKind.Symbol, _path[i].DeclaringType, _path[i]);
        }

        protected override Expression VisitListInit(ListInitExpression node)
        {
            // A collection initializer renders its Add calls, so the overloads take part in the shape.
            for (var i = 0; i < node.Initializers.Count; i++)
            {
                var addMethod = node.Initializers[i].AddMethod;
                Add(ShapeTokenKind.Symbol, addMethod.DeclaringType, addMethod);
            }

            return base.VisitListInit(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            // An indexed read over a captured array/collection is the captured-collection shape the
            // baseline keeps on the legacy route; do not key it.
            if (node.Object is ConstantExpression { Value: not null } indexRoot && IsClosureLike(indexRoot.Value))
                throw new UnsupportedShapeException();

            // Two different indexers on one type (a dictionary vs a list member, a custom this[]) render
            // different SQL, so the indexer symbol is part of the shape.
            if (node.Indexer is not null)
                Add(ShapeTokenKind.Symbol, node.Indexer.DeclaringType, node.Indexer);

            return base.VisitIndex(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // The invoked function is the statement: two calls that differ only in the method (sha1 vs
            // md5, upper vs lower, to_int16 vs reinterpret_as_uint16) must never share a shape.
            Add(ShapeTokenKind.Symbol, node.Method.DeclaringType, node.Method);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            // A projection over a DTO commits to one constructor; another constructor of the same type is
            // another statement.
            if (node.Constructor is not null)
                Add(ShapeTokenKind.Symbol, node.Constructor.DeclaringType, node.Constructor);

            return base.VisitNew(node);
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            // `new T { A = .. }` and `new T { B = .. }` project different columns. Only member assignments
            // are provable; a list/member binding is not, so it fails closed.
            for (var i = 0; i < node.Bindings.Count; i++)
            {
                if (node.Bindings[i] is not MemberAssignment assignment)
                    throw new UnsupportedShapeException();

                Add(ShapeTokenKind.Symbol, assignment.Member.DeclaringType, assignment.Member);
            }

            return base.VisitMemberInit(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            // An overloaded operator renders its own SQL; the built-in ones are already distinguished by
            // the node type.
            if (node.Method is not null)
                Add(ShapeTokenKind.Symbol, node.Method.DeclaringType, node.Method);

            return base.VisitBinary(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Method is not null)
                Add(ShapeTokenKind.Symbol, node.Method.DeclaringType, node.Method);

            return base.VisitUnary(node);
        }

        private void AddConstant(ConstantExpression node)
        {
            var value = node.Value;
            if (value is null)
            {
                Add(ShapeTokenKind.Literal, node.Type, null, 0);
                return;
            }

            if (value is QueryCommand || value is IQueryable)
                throw new UnsupportedShapeException();

            if (IsLiteral(value))
            {
                Add(ShapeTokenKind.Literal, node.Type ?? value.GetType(), LiteralPayload(value), LiteralBits(value));
                return;
            }

            if (!IsClosureLike(value))
                throw new UnsupportedShapeException();

            Add(ShapeTokenKind.Closure, value.GetType());
        }

        private static bool IsLiteral(object value) =>
            value.GetType().IsPrimitive
            || value is string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum
            || value is Type or DateOnly or TimeOnly;

        private static object? LiteralPayload(object value) =>
            value is string or Type or decimal or Guid or DateTimeOffset or DateOnly or TimeOnly ? value : null;

        private static long LiteralBits(object value) => value switch
        {
            bool b => b ? 1 : 0,
            byte b => b,
            sbyte b => b,
            short s => s,
            ushort s => s,
            int i => i,
            uint i => i,
            long l => l,
            ulong l => unchecked((long)l),
            char c => c,
            float f => BitConverter.SingleToInt32Bits(f),
            double d => BitConverter.DoubleToInt64Bits(d),
            DateTime d => d.Ticks,
            TimeSpan t => t.Ticks,
            Enum e => Convert.ToInt64(e),
            _ => 0,
        };

        // A capture is a reference-type object that is not a literal and not a collection; a value-type
        // constant that is not a known literal cannot be compared exactly, so it fails closed instead.
        private static bool IsClosureLike(object value) =>
            !value.GetType().IsValueType && value is not string and not Type and not IEnumerable;

        private static bool TryFindClosureRoot(MemberExpression node, out ConstantExpression root)
        {
            Expression? current = node;
            while (current is MemberExpression member && member.Expression is not null)
                current = member.Expression;

            if (current is ConstantExpression { Value: not null } constant && IsClosureLike(constant.Value))
            {
                root = constant;
                return true;
            }

            root = null!;
            return false;
        }

        private static MemberInfo[] BuildPath(MemberExpression node)
        {
            var path = new List<MemberInfo>(4);
            Expression? current = node;
            while (current is MemberExpression member && member.Expression is not null)
            {
                path.Add(member.Member);
                current = member.Expression;
            }

            path.Reverse();
            return path.ToArray();
        }
    }

    private sealed class UnsupportedShapeException : Exception;
}
