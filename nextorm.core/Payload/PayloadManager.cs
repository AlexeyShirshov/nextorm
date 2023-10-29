using System.Collections;

namespace nextorm.core;
public class PayloadManager : IPayloadManager
{   
    private readonly ArrayList? _payload;
    public PayloadManager(ArrayList? payload)
    {
        _payload=payload;
    }
    private bool _cache => _payload is not null;
    public bool RemovePayload<T>()
        where T : class, IPayload
    {
        if (!_cache) return false;

        foreach (var item in _payload)
        {
            if (item is T)
            {
                _payload.Remove(item);
                return true;
            }
        }
        return false;
    }
    public bool TryGetPayload<T>(out T? payload)
        where T : class, IPayload
    {
        if (_cache)
        {
            foreach (var item in _payload)
            {
                if (item is T p)
                {
                    payload = p;
                    return true;
                }
            }
        }
        payload = default;
        return false;
    }
    public bool TryGetNotNullPayload<T>(out T? payload)
        where T : class, IPayload
    {
        if (_cache)
        {
            foreach (var item in _payload)
            {
                if (item is T p && p is not null)
                {
                    payload = p;
                    return true;
                }
            }
        }
        payload = default;
        return false;
    }
    public T GetNotNullOrAddPayload<T>(Func<T> factory)
        where T : class, IPayload
    {
        if (!TryGetNotNullPayload<T>(out var payload))
        {
            ArgumentNullException.ThrowIfNull(factory);

            payload = factory();
           
            if (_cache)
                _payload.Add(payload);
        }
        return payload!;
    }
    public T? GetOrAddPayload<T>(Func<T?> factory)
        where T : class, IPayload
    {
        if (!TryGetPayload<T>(out var payload))
        {
            payload = factory is null
                ? default
                : factory();
            
            if (_cache)
                _payload.Add(payload);
        }
        return payload;
    }
    public T? AddOrUpdatePayload<T>(Func<T?> factory)
        where T : class, IPayload
    {
        if (_cache)
        {
            for (int i = 0; i < _payload.Count; i++)
            {
                var item = _payload[i];

                if (item is T)
                {
                    var p = factory();
                    _payload[i] = p;
                    return p;
                }
            }
        }

        var payload = factory();

        if (_cache)
            _payload.Add(payload);
        
        return payload;
    }

}
