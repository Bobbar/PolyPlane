using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Managers;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.Interpolation;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, IDisposable, IFlippable, ISpatialGrid
    {
        public int GridHash { get { return _gridHash; } }

        public SpatialGridGameObject SpatialGridRef { get; set; }

        public GameID ID { get; set; } = new GameID();

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

        public float Mass { get; set; } = 1f;

        public float Rotation
        {
            get { return _rotation; }

            set
            {
                _rotation = Utilities.ClampAngle(value);
            }
        }

        public float Altitude
        {
            get
            {
                return Utilities.PositionToAltitude(this.Position);
            }
        }

        /// <summary>
        /// Air speed relative to altitude & air density.
        /// </summary>
        public float AirSpeedIndicated
        {
            get
            {
                // Additional factor for units.
                // Bring the value down to better match real-life numbers. (Shooting for knots here.)
                const float UNITS_FACTOR = 0.7f;

                var dens = World.GetAltitudeDensity(this.Position);
                return this.Velocity.Length() * dens * UNITS_FACTOR;
            }
        }

        /// <summary>
        /// True air speed.
        /// </summary>
        public float AirSpeedTrue
        {
            get
            {
                return this.Velocity.Length();
            }
        }

        public float RotationSpeed
        {
            get { return _rotationSpeed; }

            set
            {
                if (Math.Abs(value) > World.MAX_ROT_SPD)
                    _rotationSpeed = World.MAX_ROT_SPD * Math.Sign(value);
                else
                    _rotationSpeed = value;
            }
        }

        /// <summary>
        /// Age in milliseconds.
        /// </summary>
        public double AgeMs(float dt)
        {
            return (this.Age / dt) * World.LastFrameTimeMs;
        }

        public int PlayerID
        {
            get { return ID.PlayerID; }
            set { ID = new GameID(value, ID.ObjectID); }
        }

        public uint ObjectID
        {
            get { return ID.ObjectID; }
            set { ID = new GameID(ID.PlayerID, value); }
        }

        public GameObject Owner { get; set; }

        public GameObjectFlags Flags { get; set; }

        /// <summary>
        /// True if gravity and physics should be applied.
        /// </summary>
        public bool IsAwake = true;
        public bool IsExpired = false;
        public float RenderScale = 1f;
        public int RenderLayer = 99;
        public float Age = 0f;
        public bool IsNetObject = false;

        private List<GameObject> _attachments = null;
        private List<GameObject> _attachmentsPhyics = null;
        private List<GameTimer> _timers = null;

        protected float _rotationSpeed = 0f;
        protected float _rotation = 0f;
        protected int _gridHash = 0;
        private bool _hasPhysicsUpdate = false;
        private bool _isSpatialGridObj = false;


        public GameObject()
        {
            if (this is not INoGameID)
                this.ID = new GameID(-1, World.GetNextObjectId());

            _hasPhysicsUpdate = ImplementsPhysicsUpdate();
        }

        public GameObject(GameObjectFlags flags) : this()
        {
            this.Flags = flags;

            if (HasFlag(GameObjectFlags.SpatialGrid))
                _isSpatialGridObj = true;
        }

        public GameObject(GameObject owner) : this()
        {
            Owner = owner;
        }

        public GameObject(D2DPoint pos, GameObjectFlags flags) : this(pos, D2DPoint.Zero, 0f, 0f, flags)
        {
            Position = pos;
        }

        public GameObject(D2DPoint pos) : this(pos, D2DPoint.Zero, 0f, 0f)
        {
            Position = pos;
        }

        public GameObject(D2DPoint pos, D2DPoint velo, float rotation, GameObjectFlags flags) : this(pos, velo, rotation, 0f, flags)
        {
            Position = pos;
            Velocity = velo;
            Rotation = rotation;
        }

        public GameObject(D2DPoint pos, D2DPoint velo, float rotation, float rotationSpeed) : this()
        {
            Position = pos;
            Velocity = velo;
            Rotation = rotation;
            RotationSpeed = rotationSpeed;
        }

        public GameObject(D2DPoint pos, D2DPoint velo, float rotation, float rotationSpeed, GameObjectFlags flags) : this(flags)
        {
            Position = pos;
            Velocity = velo;
            Rotation = rotation;
            RotationSpeed = rotationSpeed;
        }

        /// <summary>
        /// Returns true if this object has the specified flag.
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public bool HasFlag(GameObjectFlags flag)
        {
            return (this.Flags & flag) == flag;
        }

        /// <summary>
        /// Adds the specified flag if it is not already set.
        /// </summary>
        /// <param name="flag"></param>
        public void AddFlag(GameObjectFlags flag)
        {
            if (!HasFlag(flag))
                this.Flags |= flag;

            if (HasFlag(GameObjectFlags.SpatialGrid))
                _isSpatialGridObj = true;
        }

        /// <summary>
        /// Removes the specified flag is it is set.
        /// </summary>
        /// <param name="flag"></param>
        public void RemoveFlag(GameObjectFlags flag)
        {
            if (HasFlag(flag))
                this.Flags -= flag;

            if (!HasFlag(GameObjectFlags.SpatialGrid))
                _isSpatialGridObj = false;
        }

        /// <summary>
        /// Updates the position of this object to the specified position and syncs any attachments.
        /// </summary>
        /// <param name="position"></param>
        public void SetPosition(D2DPoint position)
        {
            SetPosition(position, this.Rotation);
        }

        /// <summary>
        /// Updates the position and rotation of this object to the specified position/rotation and syncs any attachments.
        /// </summary>
        /// <param name="position"></param>
        public void SetPosition(D2DPoint position, float rotation)
        {
            this.Position = position;
            this.Rotation = rotation;

            UpdateAllAttachments(0f);
            UpdatePoly();
        }

        /// <summary>
        /// Add a <see cref="GameTimer"/> which will be automatically updated within the <see cref="Update(float)"/> method.
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="cooldown"></param>
        /// <param name="autoRestart"></param>
        /// <returns></returns>
        public GameTimer AddTimer(float interval, float cooldown = 0f, bool autoRestart = false)
        {
            var timer = new GameTimer(interval, cooldown, autoRestart);

            if (_timers == null)
                _timers = new List<GameTimer>();

            _timers.Add(timer);

            return timer;
        }

        /// <summary>
        /// Add a <see cref="GameTimer"/> which will be automatically updated within the <see cref="Update(float)"/> method.
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="autoRestart"></param>
        /// <returns></returns>
        public GameTimer AddTimer(float interval, bool autoRestart = false)
        {
            return AddTimer(interval, 0f, autoRestart);
        }

        /// <summary>
        /// Add a <see cref="GameTimer"/> which will be automatically updated within the <see cref="Update(float)"/> method.
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public GameTimer AddTimer(float interval)
        {
            return AddTimer(interval, 0f, false);
        }

        /// <summary>
        /// Add an attached game object which will be automatically updated when this objects <see cref="Update(float)"/> method is called.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="attachedObject">GameObject to attach.</param>
        /// <param name="highFrequencyPhysics">True if this object will be updated within the <see cref="DoPhysicsUpdate(float)"/> method.  Otherwise it will be updated within <see cref="DoUpdate(float)"/>. </param>
        /// <returns></returns>
        public T AddAttachment<T>(T attachedObject, bool highFrequencyPhysics = false) where T : GameObject
        {
            if (highFrequencyPhysics)
            {
                if (_attachmentsPhyics == null)
                    _attachmentsPhyics = new List<GameObject>();

                _attachmentsPhyics.Add(attachedObject);

                return attachedObject;
            }
            else
            {
                if (_attachments == null)
                    _attachments = new List<GameObject>();

                _attachments.Add(attachedObject);

                return attachedObject;
            }
        }

        private bool ImplementsPhysicsUpdate()
        {
            var type = this.GetType();
            var phyiscsMethod = type.GetMethod(nameof(this.DoPhysicsUpdate));

            if (phyiscsMethod.DeclaringType == typeof(GameObject))
                return false;
            else
                return true;
        }

        public virtual void Update(float dt)
        {
            if (_hasPhysicsUpdate)
            {
                for (int i = 0; i < World.PHYSICS_SUB_STEPS; i++)
                {
                    var partialDT = World.SUB_DT;

                    DoPhysicsUpdate(partialDT);

                    UpdatePhysicsAttachments(partialDT);
                }
            }

            DoUpdate(dt);

            UpdateTimers(dt);
            UpdateAttachments(dt);

            if (_isSpatialGridObj && SpatialGridRef != null)
            {
                // Update the hash for the spatial grid.
                _gridHash = SpatialGridRef.GetGridHash(this);
            }
        }

        private void AdvancePositionAndRotation(float dt)
        {
            Position += Velocity * dt;
            Rotation += RotationSpeed * dt;
        }

        /// <summary>
        /// This method is called with <see cref="World.SUB_DT"/> and <see cref="World.PHYSICS_SUB_STEPS"/> number of turns on every frame prior the <see cref="DoUpdate(float)"/> method. 
        /// 
        /// Override this method to implement high frequency physics.
        /// </summary>
        /// <param name="dt"></param>
        public virtual void DoPhysicsUpdate(float dt)
        {
            // Don't advance net physics objects.
            if (!World.IsNetGame || World.IsNetGame && !this.IsNetObject)
                AdvancePositionAndRotation(dt);
        }

        /// <summary>
        /// This method is called every frame prior to rendering.
        /// 
        /// Override this method to implement regular, low frequency updates.
        /// </summary>
        /// <param name="dt"></param>
        public virtual void DoUpdate(float dt)
        {
            Age += dt;

            if (this.IsExpired)
                return;

            if (this.IsAwake)
            {
                if (!_hasPhysicsUpdate)
                    AdvancePositionAndRotation(dt);
            }

            ClampToGround(dt);
            UpdatePoly();
        }

        protected void UpdatePoly()
        {
            if (this is IPolygon polyObj)
            {
                if (polyObj.Polygon != null)
                    polyObj.Polygon.Update();
            }
        }

        protected void FlipPoly()
        {
            if (this is IPolygon polyObj)
            {
                if (polyObj.Polygon != null)
                    polyObj.Polygon.FlipY();
            }
        }

        protected void UpdatePhysicsAttachments(float dt)
        {
            if (_attachmentsPhyics == null)
                return;

            for (int i = _attachmentsPhyics.Count - 1; i >= 0; i--)
            {
                var attachment = _attachmentsPhyics[i];

                if (attachment.IsExpired)
                    _attachmentsPhyics.RemoveAt(i);
                else
                    attachment.Update(dt);
            }
        }

        private void UpdateAttachments(float dt)
        {
            if (_attachments == null)
                return;

            for (int i = _attachments.Count - 1; i >= 0; i--)
            {
                var attachment = _attachments[i];

                if (attachment.IsExpired)
                    _attachments.RemoveAt(i);
                else
                    attachment.Update(dt);
            }
        }

        public virtual void UpdateAllAttachments(float dt)
        {
            UpdatePhysicsAttachments(dt);
            UpdateAttachments(dt);
        }

        private void UpdateTimers(float dt)
        {
            if (_timers == null)
                return;

            for (int i = 0; i < _timers.Count; i++)
                _timers[i].Update(dt);
        }

        private void FlipAttachments()
        {
            if (_attachments != null)
                for (int i = 0; i < _attachments.Count; i++)
                    _attachments[i].FlipY();

            if (_attachmentsPhyics != null)
                for (int i = 0; i < _attachmentsPhyics.Count; i++)
                    _attachmentsPhyics[i].FlipY();
        }

        public virtual void ClampToGround(float dt)
        {
            // Don't bother with objects with no flags.
            if (Flags == 0)
                return;

            if (HasFlag(GameObjectFlags.BounceOffGround))
            {
                // Let some objects bounce.
                if (this.Altitude <= 0f)
                {
                    var veloMag = (this.Velocity * dt).Length();
                    var gravMag = (World.Gravity * dt).Length();

                    // If velo is greater than a fraction of gravity acceleration.
                    if (veloMag > (gravMag * 0.25f) && this.IsAwake)
                    {
                        // Bounce.
                        this.SetPosition(new D2DPoint(this.Position.X, 0f));
                        this.Velocity = new D2DPoint(this.Velocity.X * 0.7f, -this.Velocity.Y * 0.3f);

                        // Set rotation speed to horizonal velocity to give a rolling effect.
                        var w = this.Velocity.X;
                        this.RotationSpeed = w;
                    }
                    else
                    {
                        if (HasFlag(GameObjectFlags.CanSleep))
                        {
                            // Go to sleep.
                            this.Velocity = D2DPoint.Zero;
                            this.SetPosition(new D2DPoint(this.Position.X, 0f));
                            this.RotationSpeed = 0f;
                            this.IsAwake = false;
                        }
                        else
                        {
                            this.SetPosition(new D2DPoint(this.Position.X, 0f));
                        }
                    }
                }
            }
            else if (HasFlag(GameObjectFlags.ClampToGround))
            {
                // Clamp all other objects to ground level.
                if (this.Altitude <= 0f)
                {
                    this.SetPosition(new D2DPoint(this.Position.X, 0f));
                    this.Velocity = new D2DPoint(this.Velocity.X * 0.97f, 0f);
                }
            }
        }

        public virtual void Render(RenderContext ctx)
        {
            if (this.IsExpired)
                return;
        }

        public virtual void FlipY()
        {
            FlipAttachments();

            FlipPoly();
        }

        public float GetInertia(float mass)
        {
            return mass * World.INERTIA_MULTI;
        }

        public virtual bool ContainedBy(D2DRect rect)
        {
            return rect.Contains(this.Position);
        }

        public float FOVToObject(GameObject obj)
        {
            var dir = obj.Position - this.Position;
            var angle = dir.Angle();
            var diff = Utilities.AngleDiff(this.Rotation, angle);

            return diff;
        }

        public bool IsObjInFOV(GameObject obj, float fov)
        {
            var dir = obj.Position - this.Position;

            var angle = dir.Angle();
            var diff = Utilities.AngleDiff(this.Rotation, angle);

            return diff <= (fov * 0.5f);
        }

        public bool Equals(GameObject? other)
        {
            if (other == null)
                return false;

            return this.ID.Equals(other.ID);
        }

        public virtual void Dispose()
        {
            this.IsExpired = true;
        }


        public bool CollidesWith(GameObject obj, out D2DPoint pos, float dt)
        {
            if (this is IPolygon polyObj)
            {
                if (obj is IPolygon impactPoly)
                {
                    return CollisionHelpers.PolygonSweepCollision(obj, impactPoly.Polygon, polyObj.Polygon, this.Velocity, dt, out pos);
                }
                else
                {
                    return CollisionHelpers.PolygonSweepCollision(obj, polyObj.Polygon, this.Velocity, dt, out pos);
                }
            }

            pos = D2DPoint.Zero;
            return false;
        }

    }


    public class GameObjectNet : GameObject
    {
        /// <summary>
        /// Lag amount in milliseconds of the most recent net update.
        /// </summary>
        public double LagAmount = 0;

        /// <summary>
        /// Lag amount in number of elapsed frames.
        /// </summary>
        public float LagAmountFrames
        {
            get
            {
                var frames = (float)this.LagAmount / (float)World.LastFrameTimeMs;
                return frames;
            }
        }

        /// <summary>
        /// Milliseconds elapsed between now and the last net update.
        /// </summary>
        public double NetAge
        {
            get
            {
                var now = World.CurrentNetTimeTicks();
                var age = now - _lastNetTime;
                var ageMs = TimeSpan.FromTicks(age).TotalMilliseconds;
                return ageMs;
            }
        }

        protected InterpolationBuffer<GameObjectPacket> InterpBuffer = null;
        protected HistoricalBuffer<GameObjectPacket> HistoryBuffer = null;
        protected long _lastNetTime = 0;

        public GameObjectNet(GameObjectFlags flags) : base(flags)
        {
            InitNetBuffers();
        }
       
        public GameObjectNet(D2DPoint pos, GameObjectFlags flags) : base(pos, flags)
        {
            InitNetBuffers();
        }

        public GameObjectNet(D2DPoint pos, D2DPoint velo, float rotation, GameObjectFlags flags) : base(pos, velo, rotation, flags)
        {
            InitNetBuffers();
        }

        private void InitNetBuffers()
        {
            if (World.IsNetGame)
                this._lastNetTime = World.CurrentNetTimeTicks();

            if (World.IsNetGame && (this is FighterPlane || this is GuidedMissile))
            {
                if (HistoryBuffer == null)
                    HistoryBuffer = new HistoricalBuffer<GameObjectPacket>(GetInterpState);

                if (InterpBuffer == null)
                    InterpBuffer = new InterpolationBuffer<GameObjectPacket>(TimeSpan.FromMilliseconds(World.NET_INTERP_AMOUNT).Ticks, GetInterpState);
            }
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            this.RecordHistory();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (World.IsNetGame && IsNetObject && InterpBuffer != null)
            {

                if (!World.IsServer && InterpBuffer != null)
                {
                    var now = World.CurrentNetTimeTicks();
                    var state = InterpBuffer.InterpolateState(now);

                    if (state != null)
                    {
                        // Apply interpolated state.
                        this.Velocity = state.Velocity;
                        this.SetPosition(state.Position, state.Rotation);
                        this.RotationSpeed = state.RotationSpeed;
                    }
                    else
                    {
                        // Just try to extrapolate if we failed to get an interpolated state.
                        var extrapPos = this.Position + this.Velocity * dt;
                        var extrapRot = this.Rotation + this.RotationSpeed * dt;
                        this.SetPosition(extrapPos, extrapRot);
                    }
                }
                else if (World.IsServer)
                {
                    // Always extrapolate on server.
                    var extrapPos = this.Position + this.Velocity * dt;
                    var extrapRot = this.Rotation + this.RotationSpeed * dt;
                    this.SetPosition(extrapPos, extrapRot);
                }

                // Update physics attachments (like wings) after interpolating a new state.
                UpdatePhysicsAttachments(0f);
            }
        }

        public virtual void NetUpdate(GameObjectPacket packet)
        {
            // Don't interp on server.
            if (!World.IsServer)
            {
                InterpBuffer.Enqueue(packet, packet.FrameTime);
            }
            else
            {
                this.Velocity = packet.Velocity;
                this.SetPosition(packet.Position, packet.Rotation);
                this.RotationSpeed = packet.RotationSpeed;
            }

            var now = World.CurrentNetTimeTicks();
            this.LagAmount = TimeSpan.FromTicks(now - packet.FrameTime).TotalMilliseconds;
            this._lastNetTime = now;
        }

        public void RecordHistory()
        {
            if (!World.IsServer)
                return;

            if (HistoryBuffer != null && (this is FighterPlane || this is GuidedMissile))
            {
                var histState = new GameObjectPacket(this);
                HistoryBuffer.Enqueue(histState, World.CurrentNetTimeMs());
            }
        }

        private GameObjectPacket GetInterpState(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = new GameObjectPacket();

            state.Position = D2DPoint.Lerp(from.Position, to.Position, (float)pctElapsed);
            state.Velocity = D2DPoint.Lerp(from.Velocity, to.Velocity, (float)pctElapsed);
            state.Rotation = Utilities.LerpAngle(from.Rotation, to.Rotation, (float)pctElapsed);
            state.RotationSpeed = Utilities.Lerp(from.RotationSpeed, to.RotationSpeed, (float)pctElapsed);

            return state;
        }

        public bool CollidesWithNet(GameObject obj, out D2DPoint pos, out GameObjectPacket? histState, double frameTime, float dt)
        {
            var histPos = this.HistoryBuffer.GetHistoricalState(frameTime);

            if (histPos != null)
            {
                if (this is IPolygon polyObj)
                {
                    // Create a copy of the polygon and translate it to the historical position/rotation.
                    var histPoly = new RenderPoly(polyObj.Polygon, histPos.Position, histPos.Rotation);

                    // Check for collisions against the historical position.
                    if (CollisionHelpers.PolygonSweepCollision(obj, histPoly, histPos.Velocity, dt, out pos))
                    {
                        histState = histPos;
                        return true;
                    }
                }
            }
            else
            {
                var normalCollide = CollidesWith(obj, out D2DPoint pos2, dt);
                pos = pos2;
                histState = null;
                return normalCollide;
            }

            pos = D2DPoint.Zero;
            histState = null;
            return false;
        }

    }
}
