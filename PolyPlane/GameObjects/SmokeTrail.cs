﻿using PolyPlane.GameObjects.Interfaces;
using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class SmokeTrail : GameObject, INoGameID
    {
        private const int TRAIL_LEN = 400;
        private readonly float TRAIL_DIST = 40f;
        private Queue<D2DPoint> _trailQueue = new Queue<D2DPoint>();
        private GameObject _gameObject;
        private const float ALPHA = 0.3f;
        private D2DColor _trailColor = new D2DColor(0.3f, D2DColor.WhiteSmoke);
        private const float TIMEOUT = 40f;
        private GameTimer _timeOut = new GameTimer(TIMEOUT);
        private Func<GameObject, D2DPoint> _posSelector;
        private D2DPoint _prevPos = D2DPoint.Zero;
        private float _lineWeight = 2f;
        private bool _trailEnabled = true;

        public SmokeTrail(GameObject obj, Func<GameObject, D2DPoint> positionSelector, float lineWeight) : base(obj)
        {
            _gameObject = obj;
            _timeOut.TriggerCallback = () => this.IsExpired = true;
            _posSelector = positionSelector;
            _lineWeight = lineWeight;
        }

        public override void Update(float dt)
        {
            _timeOut.Update(dt);

            if (_gameObject.IsExpired)
            {
                _timeOut.Start();
                _trailColor.a = ALPHA * (1f - Utilities.Factor(_timeOut.Value, TIMEOUT));
                return;
            }

            if (_gameObject is GuidedMissile missile)
            {
                _trailEnabled = missile.FlameOn;
            }
            else
            {
                _trailEnabled = true;
            }

            var dist = this.Position.DistanceTo(_prevPos);

            if (dist >= TRAIL_DIST && _trailEnabled)
            {
                if (_posSelector != null)
                    _trailQueue.Enqueue(_posSelector.Invoke(_gameObject));
                else
                    _trailQueue.Enqueue(_gameObject.Position);

                if (_trailQueue.Count == TRAIL_LEN)
                    _trailQueue.Dequeue();

                _prevPos = this.Position;
            }

            this.Position = _gameObject.Position;
        }

        public void Clear()
        {
            _trailQueue.Clear();
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

                ctx.DrawLine(lastPos, nextPos, color, _lineWeight);

                lastPos = nextPos;
            }

            // Draw connecting line between last trail segment and the source position.
            var endPosition = _gameObject.Position;

            if (_posSelector != null)
                endPosition = _posSelector.Invoke(_gameObject);

            if (_trailQueue.Count > 1 && _trailEnabled)
                ctx.DrawLine(lastPos, endPosition, _trailColor, _lineWeight);

            if (_trailQueue.Count > 0 && _trailQueue.Count < TRAIL_LEN - 1)
                ctx.FillEllipse(new D2DEllipse(_trailQueue.First(), new D2DSize(50f, 50f)), _trailColor);

            if (_gameObject.IsExpired && _trailEnabled)
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
