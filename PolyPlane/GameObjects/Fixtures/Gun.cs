using PolyPlane.GameObjects.Tools;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class Gun : GameObject
    {
        public Action<Bullet> FireBulletCallback;
        public bool MuzzleFlashOn = false;

        private FixturePoint _attachPoint;
        private GunSmoke _smoke;
        private FighterPlane _ownerPlane;
        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _muzzleFlashTimer = new GameTimer(0.16f);

        public Gun(FighterPlane plane, D2DPoint position, Action<Bullet> fireBulletCallback) : base(plane)
        {
            IsNetObject = plane.IsNetObject;
            _ownerPlane = plane;
            FireBulletCallback = fireBulletCallback;

            _attachPoint = new FixturePoint(plane, position);
            _smoke = new GunSmoke(_attachPoint, D2DPoint.Zero, 8f, new D2DColor(0.7f, D2DColor.BurlyWood));

            _burstTimer.StartCallback = FireBullet;
            _burstTimer.TriggerCallback = FireBullet;

            _muzzleFlashTimer.StartCallback = () => { MuzzleFlashOn = true; };
            _muzzleFlashTimer.TriggerCallback = () => { MuzzleFlashOn = false; };
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            _burstTimer.Update(dt);
            _muzzleFlashTimer.Update(dt);
            _attachPoint.Update(dt, renderScale);
            _smoke.Update(dt, renderScale);

            Position = _attachPoint.Position;
            Rotation = _attachPoint.Rotation;

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

        public override void FlipY()
        {
            base.FlipY();
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
            Rotation = _attachPoint.Rotation;
            Position = _attachPoint.Position;

            var bullet = World.ObjectManager.RentBullet();
            bullet.ReInit(_ownerPlane);

            FireBulletCallback(bullet);
            _ownerPlane.BulletsFired++;
            _ownerPlane.NumBullets--;
            _smoke.AddPuff();
            _muzzleFlashTimer.Restart();
        }
    }
}
