﻿namespace PolyPlane.GameObjects
{
    /// <summary>
    /// Just an empty implementation for use as a temporary placeholder in net games.
    /// </summary>
    public sealed class DummyObject : GameObject
    {
        public DummyObject() : base() { }

        public DummyObject(D2DPoint pos) : base(pos) { }

    }
}
