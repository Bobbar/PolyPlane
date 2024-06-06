using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyPlane.Rendering
{
    public class PopMessage
    {
        public string Message;
        public float Age = 0f;
        public D2DPoint Position;
        public D2DPoint RenderPos = D2DPoint.Zero;
        public bool Displayed = true;
        public GameID TargetPlayerID;

        public readonly float LIFESPAN = 10f;

        private readonly float UP_RATE = 50f;
        private readonly float SIDE_RATE = 10f;
        private readonly float SIDE_AMT = 50f;

        private float _curSideAmt = 0f;
        private float _curUpAmt = 0f;
        private float _sideDirection = 1f;

        public PopMessage()
        {
            _curSideAmt = Utilities.Rnd.NextFloat(-SIDE_AMT * 0.5f, SIDE_AMT * 0.5f);
        }

        public void UpdatePos(float dt)
        {
            _curSideAmt += _sideDirection * (SIDE_RATE * dt);

            if (Math.Abs(_curSideAmt) > SIDE_AMT)
                _sideDirection *= -1f;

            var pos = _curSideAmt / SIDE_AMT;
            var amt = SIDE_AMT * EasingFunctions.EaseInOutBack(pos);
            _curUpAmt += UP_RATE * dt;

            RenderPos = new D2DPoint(Position.X + amt, Position.Y - _curUpAmt);

            Age += dt;

            if (Age > LIFESPAN)
                Displayed = false;  
        }
    }
}
