using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Particles;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class Gun : FixturePoint, INoGameID
    {
        public Action<Bullet> FireBulletCallback;
        public bool MuzzleFlashOn = false;

        private GunSmokeEmitter _smoke;
        private FighterPlane _ownerPlane;
        private GameTimer _burstTimer;
        private GameTimer _muzzleFlashTimer;

        public Gun(FighterPlane plane, D2DPoint position) : base(plane, position)
        {
            IsNetObject = plane.IsNetObject;
            _ownerPlane = plane;

            _smoke = AddAttachment(new GunSmokeEmitter(this, D2DPoint.Zero, new D2DColor(0.7f, D2DColor.BurlyWood)));

            _burstTimer = AddTimer(0.25f, true);
            _muzzleFlashTimer = AddTimer(0.16f);

            _burstTimer.StartCallback = FireBullet;
            _burstTimer.TriggerCallback = FireBullet;

            _muzzleFlashTimer.StartCallback = () => { MuzzleFlashOn = true; };
            _muzzleFlashTimer.TriggerCallback = () => { MuzzleFlashOn = false; };
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (_ownerPlane.FiringBurst && _ownerPlane.NumBullets > 0 && _ownerPlane.IsDisabled == false)
            {
                _smoke.Visible = true;
                _burstTimer.Start();
            }
            else
            {
                _smoke.Visible = false;
                _burstTimer.Stop();
                _burstTimer.Reset();
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
         
            _smoke.AddPuff();
            _muzzleFlashTimer.Restart();

            // Don't actually fire a bullet for net planes.
            if (!_ownerPlane.IsNetObject)
            {
                var bullet = new Bullet(_ownerPlane);
                FireBulletCallback(bullet);
                _ownerPlane.BulletsFired++;
                _ownerPlane.NumBullets--;
            }
        }
    }
}
