using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates a predicate expression into a SQL <c>WHERE</c> clause.
/// </summary>
public class WhereExpressionVisitor(VisitorOptions options)
    : BaseExpressionVisitor(options)
{
    /// <summary>
    /// A where clause is a condition context: providers whose dialect cannot use a boolean value
    /// as a predicate (SQL Server) have to render the expression differently.
    /// </summary>
    protected override bool AsPredicate => true;

    /// <summary>
    /// Renders an equality or inequality comparison, using the null-aware <c>is</c>/<c>is not</c> form
    /// when either side is a null literal and <c>=</c>/<c>!=</c> otherwise.
    /// </summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!_paramMode && node.Type == typeof(bool) && (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual))
        {
            // A row constructor compared to null has no row-value meaning; reject it with the real
            // reason before the null path below clones and reports a misleading "wrong position".
            if (node.Left is ConstantExpression { Value: null })
                TupleSqlTranslator.RejectNullComparisonOfFlatTupleConstructor(this, node.Right);
            else if (node.Right is ConstantExpression { Value: null })
                TupleSqlTranslator.RejectNullComparisonOfFlatTupleConstructor(this, node.Left);

            // #296: a captured nullable/string comparison is a total predicate (WHERE is an approved
            // position), so it must not fall into the plain `= @p` arm below.
            if (PredicateTranslator.TryRenderCapturedNullableComparison(this, node))
                return node;

            // Fast path: when neither operand is a null literal the null-aware "is"/"is not"
            // rewriting cannot apply, so both sides can be rendered straight into this builder.
            // That avoids two cloned visitors (and their intermediate strings) per comparison,
            // which is the shape of every join condition and most WHERE predicates.
            if (!TypeFacts.IsNullLiteral(node.Left) && !TypeFacts.IsNullLiteral(node.Right))
            {
                var leftConverter = MemberTranslator.ResolveConverter(this, node.Left);
                var rightConverter = leftConverter is null ? MemberTranslator.ResolveConverter(this, node.Right) : null;

                VisitComparisonOperand(node.Left, rightConverter);
                _builder!.Append(node.NodeType == ExpressionType.Equal ? " = " : " != ");
                VisitComparisonOperand(node.Right, leftConverter);

                return node;
            }

            using var leftVisitor = Clone();
            leftVisitor.Visit(node.Left);

            using var rightVisitor = Clone();
            rightVisitor.Visit(node.Right);

            var left = leftVisitor.ToString();
            var right = rightVisitor.ToString();

            var hasNull = TypeFacts.IsNullLiteral(node.Left) || TypeFacts.IsNullLiteral(node.Right);
            // `is` accepts only `<operand> is null`, so a null literal on the left has to be emitted
            // last; the operand rendering itself is unchanged, only its position in the predicate.
            var reversed = hasNull && TypeFacts.IsNullLiteral(node.Left) && !TypeFacts.IsNullLiteral(node.Right);

            _builder!.Append(reversed ? right : left).Append(hasNull
                ? node.NodeType switch
                {
                    ExpressionType.Equal => Kw(" is "),
                    _ => Kw(" is not ")
                }
                : node.NodeType switch
                {
                    ExpressionType.Equal => " = ",
                    _ => " != "
                });
            _builder!.Append(reversed ? left : right);

            return node;
        }

        return base.VisitBinary(node);
    }
    // public override BaseExpressionVisitor Clone()
    // {
    //     return new WhereExpressionVisitor(_entityType, _sqlClient, _from);
    // }
}