using PolyPlane.GameObjects.Interfaces;
using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Fixtures
{
    public class FixturePoint : GameObject, INoGameID
    {
        public D2DPoint ReferencePosition { get; set; }

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

            ReferencePosition = referencePosition;

            if (_copyRotation)
                Rotation = this.Owner.Rotation;

            SyncWithOwner();
        }

        public void SyncWithOwner()
        {
            if (_copyRotation)
                Rotation = this.Owner.Rotation;

            Position = Utilities.ApplyTranslation(ReferencePosition, this.Owner.Rotation, this.Owner.Position, this.RenderScale);
            Velocity = this.Owner.Velocity;

            this.IsExpired = this.Owner.IsExpired;
        }

        public override void FlipY()
        {
            base.FlipY();
            ReferencePosition = new D2DPoint(ReferencePosition.X, ReferencePosition.Y * -1);
            this.DoUpdate(0f);
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            SyncWithOwner();
        }
    }
}
