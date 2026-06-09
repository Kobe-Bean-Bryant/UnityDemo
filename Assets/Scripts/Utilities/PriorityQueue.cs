#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic
{
    /// <summary>
    /// Represents a min priority queue, ported from .NET official source
    /// (<see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs"/>).
    /// <para>
    /// Implements an array-backed quaternary min-heap. Each element is enqueued
    /// with an associated priority that determines the dequeue order: elements
    /// with the lowest priority are dequeued first.
    /// </para>
    /// <para>
    /// Provided as a drop-in replacement for Unity projects where the official
    /// <c>System.Collections.Generic.PriorityQueue</c> is not yet available.
    /// </para>
    /// </summary>
    /// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
    /// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
    public class PriorityQueue<TElement, TPriority> : IReadOnlyCollection<(TElement Element, TPriority Priority)>
    {
        private const int Arity = 4;
        private const int Log2Arity = 2;

        private (TElement Element, TPriority Priority)[] _nodes;
        private readonly IComparer<TPriority>? _comparer;
        private int _size;
        private int _version;

        #region Constructors

        public PriorityQueue()
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = null;
        }

        public PriorityQueue(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _nodes = new (TElement, TPriority)[initialCapacity];
            _comparer = null;
        }

        public PriorityQueue(IComparer<TPriority>? comparer)
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = comparer;
        }

        public PriorityQueue(int initialCapacity, IComparer<TPriority>? comparer)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _nodes = new (TElement, TPriority)[initialCapacity];
            _comparer = comparer;
        }

        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items)
            : this(items, comparer: null)
        {
        }

        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IComparer<TPriority>? comparer)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = comparer;

            int count = 0;
            var collection = items as ICollection<(TElement, TPriority)>;
            if (collection != null)
            {
                count = collection.Count;
                if (count > 0)
                {
                    Grow(count);
                    collection.CopyTo(_nodes, 0);
                    _size = count;
                }
            }
            else
            {
                foreach (var item in items)
                {
                    if (_size == _nodes.Length) Grow(_size + 1);
                    _nodes[_size++] = item;
                }
            }

            if (_size > 1)
                Heapify();
        }

        #endregion

        #region Public Properties

        public int Count => _size;

        public IComparer<TPriority> Comparer => _comparer ?? Comparer<TPriority>.Default;

        #endregion

        #region Enqueue / Dequeue / Peek

        public void Enqueue(TElement element, TPriority priority)
        {
            int currentSize = _size;
            if (_nodes.Length == currentSize)
                Grow(currentSize + 1);

            _version++;
            _size = currentSize + 1;

            if (currentSize == 0)
            {
                _nodes[0] = (element, priority);
                return;
            }

            MoveUp((element, priority), currentSize);
        }

        public TElement Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException("The queue is empty.");

            return _nodes[0].Element;
        }

        public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        public TElement Dequeue()
        {
            if (_size == 0)
                throw new InvalidOperationException("The queue is empty.");

            (TElement element, TPriority priority) = _nodes[0];
            RemoveRootNode();
            return element;
        }

        public bool TryDequeue([MaybeNullWhen(false)] out TElement element,
            [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                RemoveRootNode();
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (_size != 0)
            {
                (TElement rootElement, TPriority rootPriority) = _nodes[0];

                if (_comparer != null)
                {
                    if (_comparer.Compare(priority, rootPriority) <= 0)
                        return element; // new element has higher or equal priority, keep root
                }
                else
                {
                    int cmp = Comparer<TPriority>.Default.Compare(priority, rootPriority);
                    if (cmp <= 0)
                        return element;
                }

                RemoveRootNode();
                MoveUp((element, priority), _size);
                return rootElement;
            }

            // queue was empty
            _nodes[0] = (element, priority);
            _size = 1;
            _version++;
            return element;
        }

        public void Clear()
        {
            if (_size > 0)
            {
                Array.Clear(_nodes, 0, _size);
                _size = 0;
                _version++;
            }
        }

        #endregion

        #region UnorderedElements

        /// <summary>
        /// Enumerates the contents of the queue without any ordering guarantees.
        /// </summary>
        public UnorderedValuesCollection UnorderedElements => new UnorderedValuesCollection(this);

        public readonly struct UnorderedValuesCollection : IReadOnlyCollection<(TElement Element, TPriority Priority)>
        {
            private readonly PriorityQueue<TElement, TPriority> _queue;

            public UnorderedValuesCollection(PriorityQueue<TElement, TPriority> queue)
            {
                _queue = queue;
            }

            public int Count => _queue._size;

            public Enumerator GetEnumerator() => new Enumerator(_queue);

            IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.
                GetEnumerator()
                => new Enumerator(_queue);

            IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_queue);

            public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)>
            {
                private readonly PriorityQueue<TElement, TPriority> _queue;
                private readonly int _version;
                private int _index;

                public Enumerator(PriorityQueue<TElement, TPriority> queue)
                {
                    _queue = queue;
                    _version = queue._version;
                    _index = -1;
                }

                public (TElement Element, TPriority Priority) Current => _queue._nodes[_index];
                object IEnumerator.Current => _queue._nodes[_index];

                public bool MoveNext()
                {
                    if (_version != _queue._version)
                        throw new InvalidOperationException(
                            "Collection was modified after the enumerator was created.");

                    return ++_index < _queue._size;
                }

                public void Reset()
                {
                    if (_version != _queue._version)
                        throw new InvalidOperationException(
                            "Collection was modified after the enumerator was created.");

                    _index = -1;
                }

                public void Dispose()
                {
                }
            }
        }

        #endregion

        #region IReadOnlyCollection

        IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.
            GetEnumerator()
            => UnorderedElements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => UnorderedElements.GetEnumerator();

        #endregion

        #region Private Heap Operations

        private void RemoveRootNode()
        {
            int currentSize = --_size;
            _version++;

            if (currentSize > 0)
            {
                (TElement Element, TPriority Priority) lastNode = _nodes[currentSize];
                _nodes[currentSize] = default;

                MoveDown(lastNode, 0);
            }
            else
            {
                _nodes[0] = default;
            }
        }

        private void Heapify()
        {
            var nodes = _nodes;
            int parentIdx = (_size - 2) >> Log2Arity;

            while (parentIdx >= 0)
            {
                MoveDown(nodes[parentIdx], parentIdx);
                parentIdx--;
            }
        }

        private void MoveUp((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            var nodes = _nodes;
            var comparer = _comparer;

            int i = nodeIndex;
            while (i > 0)
            {
                int parentIndex = (i - 1) >> Log2Arity;
                (TElement Element, TPriority Priority) parent = nodes[parentIndex];

                if (comparer != null)
                {
                    if (comparer.Compare(node.Priority, parent.Priority) < 0)
                    {
                        nodes[i] = parent;
                        i = parentIndex;
                    }
                    else break;
                }
                else
                {
                    if (Comparer<TPriority>.Default.Compare(node.Priority, parent.Priority) < 0)
                    {
                        nodes[i] = parent;
                        i = parentIndex;
                    }
                    else break;
                }
            }

            nodes[i] = node;
        }

        private void MoveDown((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            var nodes = _nodes;
            int size = _size;
            int i = nodeIndex;
            var comparer = _comparer;

            while (true)
            {
                int childIndex = (i << Log2Arity) + 1;
                if (childIndex >= size) break;

                int minChildIndex = childIndex;
                TPriority minChildPriority = nodes[childIndex].Priority;

                for (int j = childIndex + 1; j < childIndex + Arity && j < size; j++)
                {
                    TPriority childPriority = nodes[j].Priority;
                    int cmp = comparer != null
                        ? comparer.Compare(childPriority, minChildPriority)
                        : Comparer<TPriority>.Default.Compare(childPriority, minChildPriority);

                    if (cmp < 0)
                    {
                        minChildIndex = j;
                        minChildPriority = childPriority;
                    }
                }

                int cmp2 = comparer != null
                    ? comparer.Compare(node.Priority, minChildPriority)
                    : Comparer<TPriority>.Default.Compare(node.Priority, minChildPriority);

                if (cmp2 > 0)
                {
                    nodes[i] = nodes[minChildIndex];
                    i = minChildIndex;
                }
                else break;
            }

            nodes[i] = node;
        }

        private void Grow(int minCapacity)
        {
            const int GrowFactor = 2;
            const int MinimumGrow = 4;

            int newCapacity = GrowFactor * _nodes.Length;
            if ((uint)newCapacity > int.MaxValue) newCapacity = int.MaxValue;
            newCapacity = Math.Max(newCapacity, _nodes.Length + MinimumGrow);

            if (newCapacity < minCapacity) newCapacity = minCapacity;

            Array.Resize(ref _nodes, newCapacity);
        }

        #endregion
    }
}
