using System.Reflection;
using NextORM.Core;

namespace NextORM.Tests;

// The extension-consumer suites exercise public APIs without a friend assembly grant.
// They may inspect the plan for SQL and identity, but must execute the original invocation.
internal static class PreparedPlanObservation
{
    internal static DbPreparedQueryCommand<T> GetPlan<T>(IPreparedQueryCommand<T> prepared)
    {
        if (prepared is DbPreparedQueryCommand<T> plan)
            return plan;

        return prepared.GetType().GetProperty("Plan", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(prepared) as DbPreparedQueryCommand<T>
            ?? throw new InvalidOperationException("Expected a database query plan or an invocation of one.");
    }
}
