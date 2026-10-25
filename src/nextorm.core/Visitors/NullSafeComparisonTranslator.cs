using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the explicit null-safe comparison helpers
/// (<see cref="CommonFunctions.is_distinct_from{T}(T, T)"/> and
/// <see cref="CommonFunctions.is_not_distinct_from{T}(T, T)"/>) into an always-two-valued SQL
/// predicate. Operand validation, literal-NULL recognition, conservative folding, dialect invocation
/// and Boolean-context shaping live here so the shared dispatch chain gains one branch and no
/// provider-name switch.
/// </summary>
/// <remarks>
/// The operator is explicit: ordinary <c>==</c>/<c>!=</c> translation is untouched and keeps the SQL
/// NULL/UNKNOWN semantics. The dialect owns the rendering of both forms
/// (<see cref="ISqlDialect.NullSafeComparison"/>); a provider without the capability is rejected
/// before any SQL is emitted. Parameter values are never folded: a captured nullable operand stays a
/// parameter, so a captured NULL works and never specializes the cached SQL to the current value.
/// </remarks>
internal static class NullSafeComparisonTranslator
{
    /// <summary>The deterministic diagnostic for operands whose CLR types cannot be compared.</summary>
    internal const string IncompatibleOperandsMessage = "Null-safe comparison requires compatible operand types.";

    /// <summary>
    /// The deterministic diagnostic for a captured operand whose SQL type cannot be resolved on a
    /// provider that requires an explicit type for comparison operands (see
    /// <see cref="INullSafeComparisonRenderer.RequiresTypedNullOperands"/>). Raised at translation
    /// time, so a malformed statement never reaches the server.
    /// </summary>
    internal const string UnmappedNullOperandMessage = "Null-safe comparison requires a SQL Server type mapping for null operands.";

    /// <summary>
    /// Returns <c>false</c> for a call that is not one of the two null-safe comparison helpers (or
    /// whose declaring type is unrelated to <see cref="CommonFunctions"/>); otherwise translates it
    /// (or throws, like the other translator branches) and returns <c>true</c>.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        // PG_SQL derives from CommonFunctions, so IsAssignableFrom covers both surfaces.
        if (node.Method.DeclaringType is not { } declaring
            || !typeof(CommonFunctions).IsAssignableFrom(declaring))
            return false;

        bool distinct;
        if (node.Method.Name == nameof(CommonFunctions.is_distinct_from))
            distinct = true;
        else if (node.Method.Name == nameof(CommonFunctions.is_not_distinct_from))
            distinct = false;
        else
            return false;

        if (node.Arguments is not [Expression left, Expression right])
            throw new NotSupportedException(IncompatibleOperandsMessage);

        // Classification looks through the compiler's conversion wrappers (the generic operand type
        // lowering), while the rendering below keeps the original expression so a meaningful cast is
        // never dropped.
        var leftOperand = TypeFacts.UnwrapConvert(left);
        var rightOperand = TypeFacts.UnwrapConvert(right);

        var leftIsNull = IsNullLiteral(leftOperand);
        var rightIsNull = IsNullLiteral(rightOperand);

        // A NULL literal is typed by the other operand, so only two non-NULL operands are checked.
        if (!leftIsNull && !rightIsNull)
            ValidateCompatibleOperands(leftOperand, rightOperand);

        // Expression-derived provider type of each captured operand: the compared column's mapped or
        // converter provider type first, then the operand's own declared CLR type. Resolved from
        // metadata only, never from a runtime value, so a captured NULL binds exactly like every later
        // value and the cached SQL shape never depends on the current value.
        var leftTypeInfo = MemberTranslator.ResolveParameterType(visitor, right, left);
        var rightTypeInfo = MemberTranslator.ResolveParameterType(visitor, left, right);

        if (visitor.IsParamMode)
        {
            // Mirror the SQL pass: the same operands with the same type context, so the extracted
            // parameter metadata is identical in both passes.
            VisitOperand(visitor, left, leftTypeInfo);
            VisitOperand(visitor, right, rightTypeInfo);
            return true;
        }

        var capability = visitor.Dialect.NullSafeComparison;
        var formName = distinct ? nameof(CommonFunctions.is_distinct_from) : nameof(CommonFunctions.is_not_distinct_from);

        // Fail closed per form: an unrelated provider must reject the helper instead of receiving SQL
        // it cannot execute. The message names both the provider and the requested form.
        if (capability is null || (distinct ? !capability.SupportsDistinct : !capability.SupportsNotDistinct))
            throw new NotSupportedException(
                $"Null-safe comparison ({formName}) is not supported by the {visitor.Dialect.GetType().Name} provider.");

        // A provider that cannot bind an untyped comparison operand needs the SQL type before the
        // statement is built: the column's mapped/converter provider type first, then the operand's
        // own declared CLR type (see MemberTranslator.ResolveParameterType). No mapping means the
        // comparison is rejected here, never sent to the server with an untyped NULL operand.
        if (capability.RequiresTypedNullOperands
            && ((IsCapturedOperand(left) && leftTypeInfo is null)
                || (IsCapturedOperand(right) && rightTypeInfo is null)))
            throw new NotSupportedException(UnmappedNullOperandMessage);

        visitor.NeedAliasForColumn = true;

        string predicate;

        if (leftIsNull && rightIsNull)
        {
            // Two NULL literals are statically equal, so the comparison is a constant.
            predicate = distinct ? "1 = 0" : "1 = 1";
        }
        else if (leftIsNull || rightIsNull)
        {
            // Exactly one NULL literal: presence test, already two-valued. The operand is emitted in
            // the second position because `is` accepts only `<operand> is [not] null`.
            var operand = leftIsNull
                ? VisitOperandToString(visitor, right, rightTypeInfo)
                : VisitOperandToString(visitor, left, leftTypeInfo);
            predicate = $"({operand}{visitor.Kw(distinct ? " is not null" : " is null")})";
        }
        else if (CanUseOrdinaryComparison(leftOperand, rightOperand))
        {
            // Both operands are statically non-nullable CLR values that no SQL null extension can
            // reach, so ordinary equality is already total.
            var fastLeft = VisitOperandToString(visitor, left, leftTypeInfo);
            var fastRight = VisitOperandToString(visitor, right, rightTypeInfo);
            predicate = $"({fastLeft}{(distinct ? " <> " : " = ")}{fastRight})";
        }
        else
        {
            var sqlLeft = VisitOperandToString(visitor, left, leftTypeInfo);
            var sqlRight = VisitOperandToString(visitor, right, rightTypeInfo);
            predicate = distinct
                ? capability.RenderDistinct(sqlLeft, sqlRight)
                : capability.RenderNotDistinct(sqlLeft, sqlRight);
        }

        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(predicate, visitor.IsPredicateContext));
        return true;
    }

    /// <summary>
    /// True for a NULL literal: a <see cref="ConstantExpression"/> holding <see langword="null"/>.
    /// The operand arrives already unwrapped by <see cref="TypeFacts.UnwrapConvert"/>, so the typed
    /// NULL (<c>(int?)null</c>) and <c>default(int?)</c> shapes are recognised as well. The predicate
    /// is deliberately local — the shared ordinary-comparison null-literal classification is owned by
    /// a sibling change and is not consumed here.
    /// </summary>
    private static bool IsNullLiteral(Expression expression) => expression is ConstantExpression { Value: null };

    /// <summary>
    /// Visits a captured operand under the expression-derived provider type, restoring the previous
    /// type context afterwards so the scope stays local to one operand (mirrors the ordinary
    /// comparison path).
    /// </summary>
    private static void VisitOperand(BaseExpressionVisitor visitor, Expression operand, ParameterTypeInfo? typeInfo)
    {
        var previous = visitor.ParameterTypeContext;
        if (typeInfo is { } info)
            visitor.ParameterTypeContext = info;

        try
        {
            visitor.Visit(operand);
        }
        finally
        {
            visitor.ParameterTypeContext = previous;
        }
    }

    /// <summary>The <see cref="VisitOperand"/> counterpart for a rendered operand.</summary>
    /// <remarks>
    /// A rendered operand goes through a cloned visitor (that is how a nested expression is rendered
    /// independently), so the type context has to be set on that clone — setting it on the caller
    /// would be discarded.
    /// </remarks>
    private static string VisitOperandToString(BaseExpressionVisitor visitor, Expression operand, ParameterTypeInfo? typeInfo)
    {
        using var clone = visitor.Clone();
        if (typeInfo is { } info)
            clone.ParameterTypeContext = info;

        clone.Visit(operand);
        return clone.ToString();
    }

    /// <summary>
    /// True for an operand that will be evaluated and bound as a parameter rather than rendered as a
    /// literal: no constant and no query source reference. Such an operand is the only shape whose
    /// SQL type has to be supplied by metadata.
    /// </summary>
    private static bool IsCapturedOperand(Expression expression)
        => !expression.Has<ParameterExpression>() && TypeFacts.UnwrapConvert(expression) is not ConstantExpression;

    /// <summary>
    /// Accepts the same effective CLR type on both sides (a nullable and a non-nullable form of one
    /// type are compatible) and rejects everything else with the deterministic diagnostic. A
    /// cross-type comparison needs an explicit cast by the caller, which the generic helper does not
    /// model.
    /// </summary>
    private static void ValidateCompatibleOperands(Expression left, Expression right)
    {
        if (EffectiveType(left.Type) != EffectiveType(right.Type))
            throw new NotSupportedException(IncompatibleOperandsMessage);
    }

    private static Type EffectiveType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// True when ordinary equality is already total for both operands: each is a statically
    /// non-nullable CLR value, so it cannot hold NULL at runtime, and neither expression reads a query
    /// source. A source reference (column, navigation, subquery) is the only operand shape an outer
    /// join can null-extend, and its SQL nullability is not provable from the CLR type, so it keeps
    /// the null-safe rendering. An NRT-annotated reference type is never non-nullable here: the
    /// annotation is not runtime proof.
    /// </summary>
    private static bool CanUseOrdinaryComparison(Expression left, Expression right)
        => IsNonNullableValue(left.Type)
            && IsNonNullableValue(right.Type)
            && !IsQuerySourceReference(left)
            && !IsQuerySourceReference(right);

    private static bool IsNonNullableValue(Type type)
        => type.IsValueType && Nullable.GetUnderlyingType(type) is null;

    private static bool IsQuerySourceReference(Expression expression) => expression.Has<ParameterExpression>();
}
