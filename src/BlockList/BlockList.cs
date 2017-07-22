using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Clever.Collections
{
    public class BlockList<T> : ICollection<T>
    {
        // TODO: Add XML docs for everything.

        private readonly int _initialCapacity;

        private SmallList<T[]> _tail; // This is a mutable struct field; do not make it readonly.
        private T[] _head;
        private int _headCount;
        private int _count;
        private int _capacity;
        
        public BlockList()
            : this(initialCapacity: 32)
        {
        }

        public BlockList(int initialCapacity)
        {
            Debug.Assert(initialCapacity > 0);

            _head = Array.Empty<T>();
            _initialCapacity = initialCapacity;
        }

        public BlockList(IEnumerable<T> enumerable)
            : this()
        {
            AddRange(enumerable);
        }

        public BlockList(int initialCapacity, IEnumerable<T> enumerable)
            : this(initialCapacity)
        {
            AddRange(enumerable);
        }

        public int BlockCount => TailCount + 1;

        public int Capacity => _capacity;

        public int Count => _count;

        private int HeadCapacity => _head.Length;

        private ArraySegment<T> HeadSpan => new ArraySegment<T>(_head, 0, _headCount);

        private bool IsHeadFull => _headCount == HeadCapacity;

        private int TailCount => _tail.Count;

        public void Add(T item)
        {
            if (IsHeadFull)
            {
                MakeRoom();
            }

            _head[_headCount++] = item;
        }

        public void AddRange(IEnumerable<T> enumerable)
        {
            foreach (T item in enumerable)
            {
                Add(item);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < TailCount; i++)
            {
                T[] block = _tail[i];
                _tail[i] = null;
                Array.Clear(block, 0, block.Length);
            }
            _tail = new SmallList<T[]>();

            Array.Clear(_head, 0, _headCount);
            _head = Array.Empty<T>();

            _headCount = 0;
            _count = 0;
            _capacity = 0;
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < TailCount; i++)
            {
                T[] block = _tail[i];
                if (Array.IndexOf(block, item) >= 0)
                {
                    return true;
                }
            }

            return Array.IndexOf(_head, item, 0, _headCount) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < TailCount; i++)
            {
                T[] block = _tail[i];
                Array.Copy(block, 0, array, arrayIndex, block.Length);
                arrayIndex += block.Length;
            }

            Array.Copy(_head, 0, array, arrayIndex, _headCount);
        }

        public ArraySegment<T> GetBlock(int index)
        {
            if (index < TailCount)
            {
                return new ArraySegment<T>(_tail[index]);
            }

            Debug.Assert(index == TailCount);
            return HeadSpan;
        }

        public ArraySegment<T>[] GetBlocks()
        {
            var blocks = new ArraySegment<T>[BlockCount];
            for (int i = 0; i < TailCount; i++)
            {
                blocks[i] = new ArraySegment<T>(_tail[i]);
            }
            blocks[TailCount] = HeadSpan;
            return blocks;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public T[] ToArray()
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }

            var array = new T[_count];
            CopyTo(array, 0);
            return array;
        }

        private void MakeRoom()
        {
            Debug.Assert(IsHeadFull);

            if (_count == 0)
            {
                _head = new T[_initialCapacity];
                _capacity = _initialCapacity;
                return;
            }

            _tail.Add(_head);
            // We want to increase the block sizes geometrically, but not on the first resize.
            // This ensures we never waste more than 50% of the memory we've allocated.
            int nextCapacity = _capacity == _initialCapacity
                ? _initialCapacity
                : HeadCapacity * 2;
            _head = new T[nextCapacity];
            _headCount = 0;
            _capacity += nextCapacity;
        }

        bool ICollection<T>.IsReadOnly => false;

        bool ICollection<T>.Remove(T item) => throw new NotImplementedException();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private readonly BlockList<T> _list;
            private ArraySegment<T> _currentBlock;
            private int _blockIndex;
            private int _elementIndex;

            internal Enumerator(BlockList<T> list)
                : this()
            {
                _list = list;
                _currentBlock = list.GetBlock(0);
                _elementIndex = -1;
            }

            // This can be changed to index _currentBlock directly once ArraySegment.this[] is available.
            public T Current => _currentBlock.Array[_currentBlock.Offset + _elementIndex];

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                int elementIndex = _elementIndex + 1;
                if (elementIndex == _currentBlock.Count)
                {
                    int blockIndex = _blockIndex + 1;
                    if (blockIndex == _list.BlockCount)
                    {
                        return false;
                    }

                    _currentBlock = _list.GetBlock(blockIndex);
                    _blockIndex = blockIndex;
                }

                _elementIndex = elementIndex;
                return true;
            }

            object IEnumerator.Current => Current;

            void IEnumerator.Reset() => throw new NotSupportedException();
        }
    }
}
