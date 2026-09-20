using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates <see cref="Math"/> method calls to the dialect's math-function rendering. Split out of
/// <see cref="ScalarFunctionTranslator"/> for cohesion; the supported set and emitted SQL are unchanged.
/// </summary>
internal static class MathFunctionTranslator
{
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name switch
        {
            nameof(Math.Abs) => "abs",
            nameof(Math.Ceiling) => "ceiling",
            nameof(Math.Floor) => "floor",
            nameof(Math.Round) => "round",
            nameof(Math.Sqrt) => "sqrt",
            nameof(Math.Pow) => "pow",
            nameof(Math.Exp) => "exp",
            nameof(Math.Log) => "log",
            nameof(Math.Sin) => "sin",
            nameof(Math.Cos) => "cos",
            nameof(Math.Tan) => "tan",
            nameof(Math.Sign) => "sign",
            nameof(Math.Truncate) => "trunc",
            _ => null
        };

        if (name is null)
            return false;

        var args = node.Arguments;

        // Math.Round's MidpointRounding overloads and the two-argument Math.Log are not portable and
        // are deliberately left unsupported rather than emitting SQL with different semantics.
        if (node.Method.Name == nameof(Math.Round) && args.Count > 2)
            return false;
        if (node.Method.Name == nameof(Math.Log) && args.Count != 1)
            return false;

        if (visitor.IsParamMode)
        {
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var sqlArgs = new string[args.Count];
        for (var i = 0; i < args.Count; i++)
            sqlArgs[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeMathFunction(name, sqlArgs));
        return true;
    }
}
