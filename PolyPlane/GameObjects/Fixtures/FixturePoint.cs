using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects.Fixtures
{
    public class FixturePoint : GameObject, INoGameID
    {
        public GameObject GameObject { get; private set; }
        public D2DPoint ReferencePosition { get; private set; }

        private bool _copyRotation = true;

        /// <summary>
        /// Creates a new instance of <see cref="FixturePoint"/> and attaches it to the specified <see cref="GameObjects.GameObject"/>.
        /// </summary>
        /// <param name="gameObject">Parent object.</param>
        /// <param name="referencePosition">Position within the parent object to attach to.</param>
        /// <param name="copyRotation">Copy current rotation from parent object on every update.  Otherwise set manually.</param>
        public FixturePoint(GameObject gameObject, D2DPoint referencePosition, bool copyRotation = true) : base(gameObject)
        {
            this.RenderScale = gameObject.RenderScale;
            _copyRotation = copyRotation;

            GameObject = gameObject;
            ReferencePosition = referencePosition;

            if (_copyRotation)
                Rotation = GameObject.Rotation;

            this.Update(0f);
        }

        public override void FlipY()
        {
            base.FlipY();
            ReferencePosition = new D2DPoint(ReferencePosition.X, ReferencePosition.Y * -1);
            this.Update(0f);
        }

        public override void Update(float dt)
        {
            if (_copyRotation)
                Rotation = GameObject.Rotation;

            Position = Utilities.ApplyTranslation(ReferencePosition, GameObject.Rotation, GameObject.Position, this.RenderScale);
            Velocity = GameObject.Velocity;

            this.IsExpired = GameObject.IsExpired;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(new D2DEllipse(Position, new D2DSize(3f, 3f)), D2DColor.Red);
        }

    }
}
