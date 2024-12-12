using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides a fast light intensity map for game objects.
    /// </summary>
    public sealed class LightMap
    {
        private float[,] _map = null;

        const float SIDE_LEN = 60f;
        const int SAMPLE_NUM = 6;
        const float GRADIENT_DIST = 450f;

        private D2DRect _viewport;
        private D2DSize _gridSize;

        public readonly LightColors Colors = new LightColors();

        public LightMap() { }


        /// <summary>
        /// Updates the light map with light intensities contributed by the specified list of objects.
        /// </summary>
        /// <param name="viewport"></param>
        /// <param name="objs"></param>
        public void Update(D2DRect viewport, IEnumerable<GameObject> objs)
        {
            // Filter out all but target object types.
            objs = objs.Where(o => o is Bullet || o is Decoy || o is GuidedMissile || o is Explosion);

            UpdateViewport(viewport);
            ClearMap();

            foreach (var obj in objs)
            {
                // Some special conditions for decoys and missiles.
                if ((obj is Decoy decoy && !decoy.IsFlashing()) || obj is GuidedMissile missile && !missile.FlameOn)
                    continue;

                GetGridPos(obj.Position, out int idxX, out int idxY);

                // Sample points around the objects to build a light intensity gradient.
                for (int x = -SAMPLE_NUM; x <= SAMPLE_NUM; x++)
                {
                    for (int y = -SAMPLE_NUM; y <= SAMPLE_NUM; y++)
                    {
                        var xo = idxX + x;
                        var yo = idxY + y;

                        if (xo >= 0 && yo >= 0 && xo < _gridSize.width && yo < _gridSize.height)
                        {
                            // Compute the gradient from distance to center.
                            var nPos = new D2DPoint((xo * SIDE_LEN) + _viewport.Location.X, (yo * SIDE_LEN) + _viewport.Location.Y);
                            var dist = obj.Position.DistanceTo(nPos);

                            if (dist <= GRADIENT_DIST)
                            {
                                var intensity = 1f - Utilities.FactorWithEasing(dist, GRADIENT_DIST, EasingFunctions.Out.EaseSine);

                                // Accumulate and clamp the new intensity.
                                intensity = Math.Clamp(_map[xo, yo] + intensity, 0f, 1f);

                                _map[xo, yo] = intensity;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the light intensity at the specified position.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public float SampleIntensity(D2DPoint pos)
        {
            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridSize.width && idxY < _gridSize.height)
            {
                var sample = _map[idxX, idxY];
                return sample;
            }
           
            return 0f;
        }

        private void ClearMap()
        {
            if (_map != null)
                Array.Clear(_map);
        }

        private void UpdateViewport(D2DRect viewport)
        {
            const int PAD = 5;

            var width = (int)Math.Floor(viewport.Width / SIDE_LEN) + PAD;
            var height = (int)Math.Floor(viewport.Height / SIDE_LEN) + PAD;

            if (_gridSize.width != width || _gridSize.height != height)
            {
                _gridSize = new D2DSize(width, height);
                _map = new float[width, height];
            }

            _viewport = viewport;
        }

        private void GetGridPos(D2DPoint pos, out int X, out int Y)
        {
            var posOffset = pos - _viewport.Location;

            X = (int)Math.Floor(posOffset.X / SIDE_LEN);
            Y = (int)Math.Floor(posOffset.Y / SIDE_LEN);
        }


        public class LightColors
        {
            public readonly D2DColor DefaultLightingColor = new D2DColor(1f, 0.98f, 0.54f);
        }
    }
}
