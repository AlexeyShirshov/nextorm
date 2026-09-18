namespace nextorm.core;

/// <summary>
/// Assorted extension helpers: conditional capture and in-place transformation.
/// </summary>
/// <remarks>
/// The name is a grab-bag and the containing file is misspelled (<c>BasicHelpders.cs</c>). The
/// <see cref="Var{T}(T, Func{T, bool}, out T)"/> helper follows the try-pattern but is not named
/// <c>Try*</c>. Consider splitting these helpers by purpose and renaming them.
/// See <c>API-NAMING-REVIEW.md</c> finding P0-8.
/// </remarks>
public static class BasicHelpers
{
    /// <summary>
    /// Captures <paramref name="item"/> into <paramref name="variable"/> when
    /// <paramref name="condition"/> holds, and returns whether it did. Follows the try-pattern but is
    /// not named <c>TryVar</c>.
    /// </summary>
    public static bool Var<T>(this T item, Func<T, bool> condition, out T? variable)
    {
        variable = default;
        var r = condition(item);
        if (r) variable = item;
        return r;
    }
    public static T Transform<T>(this T item, Action<T> work)
    {
        work?.Invoke(item);
        return item;
    }
    public static T Transform<T>(this T item, Func<T, T> work)
    {
        return work is null ? item : work(item);
    }
    public static async Task<T> Transform<T>(this T item, Func<T, Task>? work)
    {
        await (work?.Invoke(item) ?? Task.CompletedTask);
        return item;
    }
    public static async Task<T> Transform<T>(this T item, Func<T, Task<T>> work)
    {
        if (work is not null)
            return await work(item);
        return item;
    }
    public static T2 Map<T, T2>(this T item, Func<T, T2> work)
    {
        return work(item);
    }
    public static Task<T2> Map<T, T2>(this T item, Func<T, Task<T2>> work)
    {
        return work(item);
    }
    public static T Create<T>(Func<T, T> work)
        where T : new()
    {
        return work is null ? new T() : work(new T());
    }
    public static T Create<T>(Action<T> work)
        where T : new()
    {
        var item = new T();
        work?.Invoke(item);
        return item;
    }
    public static async Task<T> Create<T>(Func<T, Task<T>> work)
        where T : new()
    {
        var item = new T();
        if (work is not null)
            return await work(item);
        return item;
    }
    public static async Task<T> Create<T>(Func<T, Task>? work)
        where T : new()
    {
        var item = new T();
        await (work?.Invoke(item) ?? Task.CompletedTask);
        return item;
    }
}