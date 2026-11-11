namespace NextORM.Core;

/// <summary>
/// Adapts a synchronous sequence to <see cref="IAsyncEnumerable{T}"/> so the async bulk path can accept
/// a sync source (the async benefit is the database write, not the in-memory enumeration).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class SyncToAsyncEnumerable<T>(IEnumerable<T> source) : IAsyncEnumerable<T>
{
    /// <inheritdoc/>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new Enumerator(source.GetEnumerator(), cancellationToken);

    private sealed class Enumerator(IEnumerator<T> inner, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<bool>(inner.MoveNext());
        }

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
