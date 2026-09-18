using System.Runtime.CompilerServices;

namespace nextorm.core;

/// <summary>
/// Allocates parameter names (for example <c>p0</c>, <c>p1</c>, ...) while a command is built.
/// </summary>
/// <remarks>
/// <c>Param</c> abbreviates <c>Parameter</c>; the convention-compliant name is
/// <c>IParameterProvider</c>. See <c>API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public interface IParamProvider
{
    string GetParamName();
}
/// <summary>
/// Default <see cref="IParamProvider"/> that hands out sequentially numbered parameter names.
/// </summary>
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