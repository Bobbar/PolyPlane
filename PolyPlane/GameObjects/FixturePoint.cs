using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class FixturePoint : GameObject
    {
        public GameObject GameObject { get; private set; }
        public D2DPoint ReferencePosition { get; private set; }

        public FixturePoint(GameObject gameObject, D2DPoint referencePosition)
        {
            this.GameObject = gameObject;
            this.ReferencePosition = referencePosition;
            this.Rotation = GameObject.Rotation;
            this.Position = Utilities.ApplyTranslation(ReferencePosition, gameObject.Rotation, gameObject.Position, World.RenderScale);
        }

        public void FlipY()
        {
            ReferencePosition = new D2DPoint(ReferencePosition.X, ReferencePosition.Y * -1);
        }

        public override void Update(float dt, float renderScale)
        {
            this.Rotation = GameObject.Rotation;
            this.Position = Utilities.ApplyTranslation(ReferencePosition, GameObject.Rotation, GameObject.Position, renderScale);
        }

        public override void Render(RenderContext ctx)
        {
            ctx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(3f, 3f)), D2DColor.Red);
        }

    }
}
