using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class Gun : FixturePoint, INoGameID
    {
        public Action<Bullet> FireBulletCallback;
        public bool MuzzleFlashOn = false;

        private GunSmoke _smoke;
        private FighterPlane _ownerPlane;
        private GameTimer _burstTimer = new GameTimer(0.25f, true);
        private GameTimer _muzzleFlashTimer = new GameTimer(0.16f);

        public Gun(FighterPlane plane, D2DPoint position, Action<Bullet> fireBulletCallback) : base(plane, position)
        {
            IsNetObject = plane.IsNetObject;
            _ownerPlane = plane;
            FireBulletCallback = fireBulletCallback;

            _smoke = new GunSmoke(this, D2DPoint.Zero, 8f, new D2DColor(0.7f, D2DColor.BurlyWood));

            _burstTimer.StartCallback = FireBullet;
            _burstTimer.TriggerCallback = FireBullet;

            _muzzleFlashTimer.StartCallback = () => { MuzzleFlashOn = true; };
            _muzzleFlashTimer.TriggerCallback = () => { MuzzleFlashOn = false; };
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            _burstTimer.Update(dt);
            _muzzleFlashTimer.Update(dt);
            _smoke.Update(dt);

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

        private void FireBullet()
        {
            if (_ownerPlane.IsDisabled)
                return;

            if (_ownerPlane.NumBullets <= 0)
                return;

            if (_ownerPlane.IsNetObject)
                return;

            var bullet = new Bullet(_ownerPlane);

            FireBulletCallback(bullet);
            _ownerPlane.BulletsFired++;
            _ownerPlane.NumBullets--;
            _smoke.AddPuff();
            _muzzleFlashTimer.Restart();
        }
    }
}
