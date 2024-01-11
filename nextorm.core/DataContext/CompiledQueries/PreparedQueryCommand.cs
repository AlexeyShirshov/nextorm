using System.Runtime.CompilerServices;

namespace nextorm.core;
public class PreparedQueryCommand<TResult, TRecord> : IPreparedQueryCommand
{
    public readonly Func<TRecord, TResult>? MapDelegate;
    private readonly bool _scalar;

    public PreparedQueryCommand(Func<TRecord, TResult>? mapDelegate, bool scalar)
    {
        MapDelegate = mapDelegate;
        _scalar = scalar;
    }
    public PreparedQueryCommand(Func<Func<TRecord, TResult>?> getMap, bool scalar)
    {
        MapDelegate = getMap();
        _scalar = scalar;
    }

    public bool IsScalar
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _scalar;
    }
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public TResult Map(object dataRecord)
    // {
    //     return MapDelegate(dataRecord);
    // }
}
