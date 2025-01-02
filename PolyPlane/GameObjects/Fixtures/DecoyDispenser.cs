using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;

namespace PolyPlane.GameObjects.Fixtures
{
    public sealed class DecoyDispenser : FixturePoint, INoGameID
    {
        public Action<Decoy> DropDecoyCallback;

        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private FighterPlane _plane;

        public DecoyDispenser(FighterPlane plane, D2DPoint position) : base(plane, position)
        {
            _plane = plane;

            _decoyTimer.StartCallback = DropDecoy;
            _decoyTimer.TriggerCallback = DropDecoy;
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            _decoyTimer.Update(dt);

            if (_plane.DroppingDecoy && !_decoyTimer.IsRunning)
            {
                _decoyTimer.Restart(true);
            }
            else if (!_plane.DroppingDecoy)
            {
                _decoyTimer.Stop();
            }
        }

        private void DropDecoy()
        {
            if (_plane.NumDecoys <= 0)
                return;

            if (_plane.IsDisabled)
                return;

            var decoy = new Decoy(_plane, this.Position);

            _plane.DecoysDropped++;
            _plane.NumDecoys--;

            DropDecoyCallback(decoy);
        }
    }
}
