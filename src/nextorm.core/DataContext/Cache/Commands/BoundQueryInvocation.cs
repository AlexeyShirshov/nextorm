namespace NextORM.Core;

// Carries one preparation's captured values to checkout. Only Plan is eligible for caching.
internal sealed class BoundQueryInvocation<TResult>(
    DbPreparedQueryCommand<TResult> plan,
    object?[] values) : IPreparedQueryCommand<TResult>
{
    internal DbPreparedQueryCommand<TResult> Plan { get; } = plan;
    internal object?[] Values { get; } = values;

    internal static IPreparedQueryCommand<TResult> Create(DbPreparedQueryCommand<TResult> plan, object?[] values)
        => values.Length == 0 ? plan : new BoundQueryInvocation<TResult>(plan, values);

    internal static DbPreparedQueryCommand<TResult>? GetPlan(IPreparedQueryCommand<TResult> prepared)
        => prepared is BoundQueryInvocation<TResult> invocation ? invocation.Plan : prepared as DbPreparedQueryCommand<TResult>;
}
