using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class Gun : GameObject
    {
        public Action<Bullet> FireBulletCallback;

        private FixturePoint _attachPoint { get; set; }
        private GunSmoke _smoke;
        private FighterPlane _ownerPlane;
        private GameTimer _burstTimer = new GameTimer(0.25f, true);

        public Gun(FighterPlane plane, D2DPoint position, Action<Bullet> fireBulletCallback) : base(plane)
        {
            this.IsNetObject = plane.IsNetObject;
            _ownerPlane = plane;
            FireBulletCallback = fireBulletCallback;

            _attachPoint = new FixturePoint(plane, position);
            _smoke = new GunSmoke(_attachPoint, D2DPoint.Zero, 8f, new D2DColor(0.7f, D2DColor.BurlyWood));

            _burstTimer.StartCallback = FireBullet;
            _burstTimer.TriggerCallback = FireBullet;
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            _burstTimer.Update(dt);
            _attachPoint.Update(dt, renderScale);
            _smoke.Update(dt, renderScale);

            this.Position = _attachPoint.Position;
            this.Rotation = _attachPoint.Rotation;

            if (_ownerPlane.FiringBurst && _ownerPlane.NumBullets > 0 && _ownerPlane.IsDisabled == false)
            {
                _burstTimer.Start();
                _smoke.Visible = true;
            }
            else
            {
                _burstTimer.Stop();
                _burstTimer.Reset();
                _smoke.Visible = false;
            }
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            _smoke.Render(ctx);
        }

        public void FlipY()
        {
            _attachPoint.FlipY();
        }

        private void FireBullet()
        {
            if (_ownerPlane.IsDisabled)
                return;

            if (_ownerPlane.NumBullets <= 0)
                return;

            if (_ownerPlane.IsNetObject)
                return;

            // Make sure fixture point is synced at the time of firing.
            _attachPoint.Update(0f, World.RenderScale);
            this.Rotation = _attachPoint.Rotation;
            this.Position = _attachPoint.Position;

            var bullet = World.ObjectManager.RentBullet();
            bullet.ReInit(_ownerPlane);

            FireBulletCallback(bullet);
            _ownerPlane.BulletsFired++;
            _ownerPlane.NumBullets--;
        }
    }
}
