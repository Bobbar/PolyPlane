using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Net.Interpolation;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, IDisposable, IFlippable
    {
        public GameID ID { get; set; } = new GameID();

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

        public float Mass { get; set; } = 1f;

        public virtual float Rotation
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

        public float VerticalSpeed => _verticalSpeed;

        /// <summary>
        /// Air speed relative to altitude & air density.
        /// </summary>
        public float AirSpeedIndicated
        {
            get
            {
                var dens = World.GetAltitudeDensity(this.Position);
                return this.Velocity.Length() * dens;
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
                if (Math.Abs(value) > MAX_ROT_SPD)
                    _rotationSpeed = MAX_ROT_SPD * Math.Sign(value);
                else
                    _rotationSpeed = value;
            }
        }

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
                var frames = (float)this.LagAmount / (float)World.LAST_FRAME_TIME;
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

        /// <summary>
        /// Age in milliseconds.
        /// </summary>
        public double AgeMs(float dt)
        {
            return (this.Age / dt) * World.LAST_FRAME_TIME;
        }

        public int PlayerID
        {
            get { return ID.PlayerID; }
            set { ID = new GameID(value, ID.ObjectID); }
        }

        public GameObject Owner { get; set; }

        public GameObjectFlags Flags { get; set; }

        /// <summary>
        /// True if gravity and physics should be applied.
        /// </summary>
        public bool IsAwake = true;
        public bool IsExpired = false;
        public float RenderScale = World.RenderScale;
        public int RenderOrder = 99;
        public bool Visible = true;
        public bool IsNetObject = false;
        public float Age = 0f;

        protected Random _rnd => Utilities.Rnd;
        protected InterpolationBuffer<GameObjectPacket> InterpBuffer = null;
        protected HistoricalBuffer<GameObjectPacket> HistoryBuffer = null;
        private List<GameObject> _attachments = null;
        private List<GameObject> _attachmentsPhyics = null;
        private List<GameTimer> _timers = null;
       
        protected float _rotationSpeed = 0f;
        protected float _rotation = 0f;
        protected float _verticalSpeed = 0f;
        protected float _prevAlt = 0f;
        protected long _lastNetTime = 0;

        private bool _hasPhysicsUpdate = false;

        private const float MAX_ROT_SPD = 3000f;

        public GameObject()
        {
            if (World.IsNetGame)
                this._lastNetTime = World.CurrentNetTimeTicks();

            if (this is not INoGameID)
                this.ID = new GameID(-1, World.GetNextObjectId());

            if (World.IsNetGame && (this is FighterPlane || this is GuidedMissile))
            {
                if (HistoryBuffer == null)
                    HistoryBuffer = new HistoricalBuffer<GameObjectPacket>(GetInterpState);

                if (InterpBuffer == null)
                    InterpBuffer = new InterpolationBuffer<GameObjectPacket>(TimeSpan.FromMilliseconds(World.NET_INTERP_AMOUNT).Ticks, InterpObject);
            }

            _hasPhysicsUpdate = ImplementsPhysicsUpdate();
        }

        public GameObject(GameObject owner) : this()
        {
            Owner = owner;
        }

        public GameObject(D2DPoint pos) : this(pos, D2DPoint.Zero, 0f, 0f)
        {
            Position = pos;
        }

        public GameObject(D2DPoint pos, D2DPoint velo) : this(pos, velo, 0f, 0f)
        {
            Position = pos;
            Velocity = velo;
        }

        public GameObject(D2DPoint pos, D2DPoint velo, float rotation) : this(pos, velo, rotation, 0f)
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

            if (World.IsNetGame && IsNetObject && InterpBuffer != null && !World.IsServer)
            {
                var now = World.CurrentNetTimeTicks();
                InterpBuffer.InterpolateState(now);

                // Update physics attachments (like wings) after interpolating a new state.
                UpdatePhysicsAttachments(0f);
            }
            else
            {
                if (this.IsAwake)
                {
                    if (!_hasPhysicsUpdate)
                        AdvancePositionAndRotation(dt);

                    var altDiff = this.Altitude - _prevAlt;
                    _verticalSpeed = altDiff / dt;
                    _prevAlt = this.Altitude;
                }

                ClampToGround(dt);
            }
        }

        public virtual void NetUpdate(D2DPoint position, D2DPoint velocity, float rotation, long frameTime)
        {
            // Don't interp on server.
            if (!World.IsServer)
            {
                var newState = new GameObjectPacket(this);
                newState.Position = position;
                newState.Velocity = velocity;
                newState.Rotation = rotation;

                InterpBuffer.Enqueue(newState, frameTime);
            }
            else
            {
                this.Velocity = velocity;
                this.SetPosition(position, rotation);
            }
          
            var now = World.CurrentNetTimeTicks();
            this.LagAmount = TimeSpan.FromTicks(now - frameTime).TotalMilliseconds;
            this._lastNetTime = now;
        }

        private void UpdatePhysicsAttachments(float dt)
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

            _timers.ForEach(timer => timer.Update(dt));
        }

        private void FlipAttachments()
        {
            if (_attachments != null)
                _attachments.ForEach(a => a.FlipY());

            if (_attachmentsPhyics != null)
                _attachmentsPhyics.ForEach(a => a.FlipY());
        }

        public virtual void ClampToGround(float dt)
        {
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
                        this.Position = new D2DPoint(this.Position.X, 0f);
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
                            this.Position = new D2DPoint(this.Position.X, 0f);
                            this.RotationSpeed = 0f;
                            this.IsAwake = false;
                        }
                        else
                        {
                            this.Position = new D2DPoint(this.Position.X, 0f);
                        }
                    }
                }
            }
            else if (HasFlag(GameObjectFlags.ClampToGround))
            {
                // Clamp all other objects to ground level.
                if (this.Altitude <= 0f)
                {
                    this.Position = new D2DPoint(this.Position.X, 0f);
                    this.Velocity = new D2DPoint(this.Velocity.X + -this.Velocity.X * (dt * 1f), 0f);
                }
            }
        }

        public virtual void Render(RenderContext ctx)
        {
            if (this.IsExpired || !this.Visible)
                return;
        }

        public virtual void FlipY()
        {
            FlipAttachments();
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

        /// <summary>
        /// Recursively finds the root object within the <see cref="GameObject.Owner"/> relationships.
        /// </summary>
        /// <returns></returns>
        public GameObject FindRootObject()
        {
            return FindRootObject(this);
        }

        private GameObject FindRootObject(GameObject obj)
        {
            if (obj.Owner != null)
                return FindRootObject(obj.Owner);
            else if (obj.Owner == null)
                return obj;

            return null;
        }

        private void InterpObject(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = GetInterpState(from, to, pctElapsed);

            this.Velocity = state.Velocity;
            this.SetPosition(state.Position, state.Rotation);
        }

        private GameObjectPacket GetInterpState(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = new GameObjectPacket();

            state.Position = D2DPoint.Lerp(from.Position, to.Position, (float)pctElapsed);
            state.Velocity = D2DPoint.Lerp(from.Velocity, to.Velocity, (float)pctElapsed);
            state.Rotation = Utilities.LerpAngle(from.Rotation, to.Rotation, (float)pctElapsed);

            return state;
        }
    }


    public class GameObjectPoly : GameObject
    {
        public RenderPoly Polygon;

        public GameObjectPoly() : base()
        {
        }

        public GameObjectPoly(D2DPoint pos) : base(pos)
        {
        }

        public GameObjectPoly(D2DPoint pos, D2DPoint velo) : base(pos, velo)
        {
        }

        public GameObjectPoly(D2DPoint pos, D2DPoint velo, float rotation) : base(pos, velo, rotation)
        {
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            this.RecordHistory();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (Polygon != null)
                Polygon.Update();
        }

        public override void UpdateAllAttachments(float dt)
        {
            base.UpdateAllAttachments(dt);

            if (Polygon != null)
                Polygon.Update();
        }

        public override void FlipY()
        {
            base.FlipY();

            if (Polygon != null)
                Polygon.FlipY();
        }

        /// <summary>
        /// Records the current state to the historical buffer.  (Used for lag compensation during collisions)
        /// </summary>
        public void RecordHistory()
        {
            if (!World.IsNetGame)
                return;

            if (HistoryBuffer != null && (this is FighterPlane || this is GuidedMissile))
            {
                var histState = new GameObjectPacket(this);
                HistoryBuffer.Enqueue(histState, World.CurrentNetTimeMs());
            }
        }

        public bool CollidesWithNet(GameObjectPoly obj, out D2DPoint pos, out GameObjectPacket? histState, double frameTime, float dt)
        {
            var histPos = this.HistoryBuffer.GetHistoricalState(frameTime);

            if (histPos != null)
            {
                // Create a copy of the polygon and translate it to the historical position/rotation.
                var histPoly = new RenderPoly(this.Polygon, histPos.Position, histPos.Rotation);

                // Check for collisions against the historical position.
                if (CollisionHelpers.PolygonSweepCollision(obj, histPoly, histPos.Velocity, dt, out pos))
                {
                    histState = histPos;
                    return true;
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

        public bool CollidesWith(GameObjectPoly obj, out D2DPoint pos, float dt)
        {
            return CollisionHelpers.PolygonSweepCollision(obj, this.Polygon, this.Velocity, dt, out pos);
        }

        public bool CollidesWith(GameObject obj, out D2DPoint pos, float dt)
        {
            return CollisionHelpers.PolygonSweepCollision(obj, this.Polygon, this.Velocity, dt, out pos);
        }

        public void DrawVeloLines(D2DGraphics gfx, D2DColor color)
        {
            var dt = World.CurrentDT;

            var relVelo = this.Velocity * dt;
            var relVeloHalf = relVelo * 0.5f;

            var targAngle = 0f;

            if (this is Bullet || this is GuidedMissile || this is FighterPlane)
            {
                var nearest = World.ObjectManager.GetNear(this).Where(o => !o.ID.Equals(this.ID) && o is FighterPlane).OrderBy(o => o.Position.DistanceTo(this.Position)).FirstOrDefault();
                if (nearest != null)
                {
                    relVelo = (this.Velocity - nearest.Velocity) * dt;
                    relVeloHalf = relVelo * 0.5f;
                    targAngle = relVelo.Angle();
                }
            }

            foreach (var pnt in this.Polygon.Poly)
            {
                var aVelo = Utilities.AngularVelocity(this, pnt);
                var veloPnt1 = pnt;
                var veloPnt2 = pnt + relVelo;


                gfx.DrawLine(veloPnt1, veloPnt2, color, 0.5f);
            }

            var lagPntStart = this.Position - (this.Velocity * (this.LagAmountFrames * dt));
            var lagPntEnd = this.Position;

            if (this.AgeMs(dt) < (this.LagAmount * 2f))
                gfx.DrawLine(lagPntStart, lagPntEnd, color);


            var pnts = this.Polygon.GetPointsFacingDirection(targAngle);

            foreach (var pnt in pnts)
            {
                gfx.FillEllipseSimple(pnt, 1f, D2DColor.Blue);
            }

        }

        public virtual bool Contains(D2DPoint pnt)
        {
            return PointInPoly(pnt, this.Polygon.Poly);
        }

        private bool PointInPoly(D2DPoint pnt, D2DPoint[] poly)
        {
            int i, j = 0;
            bool c = false;
            for (i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].Y > pnt.Y) != (poly[j].Y > pnt.Y)) && (pnt.X < (poly[j].X - poly[i].X) * (pnt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    c = !c;
            }

            return c;
        }

        public D2DPoint CenterOfPolygon()
        {
            var centroid = D2DPoint.Zero;

            // List iteration
            // Link reference:
            // https://en.wikipedia.org/wiki/Centroid
            foreach (var point in this.Polygon.Poly)
                centroid += point;

            centroid /= this.Polygon.Poly.Length;
            return centroid;
        }

        public static D2DPoint[] RandomPoly(int nPoints, float radius)
        {
            var rnd = Utilities.Rnd;

            var poly = new D2DPoint[nPoints];
            var dists = new float[nPoints];

            for (int i = 0; i < nPoints; i++)
            {
                dists[i] = rnd.NextFloat(radius / 2f, radius);
            }

            var radians = rnd.NextFloat(0.8f, 1f);
            var angle = 0f;

            for (int i = 0; i < nPoints; i++)
            {
                var pnt = new D2DPoint((float)Math.Cos(angle * radians) * dists[i], (float)Math.Sin(angle * radians) * dists[i]);
                poly[i] = pnt;
                angle += (float)(2f * Math.PI / nPoints);
            }

            return poly;
        }
    }

}
