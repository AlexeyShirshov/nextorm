namespace nextorm.core;

/// <summary>
/// Marker for an accumulated join projection. It declares no members, but it is observed by the
/// SQL builder, the mapping code and the in-memory provider (via
/// <c>IsAssignableTo(typeof(IProjection))</c>), so the marker carries meaning. The ability to
/// absorb one more item is expressed separately by <see cref="IExtendableProjection"/>, because it
/// is only supported below the maximum join arity.
/// </summary>
public interface IProjection
{
}

/// <summary>
/// An <see cref="IProjection"/> that can absorb one more item. Only projections below the maximum
/// supported join arity implement it, so callers probe with <c>is</c> rather than assuming every
/// projection can be extended.
/// <para>
/// The maximum supported join arity is 8 tables (<c>Projection&lt;T1..T8&gt;</c>); that projection
/// deliberately does not implement this interface.
/// </para>
/// </summary>
public interface IExtendableProjection : IProjection
{
    IProjection Extend<T>(T newItem);
}

public class Projection<T1, T2> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;

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

public class Projection<T1, T2, T3> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = t3,
            t4 = newItem
        };
    }
}

public class Projection<T1, T2, T3, T4> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;
    public T4 t4 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = t3,
            t4 = t4,
            t5 = newItem
        };
    }
}

public class Projection<T1, T2, T3, T4, T5> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;
    public T4 t4 { get; init; } = default!;
    public T5 t5 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = t3,
            t4 = t4,
            t5 = t5,
            t6 = newItem
        };
    }
}

public class Projection<T1, T2, T3, T4, T5, T6> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;
    public T4 t4 { get; init; } = default!;
    public T5 t5 { get; init; } = default!;
    public T6 t6 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T6, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = t3,
            t4 = t4,
            t5 = t5,
            t6 = t6,
            t7 = newItem
        };
    }
}

public class Projection<T1, T2, T3, T4, T5, T6, T7> : IExtendableProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;
    public T4 t4 { get; init; } = default!;
    public T5 t5 { get; init; } = default!;
    public T6 t6 { get; init; } = default!;
    public T7 t7 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T6, T7, T>
        {
            t1 = t1,
            t2 = t2,
            t3 = t3,
            t4 = t4,
            t5 = t5,
            t6 = t6,
            t7 = t7,
            t8 = newItem
        };
    }
}

/// <summary>
/// Maximum supported join arity: eight tables (T1..T8). It intentionally does not implement
/// <see cref="IExtendableProjection"/>, so no ninth item can be absorbed; combining that with
/// <c>EntityP8&lt;T1..T8&gt;</c> (which exposes no further join methods) makes exceeding the
/// limit a compile-time error.
/// </summary>
public class Projection<T1, T2, T3, T4, T5, T6, T7, T8> : IProjection
{
    public T1 t1 { get; init; } = default!;
    public T2 t2 { get; init; } = default!;
    public T3 t3 { get; init; } = default!;
    public T4 t4 { get; init; } = default!;
    public T5 t5 { get; init; } = default!;
    public T6 t6 { get; init; } = default!;
    public T7 t7 { get; init; } = default!;
    public T8 t8 { get; init; } = default!;
}
