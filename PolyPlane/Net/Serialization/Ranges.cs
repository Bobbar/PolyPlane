using NetStack.Quantization;
using PolyPlane.GameObjects;

namespace PolyPlane.Net
{
    /// <summary>
    /// Bounded ranges for quantization of common net-play data points.
    /// </summary>
    public static class Ranges
    {
        /// <summary>
        /// World position bounds.
        /// </summary>
        public static readonly BoundedRange[] WorldBounds =
        [
            new BoundedRange(-350000f, 350000, 0.05f),
            new BoundedRange(-100000f, 1000f, 0.05f)
        ];

        /// <summary>
        /// Velocity bounds.
        /// </summary>
        public static readonly BoundedRange[] VeloBounds =
        [
            new BoundedRange(-5000f, 5000f, 0.05f),
            new BoundedRange(-5000f, 5000f, 0.05f)
        ];

        /// <summary>
        /// Origin bounds. (For impact positions on plane polys)
        /// </summary>
        public static readonly BoundedRange[] OriginBounds =
        [
            new BoundedRange(-50f, 50f, 0.001f),
            new BoundedRange(-50f, 50f, 0.001f)
        ];

        /// <summary>
        /// Angle bounds (0f to 360f).
        /// </summary>
        public static readonly BoundedRange AngleBounds = new BoundedRange(0f, 360f, 0.01f);

        /// <summary>
        /// Game speed bounds. (0f to 10f)
        /// </summary>
        public static readonly BoundedRange GameSpeedBounds = new BoundedRange(0f, 10f, 0.0001f);

        /// <summary>
        /// Deflection bounds. (-50f to 50f)
        /// </summary>
        public static readonly BoundedRange DeflectionBounds = new BoundedRange(-50f, 50f, 0.5f);

        /// <summary>
        /// Rotation speed bounds. (See: <see cref="World.MAX_ROT_SPD"/>)
        /// </summary>
        public static readonly BoundedRange RotationSpeedBounds = new BoundedRange(-World.MAX_ROT_SPD, World.MAX_ROT_SPD, 1f);

        /// <summary>
        /// Plane health bounds.
        /// </summary>
        public static readonly BoundedRange HealthBounds = new BoundedRange(0f, FighterPlane.MAX_HEALTH, 0.01f);

        /// <summary>
        /// Zero to one bounds.
        /// </summary>
        public static readonly BoundedRange ZeroToOneBounds = new BoundedRange(0f, 1f, 0.1f);

    }
}
