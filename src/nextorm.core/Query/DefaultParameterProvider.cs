using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Default <see cref="IParameterProvider"/> that hands out sequentially numbered parameter names.
/// </summary>
public class DefaultParameterProvider : IParameterProvider
{
    // p0..pN: shared name cache; the growth logic lives in ParamNameCache.
    private static readonly ParamNameCache _paramNames = new("p");
    private int _paramIdx;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetParamName()
    {
        var index = _paramIdx++;
        return _paramNames.Get(index);
    }
}
