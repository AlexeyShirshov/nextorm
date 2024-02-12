using System.Reflection;

namespace nextorm.core;

public class OuterRefMarker<T>(int i)
{
    public int I { get; } = i;
    public T? Ref { get; }

}