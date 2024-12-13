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
        const int SAMPLE_NUM = 7;
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
            objs = objs.Where(o => o is ILightMapContributor);

            UpdateViewport(viewport);
            ClearMap();

            foreach (var obj in objs)
                AddObjContribution(obj);
        }

        /// <summary>
        /// Adds/appends additional light contributions from objects without clearing the map.
        /// </summary>
        /// <param name="objs"></param>
        public void AddAdditional(IEnumerable<GameObject> objs)
        {
            // Filter out all but target object types.
            objs = objs.Where(o => o is ILightMapContributor);

            foreach (var obj in objs)
            {
                if (obj.ContainedBy(_viewport))
                    AddObjContribution(obj);
            }
        }

        private void AddObjContribution(GameObject obj)
        {
            var sampleNum = SAMPLE_NUM;
            var gradDist = GRADIENT_DIST;
            var baseIntensity = 1f;

            // Query light params for contributor.
            if (obj is ILightMapContributor lightContributor)
            {
                if (lightContributor.IsLightEnabled() == false)
                    return;

                baseIntensity = lightContributor.GetIntensityFactor();

                // Compute the number of samples needed for the current radius.
                gradDist = lightContributor.GetLightRadius();
                sampleNum = (int)(gradDist / SIDE_LEN);
            }

            GetGridPos(obj.Position, out int idxX, out int idxY);

            var centerPos = new D2DPoint((idxX * SIDE_LEN) + _viewport.Location.X, (idxY * SIDE_LEN) + _viewport.Location.Y);

            // Sample points around the objects to build a light intensity gradient.
            for (int x = -sampleNum; x <= sampleNum; x++)
            {
                for (int y = -sampleNum; y <= sampleNum; y++)
                {
                    var xo = idxX + x;
                    var yo = idxY + y;

                    if (xo >= 0 && yo >= 0 && xo < _gridSize.width && yo < _gridSize.height)
                    {
                        // Compute the gradient from distance to center.
                        var gradPos = new D2DPoint((xo * SIDE_LEN) + _viewport.Location.X, (yo * SIDE_LEN) + _viewport.Location.Y);
                        var dist = centerPos.DistanceTo(gradPos);

                        if (dist <= gradDist)
                        {
                            var intensity = 1f - Utilities.FactorWithEasing(dist, gradDist, EasingFunctions.Out.EaseSine);

                            intensity *= baseIntensity;

                            // Accumulate and clamp the new intensity.
                            intensity = Math.Clamp(_map[xo, yo] + intensity, 0f, 1f);

                            _map[xo, yo] = intensity;
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
            float sample = 0f;

            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridSize.width && idxY < _gridSize.height)
            {
                sample = _map[idxX, idxY];
            }

            return sample;
        }

        /// <summary>
        /// Sample light intensity at the specified point and compute the new lighted color from the specified colors.
        /// </summary>
        /// <param name="pos">Position to sample.</param>
        /// <param name="minIntensity">Min intensity range.</param>
        /// <param name="maxIntensity">Max intensity range.</param>
        /// <param name="initColor">Initial un-lighted color.</param>
        /// <param name="lightColor">Lighting color to be lerped in per the intensity.</param>
        /// <returns></returns>
        public D2DColor SampleColor(D2DPoint pos, D2DColor initColor, D2DColor lightColor, float minIntensity, float maxIntensity)
        {
            var intensity = SampleIntensity(pos);

            intensity = Utilities.ScaleToRange(intensity, 0f, 1f, minIntensity, maxIntensity);

            var newColor = Utilities.LerpColor(initColor, lightColor, intensity);

            return newColor;
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


    /// <summary>
    /// Objects which contribute to the <see cref="LightMap"/> must implement this interface.
    /// </summary>
    public interface ILightMapContributor
    {
        /// <summary>
        /// Radius of the light to contribute.
        /// </summary>
        float GetLightRadius();

        /// <summary>
        /// Intensity factor of the light to contribute. 
        /// </summary>
        float GetIntensityFactor();

        /// <summary>
        /// True if this object is currently contributing light.
        /// </summary>
        bool IsLightEnabled();
    }
}
