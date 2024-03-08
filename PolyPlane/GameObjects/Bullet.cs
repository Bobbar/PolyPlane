using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Bullet : GameObjectPoly
    {
        public static float Speed = 800f;
        public float Lifetime = 10f;
        public Action<D2DPoint> AddExplosionCallback { get; set; }

        private float _age = 0;

        private D2DPoint[] _poly = new D2DPoint[]
        {
            new D2DPoint(7,0),
            new D2DPoint(4,-3),
            new D2DPoint(0,-4),
            new D2DPoint(-8,-4),
            new D2DPoint(-8,4),
            new D2DPoint(0,4),
            new D2DPoint(4,3),
        };

        public Bullet() : base() { }

        public Bullet(D2DPoint pos) : base(pos) { }

        public Bullet(Plane plane) : base(plane.GunPosition, plane.Velocity, plane.Rotation)
        {
            this.Owner = plane;

            this.Polygon = new RenderPoly(_poly);

            var velo = (Helpers.AngleToVectorDegrees(plane.Rotation, Bullet.Speed));
            velo += plane.Velocity;
            this.Velocity = velo;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            _age += dt;

            if (_age >= Lifetime)
                this.IsExpired = true;
        }

        public override void Wrap(D2DSize viewport)
        {
            //base.Wrap(viewport);

            //if (this.Position.X < 0f)
            //    this.IsExpired = true;

            //if (this.Position.X > viewport.width)
            //    this.IsExpired = true;

            //if (this.Position.Y < 0f)
            //    this.IsExpired = true;

            //if (this.Position.Y > viewport.height)
            //    this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            ctx.Gfx.AntiAliasingOff();
            //ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(5, 5)), D2DColor.Goldenrod);
            ctx.DrawPolygon(this.Polygon.Poly, D2DColor.Black, 1f, D2DDashStyle.Solid, D2DColor.Yellow);

            ctx.Gfx.AntiAliasingOn();
        }
    }

    public class TargetedBullet : Bullet
    {
        public GameObject Target;
        //public Action<D2DPoint> AddExplosionCallback { get; set; }

        public TargetedBullet(D2DPoint pos, GameObject target, float speed) : base(pos)
        {
            Bullet.Speed = speed;
            this.Target = target;

            AimAtTarget(this.Target);
        }

        public TargetedBullet(D2DPoint pos, GameObject target) : base(pos)
        {
            this.Target = target;

            AimAtTarget(this.Target);
        }

        public override void Render(RenderContext ctx)
        {
            ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(5, 5)), D2DColor.Yellow);
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

            const float proxyDetDist = 50f;

            if (D2DPoint.Distance(this.Position, this.Target.Position) < proxyDetDist && !this.Target.IsExpired && !this.IsExpired)
            {
                AddExplosionCallback(this.Position);
                this.IsExpired = true;
            }

        }

        private void AimAtTarget(GameObject target)
        {
            var delta = target.Position - this.Position;
            var vr = target.Velocity - this.Velocity;
            var dist = D2DPoint.Distance(target.Position, this.Position);
            var deltaTime = AimAhead(delta, vr, Bullet.Speed);
            var toa = dist / Bullet.Speed;
            var impact = RefineImpact(target.Position, target.Velocity, target.RotationSpeed, toa, 0.01f);

            D2DPoint aimPoint = D2DPoint.Zero;

            if (target.RotationSpeed == 0f)
            {
                if (deltaTime > 0)
                    aimPoint = target.Position + target.Velocity * deltaTime;
            }
            else
            {
                aimPoint = impact;
            }

            var angle = D2DPoint.Normalize(aimPoint - this.Position);
            this.Velocity = angle * Bullet.Speed;
        }

        private float AimAhead(D2DPoint delta, D2DPoint vr, float bulletSpd)
        {
            var a = D2DPoint.Dot(vr, vr) - bulletSpd * bulletSpd;
            var b = 2f * D2DPoint.Dot(vr, delta);
            var c = D2DPoint.Dot(delta, delta);

            var det = b * b - 4f * a * c;

            if (det > 0f)
                return 2f * c / ((float)Math.Sqrt(det) - b);
            else
                return -1f;
        }

        private D2DPoint RefineImpact(D2DPoint targetPos, D2DPoint targetVelo, float targAngleDelta, float framesToImpact, float dt)
        {
            D2DPoint predicted = targetPos;

            if (framesToImpact >= 1 && framesToImpact < 6000)
            {
                var targLoc = targetPos;
                var angle = targetVelo.AngleD();

                float step = 0f;

                while (step < framesToImpact)
                {
                    var avec = AngleToVectorD(angle) * targetVelo.Length();
                    targLoc += avec * dt;
                    angle += -targAngleDelta * dt;
                    angle = ClampAngle((float)angle);
                    step += dt;
                }

                predicted = targLoc;
            }


            return predicted;
        }

    }
}
