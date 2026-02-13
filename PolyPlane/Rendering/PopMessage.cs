using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Animations;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class PopMessage
    {
        public string Message;
        public float Age = 0f;
        public D2DPoint Position;
        public D2DPoint RenderPos = D2DPoint.Zero;
        public bool Displayed = true;
        public GameID TargetPlayerID;
        public D2DColor Color = D2DColor.Red;

        public readonly float LIFESPAN = 20f;

        private readonly float UP_RATE = 50f;
        private readonly float SIDE_AMT = 50f;

        private float _curUpAmt = 0f;
        private float _curXPos = 1f;

        private FloatAnimation _animation;

        public PopMessage(string message, D2DPoint position, GameID targetPlayerID)
        {
            Message = message;
            Position = position;
            TargetPlayerID = targetPlayerID;

            _animation = new FloatAnimation(0f, 2f, 3f, EasingFunctions.InOut.EaseQuart, v => _curXPos = v);
            _animation.Loop = true;
            _animation.ReverseOnLoop = true;
            _animation.Start();
        }

        public PopMessage(string message, D2DPoint position, GameID targetPlayerID, D2DColor color)
        {
            Message = message;
            Position = position;
            TargetPlayerID = targetPlayerID;
            Color = color;

            _animation = new FloatAnimation(0f, 2f, 3f, EasingFunctions.InOut.EaseQuart, v => _curXPos = v);
            _animation.Loop = true;
            _animation.ReverseOnLoop = true;
            _animation.Start();
        }

        public void UpdatePos(float dt)
        {
            _animation.Update(dt);

            var amt = SIDE_AMT * _curXPos;

            _curUpAmt += UP_RATE * dt;

            RenderPos = new D2DPoint(Position.X + amt, Position.Y - _curUpAmt);

            Age += dt;

            if (Age > LIFESPAN)
                Displayed = false;
        }
    }
}
