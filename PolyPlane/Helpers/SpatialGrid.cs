using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane.Helpers
{
    public sealed class SpatialGrid
    {
        private const int SPATIAL_GRID_SIDE_LEN = 9;

        private Dictionary<int, List<GameObject>> _grid = new Dictionary<int, List<GameObject>>();
        private List<KeyValuePair<int, GameObject>> _tempStorage = new List<KeyValuePair<int, GameObject>>();
        private Dictionary<int, GameObject> _lookup = new Dictionary<int, GameObject>();


        /// <summary>
        /// Removes expired objects and moves live objects to their new grid positions as needed.
        /// </summary>
        public void Update()
        {
            // Since we cannot add to a dictionary within a foreach loop,
            // we need to record objects which need moved and re-add them after.

            // We save the new hashes in a KVP so that we do not need to compute
            // the hash twice.

            _tempStorage.Clear(); // Clear the temp storage.

            foreach (var kvp in _grid)
            {
                var curHash = kvp.Key;
                var objs = kvp.Value;

                for (int i = 0; i < objs.Count; i++)
                {
                    var obj = objs[i];

                    if (obj.IsExpired)
                    {
                        // Just remove expired objects.
                        objs.RemoveAt(i);
                        _lookup.Remove(obj.ID.GetHashCode());
                    }
                    else
                    {
                        // Check hash and record moved objects as needed.
                        var newHash = GetGridHash(obj);
                        if (newHash != curHash)
                        {
                            objs.RemoveAt(i);
                            _lookup.Remove(obj.ID.GetHashCode());
                            _tempStorage.Add(new KeyValuePair<int, GameObject>(newHash, obj));
                        }
                    }
                }

                // Remove empty cells.
                if (objs.Count == 0)
                    _grid.Remove(curHash);
            }

            // Add moved objects.
            for (int i = 0; i < _tempStorage.Count; i++)
            {
                var tmp = _tempStorage[i];
                AddInternal(tmp.Key, tmp.Value);
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


        public IEnumerable<GameObject> GetInViewport(D2DRect viewport)
        {
            int nX = (int)(viewport.Width / (1 << SPATIAL_GRID_SIDE_LEN));
            int nY = (int)(viewport.Height / (1 << SPATIAL_GRID_SIDE_LEN));

            GetGridIdx(viewport.Location, out int idxX, out int idxY);

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
            _tempStorage.Clear();
            _lookup.Clear();
        }

        private void AddInternal(int hash, GameObject obj)
        {
            var idHash = obj.ID.GetHashCode();
            if (!_lookup.ContainsKey(idHash))
            {
                _lookup.Add(idHash, obj);

                if (_grid.TryGetValue(hash, out var objs))
                    objs.Add(obj);
                else
                    _grid.Add(hash, new List<GameObject> { obj });
            }
        }

        private void GetGridIdx(GameObject obj, out int idxX, out int idxY)
        {
            GetGridIdx(obj.Position, out idxX, out idxY);
        }

        private void GetGridIdx(D2DPoint pos, out int idxX, out int idxY)
        {
            idxX = (int)Math.Floor(pos.X) >> SPATIAL_GRID_SIDE_LEN;
            idxY = (int)Math.Floor(pos.Y) >> SPATIAL_GRID_SIDE_LEN;
        }

        private int GetGridHash(GameObject obj)
        {
            return GetGridHash(obj.Position);
        }

        private int GetGridHash(D2DPoint pos)
        {
            GetGridIdx(pos, out int idxX, out int idxY);
            return GetGridHash(idxX, idxY);
        }

        private int GetGridHash(int idxX, int idxY)
        {
            return HashCode.Combine(idxX, idxY);
        }
    }
}
