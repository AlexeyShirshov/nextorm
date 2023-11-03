namespace nextorm.core;
public interface IProjection
{
    IProjection Extend<T>(T newItem);
}
public class Projection<T1, T2> : IProjection
{
    public T1 t1 { get; init; }
    public T2 t2 { get; init; }

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = newItem
        };
    }
}
public class Projection<T1, T2, T3> : IProjection
{
    public T1 t1 { get; init; }
    public T2 t2 { get; init; }
    public T3 t3 { get; init; }

    public IProjection Extend<T>(T newItem)
    {
        throw new NotImplementedException();
    }
}