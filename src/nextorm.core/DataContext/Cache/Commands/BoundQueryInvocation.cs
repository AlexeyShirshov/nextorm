using System.Data;

namespace NextORM.Core;

// Carries one preparation's captured values and caller-owned mapper to execution.
// Only Plan is eligible for caching.
internal sealed class BoundQueryInvocation<TResult>(
    DbPreparedQueryCommand<TResult> plan,
    object?[]? values,
    Func<IDataRecord, TResult>? mapper) : IPreparedQueryCommand<TResult>
{
    internal DbPreparedQueryCommand<TResult> Plan { get; } = plan;
    internal object?[] Values { get; } = values ?? [];
    internal Func<IDataRecord, TResult>? Mapper { get; } = mapper;

    internal static IPreparedQueryCommand<TResult> Create(DbPreparedQueryCommand<TResult> plan, object?[] values)
        => Create(plan, values, null);

    internal static IPreparedQueryCommand<TResult> Create(DbPreparedQueryCommand<TResult> plan, object?[]? values,
        Func<IDataRecord, TResult>? mapper)
        => values is not { Length: > 0 } && mapper is null
            ? plan
            : new BoundQueryInvocation<TResult>(plan, values, mapper);

    internal static DbPreparedQueryCommand<TResult>? GetPlan(IPreparedQueryCommand<TResult> prepared)
        => prepared is BoundQueryInvocation<TResult> invocation ? invocation.Plan : prepared as DbPreparedQueryCommand<TResult>;
}
