using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the SQL Server postfix XML data-type methods of <see cref="SqlServerFunctions"/>
/// (<c>xml_value</c>, <c>xml_query</c>, <c>xml_exist</c>), emitted as
/// <c>xmlcol.value('xpath', 'type')</c> / <c>xmlcol.query('xpath')</c> / <c>xmlcol.exist('xpath')</c>.
/// The XQuery and the SQL type are T-SQL string literals, so the translator requires compile-time
/// constants instead of parameters.
/// <para>
/// Only a dialect that opts in with <see cref="ISqlDialect.XmlFunctions"/> and lists the
/// individual method through <see cref="IXmlFunctions.Supports"/> (SQL Server) may use
/// these constructs; every other provider rejects them with a clear message. The rowset method
/// <c>nodes</c> is deliberately absent: it needs an outer reference inside <c>CROSS/OUTER APPLY</c>,
/// which the engine cannot express yet.
/// </para>
/// </summary>
internal static class XmlSqlTranslator
{
    /// <summary>Translates an XML data-type method call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqlServerFunctions))
            return false;

        var name = node.Method.Name switch
        {
            nameof(SqlServerFunctions.xml_value) => "value",
            nameof(SqlServerFunctions.xml_query) => "query",
            nameof(SqlServerFunctions.xml_exist) => "exist",
            _ => null
        };

        if (name is null)
            return false;

        var dialect = visitor.Dialect;

        if (dialect.XmlFunctions is not { } xmlFunctions)
            throw new NotSupportedException("The XML data-type methods (value/query/exist) are not supported by this provider.");

        if (!xmlFunctions.Supports(name))
            throw new NotSupportedException($"The XML data-type method {name} is not supported by this provider.");

        var args = node.Arguments;
        var expected = name == "value" ? 3 : 2;

        if (args.Count != expected)
            throw new NotSupportedException($"The {name} XML data-type method requires {expected} arguments.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var operand = visitor.VisitToString(args[0]);

        var rendered = new string[args.Count - 1];
        for (var (i, cnt) = (1, args.Count); i < cnt; i++)
        {
            if (!SqlLiteral.TryGetConstantString(args[i], out var literal))
                throw new NotSupportedException($"The {name} XML data-type method requires constant string arguments (T-SQL accepts only literals).");

            rendered[i - 1] = SqlLiteral.ToSqlStringLiteral(literal);
        }

        visitor.Builder!.Append(xmlFunctions.Render(name, operand, rendered));
        return true;
    }
}
