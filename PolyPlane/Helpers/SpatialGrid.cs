﻿using unvell.D2DLib;

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
        private Dictionary<int, List<Entry>> _grid = new Dictionary<int, List<Entry>>();
        private GameObjectPool<Entry> _entryPool = new GameObjectPool<Entry>(() => new Entry());
        private List<Entry> _movedObjects = new List<Entry>();
        private List<Entry> _allObjects = new List<Entry>();

        private readonly Func<T, D2DPoint> _positionSelector = positionSelector;
        private readonly Func<T, bool> _isExpiredSelector = isExpiredSelector;
        private readonly int SIDE_LEN = sideLen;

        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Since we cannot add to a dictionary within a foreach loop,
            // we need to record objects which need moved and re-add them after.

            // We save the new hashes in a KVP so that we do not need to compute
            // the hash twice.

            // Compute new hashes in parallel.
            ComputeNextHashes();

            // Iterate all items in the grid and make/record changes as needed.
            foreach (var kvp in _grid)
            {
                var curHash = kvp.Key;
                var entries = kvp.Value;

                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    var obj = entry.Item;

                    if (_isExpiredSelector(obj))
                    {
                        // Just remove expired objects.
                        entries.RemoveAt(i);
                        _entryPool.ReturnObject(entry);
                    }
                    else
                    {
                        // Check hash and record moved objects as needed.
                        var newHash = entry.NextHash;
                        if (newHash != curHash)
                        {
                            entries.RemoveAt(i);
                            _entryPool.ReturnObject(entry);
                            _movedObjects.Add(entry);
                        }
                    }
                }

                // Remove empty cells.
                if (entries.Count == 0)
                    _grid.Remove(curHash);
                else // Record current objects.
                    _allObjects.AddRange(entries);
            }

            // Add moved objects.
            for (int i = 0; i < _movedObjects.Count; i++)
            {
                var tmp = _movedObjects[i];
                AddInternal(tmp.NextHash, tmp.Item);
            }

            // Clear the temp storage.
            _movedObjects.Clear();
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

                    if (_grid.TryGetValue(nHash, out var ns))
                    {
                        for (int i = 0; i < ns.Count; i++)
                        {
                            yield return ns[i].Item;
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

                    if (_grid.TryGetValue(nHash, out var ns))
                    {
                        for (int i = 0; i < ns.Count; i++)
                        {
                            yield return ns[i].Item;
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

                    if (_grid.TryGetValue(nHash, out var ns))
                    {
                        for (int i = 0; i < ns.Count; i++)
                        {
                            yield return ns[i].Item;
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            _grid.Clear();
            _movedObjects.Clear();
            _allObjects.Clear();
        }

        private void ComputeNextHashes()
        {
            ParallelHelpers.ParallelForSlim(_allObjects.Count, ComputeHashes);

            _allObjects.Clear();
        }

        private void ComputeHashes(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                var item = _allObjects[i];
                var newHash = GetGridHash(item.Item);
                item.NextHash = newHash;
            }
        }

        private void AddInternal(int hash, T obj)
        {
            var entry = _entryPool.RentObject();
            entry.ReInit(hash, obj);

            if (_grid.TryGetValue(hash, out var objs))
                objs.Add(entry);
            else
                _grid.Add(hash, new List<Entry> { entry });

            _allObjects.Add(entry);
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


        private class Entry
        {
            public int NextHash;
            public T Item;
            public Entry() { }

            public void ReInit(int curHash, T item)
            {
                NextHash = curHash;
                Item = item;
            }
        }
    }
}
