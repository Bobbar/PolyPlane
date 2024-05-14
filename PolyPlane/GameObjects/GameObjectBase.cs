﻿using PolyPlane.Helpers;
using PolyPlane.Net;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, IDisposable
    {
        public GameID ID { get; set; } = new GameID();

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

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

        public float RotationSpeed { get; set; } = 0f;

        public float RenderOffset = 1f;
        public bool Visible = true;
        public bool IsNetObject = false;
        public double LagAmount = 0;

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
        protected HistoricalBuffer<GameObjectPacket> HistoryBuffer = new HistoricalBuffer<GameObjectPacket>();
        protected SmoothPos _posSmooth = new SmoothPos(5);

        protected float _rotation = 0f;
        protected float _verticalSpeed = 0f;
        protected float _prevAlt = 0f;

        public GameObject()
        {
            this.ID = new GameID(-1, World.GetNextObjectId());

            if (World.IsNetGame && (this is FighterPlane || this is GuidedMissile))
            {
                HistoryBuffer.Interpolate = (from, to, pctElap) => GetInterpState(from, to, pctElap);

                if (InterpBuffer == null)
                    InterpBuffer = new InterpolationBuffer<GameObjectPacket>(new GameObjectPacket(this), World.SERVER_TICK_RATE, (from, to, pctElap) => InterpObject(from, to, pctElap));
            }
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
            CurrentFrame++;

            if (World.IsNetGame && IsNetObject && InterpBuffer != null)
            {
                if (World.InterpOn)
                {
                    var nowMs = World.CurrentTime();
                    InterpBuffer.GetInterpolatedState(nowMs);
                    return;
                }
            }

            if (this.IsExpired)
                return;

            Position += Velocity * dt;

            Rotation += RotationSpeed * dt;

            var altDiff = this.Altitude - _prevAlt;
            _verticalSpeed = altDiff;
            _prevAlt = this.Altitude;

            Wrap();
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
        }

        public virtual void Wrap()
        {
            // Clamp all objects to ground level.
            if (this.Altitude <= 0f)
            {
                this.Position = new D2DPoint(this.Position.X, 0f);
                this.Velocity = new D2DPoint(this.Velocity.X, 0f);
            }
        }

        public virtual void Render(RenderContext ctx)
        {
            if (this.IsExpired || !this.Visible)
                return;
        }

        public float FOVToObject(GameObject obj)
        {
            var dir = obj.Position - this.Position;
            var angle = dir.Angle(true);
            var diff = Utilities.AngleDiff(this.Rotation, angle);

            return diff;
        }

        public bool IsObjInFOV(GameObject obj, float fov)
        {
            var dir = obj.Position - this.Position;

            var angle = dir.Angle(true);
            var diff = Utilities.AngleDiff(this.Rotation, angle);

            return diff <= (fov * 0.5f);
        }

        public bool IsObjNear(GameObject obj)
        {
            var dist = this.Position.DistanceSquaredTo(obj.Position);

            return dist <= World.MIN_COLLISION_DIST;
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
            return this.ID.Equals(other.ID);
        }

        public virtual void Dispose() { }

        private GameObjectPacket InterpObject(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            this.Position = _posSmooth.Add((from.Position + (to.Position - from.Position) * (float)pctElapsed));
            this.Velocity = (from.Velocity + (to.Velocity - from.Velocity) * (float)pctElapsed);
            this.Rotation = Utilities.LerpAngle(from.Rotation, to.Rotation, (float)pctElapsed);

            return to;
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
        public RenderPoly Polygon = new RenderPoly();

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

        public GameObjectPoly(D2DPoint pos, D2DPoint velo, D2DPoint[] polygon) : base(pos, velo)
        {
            Polygon = new RenderPoly(polygon);
        }

        public GameObjectPoly(D2DPoint[] polygon)
        {
            Polygon = new RenderPoly(polygon);
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            Polygon.Update(this.Position, this.Rotation, renderScale);

            if (World.IsNetGame && World.IsServer && (this is FighterPlane || this is GuidedMissile))
            {
                var histState = new GameObjectPacket(this);
                histState.Position = this.Position;
                histState.Velocity = this.Velocity;
                histState.Rotation = this.Rotation;
                HistoryBuffer.Enqueue(histState, World.CurrentTime());
            }
        }

        public bool CollidesWithNet(GameObjectPoly obj, out D2DPoint pos, out GameObjectPacket? histState, double frameTime, float dt = -1f)
        {
            if (!this.IsObjNear(obj) || obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                histState = null;
                return false;
            }

            var histPos = this.HistoryBuffer.GetHistoricalState(frameTime);

            if (histPos != null)
            {
                var hits = new List<D2DPoint>();

                // Create a copy of the polygon and translate it to the historical position/rotation.
                var histPoly = new RenderPoly(this.Polygon.SourcePoly, World.RenderScale * this.RenderOffset);
                histPoly.Update(histPos.Position, histPos.Rotation, World.RenderScale);

                // Flip plane poly to correct orientation.
                if (this is FighterPlane)
                {
                    var pointingRight = Utilities.IsPointingRight(histPos.Rotation);

                    if (!pointingRight)
                        histPoly.FlipY();
                }

                var poly1 = obj.Polygon.Poly;

                if (dt < 0f)
                    dt = World.DT;

                var relVelo = histPos.Velocity - obj.Velocity;
                var relVeloDTHalf = (relVelo * dt) * 0.5f;

                // Velocity compensation collisions:
                // For each point in the polygon compute two points, one for backwards 1/2 timestep and one forwards 1/2 timestep
                // and connect a line between the two.
                // Then check if any of these lines intersect any segments of the test polygon.
                // I guess this is sorta similar to what Continuous Collision Detection does.
                for (int i = 0; i < poly1.Length; i++)
                {
                    var pnt1 = poly1[i] - relVeloDTHalf;
                    var pnt2 = poly1[i] + relVeloDTHalf;

                    // Check for an intersection and get the exact location of the impact.
                    if (PolyIntersect(pnt1, pnt2, histPoly.Poly, out D2DPoint iPosPoly))
                    {
                        hits.Add(iPosPoly);
                    }
                }

                // One last check with the center point.
                var centerPnt1 = obj.Position - relVeloDTHalf;
                var centerPnt2 = obj.Position + relVeloDTHalf;

                if (PolyIntersect(centerPnt1, centerPnt2, histPoly.Poly, out D2DPoint iPosCenter))
                {
                    hits.Add(iPosCenter);
                }

                // If we have multiple hits, find the closest to the impactor.
                if (hits.Count > 0)
                {
                    var closest = hits.OrderBy(p => p.DistanceTo(obj.Position));
                    pos = closest.First();
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

        public bool CollidesWith(GameObjectPoly obj, out D2DPoint pos, float dt = -1f)
        {
            if (!this.IsObjNear(obj) || obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                return false;
            }

            var hits = new List<D2DPoint>();
            var poly1 = obj.Polygon.Poly;

            if (dt < 0f)
                dt = World.DT;

            var relVelo = this.Velocity - obj.Velocity;
            var relVeloDTHalf = (relVelo * dt) * 0.5f;

            // Velocity compensation collisions:
            // For each point in the polygon compute two points, one for backwards 1/2 timestep and one forwards 1/2 timestep
            // and connect a line between the two.
            // Then check if any of these lines intersect any segments of the test polygon.
            // I guess this is sorta similar to what Continuous Collision Detection does.
            for (int i = 0; i < poly1.Length; i++)
            {
                var pnt1 = poly1[i] - relVeloDTHalf;
                var pnt2 = poly1[i] + relVeloDTHalf;

                // Check for an intersection and get the exact location of the impact.
                if (PolyIntersect(pnt1, pnt2, this.Polygon.Poly, out D2DPoint iPosPoly))
                {
                    hits.Add(iPosPoly);
                }
            }

            // One last check with the center point.
            var centerPnt1 = obj.Position - relVeloDTHalf;
            var centerPnt2 = obj.Position + relVeloDTHalf;

            if (PolyIntersect(centerPnt1, centerPnt2, this.Polygon.Poly, out D2DPoint iPosCenter))
            {
                hits.Add(iPosCenter);
            }

            // If we have multiple hits, find the closest to the impactor.
            if (hits.Count > 0)
            {
                var closest = hits.OrderBy(p => p.DistanceTo(obj.Position));
                pos = closest.First();
                return true;
            }


            // ** Other/old collision strategies **


            //// Same as above but for the central point.
            //if (PolyIntersect2(obj.Position - ((obj.Velocity * dt) * 0.5f), obj.Position + ((obj.Velocity * dt) * 0.5f), this.Polygon.Poly, out D2DPoint iPos2))
            //{
            //    pos = iPos2;
            //    return true;
            //}

            //// Plain old point-in-poly collisions.
            //// Check for center point first.
            //if (Contains(obj.Position))
            //{
            //    pos = obj.Position;
            //    return true;

            //}

            //// Check all other poly points.
            //foreach (var pnt in obj.Polygon.Poly)
            //{
            //    if (PointInPoly(pnt, this.Polygon.Poly))
            //    {
            //        pos = pnt;
            //        return true;
            //    }
            //}

            //// Reverse of above. Just in case...
            //foreach (var pnt in Polygon.Poly)
            //{
            //    if (PointInPoly(pnt, obj.Polygon.Poly))
            //    {
            //        pos = pnt;
            //        return true;
            //    }
            //}


            pos = D2DPoint.Zero;
            return false;
        }

        public void DrawVeloLines(D2DGraphics gfx)
        {
            var dt = World.DT;

            foreach (var pnt in this.Polygon.Poly)
            {

                var veloPnt1 = pnt - ((this.Velocity * dt) * 0.5f);
                var veloPnt = pnt + ((this.Velocity * dt) * 0.5f);
                gfx.DrawLine(veloPnt1, veloPnt, D2DColor.Red);

            }
        }

        private bool PolyIntersect(D2DPoint a, D2DPoint b, D2DPoint[] poly, out D2DPoint pos)
        {
            // Check the segment against every segment in the polygon.
            for (int i = 0; i < poly.Length - 1; i++)
            {
                var pnt1 = poly[i];
                var pnt2 = poly[i + 1];

                if (CollisionHelpers.IsIntersecting(a, b, pnt1, pnt2, out D2DPoint iPos))
                {
                    pos = iPos;
                    return true;
                }
            }

            pos = D2DPoint.Zero;
            return false;
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
