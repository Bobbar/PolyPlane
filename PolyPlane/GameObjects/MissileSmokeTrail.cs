using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class MissileSmokeTrail : GameObject, INoGameID
    {
        private const int TRAIL_LEN = 400;
        private readonly float TRAIL_DIST = 40f;
        private const float TIMEOUT = 80f;
        private const float ALPHA = 0.3f;
        private const float LINE_WEIGHT = 2f;
        private const float LIGHT_INTENSITY = 0.5f;

        private List<D2DPoint> _trailList = new List<D2DPoint>();
        private GuidedMissile _parentMissile;
        private D2DColor _trailColor = new D2DColor(0.3f, D2DColor.WhiteSmoke);
        private GameTimer _timeOut = new GameTimer(TIMEOUT);
        private D2DPoint _prevPos = D2DPoint.Zero;
        private bool _trailEnabled = false;
        private MinMax _bounds = new MinMax();

        public MissileSmokeTrail(GuidedMissile missile) : base(missile)
        {
            _parentMissile = missile;
            _timeOut.TriggerCallback = () => this.IsExpired = true;
        }

        public override void DoUpdate(float dt)
        {
            _timeOut.Update(dt);

            // Start to fade out if missile is expired or the engine has burned out.
            if (!_timeOut.IsRunning && (_parentMissile.IsExpired || (_parentMissile.IsActivated && !_parentMissile.FlameOn)))
                _timeOut.Start();

            _trailColor.a = ALPHA * (1f - Utilities.Factor(_timeOut.Value, TIMEOUT));
            _trailEnabled = _parentMissile.FlameOn;

            if (_trailEnabled)
            {
                var dist = this.Position.DistanceTo(_prevPos);
                if (dist >= TRAIL_DIST)
                {
                    var position = _parentMissile.CenterOfThrust;

                    _bounds.Update(position);
                    _trailList.Add(position);

                    if (_trailList.Count == TRAIL_LEN)
                        _trailList.RemoveAt(0);

                    _prevPos = this.Position;
                }
            }

            this.Position = _parentMissile.CenterOfThrust;
        }

        public override void Render(RenderContext ctx)
        {
            if (_trailList.Count == 0)
                return;

            if (!ctx.Viewport.Contains(_bounds.GetBounds()))
                return;

            base.Render(ctx);

            var lastPos = _trailList.First();
            for (int i = 0; i < _trailList.Count; i++)
            {
                var trail = _trailList[i];
                var nextPos = trail;

                var color = _trailColor;

                ctx.DrawLineWithLighting(lastPos, nextPos, color, LIGHT_INTENSITY, LINE_WEIGHT);

                lastPos = nextPos;
            }

            // Draw connecting line between last trail segment and the source position.
            var endPosition = _parentMissile.CenterOfThrust;

            if (_trailList.Count > 1 && _trailEnabled)
                ctx.DrawLineWithLighting(lastPos, endPosition, _trailColor, LIGHT_INTENSITY, LINE_WEIGHT);

            if (_trailList.Count > 0 && _trailList.Count < TRAIL_LEN - 1)
                ctx.FillEllipseWithLighting(new D2DEllipse(_trailList.First(), new D2DSize(50f, 50f)), _trailColor, LIGHT_INTENSITY);

            if (_parentMissile.IsExpired && _trailEnabled)
                ctx.FillEllipseWithLighting(new D2DEllipse(endPosition, new D2DSize(50f, 50f)), _trailColor, LIGHT_INTENSITY);
        }

        public override bool ContainedBy(D2DRect rect)
        {
            if (_trailList.Count == 0)
                return false;
            else
            {
                for (int i = 0; i < _trailList.Count; i++)
                {
                    var trail = _trailList[i];
                    if (rect.Contains(trail))
                        return true;
                }
            }

            return false;

        }
    }
}
