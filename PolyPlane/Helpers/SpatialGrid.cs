using System.Collections;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides a dynamic sparse 2D spatial grid for fast nearest-neighbor searching.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// Create a new instance of <see cref="SpatialGrid{T}"/>
    /// </remarks>
    /// <param name="positionSelector">Selector for current object positions. Grid positions are updated on <see cref="Update"/></param>
    /// <param name="isExpiredSelector">Selector for object expired status.  Expired objects are removed on <see cref="Update"/></param>
    /// <param name="sideLen">And integer (S) representing the grid cell side length (L), L = 1 << S. </param>
    public sealed class SpatialGrid<T>
    {
        private Dictionary<int, EntrySequence> _sequences = new(1000);
        private List<Entry> _entries = new(50000);
        private Stack<int> _freeIndices = new(1000);
        private GetInViewportEnumerator _viewportEnumerator;
        private GetNearEnumerator _getNearEnumerator;

        private readonly Func<T, D2DPoint> _positionSelector;
        private readonly Func<T, bool> _isExpiredSelector;
        private readonly int SIDE_LEN = 9;

        public SpatialGrid(Func<T, D2DPoint> positionSelector, Func<T, bool> isExpiredSelector, int sideLen = 9)
        {
            _positionSelector = positionSelector;
            _isExpiredSelector = isExpiredSelector;
            SIDE_LEN = sideLen;

            _viewportEnumerator = new GetInViewportEnumerator(this);
            _getNearEnumerator = new GetNearEnumerator(this);
        }

        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Compute new hashes in parallel.
            ComputeNextHashes();

            // Clear free entries left over from the previous turn.
            PruneFreeEntries();

            // Iterate all entries and update as needed.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];

                // Remove expired entries.
                if (_isExpiredSelector(entry.Item))
                {
                    RemoveFromSequence(entry);

                    // Record the free index to possibly be reused before the next turn.
                    _freeIndices.Push(i);
                }
                else
                {
                    // Move entry to a new sequence.
                    var curHash = entry.CurrentHash;
                    if (curHash != entry.NextHash)
                    {
                        MoveEntry(entry);
                    }
                }
            }

            PruneSeqs();
        }

        private void PruneFreeEntries()
        {
            foreach (var idx in _freeIndices.OrderByDescending(i => i))
            {
                _entries[idx] = _entries[_entries.Count - 1];
                _entries.RemoveAt(_entries.Count - 1);
            }

            _freeIndices.Clear();
        }

        /// <summary>
        /// Removes any empty sequences left over after updating.
        /// </summary>
        private void PruneSeqs()
        {
            foreach (var seq in _sequences.Values)
            {
                if (seq.IsEmpty)
                {
                    _sequences.Remove(seq.Hash);
                }
            }
        }

        /// <summary>
        /// Move the specified entry to its new sequence.
        /// </summary>
        /// <param name="entry"></param>
        private void MoveEntry(Entry entry)
        {
            RemoveFromSequence(entry);

            AddToSequence(entry);

            entry.CurrentHash = entry.NextHash;
        }

        /// <summary>
        /// Removes the specified entry from its current sequence and ensures the previous sequence is correctly linked.
        /// </summary>
        /// <param name="entry"></param>
        private void RemoveFromSequence(Entry entry)
        {
            var entrySeq = entry.Sequence;

            if (entry.IsHead)
            {
                // Last entry in the sequence?
                if (entry.Next == null)
                {
                    // Set sequence empty.
                    // It will be reused if another item moves into its cell.
                    entrySeq.IsEmpty = true;
                    entrySeq.Head = null;
                    entrySeq.Tail = null;

                    entry.IsHead = false;
                    entry.Prev = null;
                    entry.Next = null;
                }
                else
                {
                    // Otherwise move the head up to the next entry.
                    entry.Next.IsHead = true;
                    entry.Next.Prev = null;
                    entrySeq.Head = entry.Next;

                    entry.Prev = null;
                    entry.Next = null;
                }

                entry.IsHead = false;
            }
            else
            {
                var prev = entry.Prev;
                var next = entry.Next;

                if (prev != null && next == null)
                {
                    // Tail entry: Move tail to previous entry.
                    entrySeq.Tail = prev;
                    prev.Next = null;

                }
                else if (prev != null && next != null)
                {
                    // Middle entry: Link previous and next entries together.
                    prev.Next = next;
                    next.Prev = prev;

                    entry.Prev = null;
                    entry.Next = null;
                }
            }
        }

        /// <summary>
        /// Adds/inserts the specified entry into a matching sequence. Or creates a new one if one does not already exist.
        /// </summary>
        /// <param name="entry"></param>
        private void AddToSequence(Entry entry)
        {
            var hash = entry.NextHash;

            if (_sequences.TryGetValue(hash, out var existingSeq))
            {
                existingSeq.Hash = hash;
                entry.Sequence = existingSeq;

                // Add to existing sequence.
                if (existingSeq.IsEmpty)
                {
                    existingSeq.IsEmpty = false;
                    existingSeq.Head = entry;
                    entry.IsHead = true;
                    entry.Next = null;
                    entry.Prev = null;
                }
                else
                {
                    // If the sequence has a tail, add to the end.
                    // Otherwise add after the head.
                    var swapEntry = existingSeq.Head;

                    if (existingSeq.Tail != null)
                        swapEntry = existingSeq.Tail;

                    entry.Prev = swapEntry;
                    swapEntry.Next = entry;

                    existingSeq.Tail = entry;
                    entry.Next = null;
                }
            }
            else
            {
                // Add to new sequence.
                entry.IsHead = true;
                entry.Prev = null;
                entry.Next = null;

                var newIndex = new EntrySequence(entry);

                newIndex.Hash = hash;
                entry.Sequence = newIndex;

                _sequences.Add(hash, newIndex);
            }
        }

        /// <summary>
        /// Add object to the spatial grid.
        /// </summary>
        /// <param name="obj"></param>
        public void Add(T obj)
        {
            var hash = GetGridHash(obj);
            AddInternal(hash, obj);
        }

        private void AddInternal(int hash, T obj)
        {
            Entry entry;

            // Reuse freed entries if any are available.
            // Otherwise allocate and add a new one.
            if (_freeIndices.TryPop(out int idx))
            {
                entry = _entries[idx];

                entry.IsHead = false;
                entry.CurrentHash = hash;
                entry.NextHash = hash;
                entry.Next = null;
                entry.Prev = null;
                entry.Sequence = null;
                entry.Item = obj;
            }
            else
            {
                entry = new Entry(hash, obj);

                _entries.Add(entry);
            }


            AddToSequence(entry);
        }

        /// <summary>
        /// Get all objects within neighboring grid cells of the specified object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IEnumerable<T> GetNear(T obj)
        {
            _getNearEnumerator.Begin(obj);
            return _getNearEnumerator;
        }

        /// <summary>
        /// Get all objects within neighboring grid cells of the specified position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public IEnumerable<T> GetNear(D2DPoint position)
        {
            GetGridIdx(position, out int idxX, out int idxY);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var xo = idxX + x;
                    var yo = idxY + y;
                    var nHash = GetGridHash(xo, yo);

                    if (_sequences.TryGetValue(nHash, out var index))
                    {
                        var cur = index.Head;

                        while (cur != null)
                        {
                            yield return cur.Item;

                            cur = cur.Next;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all objects within the specified rectangle.
        /// </summary>
        /// <param name="viewport"></param>
        /// <returns></returns>
        public IEnumerable<T> GetInViewport(D2DRect viewport)
        {
            _viewportEnumerator.Begin(viewport);
            return _viewportEnumerator;
        }

        public void Clear()
        {
            _entries.Clear();
            _sequences.Clear();
        }

        private void ComputeNextHashes()
        {
            ParallelHelpers.ParallelForSlim(_entries.Count, ComputeHashes);
        }

        private void ComputeHashes(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                var item = _entries[i];
                var newHash = GetGridHash(item.Item);
                item.NextHash = newHash;
            }
        }

        private void GetGridIdx(D2DPoint pos, out int idxX, out int idxY)
        {
            idxX = (int)Math.Floor(pos.X) >> SIDE_LEN;
            idxY = (int)Math.Floor(pos.Y) >> SIDE_LEN;
        }

        private void GetGridIdx(T obj, out int idxX, out int idxY)
        {
            GetGridIdx(_positionSelector(obj), out idxX, out idxY);
        }

        private int GetGridHash(T obj)
        {
            GetGridIdx(_positionSelector(obj), out int idxX, out int idxY);
            return GetGridHash(idxX, idxY);
        }

        private int GetGridHash(int idxX, int idxY)
        {
            return HashCode.Combine(idxX, idxY);
        }

        private class EntrySequence
        {
            public Entry? Head;
            public Entry? Tail;
            public int Hash;
            public bool IsEmpty = false;

            public EntrySequence(Entry head)
            {
                Head = head;
                Tail = null;
                IsEmpty = false;
            }
        }

        private class Entry
        {
            public int NextHash;
            public int CurrentHash;
            public T Item;

            public bool IsHead = false;

            public EntrySequence? Sequence;

            public Entry? Prev;
            public Entry? Next;

            public Entry(int curHash, T item)
            {
                IsHead = false;
                NextHash = curHash;
                CurrentHash = curHash;

                Sequence = null;
                Prev = null;
                Next = null;

                Item = item;
            }
        }

        private sealed class GetInViewportEnumerator : IEnumerable<T>, IEnumerable, IEnumerator<T>, IEnumerator, IDisposable
        {
            private int _state;

            private T _current;
            private D2DRect _viewport;
            private SpatialGrid<T> _gridRef;

            private int _numIdxX;
            private int _numIdxY;
            private int _curIdxX;
            private int _curIdxY;
            private int _curX;
            private int _curY;

            private EntrySequence _curSeq;
            private Entry _curEntry;

            T IEnumerator<T>.Current
            {
                get
                {
                    return _current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _current;
                }
            }

            public GetInViewportEnumerator(SpatialGrid<T> grid)
            {
                _gridRef = grid;
                _state = 0;
            }

            public void Begin(D2DRect viewport)
            {
                _state = 0;
                _numIdxX = 0;
                _numIdxY = 0;
                _curIdxX = 0;
                _curIdxY = 0;
                _curX = 0;
                _curY = 0;
                _viewport = viewport;

                _numIdxX = (int)(_viewport.Width / (float)(1 << _gridRef.SIDE_LEN)) + 1;
                _numIdxY = (int)(_viewport.Height / (float)(1 << _gridRef.SIDE_LEN)) + 1;
            }

            void IDisposable.Dispose()
            {
                _curSeq = null;
                _curEntry = null;
                _state = -2;
            }

            private bool MoveNext()
            {
                int state = _state;
                if (state != 0)
                {
                    if (state != 1)
                    {
                        return false;
                    }

                    _state = -1;
                    _curEntry = _curEntry.Next;

                    goto IL_012f;
                }

                _state = -1;
                _gridRef.GetGridIdx(_viewport.Location, out _curIdxX, out _curIdxY);
                _curX = _curIdxX;

                goto IL_018e;

            IL_018e:

                if (_curX <= _curIdxX + _numIdxX)
                {
                    _curY = _curIdxY;

                    goto IL_015c;
                }

                return false;

            IL_0144:

                _curSeq = null;
                _curY++;
                goto IL_015c;

            IL_015c:

                if (_curY <= _curIdxY + _numIdxY)
                {
                    if (_gridRef._sequences.TryGetValue(_gridRef.GetGridHash(_curX, _curY), out _curSeq))
                    {
                        _curEntry = _curSeq.Head;

                        goto IL_012f;
                    }

                    goto IL_0144;
                }

                _curX++;

                goto IL_018e;

            IL_012f:

                if (_curEntry != null)
                {
                    _current = _curEntry.Item;
                    _state = 1;

                    return true;
                }

                _curEntry = null;

                goto IL_0144;
            }

            bool IEnumerator.MoveNext()
            {
                return this.MoveNext();
            }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                _state = 0;

                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable<T>)this).GetEnumerator();
            }
        }

        private sealed class GetNearEnumerator : IEnumerable<T>, IEnumerable, IEnumerator<T>, IEnumerator, IDisposable
        {
            private int _state;
            private T _current;
            private T _targetObj;
            private SpatialGrid<T> _gridRef;

            private int _idxX;
            private int _idxY;
            private int _curLutIdx = 0;
            private IntPoint _offset;

            private EntrySequence _curSeq;
            private Entry _curEntry;

            private static readonly IntPoint[] OFFSET_LUT =
            [
                new IntPoint(-1, -1),
                new IntPoint(-1, 0),
                new IntPoint(-1, 1),
                new IntPoint(0, -1),
                new IntPoint(0, 0),
                new IntPoint(0, 1),
                new IntPoint(1, -1),
                new IntPoint(1, 0),
                new IntPoint(1, 1)
            ];

            T IEnumerator<T>.Current
            {
                get
                {
                    return _current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return _current;
                }
            }

            public GetNearEnumerator(SpatialGrid<T> grid)
            {
                _gridRef = grid;
                this._state = 0;
            }

            public void Begin(T gameObject)
            {
                _state = 0;
                _targetObj = gameObject;
                _idxX = 0;
                _idxY = 0;
                _curLutIdx = 0;
            }

            void IDisposable.Dispose()
            {
                _curSeq = null;
                _curEntry = null;
                _state = -2;
            }

            private bool MoveNext()
            {
                int state = _state;
                if (state != 0)
                {
                    if (state != 1)
                    {
                        return false;
                    }

                    _state = -1;
                    _curEntry = _curEntry.Next;

                    goto IL_00f5;
                }

                _state = -1;
                _gridRef.GetGridIdx(_targetObj, out _idxX, out _idxY);
                _curLutIdx = 0;

            IL_0122:
                if (_curLutIdx < OFFSET_LUT.Length)
                {
                    _offset = OFFSET_LUT[_curLutIdx];

                    if (_gridRef._sequences.TryGetValue(_gridRef.GetGridHash(_idxX + _offset.X, _idxY + _offset.Y), out _curSeq))
                    {
                        _curEntry = _curSeq.Head;

                        goto IL_00f5;
                    }

                    goto IL_010a;
                }

                return false;

            IL_00f5:
                if (_curEntry != null)
                {
                    _current = _curEntry.Item;
                    _state = 1;

                    return true;
                }

                _curEntry = null;

                goto IL_010a;

            IL_010a:

                _curSeq = null;
                _curLutIdx++;

                goto IL_0122;
            }

            bool IEnumerator.MoveNext()
            {
                return this.MoveNext();
            }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                _state = 0;
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable<T>)this).GetEnumerator();
            }

            private struct IntPoint
            {
                public int X;
                public int Y;

                public IntPoint(int x, int y)
                {
                    X = x;
                    Y = y;
                }
            }
        }
    }
}
