using PolyPlane.Net;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, ISkipFramesUpdate
    {
        public bool IsNetObject { get; set; } = false;

        public GameID ID { get; set; } = new GameID();

        public int PlayerID
        {
            get { return ID.PlayerID; }
            set { ID = new GameID(value, ID.ObjectID); }
        }

        public GameObject Owner { get; set; }

        public bool IsExpired { get; set; } = false;

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

        public long CurrentFrame { get; set; } = 0;

        public double LastNetUpdate { get; set; } = 0;

        /// <summary>
        /// How many frames must pass between updates.
        /// </summary>
        public long SkipFrames { get; set; } = 1;

        public float Rotation
        {
            get { return _rotation; }

            set
            {
                _rotation = ClampAngle(value);
            }
        }


        protected float _rotation = 0f;

        protected Random _rnd => Helpers.Rnd;

        public float RotationSpeed { get; set; }

        public float Altitude
        {
            get
            {
                return this.Position.Y * -1f;
            }
        }

        private SmoothPos _posSmooth = new SmoothPos(5);
        public InterpolationBuffer<GameObjectPacket> InterpBuffer = null;
        public HistoricalBuffer<GameObjectPacket> HistoryBuffer = new HistoricalBuffer<GameObjectPacket>();

        public GameObject()
        {
            this.ID = new GameID(-1, World.GetNextObjectId());

            HistoryBuffer.Interpolate = (from, to, pctElap) => GetInterpState(from, to, pctElap);

            if (InterpBuffer == null)
                InterpBuffer = new InterpolationBuffer<GameObjectPacket>(new GameObjectPacket(this), World.SERVER_TICK_RATE, (from, to, pctElap) => InterpObject(from, to, pctElap));
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

        public GameObject(D2DPoint pos, float rotation) : this(pos, D2DPoint.Zero, rotation, 0f)
        {
            Position = pos;
            Rotation = rotation;
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


        private GameObjectPacket InterpObject(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            //this.Position = (from.Position + (to.Position - from.Position) * (float)pctElapsed).ToD2DPoint();
            this.Position = _posSmooth.Add((from.Position + (to.Position - from.Position) * (float)pctElapsed).ToD2DPoint());
            this.Velocity = (from.Velocity + (to.Velocity - from.Velocity) * (float)pctElapsed).ToD2DPoint();
            //this.Rotation = Helpers.ClampAngle(from.Rotation + (to.Rotation - from.Rotation) * (float)pctElapsed);
            this.Rotation = to.Rotation;



            return to;
        }

        private GameObjectPacket GetInterpState(GameObjectPacket from, GameObjectPacket to, double pctElapsed)
        {
            var state = new GameObjectPacket();

            state.Position = (from.Position + (to.Position - from.Position) * (float)pctElapsed);
            state.Velocity = (from.Velocity + (to.Velocity - from.Velocity) * (float)pctElapsed);
            state.Rotation = to.Rotation;

            return state;
        }



        public void Update(float dt, D2DSize viewport, float renderScale, bool skipFrames = false)
        {
            CurrentFrame++;

            if (skipFrames)
            {
                if (SkipFrame())
                    return;

                var multiDT = dt * this.SkipFrames;

                this.Update(multiDT, viewport, renderScale);
            }
            else
                this.Update(dt, viewport, renderScale);
        }


        public virtual void Update(float dt, D2DSize viewport, float renderScale)
        {
            var nowMs = World.CurrentTime();


            //var histState = new GameObjectPacket(this);
            //histState.Position = this.Position.ToNetPoint();
            //histState.Velocity = this.Velocity.ToNetPoint();
            //histState.Rotation = this.Rotation;
            //HistoryBuffer.Enqueue(histState, nowMs);


            if (IsNetObject)
            {
                if (World.InterpOn)
                {
                    InterpBuffer.GetInterpolatedState(nowMs);
                    return;

                }

            }

            if (this.IsExpired)
                return;

            Position += Velocity * dt;

            Rotation += RotationSpeed * dt;

            Wrap(viewport);
        }

        public virtual void NetUpdate(float dt, D2DSize viewport, float renderScale, D2DPoint position, D2DPoint velocity, float rotation, double frameTime)
        {
            if (World.InterpOn)
            {
                var newState = new GameObjectPacket(this);
                newState.Position = position.ToNetPoint();
                newState.Velocity = velocity.ToNetPoint();
                newState.Rotation = rotation;

                InterpBuffer.Enqueue(newState, frameTime);
            }
            else
            {
                this.Position = position;
                this.Rotation = rotation;
                this.Velocity = velocity;
            }

            LastNetUpdate = frameTime;
        }


        private bool SkipFrame()
        {
            return this.CurrentFrame % this.SkipFrames != 0;
        }

        private void SetID(long id)
        {
            this.ID = new GameID(this.ID.PlayerID, id);
        }

        public virtual void Wrap(D2DSize viewport)
        {
            //if (this.Position.X < 0f)
            //    this.Position = new D2DPoint(viewport.width, this.Position.Y);

            //if (this.Position.X > viewport.width)
            //    this.Position = new D2DPoint(0, this.Position.Y);

            //if (this.Position.Y < 0f)
            //    this.Position = new D2DPoint(this.Position.X, viewport.height);

            //if (this.Position.Y > viewport.height)
            //    this.Position = new D2DPoint(this.Position.X, 0);
        }

        public virtual void Render(RenderContext ctx)
        {
            if (this.IsExpired)
                return;
        }

        public float FOVToObject(GameObject obj)
        {
            var dir = obj.Position - this.Position;
            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(this.Rotation, angle);

            return diff;
        }

        public bool IsObjInFOV(GameObject obj, float fov)
        {
            var dir = obj.Position - this.Position;

            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(this.Rotation, angle);

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

        protected float AngleDiff(float a, float b) => Helpers.AngleDiff(a, b);
        protected double AngleDiffD(double a, double b) => Helpers.AngleDiffD(a, b);
        protected D2DPoint AngleToVector(float angle) => Helpers.AngleToVectorDegrees(angle);
        protected D2DPoint AngleToVectorD(double angle) => Helpers.AngleToVectorDegreesD(angle);
        protected float ClampAngle(float angle) => Helpers.ClampAngle(angle);
        protected double ClampAngleD(double angle) => Helpers.ClampAngleD(angle);



        protected D2DPoint ApplyTranslation(D2DPoint src, float rotation, D2DPoint translation, float scale = 1f)
        {
            var mat = Matrix3x2.CreateScale(scale);
            mat *= Matrix3x2.CreateRotation(rotation * (float)(Math.PI / 180f), D2DPoint.Zero);
            mat *= Matrix3x2.CreateTranslation(translation);

            return D2DPoint.Transform(src, mat);
        }

        public bool Equals(GameObject? other)
        {
            return this.ID.Equals(other.ID);
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

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            Polygon.Update(this.Position, this.Rotation, renderScale);
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
                // Copy the polygon and translate it to the historical position/rotation.
                var histPoly = new D2DPoint[this.Polygon.SourcePoly.Length];
                Array.Copy(this.Polygon.SourcePoly, histPoly, this.Polygon.SourcePoly.Length);
                Helpers.ApplyTranslation(histPoly, histPoly, histPos.Rotation, histPos.Position.ToD2DPoint(), 1f * 1.5f);

                var poly1 = obj.Polygon.Poly;

                if (dt < 0f)
                    dt = World.SUB_DT;

                var relVelo = histPos.Velocity.ToD2DPoint() - obj.Velocity;
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
                    if (PolyIntersect(pnt1, pnt2, histPoly, out D2DPoint iPosPoly))
                    {
                        pos = iPosPoly;
                        histState = histPos;
                        return true;
                    }
                }

                // One last check with the center point.
                var centerPnt1 = obj.Position - relVeloDTHalf;
                var centerPnt2 = obj.Position + relVeloDTHalf;

                if (PolyIntersect(centerPnt1, centerPnt2, histPoly, out D2DPoint iPosCenter))
                {
                    pos = iPosCenter;
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

            var poly1 = obj.Polygon.Poly;

            if (dt < 0f)
                dt = World.SUB_DT;

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
                    pos = iPosPoly;
                    return true;
                }
            }

            // One last check with the center point.
            var centerPnt1 = obj.Position - relVeloDTHalf;
            var centerPnt2 = obj.Position + relVeloDTHalf;

            if (PolyIntersect(centerPnt1, centerPnt2, this.Polygon.Poly, out D2DPoint iPosCenter))
            {
                pos = iPosCenter;
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
            //var dt = World.DT;
            var dt = World.SUB_DT;

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

        public float GetInertia(RenderPoly poly, float mass)
        {
            var sum1 = 0f;
            var sum2 = 0f;
            var n = poly.SourcePoly.Length;

            for (int i = 0; i < n; i++)
            {
                var v1 = poly.SourcePoly[i];
                var v2 = poly.SourcePoly[(i + 1) % n];
                var a = Helpers.Cross(v2, v1);
                var b = D2DPoint.Dot(v1, v1) + D2DPoint.Dot(v1, v2) + D2DPoint.Dot(v2, v2);

                sum1 += a * b;
                sum2 += a;
            }

            return (mass * sum1) / (6.0f * sum2);
        }


        public static D2DPoint[] RandomPoly(int nPoints, int radius)
        {
            //var rnd = new Random();
            var rnd = Helpers.Rnd;

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
