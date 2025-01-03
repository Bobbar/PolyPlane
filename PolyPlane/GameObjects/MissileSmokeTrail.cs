using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class MissileSmokeTrail : GameObject, INoGameID
    {
        private const int TRAIL_LEN = 400;
        private readonly float TRAIL_DIST = 40f;
        private const float TIMEOUT = 80f;
        private const float ALPHA = 0.3f;
        private const float LINE_WEIGHT = 2f;

        private Queue<D2DPoint> _trailQueue = new Queue<D2DPoint>();
        private GuidedMissile _parentMissile;
        private D2DColor _trailColor = new D2DColor(0.3f, D2DColor.WhiteSmoke);
        private GameTimer _timeOut = new GameTimer(TIMEOUT);
        private D2DPoint _prevPos = D2DPoint.Zero;
        private bool _trailEnabled = false;

        public MissileSmokeTrail(GuidedMissile missile) : base(missile)
        {
            _parentMissile = missile;
            _timeOut.TriggerCallback = () => this.IsExpired = true;
        }

        public override void Update(float dt)
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
                    _trailQueue.Enqueue(_parentMissile.Position);

                    if (_trailQueue.Count == TRAIL_LEN)
                        _trailQueue.Dequeue();

                    _prevPos = this.Position;
                }
            }

            this.Position = _parentMissile.Position;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            if (_trailQueue.Count == 0)
                return;

            var lastPos = _trailQueue.First();
            foreach (var trail in _trailQueue)
            {
                var nextPos = trail;

                var color = _trailColor;

                ctx.DrawLine(lastPos, nextPos, color, LINE_WEIGHT);

                lastPos = nextPos;
            }

            // Draw connecting line between last trail segment and the source position.
            var endPosition = _parentMissile.Position;

            if (_trailQueue.Count > 1 && _trailEnabled)
                ctx.DrawLine(lastPos, endPosition, _trailColor, LINE_WEIGHT);

            if (_trailQueue.Count > 0 && _trailQueue.Count < TRAIL_LEN - 1)
                ctx.FillEllipse(new D2DEllipse(_trailQueue.First(), new D2DSize(50f, 50f)), _trailColor);

            if (_parentMissile.IsExpired && _trailEnabled)
                ctx.FillEllipse(new D2DEllipse(endPosition, new D2DSize(50f, 50f)), _trailColor);
        }

        public override bool ContainedBy(D2DRect rect)
        {
            if (_trailQueue.Count == 0)
                return false;
            else
            {
                foreach (var trail in _trailQueue)
                {
                    if (rect.Contains(trail))
                        return true;
                }
            }

            return false;

        }
    }
}
