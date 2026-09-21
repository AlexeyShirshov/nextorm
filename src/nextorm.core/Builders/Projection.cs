namespace NextORM.Core;

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
/// The maximum supported join arity is 8 tables (<c>Projection&lt;T1..Item8&gt;</c>); that projection
/// deliberately does not implement this interface.
/// </para>
/// </summary>
public interface IExtendableProjection : IProjection
{
    IProjection Extend<T>(T newItem);
}

/// <summary>
/// Accumulated result of a two-table join; items are exposed as <c>Item1</c> and <c>Item2</c>.
/// </summary>
public class Projection<T1, T2> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = newItem
        };
    }
}

/// <summary>
/// Accumulated result of a three-table join; items are exposed as <c>Item1</c>..<c>Item3</c>.
/// </summary>
public class Projection<T1, T2, T3> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = Item3,
            Item4 = newItem
        };
    }
}

/// <summary>
/// Accumulated result of a four-table join; items are exposed as <c>Item1</c>..<c>Item4</c>.
/// </summary>
public class Projection<T1, T2, T3, T4> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;
    public T4 Item4 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = Item3,
            Item4 = Item4,
            Item5 = newItem
        };
    }
}

/// <summary>
/// Accumulated result of a five-table join; items are exposed as <c>Item1</c>..<c>Item5</c>.
/// </summary>
public class Projection<T1, T2, T3, T4, T5> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;
    public T4 Item4 { get; init; } = default!;
    public T5 Item5 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = Item3,
            Item4 = Item4,
            Item5 = Item5,
            Item6 = newItem
        };
    }
}

/// <summary>
/// Accumulated result of a six-table join; items are exposed as <c>Item1</c>..<c>Item6</c>.
/// </summary>
public class Projection<T1, T2, T3, T4, T5, T6> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;
    public T4 Item4 { get; init; } = default!;
    public T5 Item5 { get; init; } = default!;
    public T6 Item6 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T6, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = Item3,
            Item4 = Item4,
            Item5 = Item5,
            Item6 = Item6,
            Item7 = newItem
        };
    }
}

/// <summary>
/// Accumulated result of a seven-table join; items are exposed as <c>Item1</c>..<c>Item7</c>.
/// </summary>
public class Projection<T1, T2, T3, T4, T5, T6, T7> : IExtendableProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;
    public T4 Item4 { get; init; } = default!;
    public T5 Item5 { get; init; } = default!;
    public T6 Item6 { get; init; } = default!;
    public T7 Item7 { get; init; } = default!;

    public IProjection Extend<T>(T newItem)
    {
        return new Projection<T1, T2, T3, T4, T5, T6, T7, T>
        {
            Item1 = Item1,
            Item2 = Item2,
            Item3 = Item3,
            Item4 = Item4,
            Item5 = Item5,
            Item6 = Item6,
            Item7 = Item7,
            Item8 = newItem
        };
    }
}

/// <summary>
/// Maximum supported join arity: eight tables (T1..Item8). It intentionally does not implement
/// <see cref="IExtendableProjection"/>, so no ninth item can be absorbed; combining that with
/// <c>JoinedEntityBuilder&lt;T1..Item8&gt;</c> (which exposes no further join methods) makes exceeding
/// the limit a compile-time error.
/// </summary>
public class Projection<T1, T2, T3, T4, T5, T6, T7, T8> : IProjection
{
    public T1 Item1 { get; init; } = default!;
    public T2 Item2 { get; init; } = default!;
    public T3 Item3 { get; init; } = default!;
    public T4 Item4 { get; init; } = default!;
    public T5 Item5 { get; init; } = default!;
    public T6 Item6 { get; init; } = default!;
    public T7 Item7 { get; init; } = default!;
    public T8 Item8 { get; init; } = default!;
}
