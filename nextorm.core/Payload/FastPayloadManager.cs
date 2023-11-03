using nextorm.core;

public class FastPayloadManager : IPayloadManager
{
    private readonly IDictionary<Type, object?>? _dic;

    public FastPayloadManager(IDictionary<Type, object?>? dic)
    {
        _dic = dic;
    }
    public T? AddOrUpdatePayload<T>(Func<T?> factory, Func<T?, T?>? update = null) where T : class, IPayload
    {
        if (_dic is null) return factory?.Invoke();

        if (update is not null && _dic.TryGetValue(typeof(T), out var value))
            value = update(value as T);
        else
            value = factory is null ? default : factory();

        _dic[typeof(T)] = value;
        return value as T;
    }
    public void AddOrUpdatePayload<T>(T? payload) where T : class, IPayload
    {
        if (_dic is null) return;

        _dic[typeof(T)] = payload;
    }
    public T GetNotNullOrAddPayload<T>(Func<T> factory) where T : class, IPayload
    {
        if (_dic is null) return factory();

        if (_dic.TryGetValue(typeof(T), out var value) && value is T p)
        {
            if (p is not null)
                return p;
        }

        ArgumentNullException.ThrowIfNull(factory);

        p = factory();

        _dic[typeof(T)] = p;

        return p;
    }

    public T? GetOrAddPayload<T>(Func<T?> factory) where T : class, IPayload
    {
        if (_dic is null) return factory?.Invoke();

        if (!_dic.TryGetValue(typeof(T), out var value))
        {
            value = factory is null ? default : factory();
            _dic[typeof(T)] = value;
        }

        return value as T;
    }

    public bool RemovePayload<T>() where T : class, IPayload
    {
        if (_dic is null) return true;

        return _dic.Remove(typeof(T));
    }

    public bool TryGetNotNullPayload<T>(out T? payload) where T : class, IPayload
    {
        if (_dic is not null && _dic.TryGetValue(typeof(T), out var value) && value is T p && p is not null)
        {
            payload = p;
            return true;
        }
        payload = default;
        return false;
    }

    public bool TryGetPayload<T>(out T? payload) where T : class, IPayload
    {
        if (_dic is null)
        {
            payload = default;
            return false;
        }

        var r = _dic.TryGetValue(typeof(T), out var value);
        if (!r)
        {
            value = null;
        }

        payload = value as T;
        return r;
    }
}