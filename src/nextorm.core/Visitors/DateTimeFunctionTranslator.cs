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
        if (node.Object is null)
            return false;

        // The same method names (AddYears/.../AddMilliseconds) exist on DateTime and DateTimeOffset; the
        // receiver type selects the DTO-aware path (timezone normalization, offset preservation, guards).
        var isDateTimeOffset = node.Object.Type == typeof(DateTimeOffset);

        // A DateTimeOffset instance method only has a server translation for the single-argument Add*
        // family below. Any other shape — including the zero-argument ToUnixTimeSeconds/
        // ToUnixTimeMilliseconds/ToLocalTime/ToUniversalTime — must fail closed instead of falling
        // through to a silent client fold (which would drop the call or capture the client clock).
        if (isDateTimeOffset && node.Arguments.Count != 1)
            throw new NotSupportedException(
                $"DateTimeOffset.{node.Method.Name} is not supported in a server-side query: it has no " +
                "SQL translation and would be evaluated on the client.");

        if (node.Arguments.Count != 1)
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
        {
            // AddTicks/Add(TimeSpan)/ToOffset have no server translation; they must fail closed instead
            // of being folded into a client-side value. DateTime keeps its existing fall-through.
            if (isDateTimeOffset)
                throw new NotSupportedException(
                    $"DateTimeOffset.{node.Method.Name} is not supported in a server-side query: it has no SQL " +
                    "translation and would be evaluated on the client.");

            return false;
        }

        if (isDateTimeOffset)
        {
            if (!visitor.Dialect.SupportsDateTimeOffsetTranslation)
                throw new NotSupportedException(
                    "DateTimeOffset.Add* translation is not supported by this SQL dialect.");
        }
        else if (!visitor.Dialect.SupportsDateArithmetic)
        {
            throw new NotSupportedException("Date arithmetic (DateTime.Add*) is not supported by this provider.");
        }

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(node.Arguments[0]);
            return true;
        }

        // A date arithmetic expression is a computed column and has to be aliased when selected. The
        // DateTime operand is promoted through the existing hook; a DateTimeOffset operand is not (a
        // date-only promotion would drop its offset), so its provider-local adaptation lives in the
        // isDateTimeOffset-aware MakeDateAdd overload.
        visitor.NeedAliasForColumn = true;
        var value = isDateTimeOffset
            ? visitor.VisitToString(node.Object)
            : visitor.Dialect.PromoteDateOperand(field, visitor.VisitToString(node.Object));
        visitor.Builder!.Append(visitor.Dialect.MakeDateAdd(
            field,
            visitor.VisitToString(node.Arguments[0]),
            value,
            isDateTimeOffset));
        return true;
    }
}
