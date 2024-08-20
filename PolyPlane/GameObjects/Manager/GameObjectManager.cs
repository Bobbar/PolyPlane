﻿using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    /// <summary>
    /// Handles collections for all game objects and provides fast lookups and nearest neighbor searching.
    /// </summary>
    public class GameObjectManager
    {
        public int TotalObjects = 0;

        private const int MAX_GROUND_IMPACTS = 500;

        public List<GameObject> Missiles = new List<GameObject>();
        public List<GameObject> MissileTrails = new List<GameObject>();
        public List<GameObject> Decoys = new List<GameObject>();
        public List<GameObject> Bullets = new List<GameObject>();
        public List<GameObject> Explosions = new List<GameObject>();
        public List<GameObject> Debris = new List<GameObject>();
        public List<GameObject> Flames = new List<GameObject>();

        public List<GroundImpact> GroundImpacts = new List<GroundImpact>();
        public List<FighterPlane> Planes = new List<FighterPlane>();

        public RingBuffer<GameObject> NewDecoys = new RingBuffer<GameObject>(500);
        public RingBuffer<GameObject> NewBullets = new RingBuffer<GameObject>(500);
        public RingBuffer<GameObject> NewMissiles = new RingBuffer<GameObject>(500);
        public RingBuffer<FighterPlane> NewPlanes = new RingBuffer<FighterPlane>(500);
        public RingBuffer<FlamePart> NewFlames = new RingBuffer<FlamePart>(500);

        private Dictionary<int, GameObject> _objLookup = new Dictionary<int, GameObject>();
        private SpatialGrid _spatialGrid = new SpatialGrid();

        private List<GameObject> _allNetObjects = new List<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private List<GameObject> _expiredObjs = new List<GameObject>();

        private GameObjectPool<FlamePart> _flamePool = new GameObjectPool<FlamePart>(() => new FlamePart());
        private GameObjectPool<Bullet> _bulletPool = new GameObjectPool<Bullet>(() => new Bullet());

        public event EventHandler<PlayerScoredEventArgs> PlayerScoredEvent;
        public event EventHandler<EventMessage> PlayerKilledEvent;
        public event EventHandler<FighterPlane> NewPlayerEvent;


        public Bullet RentBullet()
        {
            var bullet = _bulletPool.RentObject();
            return bullet;
        }

        public void ReturnBullet(Bullet bullet)
        {
            _bulletPool.ReturnObject(bullet);
        }

        public FlamePart RentFlamePart()
        {
            var part = _flamePool.RentObject();
            return part;
        }

        public void ReturnFlamePart(FlamePart part)
        {
            part.IsExpired = true;
            _flamePool.ReturnObject(part);
        }

        public void AddFlame(FlamePart flame)
        {
            Flames.Add(flame);

            _spatialGrid.Add(flame);
        }

        public void EnqueueFlame(FlamePart flame)
        {
            NewFlames.Enqueue(flame);
        }

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
                AddObject(missile);
                Missiles.Add(missile);

                var trail = new SmokeTrail(missile, o =>
                {
                    var m = o as GuidedMissile;
                    return m.CenterOfThrust;
                }, lineWeight: 2f);

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
                plane.PlayerCrashedCallback = HandlePlayerCrashed;

                if (plane.IsAI)
                    NewPlayerEvent?.Invoke(this, plane);

                // Add first plane as the initial view plane.
                if (Planes.Count == 1)
                    World.ViewPlaneID = Planes.First().ID;
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

        public DummyObject AddDummyObject()
        {
            var obj = new DummyObject();

            if (!Contains(obj))
                AddObject(obj);

            return obj;
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
                {
                    if (explosion.Owner is Missile)
                    {
                        var missileRadius = Utilities.Rnd.NextFloat(23f, 27f);
                        GroundImpacts.Add(new GroundImpact(new D2DPoint(explosion.Position.X, Utilities.Rnd.NextFloat(0f, 8f)), new D2DSize(missileRadius, missileRadius)));
                    }
                    else if (explosion.Owner is Bullet)
                    {
                        var bulletRadius = Utilities.Rnd.NextFloat(9f, 12f);
                        GroundImpacts.Add(new GroundImpact(new D2DPoint(explosion.Position.X, Utilities.Rnd.NextFloat(0f, 5f)), new D2DSize(bulletRadius, bulletRadius)));
                    }
                }
            }
        }

        public void AddMissileExplosion(GuidedMissile missile)
        {
            var explosion = new Explosion(missile, 300f, 2.4f);
            AddExplosion(explosion);
        }

        public void AddBulletExplosion(Bullet bullet)
        {
            var explosion = new Explosion(bullet, 50f, 0.5f);
            AddExplosion(explosion);
        }

        public void Clear()
        {
            _allNetObjects.Clear();
            Missiles.Clear();
            MissileTrails.Clear();
            Decoys.Clear();
            Bullets.Clear();
            Explosions.Clear();
            Planes.Clear();
            Debris.Clear();
            Flames.ForEach(f => f.Dispose());
            Flames.Clear();
            _objLookup.Clear();
            _spatialGrid.Clear();
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

                World.ViewPlaneID = obj.ID;
            }
        }

        public void PruneExpired()
        {
            TotalObjects = 0;

            PruneExpired(Missiles);
            PruneExpired(MissileTrails);
            PruneExpired(Decoys);
            PruneExpired(Bullets);
            PruneExpired(Explosions);
            PruneExpired(Debris);
            PruneExpired(Flames);

            TotalObjects += Planes.Count;

            for (int i = 0; i < Planes.Count; i++)
            {
                var plane = Planes[i];

                if (plane.IsExpired)
                {
                    _expiredObjs.Add(plane);
                    Planes.RemoveAt(i);
                    _objLookup.Remove(plane.ID.GetHashCode());
                    plane.Dispose();
                }
            }

            if (GroundImpacts.Count > MAX_GROUND_IMPACTS)
                GroundImpacts.RemoveAt(0);
        }

        private void PruneExpired(List<GameObject> objs)
        {
            TotalObjects += objs.Count;

            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i];

                if (obj.IsExpired)
                {
                    objs.RemoveAt(i);
                    _objLookup.Remove(obj.ID.GetHashCode());
                    obj.Dispose();

                    if (World.IsNetGame)
                        _expiredObjs.Add(obj);

                    // Add explosions when missiles & bullets are expired.
                    if (obj is GuidedMissile missile)
                    {
                        AddMissileExplosion(missile);

                        // Remove dummy objects as needed.
                        if (missile.Target != null && missile.Target is DummyObject)
                        {
                            missile.Target.IsExpired = true;
                            _objLookup.Remove(missile.Target.ID.GetHashCode());
                        }
                    }
                    else if (obj is Bullet bullet)
                    {
                        _bulletPool.ReturnObject(bullet);
                        AddBulletExplosion(bullet);
                    }
                }
            }
        }

        private void AddObject(GameObject obj)
        {
            var hash = obj.ID.GetHashCode();
            _objLookup.Add(hash, obj);

            // Add collidable objects to spatial lookup.
            if (obj is ICollidable)
                _spatialGrid.Add(obj);
        }

        private void HandlePlayerKilled(FighterPlane plane, GameObject impactor)
        {
            var impactorPlayer = impactor.Owner as FighterPlane;

            if (impactorPlayer == null)
                return;

            PlayerScoredEvent?.Invoke(this, new PlayerScoredEventArgs(impactorPlayer, plane));


            if (!World.IsClient)
            {
                PlayerKilledEvent?.Invoke(this, new EventMessage($"'{impactorPlayer.PlayerName}' {(plane.WasHeadshot ? "headshot" : "destroyed")} '{plane.PlayerName}' with {(impactor is Bullet ? "bullets." : "a missile.")}", EventType.Kill));
            }
        }

        private void HandlePlayerCrashed(FighterPlane plane)
        {
            PlayerKilledEvent?.Invoke(this, new EventMessage($"'{plane.PlayerName}' crashed into the ground...", EventType.Kill));
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

            while (NewFlames.Count > 0)
            {
                if (NewFlames.TryDequeue(out FlamePart flame))
                {
                    AddFlame(flame);
                }
            }

        }

        private void SyncObjCollections()
        {
            if (World.IsNetGame)
                _allNetObjects.Clear();

            _allObjects.Clear();

            foreach (var obj in _objLookup.Values)
            {
                if (World.IsNetGame && obj.IsNetObject)
                    _allNetObjects.Add(obj);

                _allObjects.Add(obj);
            }

            _spatialGrid.Update();
        }

        public IEnumerable<GameObject> GetNear(GameObject obj) => _spatialGrid.GetNear(obj);

    }

    public struct GroundImpact
    {
        public D2DPoint Position;
        public D2DSize Size;

        public GroundImpact(D2DPoint pos, D2DSize size)
        {
            Position = pos;
            Size = size;
        }
    }
}
