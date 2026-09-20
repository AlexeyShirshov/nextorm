namespace NextORM.Core;
/// <summary>
/// Base class for prepared (compiled) query commands.
/// </summary>
public class PreparedQueryCommand<TResult, TRecord> : IPreparedQueryCommand<TResult>
{
    public readonly Func<TRecord, TResult>? MapDelegate;

    public PreparedQueryCommand(Func<TRecord, TResult>? mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    public PreparedQueryCommand(Func<Func<TRecord, TResult>?> getMap)
    {
        MapDelegate = getMap();
    }
}
