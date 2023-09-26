namespace nextorm.core;

public class ListEnumerator<TResult> : IAsyncEnumerator<TResult>
{
    
    public TResult Current => throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public ValueTask<bool> MoveNextAsync()
    {
        throw new NotImplementedException();
    }
}