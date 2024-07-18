using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FixturePoint : GameObject
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
        public FixturePoint(GameObject gameObject, D2DPoint referencePosition, bool copyRotation = true)
        {
            _copyRotation = copyRotation;

            this.GameObject = gameObject;
            this.ReferencePosition = referencePosition;

            if (_copyRotation)
                this.Rotation = GameObject.Rotation;

            this.Position = Utilities.ApplyTranslation(ReferencePosition, gameObject.Rotation, gameObject.Position, World.RenderScale);
        }
  
        public void FlipY()
        {
            ReferencePosition = new D2DPoint(ReferencePosition.X, ReferencePosition.Y * -1);
        }

        public override void Update(float dt, float renderScale)
        {
            if (_copyRotation)
                this.Rotation = GameObject.Rotation;

            this.Position = Utilities.ApplyTranslation(ReferencePosition, GameObject.Rotation, GameObject.Position, renderScale);
            this.Velocity = GameObject.Velocity;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(3f, 3f)), D2DColor.Red);
        }

    }
}
