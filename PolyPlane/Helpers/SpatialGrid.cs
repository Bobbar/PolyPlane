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
    public sealed class SpatialGrid<T>(Func<T, D2DPoint> positionSelector, Func<T, bool> isExpiredSelector, int sideLen = 9)
    {
        private Dictionary<int, EntrySequence> _sequences = new();
        private List<Entry> _entries = new();
        private GameObjectPool<Entry> _entryPool = new GameObjectPool<Entry>(() => new Entry());
        private GameObjectPool<EntrySequence> _sequencePool = new GameObjectPool<EntrySequence>(() => new EntrySequence());

        private readonly Func<T, D2DPoint> _positionSelector = positionSelector;
        private readonly Func<T, bool> _isExpiredSelector = isExpiredSelector;
        private readonly int SIDE_LEN = sideLen;

        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Compute new hashes in parallel.
            ComputeNextHashes();

            // Iterate all entries and update as needed.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];

                // Remove expired entries.
                if (_isExpiredSelector(entry.Item))
                {
                    RemoveFromSequence(entry);

                    _entries.RemoveAt(i);

                    _entryPool.ReturnObject(entry);
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
                // Remove head if it's the final entry.
                if (entry.Next == null)
                {
                    _sequences.Remove(entry.CurrentHash);
                    _sequencePool.ReturnObject(entrySeq);
                }
                // Otherwise move the head up to the next entry.
                else
                {
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

                // Tail entry: Move tail to previous entry.
                if (prev != null && next == null)
                {
                    // It's a tail entry?
                    entrySeq.Tail = prev;
                    prev.Next = null;

                }
                // Middle entry: Link previous and next entries together.
                else if (prev != null && next != null)
                {
                    prev.Next = next;
                    next.Prev = prev;

                    entry.Prev = null;
                    entry.Next = null;
                }
            }
        }


        private void AddInternal(int hash, T obj)
        {
            var entry = _entryPool.RentObject();
            entry.ReInit(hash, obj);

            AddToSequence(entry);

            _entries.Add(entry);
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
                // Add to existing head.
                entry.Sequence = existingSeq;

                // Has tail, add to end.
                if (existingSeq.Tail != null)
                {
                    entry.IsHead = false;
                    entry.Prev = existingSeq.Tail;
                    existingSeq.Tail.Next = entry;
                    existingSeq.Tail = entry;
                    entry.Next = null;
                    entry.Sequence = existingSeq;
                }
                else// No tail, add second item.
                {
                    entry.Prev = existingSeq.Head;
                    existingSeq.Head.Next = entry;
                    existingSeq.Tail = entry;
                    entry.Next = null;
                    entry.Sequence = existingSeq;
                }
            }
            else
            {
                // Add new head.
                entry.IsHead = true;
                entry.Prev = null;
                entry.Next = null;

                var newIndex = _sequencePool.RentObject();
                newIndex.ReInit(entry);

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

        /// <summary>
        /// Get all objects within neighboring grid cells of the specified object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IEnumerable<T> GetNear(T obj)
        {
            GetGridIdx(obj, out int idxX, out int idxY);

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
            // Calc number of indexes for x/y coords.
            int nX = (int)(viewport.Width / (1 << SIDE_LEN)) + 1;
            int nY = (int)(viewport.Height / (1 << SIDE_LEN)) + 1;

            // Find the initial indices for the top-left corner.
            GetGridIdx(viewport.Location, out int idxX, out int idxY);

            // Iterate x/y indices and return objects.
            for (int x = idxX; x <= idxX + nX; x++)
            {
                for (int y = idxY; y <= idxY + nY; y++)
                {
                    var nHash = GetGridHash(x, y);

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
            public Entry Head;
            public Entry? Tail;

            public EntrySequence() { }

            public void ReInit(Entry head)
            {
                Head = head;
                Tail = null;
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

            public Entry() { }

            public void ReInit(int curHash, T item)
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
    }
}
