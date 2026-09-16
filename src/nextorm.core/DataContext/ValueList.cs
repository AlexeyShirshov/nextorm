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
    private List<T>? _list;

    public ValueList()
    {
        _curIdx = 0;
    }
    public readonly int Count => _curIdx;
    public void Add(T item)
    {
        if (_curIdx >= MaxLength || _list is not null)
        {
            _list ??= [.. _buffer];
            _list.Add(item);
            _curIdx++;
        }
        else
        {
            _buffer[_curIdx++] = item;
        }
    }

    public void Pop()
    {
        if (_list is null)
        {
            _curIdx -= 1;
        }
        else
            _list.RemoveAt(_list.Count - 1);
    }
    public readonly T Peek()
    {
        if (_list is null)
        {
            return _buffer[_curIdx - 1];
        }
        else
            return _list[^1];
    }
    public T this[int idx]
    {
        get
        {
            if (_list is null)
                return _buffer[idx];

            return _list![idx];
        }
        set
        {
            if (_list is null)
                _buffer[idx] = value;
            else
                _list![idx] = value;
        }
    }
}
[System.Runtime.CompilerServices.InlineArray(3)]
public struct Buffer3<T>
{
    private T _;
}
public struct ValueList3<T>
{
    private const int MaxLength = 3;
    private int _curIdx;
    private Buffer3<T> _buffer;
    private List<T>? _list;

    public ValueList3()
    {
        _curIdx = 0;
    }
    public readonly int Count => _curIdx;
    public void Add(T item)
    {
        if (_curIdx >= MaxLength || _list is not null)
        {
            _list ??= [.. _buffer];
            _list.Add(item);
            _curIdx++;
        }
        else
        {
            _buffer[_curIdx++] = item;
        }
    }

    public void Pop()
    {
        if (_list is null)
        {
            _curIdx -= 1;
        }
        else
            _list.RemoveAt(_list.Count - 1);
    }
    public readonly T Peek()
    {
        if (_list is null)
        {
            return _buffer[_curIdx - 1];
        }
        else
            return _list[^1];
    }
    public T this[int idx]
    {
        get
        {
            if (_list is null)
                return _buffer[idx];

            return _list![idx];
        }
        set
        {
            if (_list is null)
                _buffer[idx] = value;
            else
                _list![idx] = value;
        }
    }
}
#else
public static class ListExtensions
{
    public static void Pop<T>(this List<T> list)=>list.RemoveAt(list.Count - 1);
    public static T Peek<T>(this List<T> list)=>list[list.Count - 1];
}
#endif
