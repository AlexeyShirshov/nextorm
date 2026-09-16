namespace nextorm.core;
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
