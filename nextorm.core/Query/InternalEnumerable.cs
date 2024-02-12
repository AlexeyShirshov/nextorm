using System.Collections;

namespace nextorm.core;

class InternalEnumerable<TResult> : IEnumerable<TResult>
{
    private readonly IEnumerator<TResult> _enumerator;
    public InternalEnumerable(IEnumerator<TResult> enumerator)
    {
        _enumerator = enumerator;
    }
    IEnumerator<TResult> IEnumerable<TResult>.GetEnumerator() => _enumerator;
    IEnumerator IEnumerable.GetEnumerator() => _enumerator;
}
