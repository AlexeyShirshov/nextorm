namespace nextorm.core;

public class Projection<T1, T2>
{
    public T1 t1 { get; init; }
    public T2 t2 { get; init; }
}
public class Projection<T1, T2, T3>
{
    public T1 t1 { get; init; }
    public T2 t2 { get; init; }
    public T3 t3 { get; init; }
}