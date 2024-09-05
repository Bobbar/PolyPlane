﻿using PolyPlane.GameObjects.Interfaces;
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

        public readonly int LocalID;
        public int RenderOrder = 99;

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

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
                return this.Position.Y * -1f;
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

        public float RenderOffset = 1f;
        public bool Visible = true;
        public bool IsNetObject = false;
        public double LagAmount = 0;
        public float Age = 0f;
        /// <summary>
        /// True if gravity and physics should be applied.
        /// </summary>
        public bool IsAwake = true;

        /// <summary>
        /// Age in milliseconds.
        /// </summary>
        public float AgeMs
        {
            get { return (this.Age / World.DT) * (1000f / 16f); }
        }


        public int PlayerID
        {
            get { return ID.PlayerID; }
            set { ID = new GameID(value, ID.ObjectID); }
        }

        public GameObject Owner { get; set; }

        public bool IsExpired = false;
        public long CurrentFrame { get; set; } = 0;

        protected Random _rnd => Utilities.Rnd;
        protected InterpolationBuffer<GameObjectPacket> InterpBuffer = null;
        protected HistoricalBuffer<GameObjectPacket> HistoryBuffer = null;
        protected SmoothPoint _posSmooth = null;

        protected float _rotationSpeed = 0f;
        protected float _rotation = 0f;
        protected float _verticalSpeed = 0f;
        protected float _prevAlt = 0f;

        public GameObject()
        {
            this.ID = new GameID(-1, World.GetNextObjectId());
            this.LocalID = World.GetNextLocalId();

            if (World.IsNetGame && (this is FighterPlane || this is GuidedMissile))
            {
                _posSmooth = new SmoothPoint(5);

                if (HistoryBuffer == null)
                {
                    HistoryBuffer = new HistoricalBuffer<GameObjectPacket>();
                    HistoryBuffer.Interpolate = (from, to, pctElap) => GetInterpState(from, to, pctElap);
                }

                if (InterpBuffer == null)
                    InterpBuffer = new InterpolationBuffer<GameObjectPacket>(new GameObjectPacket(this), World.SERVER_TICK_RATE, (from, to, pctElap) => InterpObject(from, to, pctElap));
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

        public virtual void Update(float dt, float renderScale)
        {
            Age += dt;

            if (this.IsExpired)
                return;

            if (World.InterpOn && World.IsNetGame && IsNetObject && InterpBuffer != null)
            {
                var nowMs = World.CurrentTime();
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

        public virtual void NetUpdate(float dt, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            if (World.InterpOn)
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

            this.LagAmount = World.CurrentTime() - frameTime;
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

                    // If velo is greater than half gravity acceleration.
                    if (veloMag > (gravMag * 0.5f) && this.IsAwake)
                    {
                        // Bounce.
                        this.Velocity = new D2DPoint(this.Velocity.X * 0.95f, -this.Velocity.Y * 0.3f);
                        this.Position = new D2DPoint(this.Position.X, 0f);
                        this.RotationSpeed *= 0.5f;
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
            CurrentFrame++;

            if (this.IsExpired || !this.Visible)
                return;
        }

        public virtual void FlipY()
        { }

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

        public float ClosingRate(GameObject obj)
        {
            var nextPos1 = this.Position + this.Velocity;
            var nextPos2 = obj.Position + obj.Velocity;

            var curDist = this.Position.DistanceTo(obj.Position);
            var nextDist = nextPos1.DistanceTo(nextPos2);

            return curDist - nextDist;
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

        private void InterpObject(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = GetInterpState(from, to, pctElapsed);

            this.Position = _posSmooth.Add(state.Position);
            this.Velocity = state.Velocity;
            this.Rotation = state.Rotation;
        }

        private GameObjectPacket GetInterpState(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = new GameObjectPacket();

            state.Position = (from.Position + (to.Position - from.Position) * (float)pctElapsed);
            state.Velocity = (from.Velocity + (to.Velocity - from.Velocity) * (float)pctElapsed);
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


        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            if (Polygon != null)
                Polygon.Update();
        }

        public override void FlipY()
        {
            base.FlipY();
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
                histState.Position = this.Position;
                histState.Velocity = this.Velocity;
                histState.Rotation = this.Rotation;
                HistoryBuffer.Enqueue(histState, World.CurrentTime());
            }
        }

        public bool CollidesWithNet(GameObjectPoly obj, out D2DPoint pos, out GameObjectPacket? histState, double frameTime)
        {
            if (obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                histState = null;
                return false;
            }

            var histPos = this.HistoryBuffer.GetHistoricalState(frameTime);

            if (histPos != null)
            {
                // Create a copy of the polygon and translate it to the historical position/rotation.
                var histPoly = new RenderPoly(this, this.Polygon.SourcePoly, World.RenderScale * this.RenderOffset);
                histPoly.Update(histPos.Position, histPos.Rotation, World.RenderScale);

                // Flip plane poly to correct orientation.
                if (this is FighterPlane)
                {
                    var pointingRight = Utilities.IsPointingRight(histPos.Rotation);

                    if (!pointingRight)
                        histPoly.FlipY();
                }

                if (CollisionHelpers.PolygonSweepCollision(obj, histPoly.Poly, histPos.Velocity, World.DT, out pos))
                {
                    histState = histPos;
                    return true;
                }
            }
            else
            {
                var normalCollide = CollidesWith(obj, out D2DPoint pos2);
                pos = pos2;
                histState = null;
                return normalCollide;
            }

            pos = D2DPoint.Zero;
            histState = null;
            return false;
        }

        public bool CollidesWith(GameObjectPoly obj, out D2DPoint pos)
        {
            if (obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                return false;
            }

            return CollisionHelpers.PolygonSweepCollision(obj, this, World.DT, out pos);
        }

        public bool CollidesWith(GameObject obj, out D2DPoint pos)
        {
            if (obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                return false;
            }

            return CollisionHelpers.PolygonSweepCollision(obj, this.Polygon.Poly, this.Velocity, World.DT, out pos);
        }

        public void DrawVeloLines(D2DGraphics gfx, D2DColor color)
        {
            var dt = World.DT;

            var relVelo = this.Velocity * dt;
            var relVeloHalf = relVelo * 0.5f;

            if (this is Bullet)
            {
                var nearest = World.ObjectManager.GetNear(this).Where(o => !o.ID.Equals(this.ID) && o is FighterPlane).OrderBy(o => o.Position.DistanceTo(this.Position)).FirstOrDefault();
                if (nearest != null)
                {
                    relVelo = (this.Velocity - nearest.Velocity) * dt;
                    relVeloHalf = relVelo * 0.5f;
                }
            }

            foreach (var pnt in this.Polygon.Poly)
            {
                var aVelo = Utilities.AngularVelocity(this, pnt, dt) * dt;
                var veloPnt1 = pnt;
                var veloPnt2 = pnt + relVelo;


                gfx.DrawLine(veloPnt1, veloPnt2, color);
            }

            var lagPntStart = this.Position - (this.Velocity * (float)((this.LagAmount / 16.6f) * World.DT));
            var lagPntEnd = this.Position;

            if (this.AgeMs < (this.LagAmount * 2f))
                gfx.DrawLine(lagPntStart, lagPntEnd, color);
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

        public static D2DPoint[] RandomPoly(int nPoints, int radius)
        {
            var rnd = Utilities.Rnd;

            var poly = new D2DPoint[nPoints];
            var dists = new float[nPoints];

            for (int i = 0; i < nPoints; i++)
            {
                dists[i] = rnd.Next(radius / 2, radius);
            }

            var radians = rnd.NextFloat(0.8f, 1.01f);
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
