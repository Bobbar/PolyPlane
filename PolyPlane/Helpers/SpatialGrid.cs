using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    /// <summary>
    /// Provides a dynamic sparse 2D spatial grid for fast nearest-neighbor searching.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class SpatialGrid<T>
    {
        private Dictionary<int, List<T>> _grid = new Dictionary<int, List<T>>();
        private List<KeyValuePair<int, T>> _movedObjects = new List<KeyValuePair<int, T>>();

        private readonly Func<T, D2DPoint> _positionSelector;
        private readonly Func<T, bool> _isExpiredSelector;
        private readonly int SIDE_LEN = 9;

        /// <summary>
        /// Create a new instance of <see cref="SpatialGrid{T}"/>
        /// </summary>
        /// <param name="positionSelector">Selector for current object positions. Grid positions are updated on <see cref="Update"/></param>
        /// <param name="isExpiredSelector">Selector for object expired status.  Expired objects are removed on <see cref="Update"/></param>
        /// <param name="sideLen">And integer (S) representing the grid cell side length (L), L = 1 << S. </param>
        public SpatialGrid(Func<T, D2DPoint> positionSelector, Func<T, bool> isExpiredSelector, int sideLen = 9)
        {
            SIDE_LEN = sideLen;
            _positionSelector = positionSelector;
            _isExpiredSelector = isExpiredSelector;
        }

        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Since we cannot add to a dictionary within a foreach loop,
            // we need to record objects which need moved and re-add them after.

            // We save the new hashes in a KVP so that we do not need to compute
            // the hash twice.

            _movedObjects.Clear(); // Clear the temp storage.

            foreach (var kvp in _grid)
            {
                var curHash = kvp.Key;
                var objs = kvp.Value;

                for (int i = objs.Count - 1; i >= 0; i--)
                {
                    var obj = objs[i];

                    if (_isExpiredSelector(obj))
                    {
                        // Just remove expired objects.
                        objs.RemoveAt(i);
                    }
                    else
                    {
                        // Check hash and record moved objects as needed.
                        var newHash = GetGridHash(obj);
                        if (newHash != curHash)
                        {
                            objs.RemoveAt(i);
                            _movedObjects.Add(new KeyValuePair<int, T>(newHash, obj));
                        }
                    }
                }

                // Remove empty cells.
                if (objs.Count == 0)
                    _grid.Remove(curHash);
            }

            // Add moved objects.
            for (int i = 0; i < _movedObjects.Count; i++)
            {
                var tmp = _movedObjects[i];
                AddInternal(tmp.Key, tmp.Value);
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

                    if (_grid.TryGetValue(nHash, out var ns))
                    {
                        for (int i = 0; i < ns.Count; i++)
                        {
                            yield return ns[i];
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
                            yield return ns[i];
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            _grid.Clear();
            _movedObjects.Clear();
        }

        private void AddInternal(int hash, T obj)
        {
            if (_grid.TryGetValue(hash, out var objs))
                objs.Add(obj);
            else
                _grid.Add(hash, new List<T> { obj });
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
    }
}
