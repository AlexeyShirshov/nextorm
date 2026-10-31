using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextORM.Core;

/// <summary>
/// Static classification helpers for expressions and CLR types shared across the query pipeline:
/// whether an expression already renders as a predicate and whether a numeric conversion needs an
/// explicit <c>cast(...)</c> (used by the visitors), plus the single-column projection shape and the
/// <see cref="System.Tuple"/> shape (used by the query preparer and the row reader).
/// </summary>
internal static class TypeFacts
{
    /// <summary>True for a value whose SQL rendering has to be a boolean scalar.</summary>
    internal static bool IsBoolean(Type type) => type == typeof(bool) || type == typeof(bool?);

    /// <summary>
    /// Strips <see cref="ExpressionType.Convert"/>/<see cref="ExpressionType.ConvertChecked"/> wrappers
    /// (the compiler's enum-comparison and boxing lowering) so the underlying operand is inspected.
    /// </summary>
    internal static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;

    /// <summary>
    /// True when the expression is a null literal, possibly behind method-free conversions that
    /// preserve null. The compiler lowers <c>(int?)null</c> to
    /// <c>Convert(Constant(null, object), int?)</c>, so a plain <see cref="ConstantExpression"/>
    /// test misses the shape the nullable-comparison paths have to recognise. Only
    /// <see cref="ExpressionType.Convert"/>/<see cref="ExpressionType.ConvertChecked"/> nodes
    /// without a user-defined method and whose target is nullable or a reference type are
    /// unwrapped; a conversion to a non-nullable value type is not null-preserving. Members,
    /// calls, closures and every other node are not evaluated and classify as <see langword="false"/>.
    /// </summary>
    internal static bool IsNullLiteral(Expression expression) => expression switch
    {
        ConstantExpression { Value: null } => true,
        UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } unary
            when IsNullPreservingConversion(unary.Type) => IsNullLiteral(unary.Operand),
        _ => false
    };

    private static bool IsNullPreservingConversion(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    /// <summary>
    /// True for a captured/closure/static member operand whose declared type is a nullable value type
    /// or a <see cref="string"/>. The classification is purely structural: the access chain has to end
    /// at a closure host (<see cref="ConstantExpression"/>) or at a static member (no receiver), which
    /// is exactly the shape <see cref="MemberTranslator"/> folds into a captured parameter. A mapped
    /// column (<c>x.Int</c>) still references a query parameter and therefore classifies as
    /// <see langword="false"/>. The value is never evaluated, so the shape never depends on it.
    /// <see cref="string.Empty"/> is excluded: it is rendered as the dialect's empty-string literal,
    /// not as a captured parameter.
    /// </summary>
    internal static bool IsCapturedNullableOrStringOperand(Expression expression)
    {
        expression = UnwrapConvert(expression);
        if (expression is not MemberExpression member)
            return false;

        if (member.Expression is null
            && member.Member.DeclaringType == typeof(string)
            && member.Member.Name == nameof(string.Empty))
            return false;

        var type = member.Type;
        if (type != typeof(string) && Nullable.GetUnderlyingType(type) is null)
            return false;

        var root = member.Expression;
        while (root is MemberExpression nested)
            root = nested.Expression;

        return root is null or ConstantExpression;
    }

    /// <summary>
    /// True for the mapped <c>Nullable&lt;T&gt;.HasValue</c> member access on a row-dependent
    /// receiver (a query column), which translates to a <c>is [not] null</c> presence predicate. A
    /// captured value's own <c>HasValue</c> has no query parameter in its receiver chain and keeps its
    /// ordinary evaluation.
    /// </summary>
    internal static bool IsPresenceMember(MemberExpression member)
        => member.Member.Name == nameof(Nullable<int>.HasValue)
        && member.Member.DeclaringType is { } declaring
        && Nullable.GetUnderlyingType(declaring) is not null
        && member.Expression is { } receiver
        && receiver.Has<ParameterExpression>();

    /// <summary>
    /// True when <paramref name="expression"/> is a predicate whose rendering needs the approved
    /// null-safe translation (a captured nullable/string comparison or a mapped presence form). Used
    /// to decide whether a boolean <c>SELECT</c> projection is assembled in predicate context and
    /// legalised once at its scalar boundary; every other projection keeps its previous translation.
    /// </summary>
    internal static bool IsNullSafePredicateRoot(Expression expression)
        => ProducesPredicate(expression) && ContainsNullSafeOperation(expression);

    /// <summary>
    /// True when the expression is a predicate node that the dialect has to legalise in a scalar
    /// position. Bare boolean values (a bit column, a CASE) are excluded: they are already values.
    /// </summary>
    private static bool ProducesPredicate(Expression expression) => expression.NodeType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.AndAlso or ExpressionType.OrElse
            or ExpressionType.Not => true,
        ExpressionType.MemberAccess => IsPresenceMember((MemberExpression)expression),
        _ => false,
    };

    /// <summary>
    /// True when the expression tree contains a captured nullable/string comparison or a mapped
    /// presence form that the approved translation applies to.
    /// </summary>
    internal static bool ContainsNullSafeOperation(Expression expression)
    {
        var finder = new NullSafeOperationFinder();
        finder.Visit(expression);
        return finder.Found;
    }

    /// <summary>Depth-first search for the approved null-safe operations; stops at the first hit.</summary>
    private sealed class NullSafeOperationFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found || node is null)
                return node;

            if (node is BinaryExpression { Type: var type } binary
                && type == typeof(bool)
                && binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                && !IsNullLiteral(binary.Left)
                && !IsNullLiteral(binary.Right)
                && IsCapturedNullableOrStringOperand(binary.Left) != IsCapturedNullableOrStringOperand(binary.Right))
            {
                Found = true;
                return node;
            }

            if (node is MemberExpression member && IsPresenceMember(member))
            {
                Found = true;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>
    /// True when the expression already renders as a predicate. Comparison/logical operators,
    /// boolean CASE/COALESCE and the recognised predicate methods produce a condition directly;
    /// anything else (a column, a constant, an arithmetic result) is a boolean value that the
    /// dialect has to convert to a predicate where a condition is required.
    /// </summary>
    internal static bool IsPredicate(Expression expression) => expression.NodeType switch
    {
        ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.AndAlso or ExpressionType.OrElse
            or ExpressionType.Not or ExpressionType.Coalesce
            or ExpressionType.Conditional or ExpressionType.Switch => true,
        ExpressionType.Call => IsPredicateCall((MethodCallExpression)expression),
        ExpressionType.MemberAccess => IsPresenceMember((MemberExpression)expression),
        _ => false
    };

    /// <summary>True for the method calls that the visitor renders as a predicate (LIKE/IN/EXISTS/full-text).</summary>
    internal static bool IsPredicateCall(MethodCallExpression call)
    {
        if (call.Method.DeclaringType == typeof(string))
            return call.Method.Name is nameof(string.Contains) or nameof(string.StartsWith)
                or nameof(string.EndsWith) or nameof(string.IsNullOrEmpty);

        if (call.Method.DeclaringType == typeof(System.Text.RegularExpressions.Regex))
            return call.Method.Name is nameof(System.Text.RegularExpressions.Regex.IsMatch);

        return call.Method.Name is nameof(CommonFunctions.exists) or nameof(CommonFunctions.any)
            or nameof(CommonFunctions.all) or nameof(CommonFunctions.@in) or nameof(ClickHouseFunctions.global_in)
            or "Contains"
            or nameof(CommonFunctions.contains) or nameof(CommonFunctions.freetext)
            or nameof(SqlServerFunctions.isjson)
            or nameof(SqlServerFunctions.json_contains) or nameof(SqlServerFunctions.json_path_exists);
    }

    /// <summary>
    /// True for a type a projection maps to a single column: the primitives, strings, dates, decimals,
    /// GUIDs and nullables, plus an array (<c>T[]</c>, covering binary <c>byte[]</c>). A bare
    /// <see cref="JsonNode"/> is included by exact type (the native <c>json</c>/<c>jsonb</c> read path);
    /// the derived <c>JsonObject</c>/<c>JsonArray</c>/<c>JsonValue</c> are not. A tuple is
    /// deliberately excluded because <c>new Tuple&lt;...&gt;(a, b)</c> is a multi-column constructor
    /// projection; a native <c>Tuple(...)</c> column is recognised separately by
    /// <see cref="IsTupleType"/> at the non-<c>NewExpression</c> call site.
    /// </summary>
    internal static bool IsSingleColumnProjection(Type type) =>
        type.IsPrimitive
        || type == typeof(string)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(decimal)
        || type == typeof(Guid)
        || type == typeof(JsonNode)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        || type.IsArray;

    /// <summary>
    /// True for the JSON DOM scalar types that are explicitly named as a bare top-level projection
    /// limitation: <see cref="JsonObject"/>, <see cref="JsonDocument"/> and <see cref="JsonElement"/>.
    /// These have a provider representation (native <c>JSON</c>) but no single-column projection shape
    /// of their own, so <c>Select(x =&gt; x.Doc)</c> over such a property (with no <c>[JsonColumn]</c>
    /// converter) is rejected up front. The exact <see cref="JsonNode"/> type is deliberately excluded:
    /// it is a recognised single-column scalar (see <see cref="IsSingleColumnProjection"/>), and a
    /// <see cref="Nullable{T}"/> wrapping <see cref="JsonElement"/> is handled by the nullable branch
    /// there. Used by the query preparer to fail fast instead of building an empty select list.
    /// </summary>
    internal static bool IsBareDomScalar(Type type) =>
        type == typeof(JsonObject) || type == typeof(JsonDocument) || type == typeof(JsonElement);

    /// <summary>
    /// True for the <see cref="System.Tuple"/> family (arity 1..7), the CLR shape the ClickHouse
    /// driver returns for a <c>Tuple(...)</c> column. The &gt;7-element driver type is internal and
    /// cannot be declared, so it is intentionally not recognised.
    /// </summary>
    internal static bool IsTupleType(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(Tuple<>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,,>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,,,>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,,,,>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,>)
            || type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,>));

    /// <summary>
    /// True for the <see cref="ValueTuple"/> family (arity 1..7), the value-type tuple shape a
    /// projection can also construct.
    /// </summary>
    internal static bool IsValueTupleType(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(ValueTuple<>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,,>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>)
            || type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,>));

    /// <summary>True for either the <see cref="Tuple"/> or the <see cref="ValueTuple"/> family.</summary>
    internal static bool IsTupleLike(Type type) => IsTupleType(type) || IsValueTupleType(type);

    /// <summary>
    /// True for any tuple-shaped type the ClickHouse type renderer can decompose into a
    /// <c>Tuple(...)</c> definition: the <see cref="Tuple"/>/<see cref="ValueTuple"/> families
    /// at any arity (including the 8-element <c>Rest</c> form), a <see cref="Nullable{T}"/>
    /// wrapping one of them, and any generic type implementing
    /// <see cref="System.Runtime.CompilerServices.ITuple"/>. Used so tuple elements never fall
    /// through to the CLR name ``Tuple`N``.
    /// </summary>
    internal static bool IsTupleRenderingType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsTupleFamily(type))
            return true;
        return typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type);
    }

    /// <summary>
    /// True for any arity of the <see cref="System.Tuple"/> / <see cref="System.ValueTuple"/> families,
    /// including the 8-element <c>Rest</c> form that <see cref="IsTupleLike"/> deliberately excludes, and
    /// for a <see cref="Nullable{T}"/> wrapping a value-tuple (the recognisable nullable tuple shape; a
    /// nullable <see cref="System.Tuple"/> is not legal CLR). Used to route a tuple-valued value list to
    /// the tuple translator so an unsupported arity or a nullable element is rejected with an explicit
    /// message instead of falling through to the scalar value-list path.
    /// </summary>
    internal static bool IsTupleFamily(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!type.IsGenericType)
            return false;

        var name = type.GetGenericTypeDefinition().FullName;
        return name is not null
            && (name.StartsWith("System.Tuple`", StringComparison.Ordinal)
                || name.StartsWith("System.ValueTuple`", StringComparison.Ordinal));
    }

    /// <summary>
    /// The number of components of a tuple type in the <see cref="Tuple"/>/<see cref="ValueTuple"/>
    /// family (arities 1..7 are supported by the tuple value-list renderer; the 8-element <c>Rest</c>
    /// form is out of scope), or <c>0</c> for any other type. A <see cref="Nullable{T}"/> wrapper is
    /// unwrapped so a nullable value-tuple reports the arity of its underlying tuple.
    /// </summary>
    internal static int TupleArity(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return IsTupleFamily(type) ? type.GetGenericArguments().Length : 0;
    }

    /// <summary>
    /// True when a numeric CLR conversion actually changes the type and therefore has to be emitted
    /// as <c>cast(...)</c>; dropping it would silently change the SQL semantics.
    /// </summary>
    internal static bool TryGetNumericConversion(Type source, Type target, out Type underlyingTarget)
    {
        static Type Unwrap(Type t) => Nullable.GetUnderlyingType(t) ?? t;
        static bool IsNumeric(Type t) => t == typeof(byte) || t == typeof(short) || t == typeof(int)
            || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        // Unsigned CLR types (ClickHouse's native UInt*) are valid conversion sources; the target must
        // still be a type the dialects know how to name.
        static bool IsConversionSource(Type t) => IsNumeric(t) || t == typeof(sbyte)
            || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong);

        var unwrappedSource = Unwrap(source);
        underlyingTarget = Unwrap(target);

        return IsConversionSource(unwrappedSource) && IsNumeric(underlyingTarget) && unwrappedSource != underlyingTarget;
    }
}
