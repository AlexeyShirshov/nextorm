using System.Runtime.CompilerServices;

namespace nextorm.core;

public interface IParamProvider
{
    string GetParamName();
}
public class DefaultParamProvider : IParamProvider
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