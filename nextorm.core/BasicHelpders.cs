namespace nextorm.core;
public static class BasicHelpers
{
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
        await work?.Invoke(item);
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
        await work?.Invoke(item);
        return item;
    }
}