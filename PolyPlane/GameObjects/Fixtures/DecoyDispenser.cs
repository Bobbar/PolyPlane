using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class DecoyDispenser : GameObject, INoGameID
    {
        public Action<Decoy> DropDecoyCallback;

        private FixturePoint _attachPoint;
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private FighterPlane _plane;

        public DecoyDispenser(FighterPlane plane, D2DPoint position)
        {
            _plane = plane;

            _attachPoint = new FixturePoint(plane, position);
            this.Position = _attachPoint.Position;

            _decoyTimer.StartCallback = DropDecoy;
            _decoyTimer.TriggerCallback = DropDecoy;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            _decoyTimer.Update(dt);
            _attachPoint.Update(dt);

            if (_plane.DroppingDecoy && !_decoyTimer.IsRunning)
            {
                _decoyTimer.Restart(true);
            }
            else if (!_plane.DroppingDecoy)
            {
                _decoyTimer.Stop();
            }

            this.Position = _attachPoint.Position;
        }

        private void DropDecoy()
        {
            if (_plane.NumDecoys <= 0)
                return;

            if (_plane.IsDisabled)
                return;

            _attachPoint.Update(0f);

            var decoy = new Decoy(_plane, _attachPoint.Position);

            _plane.DecoysDropped++;
            _plane.NumDecoys--;

            DropDecoyCallback(decoy);
        }
    }
}
