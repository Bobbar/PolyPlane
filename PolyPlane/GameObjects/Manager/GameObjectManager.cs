using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects
{
    public class GameObjectManager
    {

        public int TotalObjects => _objLookup.Count;


        public List<GameObject> Missiles = new List<GameObject>();
        public List<GameObject> MissileTrails = new List<GameObject>();
        public List<GameObject> Decoys = new List<GameObject>();
        public List<GameObject> Bullets = new List<GameObject>();
        public List<GameObject> Explosions = new List<GameObject>();
        public List<Plane> Planes = new List<Plane>();

        public ConcurrentQueue<GameObject> NewDecoys = new ConcurrentQueue<GameObject>();
        public ConcurrentQueue<GameObject> NewBullets = new ConcurrentQueue<GameObject>();
        public ConcurrentQueue<GameObject> NewMissiles = new ConcurrentQueue<GameObject>();
        public ConcurrentQueue<Plane> NewPlanes = new ConcurrentQueue<Plane>();

        private Dictionary<int, GameObject> _objLookup = new Dictionary<int, GameObject>();

        private List<GameObject> _allNetObjects = new List<GameObject>();
        private List<GameObject> _allLocalObjects = new List<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private List<GameObject> _expiredObjs = new List<GameObject>();


        public void AddBullet(Bullet bullet)
        {
            if (!Contains(bullet))
            {
                _objLookup.Add(bullet.ID.GetHashCode(), bullet);
                Bullets.Add(bullet);
            }
        }

        public void EnqueueBullet(Bullet bullet)
        {
            NewBullets.Enqueue(bullet);
        }


        public void AddMissile(GuidedMissile missile)
        {
            if (!Contains(missile))
            {
                _objLookup.Add(missile.ID.GetHashCode(), missile);
                Missiles.Add(missile);

                var trail = new SmokeTrail(missile, o =>
                {
                    var m = o as GuidedMissile;
                    return m.CenterOfThrust;
                });


                _objLookup.Add(trail.ID.GetHashCode(), trail);
                MissileTrails.Add(trail);
            }
        }

        public void EnqueueMissile(GuidedMissile missile)
        {
            NewMissiles.Enqueue(missile);
        }

        public void AddPlane(Plane plane)
        {
            if (!Contains(plane))
            {
                _objLookup.Add(plane.ID.GetHashCode(), plane);
                Planes.Add(plane);
            }
        }

        public void EnqueuePlane(Plane plane)
        {
            NewPlanes.Enqueue(plane);
        }

        public void AddDecoy(Decoy decoy)
        {
            if (!Contains(decoy))
            {
                _objLookup.Add(decoy.ID.GetHashCode(), decoy);
                Decoys.Add(decoy);
            }
        }

        public void EnqueueDecoy(Decoy decoy)
        {
            NewDecoys.Enqueue(decoy);
        }


        public void AddExplosion(GameObject explosion)
        {
            if (!Contains(explosion))
            {
                _objLookup.Add(explosion.ID.GetHashCode(), explosion);
                Explosions.Add(explosion);
            }
        }

        public void Clear()
        {
            _allLocalObjects.Clear();
            _allNetObjects.Clear();
            Missiles.Clear();
            MissileTrails.Clear();
            Decoys.Clear();
            Bullets.Clear();
            Explosions.Clear();
            Planes.Clear();
            NewDecoys.Clear();
            NewBullets.Clear();
            NewMissiles.Clear();
            NewPlanes.Clear();
            _objLookup.Clear();

        }

        public GameObject GetObjectByID(GameID gameID)
        {
            if (_objLookup.TryGetValue(gameID.GetHashCode(), out GameObject obj))
                return obj;

            return null;
        }

        public bool TryGetObjectByID(GameID gameID, out GameObject obj)
        {
            return _objLookup.TryGetValue(gameID.GetHashCode(), out obj);
        }

        public IEnumerable<GameObject> GetObjectsByPlayer(int playerID)
        {
            foreach (var obj in _objLookup.Values)
            {
                if (obj.PlayerID == playerID)
                    yield return obj;
            }
        }

        public Plane GetPlaneByPlayerID(int playerID)
        {
            return Planes.Where(p => p.PlayerID == playerID).FirstOrDefault();
        }


        public List<GameObject> GetAllNetObjects()
        {
            return _allNetObjects;
        }

        public List<GameObject> GetAllLocalObjects()
        {
            return _allLocalObjects;
        }

        public List<GameObject> GetAllObjects()
        {
            return _allObjects;
        }


        public void SyncAll()
        {
            SyncObjQueues();
            SyncObjCollections();
        }

        public bool Contains(GameObject obj)
        {
            return Contains(obj.ID);
        }

        public bool Contains(GameID id)
        {
            return _objLookup.ContainsKey(id.GetHashCode());
        }

        public List<GameObject> ExpiredObjects()
        {
            return _expiredObjs;
        }

        public void ChangeObjID(GameObject obj, GameID id)
        {
            if (_objLookup.TryGetValue(obj.ID.GetHashCode(), out GameObject existingObj))
            {
                _objLookup.Remove(obj.ID.GetHashCode());
                obj.ID = id;
                _objLookup.Add(id.GetHashCode(), obj);
            }
        }

        public void PruneExpired()
        {
            _expiredObjs.Clear();

            PruneExpired(Missiles);
            PruneExpired(MissileTrails);
            PruneExpired(Decoys);
            PruneExpired(Bullets);
            PruneExpired(Explosions);

            for (int i = 0; i < Planes.Count; i++)
            {
                var plane = Planes[i];

                if (plane.IsExpired)
                {
                    Planes.RemoveAt(i);
                    _objLookup.Remove(plane.ID.GetHashCode());
                }
            }
        }

        private void PruneExpired(List<GameObject> objs)
        {
            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i];

                if (obj.IsExpired)
                {
                    objs.RemoveAt(i);
                    _objLookup.Remove(obj.ID.GetHashCode());
                    _expiredObjs.Add(obj);
                }
            }
        }

        private void SyncObjQueues()
        {
            while (NewDecoys.Count > 0)
            {
                if (NewDecoys.TryDequeue(out GameObject decoy))
                {
                    AddDecoy(decoy as Decoy);
                }
            }

            while (NewBullets.Count > 0)
            {
                if (NewBullets.TryDequeue(out GameObject bullet))
                {
                    AddBullet(bullet as Bullet);
                }
            }

            while (NewMissiles.Count > 0)
            {
                if (NewMissiles.TryDequeue(out GameObject missile))
                {
                    AddMissile(missile as GuidedMissile);
                }
            }

            while (NewPlanes.Count > 0)
            {
                if (NewPlanes.TryDequeue(out Plane plane))
                {
                    AddPlane(plane);
                }
            }

            //// Sync object IDs that might have been changed after adding?
            //foreach (var obj in _objLookup)
            //{
            //    var newHash = obj.Value.ID.GetHashCode();
            //    var curHash = obj.Key;

            //    if (newHash != curHash)
            //    {
            //        _objLookup.Remove(curHash);
            //        _objLookup.Add(newHash, obj.Value);
            //    }
            //}
        }

        private void SyncObjCollections()
        {
            _allLocalObjects.Clear();
            _allNetObjects.Clear();
            _allObjects.Clear();

            foreach (var obj in _objLookup.Values)
            {
                if (obj.IsNetObject)
                    _allNetObjects.Add(obj);
                else
                    _allLocalObjects.Add(obj);

                _allObjects.Add(obj);
            }
        }



    }
}
