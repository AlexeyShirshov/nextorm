using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A SQL Server <c>xml.nodes('xquery')</c> rowset used as a <c>CROSS/OUTER APPLY</c> source. The
/// operand is an already-rewritten column reference (an <see cref="OuterRefMarker{T}"/> of the
/// left-hand row); the XQuery is a compile-time constant. Implementation detail of
/// <see cref="SqlServerFunctions.xml_nodes(string?, string?)"/>.
/// </summary>
internal sealed class XmlNodesExpression
{
    internal XmlNodesExpression(Expression operand, string xpath)
    {
        Operand = operand;
        XPath = xpath;
    }

    /// <summary>The rewritten XML column expression (rendered as <c>alias.column</c>).</summary>
    internal Expression Operand { get; }

    /// <summary>The XQuery string literal.</summary>
    internal string XPath { get; }

    /// <summary>
    /// Recognises a correlated APPLY body produced by <see cref="SqlServerFunctions.xml_nodes"/> and
    /// rewrites its operand into an outer reference of the command being prepared. Returns
    /// <c>false</c> for any other body.
    /// </summary>
    internal static bool TryCreate(LambdaExpression applySource, CorrelatedQueryExpressionVisitor visitor, out XmlNodesExpression? nodes)
    {
        var body = applySource.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        if (body is MethodCallExpression call
            && call.Method.DeclaringType == typeof(SqlServerFunctions)
            && call.Method.Name == nameof(SqlServerFunctions.xml_nodes)
            && call.Arguments is [var xml, var xpath])
        {
            if (!SqlLiteral.TryGetConstantString(xpath, out var xquery))
                throw new NotSupportedException("The xml_nodes XQuery must be a constant string (T-SQL accepts only literals).");

            var operand = visitor.RewriteOuterReference(xml);
            if (operand.Has<ParameterExpression>())
                throw new NotSupportedException("The xml_nodes operand must be a column of the outer row.");

            nodes = new XmlNodesExpression(operand, xquery);
            return true;
        }

        nodes = null;
        return false;
    }
}
