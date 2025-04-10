using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Particles;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using System.Collections.Concurrent;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Managers
{
    /// <summary>
    /// Handles collections for all game objects and provides fast lookups and nearest neighbor searching.
    /// </summary>
    public class GameObjectManager : IPlayerScoredEvent
    {
        public int TotalObjects = 0;

        public List<GameObject> Missiles = new();
        public List<GameObject> MissileTrails = new();
        public List<GameObject> Decoys = new();
        public List<GameObject> Bullets = new();
        public List<GameObject> Explosions = new();
        public List<GameObject> Debris = new();
        public List<GameObject> Particles = new();
        public List<GameObject> DummyObjs = new();

        public List<GroundImpact> GroundImpacts = new();
        public List<FighterPlane> Planes = new();

        public ConcurrentQueue<GameObject> NewDecoys = new();
        public ConcurrentQueue<GameObject> NewDebris = new();
        public ConcurrentQueue<GameObject> NewBullets = new();
        public ConcurrentQueue<GameObject> NewMissiles = new();
        public ConcurrentQueue<FighterPlane> NewPlanes = new();
        public ConcurrentQueue<Particle> NewParticles = new();

        private Dictionary<int, GameObject> _objLookup = new();
        private SpatialGridGameObject _spatialGrid = new();

        private List<GameObject> _allNetObjects = new();
        private List<GameObject> _allObjects = new();
        private List<GameObject> _expiredObjs = new();

        private GameObjectPool<Particle> _particlePool = new(() => new Particle());
        private GameObjectPool<Explosion> _explosionPool = new(() => new Explosion());
        private GameObjectPool<Debris> _debrisPool = new(() => new Debris());
        private GameObjectPool<GroundImpact> _groundImpactPool = new(() => new GroundImpact());
        private ConcurrentQueue<BulletHole> _bulletHolePool = new();

        public event EventHandler<PlayerScoredEventArgs> PlayerScoredEvent;
        public event EventHandler<EventMessage> PlayerKilledEvent;
        public event EventHandler<FighterPlane> NewPlayerEvent;


        public BulletHole RentBulletHole(GameObject obj, D2DPoint offset, float angle)
        {
            if (_bulletHolePool.TryDequeue(out BulletHole hole))
            {
                hole.Owner = obj;
                hole.ReferencePosition = offset;
                hole.Angle = angle;
                hole.Age = 0f;
                hole.IsExpired = false;
                hole.SyncWithOwner();

                return hole;
            }
            else
            {
                var newHole = new BulletHole(obj, offset, angle);
                return newHole;
            }
        }

        public void ReturnBulletHole(BulletHole bulletHole)
        {
            _bulletHolePool.Enqueue(bulletHole);
        }

        public Particle RentParticle()
        {
            var part = _particlePool.RentObject();
            return part;
        }

        public void ReturnParticle(Particle particle)
        {
            particle.IsExpired = true;
            _particlePool.ReturnObject(particle);
        }

        private void AddParticle(Particle particle)
        {
            Particles.Add(particle);
            _spatialGrid.Add(particle);
        }

        public void EnqueueParticle(Particle particle)
        {
            if (!World.IsServer)
                NewParticles.Enqueue(particle);
        }

        public void EnqueueDebris(Debris debris)
        {
            if (!World.IsServer)
                NewDebris.Enqueue(debris);
        }

        public Debris RentDebris()
        {
            var debris = _debrisPool.RentObject();
            return debris;
        }

        public void ReturnDebris(Debris debris)
        {
            _debrisPool.ReturnObject(debris);
        }

        private void AddDebris(Debris debris)
        {
            if (!Contains(debris))
            {
                AddObject(debris);
                Debris.Add(debris);
            }
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

                var trail = new MissileSmokeTrail(missile);
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

                plane.PlayerKilledCallback += HandlePlayerKilled;
                plane.PlayerCrashedCallback += HandlePlayerCrashed;

                if (plane.IsAI)
                    NewPlayerEvent?.Invoke(this, plane);

                // Add first plane as the initial view plane.
                if (Planes.Count == 1)
                    World.ViewObject = Planes.First();
            }
        }

        public void EnqueuePlane(FighterPlane plane)
        {
            NewPlanes.Enqueue(plane);
        }

        private void AddDecoy(Decoy decoy)
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

        private void AddExplosion(GameObject explosion)
        {
            if (!Contains(explosion))
            {
                AddObject(explosion);
                Explosions.Add(explosion);

                if (explosion.Altitude <= 10f)
                {
                    if (explosion.Owner is GuidedMissile)
                    {
                        var missileRadius = Utilities.Rnd.NextFloat(23f, 27f);
                        var impact = _groundImpactPool.RentObject();
                        impact.ReInit(new D2DPoint(explosion.Position.X, Utilities.Rnd.NextFloat(0f, 8f)), new D2DSize(missileRadius + 8f, missileRadius), explosion.Owner.Rotation);
                        GroundImpacts.Add(impact);
                    }
                    else if (explosion.Owner is Bullet)
                    {
                        var bulletRadius = Utilities.Rnd.NextFloat(9f, 12f);
                        var impact = _groundImpactPool.RentObject();
                        impact.ReInit(new D2DPoint(explosion.Position.X, Utilities.Rnd.NextFloat(0f, 5f)), new D2DSize(bulletRadius + 8f, bulletRadius), explosion.Owner.Rotation);
                        GroundImpacts.Add(impact);
                    }
                }
            }
        }

        private void AddMissileExplosion(GuidedMissile missile)
        {
            var explosion = _explosionPool.RentObject();
            explosion.ReInit(missile, 300f, 2.4f);

            AddExplosion(explosion);
        }

        private void AddBulletExplosion(Bullet bullet)
        {
            var explosion = _explosionPool.RentObject();
            explosion.ReInit(bullet, 50f, 1.5f);

            AddExplosion(explosion);
        }

        public void ReturnExplosion(Explosion explosion)
        {
            _explosionPool.ReturnObject(explosion);
        }

        public DummyObject AddDummyObject(GameID id)
        {
            var obj = new DummyObject();
            obj.ID = id;

            if (!Contains(obj))
            {
                AddObject(obj);
                DummyObjs.Add(obj);
            }

            return obj;
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
            Particles.ForEach(f => f.Dispose());
            Particles.Clear();
            DummyObjs.Clear();
            NewDecoys.Clear();
            NewDebris.Clear();
            NewBullets.Clear();
            NewMissiles.Clear();
            NewPlanes.Clear();
            NewParticles.Clear();

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


        /// <summary>
        /// Updates and syncs all collections/queues, spatial grid and advances all objects.
        /// </summary>
        public void Update(float dt)
        {
            SyncObjQueues();

            PruneExpired();

            SyncObjCollections();

            // Update all regular objects.
            _allObjects.ForEachParallel(o => o.Update(World.CurrentDT));

            // Update planes separately.
            // They are pretty expensive, so we want "all threads on deck"
            // to be working on the updates.
            Planes.ForEachParallel(o => o.Update(World.CurrentDT));

            if (!World.IsNetGame || World.IsClient)
            {
                // Update particles.
                Particles.ForEachParallel(o => o.Update(World.CurrentDT));
            }

            _spatialGrid.Update();

            // Update ground impacts.
            for (int i = 0; i < GroundImpacts.Count; i++)
                GroundImpacts[i].Age += dt;
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
                _objLookup.Add(id.GetHashCode(), obj);
            }

            obj.ID = id;
        }

        public void PruneExpired()
        {
            TotalObjects = 0;

            PruneExpired(Missiles, recordExpired: true);
            PruneExpired(MissileTrails);
            PruneExpired(Decoys, recordExpired: true);
            PruneExpired(Bullets);
            PruneExpired(Explosions);
            PruneExpired(Debris);
            PruneExpired(DummyObjs);

            // Prune planes.
            for (int i = Planes.Count - 1; i >= 0; i--)
            {
                var plane = Planes[i];

                // Expire stale net planes.
                ExpireStaleNetObject(plane);

                if (plane.IsExpired)
                {
                    if (World.IsNetGame)
                        _expiredObjs.Add(plane);

                    Planes.RemoveAt(i);
                    _objLookup.Remove(plane.ID.GetHashCode());
                    plane.Dispose();
                }
            }

            // Prune ground impacts.
            for (int i = GroundImpacts.Count - 1; i >= 0; i--)
            {
                var impact = GroundImpacts[i];

                if (impact.Age > GroundImpact.MAX_AGE)
                {
                    GroundImpacts.RemoveAt(i);
                    _groundImpactPool.ReturnObject(impact);
                }
            }

            // Prune particles.
            PruneParticles();

            // Accum object counts.
            TotalObjects += Planes.Count;
            TotalObjects += GroundImpacts.Count;
            TotalObjects += Particles.Count;
        }

        private void PruneExpired(List<GameObject> objs, bool recordExpired = false)
        {

            for (int i = objs.Count - 1; i >= 0; i--)
            {
                var obj = objs[i];

                // Expire any stale net objects.
                ExpireStaleNetObject(obj);

                if (obj.IsExpired)
                {
                    objs.RemoveAt(i);
                    obj.Dispose();

                    if (obj is not INoGameID)
                        _objLookup.Remove(obj.ID.GetHashCode());

                    if (recordExpired && World.IsNetGame)
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
                        AddBulletExplosion(bullet);
                    }
                }
            }

            TotalObjects += objs.Count;
        }

        private void ExpireStaleNetObject(GameObject obj)
        {
            const double MAX_NET_AGE = 500;

            if (World.IsNetGame)
            {
                // Check for stale net objects which don't appear to be receiving updates.
                // This really shouldn't happen, but it does rarely.
                // Perhaps if an update packet arrives after an expired packet for the same object.
                if (obj is GuidedMissile || obj is FighterPlane)
                {
                    var netObj = obj as GameObjectNet;

                    if (obj.IsNetObject && netObj.NetAge > MAX_NET_AGE)
                        obj.IsExpired = true;
                }
            }
        }

        private void PruneParticles()
        {
            int num = 0;
            int tailIdx = 0;
            int count = Particles.Count;

            if (count == 0)
                return;

            // Find the index (from the end) of the first un-expired particle.
            for (int i = count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                if (!p.IsExpired)
                {
                    tailIdx = i;
                    break;
                }
            }

            // Start at the index found above and iterate.
            int swapIdx = tailIdx;
            for (int i = tailIdx; i >= 0; i--)
            {
                var p = Particles[i];

                if (p.IsExpired)
                {
                    p.Dispose();

                    // Swap the expired particles to the end of the list.
                    var tmp = Particles[i];
                    Particles[i] = Particles[swapIdx];
                    Particles[swapIdx] = tmp;

                    // Move to the next swap index.
                    swapIdx--;
                    num++;
                }
            }

            // Remove the chunk of expired particles now located at the end of the list.
            int numTail = Particles.Count - tailIdx - 1;
            int numRemoved = numTail + num;

            Particles.RemoveRange(Particles.Count - numRemoved, numRemoved);
        }

        private void AddObject(GameObject obj)
        {
            if (obj is not INoGameID)
            {
                var hash = obj.ID.GetHashCode();
                _objLookup.Add(hash, obj);
            }

            // Add objects to spatial lookup as needed.
            if (obj.HasFlag(GameObjectFlags.SpatialGrid))
                _spatialGrid.Add(obj);
        }

        private void HandlePlayerKilled(PlayerKilledEventArgs killedEvent)
        {
            var attackPlane = killedEvent.AttackPlane;
            var killedPlane = killedEvent.KilledPlane;

            if (attackPlane == null)
                return;

            PlayerScoredEvent?.Invoke(this, new PlayerScoredEventArgs(attackPlane, killedPlane, killedPlane.WasHeadshot));

            if (!World.IsClient)
            {
                var wasBullet = (killedEvent.ImpactType & ImpactType.Bullet) == ImpactType.Bullet;
                PlayerKilledEvent?.Invoke(this, new EventMessage($"'{attackPlane.PlayerName}' {(killedPlane.WasHeadshot ? "headshot" : "destroyed")} '{killedPlane.PlayerName}' with {(wasBullet ? "bullets." : "a missile.")}", EventType.Kill));
            }
        }

        private void HandlePlayerCrashed(FighterPlane plane)
        {
            if (!World.IsClient)
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

            while (NewDebris.Count > 0)
            {
                if (NewDebris.TryDequeue(out GameObject debris))
                {
                    AddDebris(debris as Debris);
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

            while (NewParticles.Count > 0)
            {
                if (NewParticles.TryDequeue(out Particle particle))
                {
                    AddParticle(particle);
                }
            }
        }

        private void SyncObjCollections()
        {
            if (World.IsNetGame)
                _allNetObjects.Clear();

            _allObjects.Clear();

            // Add objects from the lookup.
            foreach (var obj in _objLookup.Values)
            {
                if (World.IsNetGame && obj.IsNetObject)
                    _allNetObjects.Add(obj);

                // Don't add planes. We will update them separately.
                if (obj is not FighterPlane)
                    _allObjects.Add(obj);
            }

            // Add other objects which are not in the lookup.
            _allObjects.AddRange(MissileTrails);
            _allObjects.AddRange(Debris);
            _allObjects.AddRange(Explosions);

        }

        public IEnumerable<GameObject> GetNear(GameObject obj) => _spatialGrid.GetNear(obj);
        public IEnumerable<GameObject> GetNear(D2DPoint position) => _spatialGrid.GetNear(position);
        public IEnumerable<GameObject> GetInViewport(D2DRect viewport) => _spatialGrid.GetInViewport(viewport);
    }
}
