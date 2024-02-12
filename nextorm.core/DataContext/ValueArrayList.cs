using System.Collections;

namespace nextorm.core;

#if NET8_0_OR_GREATER
    [System.Runtime.CompilerServices.InlineArray(10)]
    public struct Buffer10
    {
        private object _;
    }

    public struct ValueArrayList
    {
        private const int MaxLength = 10;
        private int _curIdx;
        private Buffer10 _buffer;
        private ArrayList? _arr;

        public ValueArrayList()
        {
            _curIdx = 0;
        }
        public readonly int Length => _curIdx;
        public void Add(object item)
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
    }
#endif
