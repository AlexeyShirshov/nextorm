using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>Recognizes SQL or render-time validation that depends on a value absent from the plan key.</summary>
internal static class SqlShapeDependency
{
    internal static bool IsUnkeyed(MethodCallExpression call)
        => TableAliasAccessors.HasComputedColumnName(call)
            || WindowSql.HasUnkeyedSpecification(call)
            || RangeColumnsSqlTranslator.HasUnkeyedOperand(call)
            || AdvancedAggregateTranslator.HasUnkeyedFraction(call);
}
