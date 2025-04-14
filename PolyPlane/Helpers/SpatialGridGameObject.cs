using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides a dynamic sparse 2D spatial grid for fast nearest-neighbor searching of <see cref="GameObject"/>.
    /// </summary>
    /// <param name="sideLen">And integer (S) representing the grid cell side length (L), L = 1 << S. </param>
    public sealed class SpatialGridGameObject(int sideLen = 9)
    {
        private Dictionary<int, EntrySequence> _sequences = new();
        private List<Entry> _entries = new();

        private readonly int SIDE_LEN = sideLen;

        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Iterate all entries and update as needed.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];

                // Remove expired entries.
                if (entry.Object.IsExpired)
                {
                    RemoveFromSequence(entry);

                    // Move to the end of the list and remove.
                    _entries[i] = _entries[_entries.Count - 1];
                    _entries.RemoveAt(_entries.Count - 1);
                }
                else
                {
                    // Move entry to a new sequence.
                    var curHash = entry.CurrentHash;
                    entry.NextHash = entry.Object.GridHash;

                    if (curHash != entry.NextHash)
                    {
                        MoveEntry(entry);
                    }
                }
            }

            PruneSeqs();
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
        public void Add(GameObject obj)
        {
            var hash = GetGridHash(obj);
            AddInternal(hash, obj);
        }

        private void AddInternal(int hash, GameObject obj)
        {
            var entry = new Entry(hash, obj);

            AddToSequence(entry);

            _entries.Add(entry);
        }

        /// <summary>
        /// Get all objects within neighboring grid cells of the specified object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IEnumerable<GameObject> GetNear(GameObject obj)
        {
            GetGridIdx(obj, out int idxX, out int idxY);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (_sequences.TryGetValue(GetGridHash(idxX + x, idxY + y), out var index))
                    {
                        var cur = index.Head;

                        while (cur != null)
                        {
                            yield return cur.Object;

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
        public IEnumerable<GameObject> GetNear(D2DPoint position)
        {
            GetGridIdx(position, out int idxX, out int idxY);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (_sequences.TryGetValue(GetGridHash(idxX + x, idxY + y), out var index))
                    {
                        var cur = index.Head;

                        while (cur != null)
                        {
                            yield return cur.Object;

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
        public IEnumerable<GameObject> GetInViewport(D2DRect viewport)
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
                    if (_sequences.TryGetValue(GetGridHash(x, y), out var index))
                    {
                        var cur = index.Head;

                        while (cur != null)
                        {
                            yield return cur.Object;

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

        private void GetGridIdx(D2DPoint pos, out int idxX, out int idxY)
        {
            idxX = (int)Math.Floor(pos.X) >> SIDE_LEN;
            idxY = (int)Math.Floor(pos.Y) >> SIDE_LEN;
        }

        private void GetGridIdx(GameObject obj, out int idxX, out int idxY)
        {
            GetGridIdx(obj.Position, out idxX, out idxY);
        }

        private int GetGridHash(GameObject obj)
        {
            GetGridIdx(obj.Position, out int idxX, out int idxY);
            return GetGridHash(idxX, idxY);
        }

        private int GetGridHash(int idxX, int idxY)
        {
            return HashCode.Combine(idxX, idxY);
        }

        public static int GetGridHash(D2DPoint pos, int sideLen)
        {
            int idxX = (int)Math.Floor(pos.X) >> sideLen;
            int idxY = (int)Math.Floor(pos.Y) >> sideLen;

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
            public GameObject Object;

            public bool IsHead = false;

            public EntrySequence? Sequence;

            public Entry? Prev;
            public Entry? Next;

            public Entry(int curHash, GameObject item)
            {
                IsHead = false;
                NextHash = curHash;
                CurrentHash = curHash;

                Sequence = null;
                Prev = null;
                Next = null;

                Object = item;
            }
        }
    }
}
