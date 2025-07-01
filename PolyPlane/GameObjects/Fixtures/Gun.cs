using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Particles;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class Gun : FixturePoint, INoGameID
    {
        public Action<Bullet> FireBulletCallback;
        public bool MuzzleFlashOn = false;
        public double LastBurstTime = 0;

        private GunSmokeEmitter _smoke;
        private FighterPlane _ownerPlane;
        private GameTimer _burstTimer;
        private float _muzzFlashTime = 0f;

        private const float BURST_INTERVAL = 0.25f;
        private const float BURST_INTERVAL_DAMAGED = 0.35f;

        public Gun(FighterPlane plane, D2DPoint position) : base(plane, position)
        {
            IsNetObject = plane.IsNetObject;
            _ownerPlane = plane;

            _smoke = AddAttachment(new GunSmokeEmitter(this, D2DPoint.Zero, new D2DColor(0.7f, D2DColor.BurlyWood)));
            _smoke.Visible = false;

            _burstTimer = AddTimer(BURST_INTERVAL, true);
            _burstTimer.RateLimitStartCallback = true;
            _burstTimer.StartCallback = FireBullet;
            _burstTimer.TriggerCallback = FireBullet;
        }

        public void StartBurst()
        {
            if (_ownerPlane.NumBullets > 0 && _ownerPlane.IsDisabled == false)
            {
                _smoke.Visible = true;
                _burstTimer.Start();
            }
            else
            {
                StopBurst();
            }
        }

        public void StopBurst()
        {
            _smoke.Visible = false;
            _burstTimer.Stop();
            _burstTimer.Reset();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (_ownerPlane.NumBullets <= 0 || _ownerPlane.IsDisabled)
                StopBurst();

            // Decay muzzle flash time.
            _muzzFlashTime = Math.Clamp(_muzzFlashTime - (4f * dt), 0f, 1f);

            if (_muzzFlashTime > 0f)
                MuzzleFlashOn = true;
            else if (_muzzFlashTime <= 0f)
                MuzzleFlashOn = false;
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

            // Add random variation to burst rate when damaged.
            if (_ownerPlane.GunDamaged)
                _burstTimer.Interval = BURST_INTERVAL_DAMAGED + Utilities.Rnd.NextFloat(0f, 0.3f);
            else
                _burstTimer.Interval = BURST_INTERVAL;

            _smoke.Visible = true;
            _smoke.AddPuff();
            _muzzFlashTime = 1f;

            // Don't actually fire a bullet for net planes.
            if (!_ownerPlane.IsNetObject)
            {
                var bullet = new Bullet(_ownerPlane);

                FireBulletCallback(bullet);
                _ownerPlane.BulletsFired++;
                _ownerPlane.NumBullets--;
            }

            LastBurstTime = World.CurrentTimeMs();
        }
    }
}
