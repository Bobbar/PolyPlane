using System.Diagnostics;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public abstract class GameObject : IEquatable<GameObject>, ISkipFramesUpdate
    {
        public long ID => _id;
        private long _id = 0;
        public GameObject Owner { get; set; }

        public bool IsExpired { get; set; } = false;

        public D2DPoint Position { get; set; }

        public D2DPoint Velocity { get; set; }

        public long CurrentFrame { get; set; } = 0;

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

        //protected Random _rnd = new Random();
        protected Random _rnd => Helpers.Rnd;

        public float RotationSpeed { get; set; }

        public float Altitude
        {
            get
            {
                return this.Position.Y * -1f;
            }
        }

        public GameObject()
        {
            _id = World.GetNextId();
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

        public void Update(float dt, D2DSize viewport, float renderScale, bool skipFrames = false)
        {
            CurrentFrame++;

            if (SkipFrame())
                return;

            var multiDT = dt * this.SkipFrames;

            this.Update(multiDT, viewport, renderScale);
        }


        public virtual void Update(float dt, D2DSize viewport, float renderScale)
        {
            if (this.IsExpired)
                return; 

            Position += Velocity * dt;

            Rotation += RotationSpeed * dt;

            Wrap(viewport);

         
        }

        private bool SkipFrame()
        {
            return this.CurrentFrame % this.SkipFrames != 0;
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
            var idx1 = new D2DPoint((int)Math.Floor(this.Position.X) >> World.VIRT_GRID_SIDE, (int)Math.Floor(this.Position.Y) >> World.VIRT_GRID_SIDE);
            var idx2 = new D2DPoint((int)Math.Floor(obj.Position.X) >> World.VIRT_GRID_SIDE, (int)Math.Floor(obj.Position.Y) >> World.VIRT_GRID_SIDE);

            var diff = idx1.AbsDiff(idx2);

            if (diff.X > 1 || diff.Y > 1)
                return false;

            return true;
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
            return this.ID == other.ID;
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

        //public virtual bool Contains(GameObjectPoly obj)
        //{
        //    if (!this.IsObjNear(obj))
        //        return false;

        //    var poly1 = obj.Polygon.Poly;

        //    // First do velocity compensation collisions.
        //    // Extend line segments from the current position to the next position and check for intersections.
        //    for (int i = 0; i < poly1.Length; i++)
        //    {
        //        var pnt1 = poly1[i];
        //        var pnt2 = pnt1 + (obj.Velocity * World.SUB_DT);
        //        if (PolyIntersect(pnt1, pnt2, this.Polygon.Poly))
        //            return true;
        //    }

        //    // Same as above but for the central point.
        //    if (PolyIntersect(obj.Position, obj.Position + (obj.Velocity * World.SUB_DT), this.Polygon.Poly))
        //        return true;

        //    // Plain old point-in-poly collisions.
        //    // Check for center point first.
        //    if (Contains(obj.Position))
        //        return true;

        //    // Check all other poly points.
        //    foreach (var pnt in obj.Polygon.Poly)
        //    {
        //        if (PointInPoly(pnt, this.Polygon.Poly))
        //            return true;
        //    }

        //    // Reverse of above. Just in case...
        //    foreach (var pnt in Polygon.Poly)
        //    {
        //        if (PointInPoly(pnt, obj.Polygon.Poly))
        //            return true;
        //    }

        //    return false;
        //}

        public virtual bool Contains(GameObjectPoly obj,  out D2DPoint pos, float dt = -1f)
        {
            if (!this.IsObjNear(obj) || obj.Owner == this)
            {
                pos = D2DPoint.Zero;
                return false;
            }

            var poly1 = obj.Polygon.Poly;

            if (dt < 0f)
                dt = World.SUB_DT;

            // First do velocity compensation collisions.
            // Extend line segments from the current position to the next position and check for intersections.
            for (int i = 0; i < poly1.Length; i++)
            {
                var pnt1 = poly1[i] - ((obj.Velocity * dt) * 0.5f);
                var pnt2 = poly1[i] + ((obj.Velocity * dt) * 0.5f);
                if (PolyIntersect2(pnt1, pnt2, this.Polygon.Poly, out D2DPoint iPos1))
                {
                    pos = iPos1;
                    return true;
                }
            }

            // Same as above but for the central point.
            if (PolyIntersect2(obj.Position - ((obj.Velocity * dt) * 0.5f), obj.Position + ((obj.Velocity * dt) * 0.5f), this.Polygon.Poly, out D2DPoint iPos2))
            {
                pos = iPos2;
                return true;
            }

            // Plain old point-in-poly collisions.
            // Check for center point first.
            if (Contains(obj.Position))
            {
                pos = obj.Position;
                return true;

            }

            // Check all other poly points.
            foreach (var pnt in obj.Polygon.Poly)
            {
                if (PointInPoly(pnt, this.Polygon.Poly))
                {
                    pos = pnt;
                    return true;
                }
            }

            // Reverse of above. Just in case...
            foreach (var pnt in Polygon.Poly)
            {
                if (PointInPoly(pnt, obj.Polygon.Poly))
                {
                    pos = pnt;
                    return true;
                }
            }


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

        private bool LineSegementsIntersect(D2DPoint p, D2DPoint p2, D2DPoint q, D2DPoint q2, out D2DPoint intersection, bool considerCollinearOverlapAsIntersect = false)
        {
            intersection = new D2DPoint();
            const float eps = 1e-10f;
            var r = p2 - p;
            var s = q2 - q;
            var rxs = r.Cross(s);
            var qpxr = (q - p).Cross(r);

            // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            if (rxs < eps && qpxr < eps)
            {
                // 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
                // then the two lines are overlapping,
                if (considerCollinearOverlapAsIntersect)
                    if ((0f <= ((q - p) * r).Length() && ((q - p) * r).Length() <= (r * r).Length()) || (0f <= ((p - q) * s).Length() && ((p - q) * s).Length() <= (s * s).Length()))
                        return true;

                // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
                // then the two lines are collinear but disjoint.
                // No need to implement this expression, as it follows from the expression above.
                return false;
            }

            // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
            if (rxs < eps && !(qpxr < eps))
                return false;

            // t = (q - p) x s / (r x s)
            var t = (q - p).Cross(s) / rxs;

            // u = (q - p) x r / (r x s)

            var u = (q - p).Cross(r) / rxs;

            // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            // the two line segments meet at the point p + t r = q + u s.
            if (!(rxs < eps) && (0 <= t && t <= 1) && (0 <= u && u <= 1))
            {
                // We can calculate the intersection point using either t or u.
                intersection = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }
      
        private bool PolyIntersect2(D2DPoint a, D2DPoint b, D2DPoint[] poly, out D2DPoint pos)
        {
            // Check the segment against every segment in the polygon.
            for (int i = 0; i < poly.Length - 1; i++)
            {
                var pnt1 = poly[i];
                var pnt2 = poly[i + 1];

                if (LineSegementsIntersect(a, b, pnt1, pnt2, out D2DPoint iPos))
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
