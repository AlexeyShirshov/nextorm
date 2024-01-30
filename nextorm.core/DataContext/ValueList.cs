using System.Collections;

namespace nextorm.core;

#if NET8_0_OR_GREATER
[System.Runtime.CompilerServices.InlineArray(10)]
public struct Buffer10<T>
{
    private T _;
}
public struct ValueList<T>
{
    private const int MaxLength = 10;
    private int _curIdx;
    private Buffer10<T> _buffer;
    private List<T>? _arr;

    public ValueList()
    {
        _curIdx = 0;
    }
    public readonly int Length => _curIdx;
    public void Add(T item)
    {
        if (_curIdx == MaxLength)
        {
            _arr ??= [.. _buffer];
            _arr.Add(item);
            _curIdx++;
        }
        else
        {
            _buffer[_curIdx++] = item;
        }
    }
    public readonly T Get(int idx)
    {
        if (_curIdx < MaxLength)
            return _buffer[idx];

        return _arr![idx];
    }
}
#endif
