using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Renders boolean/predicate expressions into SQL: comparison, logical and bitwise operators,
/// CASE (<c>?:</c>) and <c>switch</c>, and the value-to-predicate conversion required by dialects
/// without a boolean type. Extracted from <see cref="BaseExpressionVisitor"/>; the visitor walk and
/// the emitted SQL are unchanged.
/// </summary>
internal static class PredicateTranslator
{
    internal static Expression? VisitUnary(BaseExpressionVisitor visitor, UnaryExpression node)
    {
        // #148-B r3 A3′: the outer checked Int32 narrowing of a wide navigation count is a CLR-only
        // materialization boundary. In SQL the correlated scalar stays bigint, so an in-database
        // predicate/boolean/arithmetic evaluates at 64-bit (exact, never truncated by an int cast).
        // The command is recognised by provenance (the tagged wide-count command the placeholder
        // indexes), never by the node shape alone, so an ordinary checked numeric conversion below is
        // untouched.
        if (!visitor.IsParamMode
            && node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && node.Type == typeof(int)
            && CorrelatedQueryExpressionVisitor.TryGetWideCountCommand(node.Operand, visitor.QueryProvider, out _))
        {
            visitor.Visit(node.Operand);
            return node;
        }

        // Numeric conversions are otherwise dropped, which silently changes the SQL semantics
        // (integer division, SQL Server integer avg, a projection wider than the column type).
        if (!visitor.IsParamMode
            && node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && TypeFacts.TryGetNumericConversion(node.Operand.Type, node.Type, out var target))
        {
            visitor.NeedAliasForColumn = true;

            visitor.Builder!.Append(visitor.Kw("cast("));
            visitor.Visit(node.Operand);
            visitor.Builder!.Append(visitor.Kw(" as ")).Append(visitor.Dialect.MakeTypeName(target)).Append(')');

            return node;
        }

        // The C# bool negation and the integral ones-complement both use ExpressionType.Not, so the
        // result type decides between the logical and the bitwise SQL operator.
        if (node.NodeType == ExpressionType.Not && !TypeFacts.IsBoolean(node.Type))
            return VisitOnesComplement(visitor, node);

        switch (node.NodeType)
        {
            case ExpressionType.Not:
                return VisitNot(visitor, node);
            case ExpressionType.OnesComplement:
                return VisitOnesComplement(visitor, node);
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnaryOperator(visitor, node, "-");
            case ExpressionType.UnaryPlus:
                return VisitUnaryOperator(visitor, node, "+");
        }

        return null;
    }

    /// <summary>
    /// Translates a logical NOT. The operand is a boolean expression, so it is rendered with
    /// predicate semantics and the whole negation is handed to the dialect: a provider without a
    /// boolean type (SQL Server) has to turn a projected predicate into a bit scalar.
    /// </summary>
    private static Expression VisitNot(BaseExpressionVisitor visitor, UnaryExpression node)
    {
        // #296: !HasValue on a mapped nullable column renders the approved direct IS NULL form, not
        // `not (<receiver> is not null)`. Outside the approved positions it falls through unchanged.
        if (TypeFacts.UnwrapConvert(node.Operand) is MemberExpression presenceReceiver
            && MemberTranslator.TryRenderPresence(visitor, presenceReceiver, negated: true))
            return node;

        if (visitor.IsParamMode)
        {
            // Nothing is emitted, but the operand is still walked so captured constants are collected.
            visitor.Visit(node.Operand);
            return node;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"{visitor.Kw("not")} ({RenderPredicate(visitor, node.Operand)})", visitor.IsPredicateContext));

        return node;
    }

    /// <summary>Translates the integer ones-complement operator (<c>~</c>).</summary>
    private static Expression VisitOnesComplement(BaseExpressionVisitor visitor, UnaryExpression node)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Operand);
            return node;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeOnesComplement(visitor.VisitToString(node.Operand)));

        return node;
    }

    /// <summary>
    /// Translates a unary arithmetic/bitwise operator. The operand is parenthesised so that the
    /// grouping of the source expression is preserved (e.g. <c>-(a + b)</c> stays a single operand).
    /// </summary>
    private static Expression VisitUnaryOperator(BaseExpressionVisitor visitor, UnaryExpression node, string sqlOperator)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Operand);
            return node;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(sqlOperator).Append('(');
        visitor.Visit(node.Operand);
        visitor.Builder!.Append(')');

        return node;
    }

    /// <summary>
    /// Renders a boolean expression with predicate semantics (the context of a WHERE clause) into a
    /// fresh builder, so that the test of a conditional/switch uses the dialect's predicate form
    /// (e.g. EXISTS/ANY/ALL) instead of a scalar one.
    /// </summary>
    internal static string RenderPredicate(BaseExpressionVisitor visitor, Expression expression)
    {
        // A bare boolean value (a bit column, a CASE, a method call) is not a predicate on a dialect
        // without a boolean type, so it is rendered as a value and the dialect turns it into one.
        if (!visitor.IsParamMode && TypeFacts.IsBoolean(expression.Type) && !TypeFacts.IsPredicate(expression))
        {
            using var valueVisitor = visitor.Clone();
            valueVisitor.Visit(expression);

            return visitor.Dialect.MakeBooleanValuePredicate(valueVisitor.ToString());
        }

        using var whereVisitor = new WhereExpressionVisitor(visitor.Options with { DontNeedAlias = false });
        whereVisitor.Visit(expression);

        return whereVisitor.ToString();
    }

    /// <summary>
    /// Renders a boolean expression that is used as a condition (a logical operand such as the left
    /// and right side of <c>&amp;&amp;</c>/<c>||</c>). A value that is not itself a predicate is
    /// turned into one by the dialect, so a bare boolean value stays valid on providers without a
    /// boolean type.
    /// </summary>
    private static void AppendCondition(BaseExpressionVisitor visitor, Expression expression)
    {
        if (!visitor.IsParamMode && TypeFacts.IsBoolean(expression.Type) && !TypeFacts.IsPredicate(expression))
        {
            using var valueVisitor = visitor.Clone();
            valueVisitor.Visit(expression);
            visitor.Builder!.Append(visitor.Dialect.MakeBooleanValuePredicate(valueVisitor.ToString()));
            return;
        }

        visitor.Visit(expression);
    }

    /// <summary>
    /// Renders a whole condition (WHERE/HAVING/JOIN ON) into this visitor's builder. A condition
    /// that is a bare boolean value is turned into a predicate by the dialect.
    /// </summary>
    internal static void VisitCondition(BaseExpressionVisitor visitor, Expression condition)
    {
        // The root condition arrives as a lambda; the parameter is resolved by the member visitor,
        // so the body can be rendered directly (with the value-to-predicate conversion applied).
        if (condition is LambdaExpression lambda)
            condition = lambda.Body;

        AppendCondition(visitor, condition);
    }

    internal static Expression VisitConditional(BaseExpressionVisitor visitor, ConditionalExpression node)
    {
        // A CASE is a computed column and has to be aliased when it appears in a select list.
        visitor.NeedAliasForColumn = true;

        // A pending converter (this CASE being the value side of a comparison against a converted
        // column) applies to the branch values only, never to the test. Both passes clear the context
        // around the test and render the branches through VisitComparisonOperand, so the parameter-only
        // pass used by the cache refresh collects exactly the same names and provider values as SQL.
        var branchConverter = visitor.ConverterContext;

        if (visitor.IsParamMode)
        {
            // The parameter-extraction pass emits no SQL, but the whole tree still has to be walked
            // so that captured constants/parameters inside the test and the branches are collected.
            visitor.ConverterContext = null;
            visitor.Visit(node.Test);
            visitor.ConverterContext = branchConverter;
            visitor.VisitComparisonOperand(node.IfTrue, branchConverter);
            visitor.VisitComparisonOperand(node.IfFalse, branchConverter);
            return node;
        }

        visitor.ConverterContext = null;
        string test;
        try
        {
            test = RenderPredicate(visitor, node.Test);
        }
        finally
        {
            visitor.ConverterContext = branchConverter;
        }

        using var trueVisitor = visitor.Clone();
        trueVisitor.ConverterContext = null;
        trueVisitor.VisitComparisonOperand(node.IfTrue, branchConverter);

        using var falseVisitor = visitor.Clone();
        falseVisitor.ConverterContext = null;
        falseVisitor.VisitComparisonOperand(node.IfFalse, branchConverter);

        var caseBuilder = visitor.BuilderPool.Get();
        try
        {
            caseBuilder.Append(visitor.Kw("case when ")).Append(test)
                .Append(visitor.Kw(" then ")).Append(trueVisitor.ToString())
                .Append(visitor.Kw(" else ")).Append(falseVisitor.ToString())
                .Append(visitor.Kw(" end"));

            visitor.Builder!.Append(visitor.Dialect.MakeCase(caseBuilder.ToString(), TypeFacts.IsBoolean(node.Type), visitor.IsPredicateContext, visitor.KeywordCase));
        }
        finally
        {
            visitor.BuilderPool.Return(caseBuilder);
        }

        return node;
    }

    internal static Expression VisitSwitch(BaseExpressionVisitor visitor, SwitchExpression node)
    {
        // A custom comparison method is not equality and cannot be translated without translating
        // the method itself; the C# compiler never produces one for a switch over constants.
        if (node.Comparison is not null)
            throw new NotSupportedException("A switch expression with a custom comparison method is not supported");

        visitor.NeedAliasForColumn = true;

        // A pending converter means this SWITCH is the value side of a comparison against a converted
        // column: only its branch values are converted, not the selector or the test values.
        var branchConverter = visitor.ConverterContext;

        if (visitor.IsParamMode)
        {
            // Mirror the SQL pass exactly: the selector and test values are not converted, the bodies are.
            visitor.ConverterContext = null;
            visitor.Visit(node.SwitchValue);

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];

                visitor.VisitComparisonOperand(@case.Body, branchConverter);
                visitor.ConverterContext = null;

                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                    visitor.Visit(@case.TestValues[j]);
            }

            visitor.ConverterContext = branchConverter;
            if (node.DefaultBody is not null)
                visitor.VisitComparisonOperand(node.DefaultBody, branchConverter);

            return node;
        }

        if (node.Cases.Count == 0)
        {
            // A switch without cases is just its default arm.
            if (node.DefaultBody is null)
                visitor.Builder!.Append(visitor.Kw("null"));
            else
                visitor.VisitComparisonOperand(node.DefaultBody, branchConverter);

            return node;
        }

        var caseBuilder = visitor.BuilderPool.Get();
        var switchValueVisitor = visitor.Clone();
        try
        {
            switchValueVisitor.ConverterContext = null;
            switchValueVisitor.Visit(node.SwitchValue);
            var switchValue = switchValueVisitor.ToString();

            caseBuilder.Append(visitor.Kw("case"));

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];

                using var bodyVisitor = visitor.Clone();
                bodyVisitor.ConverterContext = null;
                bodyVisitor.VisitComparisonOperand(@case.Body, branchConverter);
                var body = bodyVisitor.ToString();

                // Several test values share one body (case 1: case 2:), so the body is rendered once.
                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                {
                    using var testVisitor = visitor.Clone();
                    testVisitor.ConverterContext = null;
                    testVisitor.Visit(@case.TestValues[j]);

                    caseBuilder.Append(visitor.Kw(" when ")).Append(switchValue)
                        .Append(" = ").Append(testVisitor.ToString())
                        .Append(visitor.Kw(" then ")).Append(body);
                }
            }

            caseBuilder.Append(visitor.Kw(" else "));

            if (node.DefaultBody is null)
                caseBuilder.Append(visitor.Kw("null"));
            else
            {
                using var defaultVisitor = visitor.Clone();
                defaultVisitor.ConverterContext = null;
                defaultVisitor.VisitComparisonOperand(node.DefaultBody, branchConverter);
                caseBuilder.Append(defaultVisitor.ToString());
            }

            caseBuilder.Append(visitor.Kw(" end"));

            visitor.Builder!.Append(visitor.Dialect.MakeCase(caseBuilder.ToString(), TypeFacts.IsBoolean(node.Type), visitor.IsPredicateContext, visitor.KeywordCase));
        }
        finally
        {
            switchValueVisitor.Dispose();
            visitor.BuilderPool.Return(caseBuilder);
        }

        return node;
    }

    internal static Expression VisitBinary(BaseExpressionVisitor visitor, BinaryExpression node)
    {
        // An array index reaches this point only after DictionaryLookupTranslator declined it, so the
        // array is not a captured collection (server-side array or an array-returning expression).
        if (node.NodeType == ExpressionType.ArrayIndex)
            throw new NotSupportedException(
                "An array index is supported only on a captured array (arr[i]); server-side array element access has no portable SQL form.");

        // A comparison against a null literal renders with the null-aware is/is not form, so a boolean
        // value projection (for example an expanded navigation presence check) is a real predicate and
        // not an UNKNOWN that a driver reads as false. WHERE has its own null-aware path; this covers
        // value/bool projections where the column visitor uses this translator.
        if (!visitor.IsParamMode
            && node.Type == typeof(bool)
            && node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            && (TypeFacts.IsNullLiteral(node.Left) || TypeFacts.IsNullLiteral(node.Right)))
        {
            using var nullLeftVisitor = visitor.Clone();
            nullLeftVisitor.Visit(node.Left);

            using var nullRightVisitor = visitor.Clone();
            nullRightVisitor.Visit(node.Right);

            var nullLeft = nullLeftVisitor.ToString();
            var nullRight = nullRightVisitor.ToString();

            if (nullLeft == visitor.Kw("null") || nullRight == visitor.Kw("null"))
            {
                visitor.NeedAliasForColumn = true;
                // A dialect without a boolean type (SQL Server) cannot use a bare predicate in a select
                // list, so the value context is materialised as a bit scalar; a condition context keeps
                // the predicate as-is (asPredicate = true is a no-op for such a dialect).
                // `is` accepts only `<operand> is null`, so a null literal on the left is emitted last;
                // the operand rendering itself is unchanged, only its position in the predicate.
                var presenceOp = node.NodeType == ExpressionType.Equal ? visitor.Kw(" is ") : visitor.Kw(" is not ");
                var presence = nullLeft == visitor.Kw("null") && nullRight != visitor.Kw("null")
                    ? $"{nullRight}{presenceOp}{nullLeft}"
                    : $"{nullLeft}{presenceOp}{nullRight}";
                visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(presence, visitor.IsPredicateContext));
                return node;
            }
        }

        // #296: a comparison against a captured nullable/string value is a total Boolean predicate in
        // the approved positions (WHERE/Exists/ON and a boolean SELECT projection): a runtime NULL
        // then follows the C# truth table instead of SQL UNKNOWN. Literal comparisons keep their
        // existing null-aware form above; column-to-column comparisons and the ordering operators are
        // never rewritten.
        if (TryRenderCapturedNullableComparison(visitor, node))
            return node;

        visitor.NeedAliasForColumn = true;

        switch (node.NodeType)
        {
            case ExpressionType.Coalesce:
                if (!visitor.IsParamMode)
                {
                    using var leftVisitor = visitor.Clone();
                    leftVisitor.Visit(node.Left);

                    using var rightVisitor = visitor.Clone();
                    rightVisitor.Visit(node.Right);

                    var coalesceLeft = leftVisitor.ToString();
                    var coalesceRight = rightVisitor.ToString();

                    visitor.Builder!.Append(node.Type == typeof(bool)
                        ? visitor.Dialect.MakeBoolCoalesce(coalesceLeft, coalesceRight)
                        : visitor.Dialect.MakeCoalesce(coalesceLeft, coalesceRight));
                    return node;
                }
                break;
        }

        // A logical AND/OR is a condition, so each operand is rendered as a predicate. On a dialect
        // without a boolean type a bare boolean value (e.g. a bit column) has to be turned into a
        // predicate first, otherwise the emitted <c>and</c>/<c>or</c> is rejected (SQL Server).
        if (!visitor.IsParamMode && node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            var logicalOperator = node.NodeType == ExpressionType.AndAlso
                ? visitor.Kw(" and ")
                : visitor.Kw(" or ");

            // A logical value in a value context (a select-list projection, e.g. a composite reference
            // presence check) is not a condition: a dialect without a boolean type has no infix form
            // for it, so the whole expression is rendered as a predicate and materialised by the
            // dialect (T-SQL: cast(case when ... then 1 else 0 end as bit)). On a dialect with a
            // boolean type MakeBooleanPredicate is identity, so the emitted SQL is unchanged.
            if (!visitor.IsPredicateContext)
            {
                visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(
                    $"({RenderPredicate(visitor, node.Left)}{logicalOperator}{RenderPredicate(visitor, node.Right)})",
                    asPredicate: false));
                return node;
            }

            visitor.Builder!.Append('(');
            AppendCondition(visitor, node.Left);
            visitor.Builder!.Append(logicalOperator);
            AppendCondition(visitor, node.Right);
            visitor.Builder!.Append(')');
            return node;
        }

        // A string concatenation is delegated to the dialect: dialects where the infix operator is
        // not a concatenation (MySQL/MariaDB, ClickHouse) emit the concat function instead.
        if (!visitor.IsParamMode && node.NodeType == ExpressionType.Add && node.Type == typeof(string))
        {
            visitor.Builder!.Append('(');
            visitor.Builder!.Append(visitor.Dialect.MakeConcat([visitor.VisitToString(node.Left), visitor.VisitToString(node.Right)]));
            visitor.Builder!.Append(')');
            return node;
        }

        // A comparison against a duration column must convert a folded TimeSpan parameter to the
        // column's storage unit. Resolve the unit before visiting either operand so the operand order
        // does not matter (the constant may be on the left), then restore the previous context so the
        // unit cannot leak into a later, unrelated TimeSpan parameter.
        var previousDurationUnit = visitor.DurationUnitContext;
        if (!visitor.Dialect.SupportsNativeDuration)
        {
            var durationUnit = MemberTranslator.ResolveDurationUnit(visitor, node.Left)
                ?? MemberTranslator.ResolveDurationUnit(visitor, node.Right);
            if (durationUnit is not null)
                visitor.DurationUnitContext = durationUnit;
        }

        // A comparison against a value-converted column binds a folded constant/captured value as the
        // column's provider representation. Resolve the converter before visiting either operand so the
        // operand order does not matter (the constant may be on the left); each operand is rendered
        // through VisitComparisonOperand, which applies the converter only to the opposite (value)
        // operand and restores the context afterwards.
        var isComparison = node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;
        var leftConverter = isComparison ? MemberTranslator.ResolveConverter(visitor, node.Left) : null;
        var rightConverter = isComparison && leftConverter is null ? MemberTranslator.ResolveConverter(visitor, node.Right) : null;

        if (!visitor.IsParamMode)
            visitor.Builder!.Append('(');

        visitor.VisitComparisonOperand(isComparison ? NaturalComparisonLiteral(node.Left) : node.Left, rightConverter);

        if (!visitor.IsParamMode)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Add:
                    visitor.Builder!.Append(" + ");
                    break;
                case ExpressionType.And:
                    visitor.Builder!.Append(" & "); break;
                case ExpressionType.AndAlso:
                    visitor.Builder!.Append(visitor.Kw(" and ")); break;
                case ExpressionType.Decrement:
                    visitor.Builder!.Append(" -1 "); break;
                case ExpressionType.Divide:
                    visitor.Builder!.Append(" / "); break;
                case ExpressionType.GreaterThan:
                    visitor.Builder!.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual:
                    visitor.Builder!.Append(" >= "); break;
                case ExpressionType.Increment:
                    visitor.Builder!.Append(" + 1"); break;
                case ExpressionType.LeftShift:
                    visitor.Builder!.Append(" << "); break;
                case ExpressionType.LessThan:
                    visitor.Builder!.Append(" < "); break;
                case ExpressionType.LessThanOrEqual:
                    visitor.Builder!.Append(" <= "); break;
                case ExpressionType.Modulo:
                    visitor.Builder!.Append(" % "); break;
                case ExpressionType.Multiply:
                    visitor.Builder!.Append(" * "); break;
                case ExpressionType.Negate:
                    visitor.Builder!.Append(" - "); break;
                case ExpressionType.Not:
                    visitor.Builder!.Append(" ~ "); break;
                case ExpressionType.NotEqual:
                    visitor.Builder!.Append(" != "); break;
                case ExpressionType.Equal:
                    visitor.Builder!.Append(" = "); break;
                case ExpressionType.Or:
                    visitor.Builder!.Append(" | "); break;
                case ExpressionType.OrElse:
                    visitor.Builder!.Append(visitor.Kw(" or ")); break;
                case ExpressionType.Power:
                    visitor.Builder!.Append(" ^ "); break;
                case ExpressionType.RightShift:
                    visitor.Builder!.Append(" >> "); break;
                case ExpressionType.Subtract:
                    visitor.Builder!.Append(" - "); break;
                default:
                    throw new NotSupportedException(node.NodeType.ToString());
            }
        }

        visitor.VisitComparisonOperand(isComparison ? NaturalComparisonLiteral(node.Right) : node.Right, leftConverter);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(')');

        visitor.DurationUnitContext = previousDurationUnit;

        return node;
    }

    /// <summary>
    /// Renders an integral literal lifted to a floating type at its natural literal type. When the
    /// opposite operand of a comparison is a nullable floating value, the C# compiler wraps the
    /// literal in a conversion chain (<c>Convert(Convert(1, Double), Nullable&lt;double&gt;)</c>);
    /// after an integral AVG was declared <c>double?</c>, <c>Avg(...) &gt; 1</c> has exactly that
    /// shape. SQL promotes the integer literal to the floating operand itself, so the explicit
    /// <c>cast(1 as double)</c> carries no information and the redundant cast is not emitted. Only
    /// an exactly representable integral value is unwrapped, so no rounding is introduced; a
    /// decimal target and every non-literal operand keep their previous rendering.
    /// </summary>
    private static Expression NaturalComparisonLiteral(Expression operand)
    {
        if (operand is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } lifted
            && lifted.Type is { IsGenericType: true } liftedType
            && liftedType.GetGenericTypeDefinition() == typeof(Nullable<>)
            && lifted.Operand is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } conversion
            && conversion.Operand is ConstantExpression literal
            && IsExactIntegralFloatingLiteral(literal, conversion.Type))
        {
            return literal;
        }

        return operand;
    }

    /// <summary>
    /// True when <paramref name="literal"/> is an integral constant that <paramref name="target"/>
    /// represents without any value change: every integral value is exact in <see cref="double"/>
    /// up to 2^53 and in <see cref="float"/> up to 2^24. The decimal round trip checks the actual
    /// target representation, so a large <c>long</c> literal keeps its explicit cast.
    /// </summary>
    private static bool IsExactIntegralFloatingLiteral(ConstantExpression literal, Type target)
    {
        if (target != typeof(double) && target != typeof(float))
            return false;

        if (literal.Type != typeof(byte) && literal.Type != typeof(sbyte)
            && literal.Type != typeof(short) && literal.Type != typeof(ushort)
            && literal.Type != typeof(int) && literal.Type != typeof(uint)
            && literal.Type != typeof(long) && literal.Type != typeof(ulong))
            return false;

        if (literal.Value is null)
            return false;

        var value = Convert.ToDecimal(literal.Value);
        return target == typeof(double)
            ? (decimal)(double)value == value
            : (decimal)(float)value == value;
    }

    /// <summary>
    /// Renders a comparison of a row-dependent expression with a captured nullable/string value as a
    /// total Boolean predicate: equality has the approved two-arm form and inequality the three-arm
    /// complement, so the result follows the C# null-equality truth table instead of SQL
    /// <c>UNKNOWN</c> (which drops null rows from an inequality). Only in the approved positions, only
    /// for <c>==</c>/<c>!=</c>, only when exactly one side is a captured nullable/string operand and
    /// the other side references the query. The captured value is registered through the existing
    /// comparison-operand machinery and reused for every occurrence (one logical parameter, never an
    /// inlined value, never a branch on the runtime value).
    /// </summary>
    /// <returns><see langword="true"/> when the comparison was rendered here.</returns>
    internal static bool TryRenderCapturedNullableComparison(BaseExpressionVisitor visitor, BinaryExpression node)
    {
        if (node.Type != typeof(bool)
            || node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual)
            || !visitor.NullSafePredicateEnabled
            || TypeFacts.IsNullLiteral(node.Left)
            || TypeFacts.IsNullLiteral(node.Right))
            return false;

        var leftCaptured = TypeFacts.IsCapturedNullableOrStringOperand(node.Left);
        var rightCaptured = TypeFacts.IsCapturedNullableOrStringOperand(node.Right);

        // Neither side is captured (an ordinary column comparison) or both are (no row-dependent side).
        if (leftCaptured == rightCaptured)
            return false;

        var dbOperand = leftCaptured ? node.Right : node.Left;
        var capturedOperand = leftCaptured ? node.Left : node.Right;

        // The other side must actually read the query, so a captured value is never compared with a
        // constant/another captured value under the null-safe shape.
        if (!dbOperand.Has<ParameterExpression>())
            return false;

        var dbConverter = MemberTranslator.ResolveConverter(visitor, dbOperand);
        var capturedConverter = MemberTranslator.ResolveConverter(visitor, capturedOperand);

        visitor.NeedAliasForColumn = true;

        var previousParameterType = visitor.ParameterTypeContext;
        if (MemberTranslator.ResolveParameterType(visitor, dbOperand, capturedOperand) is { } parameterType)
            visitor.ParameterTypeContext = parameterType;

        try
        {
            if (visitor.IsParamMode)
            {
                // Mirror the SQL pass: the same operands are visited in the same canonical order
                // (row-dependent side first), so the extracted parameter list is identical. Repeats
                // are deduplicated by the captured-parameter registration.
                visitor.VisitComparisonOperand(dbOperand, capturedConverter);
                visitor.VisitComparisonOperand(capturedOperand, dbConverter);
                return true;
            }

            var isEqual = node.NodeType == ExpressionType.Equal;
            var builder = visitor.Builder!;

            // The whole total predicate is parenthesised so it composes safely inside &&/||.
            builder.Append('(');

            builder.Append('(');
            visitor.VisitComparisonOperand(dbOperand, capturedConverter);
            builder.Append(visitor.Kw(" is null")).Append(visitor.Kw(" and "));
            visitor.VisitComparisonOperand(capturedOperand, dbConverter);
            builder.Append(visitor.Kw(isEqual ? " is null" : " is not null"));
            builder.Append(')').Append(visitor.Kw(" or "));

            if (!isEqual)
            {
                // (a is not null and p is null) — only the inequality complement has this arm.
                builder.Append('(');
                visitor.VisitComparisonOperand(dbOperand, capturedConverter);
                builder.Append(visitor.Kw(" is not null")).Append(visitor.Kw(" and "));
                visitor.VisitComparisonOperand(capturedOperand, dbConverter);
                builder.Append(visitor.Kw(" is null"));
                builder.Append(')').Append(visitor.Kw(" or "));
            }

            builder.Append('(');
            visitor.VisitComparisonOperand(dbOperand, capturedConverter);
            builder.Append(visitor.Kw(" is not null")).Append(visitor.Kw(" and "));
            visitor.VisitComparisonOperand(capturedOperand, dbConverter);
            builder.Append(visitor.Kw(" is not null")).Append(visitor.Kw(" and "));
            visitor.VisitComparisonOperand(dbOperand, capturedConverter);
            builder.Append(isEqual ? " = " : " != ");
            visitor.VisitComparisonOperand(capturedOperand, dbConverter);
            builder.Append(')');

            builder.Append(')');
            return true;
        }
        finally
        {
            visitor.ParameterTypeContext = previousParameterType;
        }
    }

}
