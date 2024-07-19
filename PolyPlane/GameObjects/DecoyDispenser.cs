using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.GameObjects
{
    public sealed class DecoyDispenser : GameObject
    {
        public Action<Decoy> DropDecoyCallback;

        private FixturePoint _attachPoint;
        private GameTimer _decoyTimer = new GameTimer(0.25f, true);
        private FighterPlane _plane;

        public DecoyDispenser(FighterPlane plane, D2DPoint position)
        {
            _plane = plane;

            _attachPoint = new FixturePoint(plane, position);

            _decoyTimer.StartCallback = DropDecoy;
            _decoyTimer.TriggerCallback = DropDecoy;
        }

        public override void Update(float dt, float renderScale)
        {
            base.Update(dt, renderScale);

            _decoyTimer.Update(dt);
            _attachPoint.Update(dt, renderScale);

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

            _attachPoint.Update(0f, World.RenderScale);

            var decoy = new Decoy(_plane, _attachPoint.Position);

            _plane.DecoysDropped++;
            _plane.NumDecoys--;

            DropDecoyCallback(decoy);
        }
    }
}
