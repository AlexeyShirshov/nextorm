using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Hand-written async enumerable that reuses an already-created <see cref="IAsyncEnumerator{TResult}"/>.
/// It replaces the compiler-generated async iterator for the streaming path, removing the extra
/// state machine and the <c>yield return enumerator.Current</c> dispatch per row (the mapping now
/// happens inside <c>MoveNextAsync</c>).
/// </summary>
internal sealed class ResultSetAsyncEnumerable<TResult> : IAsyncEnumerable<TResult>
{
    private readonly IAsyncEnumerator<TResult> _enumerator;

    public ResultSetAsyncEnumerable(IAsyncEnumerator<TResult> enumerator)
    {
        _enumerator = enumerator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default) => _enumerator;
}
