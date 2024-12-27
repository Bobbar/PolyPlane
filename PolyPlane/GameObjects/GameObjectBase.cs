using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, IDisposable, IFlippable
    {
        private const float MAX_ROT_SPD = 3000f;

        public GameID ID { get; set; } = new GameID();

        public int RenderOrder = 99;

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
                var dens = World.GetDensityAltitude(this.Position);
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
        /// Lag amount in number of elapsed frames.
        /// </summary>
        public float LagAmountFrames
        {
            get
            {
                var frames = (float)this.LagAmount / World.TARGET_FRAME_TIME;
                return frames;
            }
        }

        public float RenderScale = World.RenderScale;
        public bool Visible = true;
        public bool IsNetObject = false;
        public double LagAmount = 0;
        public float Age = 0f;
        public double LastNetTime = 0;

        public double NetAge
        {
            get
            {
                var now = World.CurrentNetTimeMs();
                var age = now - LastNetTime;
                return age;
            }
        }
        /// <summary>
        /// True if gravity and physics should be applied.
        /// </summary>
        public bool IsAwake = true;

        /// <summary>
        /// Age in milliseconds.
        /// </summary>
        public float AgeMs(float dt)
        {
            return (this.Age / dt) * World.TARGET_FRAME_TIME;
        }

        public int PlayerID
        {
            get { return ID.PlayerID; }
            set { ID = new GameID(value, ID.ObjectID); }
        }

        public GameObject Owner { get; set; }

        public bool IsExpired = false;

        protected Random _rnd => Utilities.Rnd;
        protected InterpolationBuffer<GameObjectPacket> InterpBuffer = null;
        protected HistoricalBuffer<GameObjectPacket> HistoryBuffer = null;

        protected float _rotationSpeed = 0f;
        protected float _rotation = 0f;
        protected float _verticalSpeed = 0f;
        protected float _prevAlt = 0f;

        public GameObject()
        {
            if (World.IsNetGame)
                this.LastNetTime = World.CurrentNetTimeMs();

            if (this is not INoGameID)
                this.ID = new GameID(-1, World.GetNextObjectId());

            if (World.IsNetGame && (this is FighterPlane || this is GuidedMissile))
            {
                if (HistoryBuffer == null)
                    HistoryBuffer = new HistoricalBuffer<GameObjectPacket>(GetInterpState);

                if (InterpBuffer == null)
                    InterpBuffer = new InterpolationBuffer<GameObjectPacket>(World.NET_INTERP_AMOUNT, InterpObject);
            }
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

        public virtual void Update(float dt)
        {
            Age += dt;

            if (this.IsExpired)
                return;

            if (World.InterpOn && World.IsNetGame && IsNetObject && InterpBuffer != null)
            {
                var nowMs = World.CurrentNetTimeMs();
                InterpBuffer.InterpolateState(nowMs);
            }
            else
            {
                if (this.IsAwake)
                {
                    Position += Velocity * dt;

                    Rotation += RotationSpeed * dt;

                    var altDiff = this.Altitude - _prevAlt;
                    _verticalSpeed = altDiff / dt;
                    _prevAlt = this.Altitude;
                }

                ClampToGround(dt);
            }
        }

        public virtual void NetUpdate(D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            if (World.InterpOn && World.IsClient)
            {
                var newState = new GameObjectPacket(this);
                newState.Position = position;
                newState.Velocity = velocity;
                newState.Rotation = rotation;

                InterpBuffer.Enqueue(newState, frameTime);
            }
            else
            {
                this.Position = position;
                this.Rotation = rotation;
                this.Velocity = velocity;
            }

            var now = World.CurrentNetTimeMs();
            this.LagAmount = now - frameTime;
            this.LastNetTime = now;
        }

        public virtual void ClampToGround(float dt)
        {
            if (this is Debris)
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
                        this.Velocity = new D2DPoint(this.Velocity.X * 0.7f, -this.Velocity.Y * 0.3f);
                        this.Position = new D2DPoint(this.Position.X, 0f);

                        // Set rotation speed to horizonal velocity to give a rolling effect.
                        var w = this.Velocity.X;
                        this.RotationSpeed = w;
                    }
                    else
                    {
                        // Go to sleep.
                        this.Velocity = D2DPoint.Zero;
                        this.Position = new D2DPoint(this.Position.X, 0f);
                        this.RotationSpeed = 0f;
                        this.IsAwake = false;
                    }
                }
            }
            else
            {
                // Let explosions spawn below ground.
                if (this is Explosion)
                    return;

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
        { }

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

            this.Position = state.Position;
            this.Velocity = state.Velocity;
            this.Rotation = state.Rotation;
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
            var dt = World.DynamicDT;

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
