using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Rewrites the SQL-string functions that have a native in-memory equivalent before the in-memory
/// provider compiles an expression. Today it replaces
/// <c>SqlFunctions.Sql.collate(value, collation)</c> with <c>value</c>: the in-memory
/// provider has no collations and its comparisons are already ordinal, so the call is the identity.
/// Without this the compiled expression would evaluate the <c>SqlFunctions.Sql</c> marker (which is
/// <c>null</c>) and fail with a <see cref="NullReferenceException"/>.
/// </summary>
internal sealed class InMemoryStringFunctionRewriter : ExpressionVisitor
{
    internal static Expression Rewrite(Expression expression) => new InMemoryStringFunctionRewriter().Visit(expression)!;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(CommonFunctions)
            && node.Method.Name == nameof(CommonFunctions.collate)
            && node.Arguments.Count == 2)
        {
            return Visit(node.Arguments[0])!;
        }

        return base.VisitMethodCall(node);
    }
}
