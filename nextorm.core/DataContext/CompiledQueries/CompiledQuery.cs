#define PARAM_CONDITION
using System.Buffers;
using System.Data;
using System.Data.Common;

namespace nextorm.core;
public class CompiledQuery<TResult, TRecord>
{
    public readonly Func<TRecord, TResult>? MapDelegate;

    public CompiledQuery(Func<TRecord, TResult>? mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    public CompiledQuery(Func<Func<TRecord, TResult>?> getMap)
    {
        MapDelegate = getMap();
    }
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public TResult Map(object dataRecord)
    // {
    //     return MapDelegate(dataRecord);
    // }
}
