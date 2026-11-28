using System.Data.Common;
using NextORM.Core;

namespace NextORM.Integration.Tests;

internal static class PreparedInvocationObservation
{
    internal static DbPreparedQueryCommand<T> PlanOf<T>(this IPreparedQueryCommand<T> invocation)
        => BoundQueryInvocation<T>.GetPlan(invocation)
            ?? throw new InvalidOperationException("Expected a prepared database query.");

    internal static object ParameterValue<T>(this IPreparedQueryCommand<T> invocation, int index)
        => invocation is BoundQueryInvocation<T> bound && index < bound.Values.Length
            ? bound.Values[index] ?? DBNull.Value
            : invocation.PlanOf().DbCommandParams[index].Value ?? DBNull.Value;

    internal static IEnumerable<(DbParameter Descriptor, object Value)> ObservedParameters<T>(
        this IPreparedQueryCommand<T> invocation)
    {
        var descriptors = invocation.PlanOf().DbCommandParams;
        for (var i = 0; i < descriptors.Count; i++)
            yield return (descriptors[i], invocation.ParameterValue(i));
    }
}
