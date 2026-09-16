using System.Runtime.CompilerServices;

namespace nextorm.core;

public interface IParamProvider
{
    string GetParamName();
}
public class DefaultParamProvider : IParamProvider
{
    private int _paramIdx;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetParamName()
    {
        return string.Format("p{0}", _paramIdx++);
    }

}