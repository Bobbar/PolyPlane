using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class SmokeTrail : GameObject
    {
        private const int TRAIL_LEN = 400;
        private readonly int TRAIL_STEP = 10;
        private readonly float TRAIL_DIST = 40f;
        private long _trailFrame = 0;
        private Queue<D2DPoint> _trailQueue = new Queue<D2DPoint>();
        private GameObject _gameObject;
        private const float ALPHA = 0.3f;
        private D2DColor _trailColor = new D2DColor(0.3f, D2DColor.WhiteSmoke);
        private const float TIMEOUT = 40f;
        private GameTimer _timeOut = new GameTimer(TIMEOUT);
        private Func<GameObject, D2DPoint> _posSelector;
        private D2DPoint _prevPos = D2DPoint.Zero;

        public SmokeTrail(GameObject obj)
        {
            _gameObject = obj;
            _timeOut.TriggerCallback = () => this.IsExpired = true;
        }

        public SmokeTrail(GameObject obj, Func<GameObject, D2DPoint> positionSelector)
        {
            _gameObject = obj;
            _timeOut.TriggerCallback = () => this.IsExpired = true;
            _posSelector = positionSelector;
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            _timeOut.Update(dt);

            if (_gameObject.IsExpired)
            {
                _timeOut.Start();
                _trailColor.a = ALPHA * (1f - Helpers.Factor(_timeOut.Value, TIMEOUT));
                return;
            }


            var dist = this.Position.DistanceTo(_prevPos);


            if (dist >= TRAIL_DIST)
            {
                if (_posSelector != null)
                    _trailQueue.Enqueue(_posSelector.Invoke(_gameObject));
                else
                    _trailQueue.Enqueue(_gameObject.Position);

                if (_trailQueue.Count == TRAIL_LEN)
                    _trailQueue.Dequeue();

                _prevPos = this.Position;
            }



            //_trailFrame++;

            //if (_trailFrame % TRAIL_STEP == 0)
            //{
            //    if (_posSelector != null)
            //        _trailQueue.Enqueue(_posSelector.Invoke(_gameObject));
            //    else
            //        _trailQueue.Enqueue(_gameObject.Position);

            //    if (_trailQueue.Count == TRAIL_LEN)
            //        _trailQueue.Dequeue();
            //}

            this.Position = _gameObject.Position;
        }

        public void Update(float dt, D2DPoint position)
        {
            _timeOut.Update(dt);

            if (_gameObject.IsExpired)
            {
                _timeOut.Start();
                _trailColor.a = ALPHA * (1f - Helpers.Factor(_timeOut.Value, TIMEOUT));
                return;
            }

            _trailFrame++;

            if (_trailFrame % TRAIL_STEP == 0)
            {
                _trailQueue.Enqueue(position);

                if (_trailQueue.Count == TRAIL_LEN)
                    _trailQueue.Dequeue();
            }
        }

        public void Clear()
        {
            _trailQueue.Clear();
        }

        public override void Render(RenderContext ctx)
        {
            if (_trailQueue.Count == 0)
                return;


            var lastPos = _trailQueue.First();
            foreach (var trail in _trailQueue)
            {
                var nextPos = trail;

                var color = _trailColor;

                ctx.DrawLine(lastPos, nextPos, color, 2f);

                lastPos = nextPos;
            }

            if (_gameObject.IsExpired)
                ctx.FillEllipse(new D2DEllipse(_trailQueue.Last(), new D2DSize(50f, 50f)), _trailColor);
        }

        public void Render(RenderContext ctx, Func<D2DPoint, bool> visiblePredicate)
        {
            if (_trailQueue.Count == 0)
                return;


            var lastPos = _trailQueue.First();
            foreach (var trail in _trailQueue)
            {
                var nextPos = trail;

                var color = _trailColor;

                //if (!visiblePredicate.Invoke(nextPos))
                //    color = D2DColor.Transparent;

                //ctx.DrawLine(lastPos, nextPos, color, 2f);

                if (visiblePredicate.Invoke(nextPos))
                    ctx.DrawLine(lastPos, nextPos, color, 2f);

                lastPos = nextPos;
            }

            if (_gameObject.IsExpired)
                ctx.FillEllipse(new D2DEllipse(_trailQueue.Last(), new D2DSize(50f, 50f)), _trailColor);
        }
    }
}
