using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates <see cref="DateTime"/> arithmetic methods (<c>AddYears</c>/<c>AddMonths</c>/...) to the
/// dialect's date-addition rendering (<c>date_add</c>). Split out of
/// <see cref="ScalarFunctionTranslator"/> for cohesion; the emitted SQL is unchanged.
/// </summary>
internal static class DateTimeFunctionTranslator
{
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Object is null || node.Arguments.Count != 1)
            return false;

        var field = node.Method.Name switch
        {
            nameof(DateTime.AddYears) => "year",
            nameof(DateTime.AddMonths) => "month",
            nameof(DateTime.AddDays) => "day",
            nameof(DateTime.AddHours) => "hour",
            nameof(DateTime.AddMinutes) => "minute",
            nameof(DateTime.AddSeconds) => "second",
            nameof(DateTime.AddMilliseconds) => "milliseconds",
            _ => null
        };

        if (field is null)
            return false;

        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("Date arithmetic (DateTime.Add*) is not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(node.Arguments[0]);
            return true;
        }

        // A date arithmetic expression is a computed column and has to be aliased when selected.
        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateAdd(
            field,
            visitor.VisitToString(node.Arguments[0]),
            visitor.Dialect.PromoteDateOperand(field, visitor.VisitToString(node.Object))));
        return true;
    }
}
