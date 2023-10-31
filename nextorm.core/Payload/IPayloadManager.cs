namespace nextorm.core;

public interface IPayloadManager
{
    bool RemovePayload<T>() where T : class, IPayload;
    bool TryGetPayload<T>(out T? payload) where T : class, IPayload;
    bool TryGetNotNullPayload<T>(out T? payload) where T : class, IPayload;
    T GetNotNullOrAddPayload<T>(Func<T> factory) where T : class, IPayload;
    T? GetOrAddPayload<T>(Func<T?> factory) where T : class, IPayload;
    T? AddOrUpdatePayload<T>(Func<T?> factory) where T : class, IPayload;
    void AddOrUpdatePayload<T>(T? payload) where T : class, IPayload;
}