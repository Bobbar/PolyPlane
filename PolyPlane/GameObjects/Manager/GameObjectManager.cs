using PolyPlane.Helpers;
using PolyPlane.Rendering;

namespace PolyPlane.GameObjects
{
    /// <summary>
    /// Handles collections for all game objects and provides fast lookups and nearest neighbor searching.
    /// </summary>
    public class GameObjectManager
    {
        public int TotalObjects => _objLookup.Count;

        private const int MAX_GROUND_IMPACTS = 500;
        private const int SPATIAL_GRID_SIDE_LEN = 7;

        public List<GameObject> Missiles = new List<GameObject>();
        public List<GameObject> MissileTrails = new List<GameObject>();
        public List<GameObject> Decoys = new List<GameObject>();
        public List<GameObject> Bullets = new List<GameObject>();
        public List<GameObject> Explosions = new List<GameObject>();
        public List<GameObject> Debris = new List<GameObject>();

        public List<D2DPoint> GroundImpacts = new List<D2DPoint>();
        public List<FighterPlane> Planes = new List<FighterPlane>();

        public RingBuffer<GameObject> NewDecoys = new RingBuffer<GameObject>(50);
        public RingBuffer<GameObject> NewBullets = new RingBuffer<GameObject>(100);
        public RingBuffer<GameObject> NewMissiles = new RingBuffer<GameObject>(50);
        public RingBuffer<FighterPlane> NewPlanes = new RingBuffer<FighterPlane>(100);

        private Dictionary<int, GameObject> _objLookup = new Dictionary<int, GameObject>();
        private Dictionary<D2DPoint, List<GameObject>> _objLookupSpatial = new Dictionary<D2DPoint, List<GameObject>>();

        private List<GameObject> _allNetObjects = new List<GameObject>();
        private List<GameObject> _allLocalObjects = new List<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private List<GameObject> _expiredObjs = new List<GameObject>();

        public event EventHandler<EventMessage> PlayerKilledEvent;
        public event EventHandler<FighterPlane> NewPlayerEvent;


        public void AddDebris(Debris debris)
        {
            if (!Contains(debris))
            {
                AddObject(debris);
                Debris.Add(debris);
            }
        }

        public void CleanDebris(GameID ownerID)
        {
            foreach (var debris in Debris.Where(d => d.Owner.ID.Equals(ownerID)))
                debris.IsExpired = true;
        }

        public void AddBullet(Bullet bullet)
        {
            if (!Contains(bullet))
            {
                AddObject(bullet);
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
                missile.Manager = this;

                AddObject(missile);
                Missiles.Add(missile);

                var trail = new SmokeTrail(missile, o =>
                {
                    var m = o as GuidedMissile;
                    return m.CenterOfThrust;
                });


                AddObject(trail);
                MissileTrails.Add(trail);
            }
        }

        public void EnqueueMissile(GuidedMissile missile)
        {
            NewMissiles.Enqueue(missile);
        }

        public void AddPlane(FighterPlane plane)
        {
            if (!Contains(plane))
            {
                AddObject(plane);
                Planes.Add(plane);

                plane.PlayerKilledCallback = HandlePlayerKilled;

                NewPlayerEvent?.Invoke(this, plane);
            }
        }

        public void EnqueuePlane(FighterPlane plane)
        {
            NewPlanes.Enqueue(plane);
        }

        public void AddDecoy(Decoy decoy)
        {
            if (!Contains(decoy))
            {
                AddObject(decoy);
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
                AddObject(explosion);
                Explosions.Add(explosion);

                if (explosion.Altitude <= 10f)
                    GroundImpacts.Add(new D2DPoint(explosion.Position.X, Utilities.Rnd.NextFloat(0f, 5f)));
            }
        }

        public void AddExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 200f, 1.4f);
            AddExplosion(explosion);
        }

        public void AddBulletExplosion(D2DPoint pos)
        {
            var explosion = new Explosion(pos, 50f, 0.5f);
            AddExplosion(explosion);
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
            _objLookup.Clear();
            _objLookupSpatial.Clear();
            _expiredObjs.Clear();
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

        public FighterPlane GetPlaneByPlayerID(int playerID)
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
            PruneExpired(Missiles);
            PruneExpired(MissileTrails);
            PruneExpired(Decoys);
            PruneExpired(Bullets);
            PruneExpired(Explosions);
            PruneExpired(Debris);

            for (int i = 0; i < Planes.Count; i++)
            {
                var plane = Planes[i];

                if (plane.IsExpired)
                {
                    plane.Manager = null;
                    _expiredObjs.Add(plane);
                    Planes.RemoveAt(i);
                    _objLookup.Remove(plane.ID.GetHashCode());
                    plane.Dispose();
                }
            }

            if (GroundImpacts.Count > MAX_GROUND_IMPACTS)
                GroundImpacts.RemoveAt(0);
        }

        private void AddObject(GameObject obj)
        {
            obj.Manager = this;
            _objLookup.Add(obj.ID.GetHashCode(), obj);
        }

        private void HandlePlayerKilled(FighterPlane plane, GameObject impactor)
        {
            var impactorPlayer = impactor.Owner as FighterPlane;

            if (impactorPlayer == null)
                return;

            if (plane.WasHeadshot)
                PlayerKilledEvent?.Invoke(this, new EventMessage($"'{impactorPlayer.PlayerName}' headshot '{plane.PlayerName}' with {(impactor is Bullet ? "bullets." : "a missile.")}", EventType.Kill));
            else
                PlayerKilledEvent?.Invoke(this, new EventMessage($"'{impactorPlayer.PlayerName}' destroyed '{plane.PlayerName}' with {(impactor is Bullet ? "bullets." : "a missile.")}", EventType.Kill));

        }

        private void PruneExpired(List<GameObject> objs)
        {
            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i];

                if (obj.IsExpired)
                {
                    obj.Manager = null;
                    objs.RemoveAt(i);
                    _objLookup.Remove(obj.ID.GetHashCode());
                    _expiredObjs.Add(obj);
                    obj.Dispose();

                    // Add explosions when missiles & bullets are expired.
                    if (obj is GuidedMissile missile)
                        AddExplosion(missile.Position);
                    else if (obj is Bullet bullet)
                        AddBulletExplosion(bullet.Position);
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
                if (NewPlanes.TryDequeue(out FighterPlane plane))
                {
                    AddPlane(plane);
                }
            }
        }

        private void UpdateSpatialLookup()
        {
            _objLookupSpatial.Clear();
            foreach (var obj in _allObjects)
            {
                var rndPos = GetGridIdx(obj);

                if (_objLookupSpatial.TryGetValue(rndPos, out List<GameObject> objs))
                {
                    objs.Add(obj);
                }
                else
                    _objLookupSpatial.Add(rndPos, new List<GameObject> { obj });
            }
        }

        private D2DPoint GetGridIdx(GameObject obj)
        {
            return GetGridIdx(obj.Position);
        }

        private D2DPoint GetGridIdx(D2DPoint pos)
        {
            var roundedPos = new D2DPoint(((int)Math.Floor(pos.X)) >> SPATIAL_GRID_SIDE_LEN, ((int)Math.Floor(pos.Y) >> SPATIAL_GRID_SIDE_LEN));

            return roundedPos;
        }

        public IEnumerable<GameObject> GetNear(GameObject obj)
        {
            var rndPos = GetGridIdx(obj);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var xo = rndPos.X + x;
                    var yo = rndPos.Y + y;
                    var nPos = new D2DPoint(xo, yo);

                    if (_objLookupSpatial.TryGetValue(nPos, out var ns))
                        foreach (var o in ns)
                            yield return o;
                }
            }
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

            UpdateSpatialLookup();
        }
    }
}
