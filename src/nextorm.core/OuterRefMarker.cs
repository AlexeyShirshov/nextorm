using System.Reflection;

namespace NextORM.Core;

public class OuterRefMarker<T>(int i)
{
    public int I { get; } = i;
    public T? Ref { get; }

}