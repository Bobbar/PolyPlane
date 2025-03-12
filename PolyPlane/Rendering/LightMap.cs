using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using System.Numerics;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides a fast light color and intensity map for game objects.
    /// </summary>
    public sealed class LightMap
    {
        private Vector4[,] _map = null;

        public float SIDE_LEN
        {
            get { return _sideLen; }
        }

        const int SAMPLE_NUM = 7;
        const float GRADIENT_RADIUS = 450f;

        private D2DRect _viewport;
        private D2DSize _gridSize;
        private float _sideLen = 60f;

        public LightMap() { }

        /// <summary>
        /// Clears and updates the light map to fit the specified viewport
        /// </summary>
        /// <param name="viewport"></param>
        public void Clear(D2DRect viewport)
        {
            UpdateViewport(viewport);
            ClearMap();
        }

        /// <summary>
        /// Adds light contributions from the specified list of objects which implement <see cref="ILightMapContributor"/>.
        /// </summary>
        /// <param name="objs"></param>
        public void AddContributions(IEnumerable<GameObject> objs)
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
            var gradRadius = GRADIENT_RADIUS;
            var intensityFactor = 1f;
            var lightColor = Vector4.Zero;
            var lightPosition = obj.Position;

            // Query light params for contributor.
            if (obj is ILightMapContributor lightContributor)
            {
                if (lightContributor.IsLightEnabled() == false)
                    return;

                intensityFactor = lightContributor.GetIntensityFactor();
                lightColor = lightContributor.GetLightColor().ToVector4();
                lightPosition = lightContributor.GetLightPosition();
                gradRadius = lightContributor.GetLightRadius();

                // Compute the number of samples needed for the current radius.
                sampleNum = (int)(gradRadius / SIDE_LEN);
            }

            GetGridPos(lightPosition, out int idxX, out int idxY);

            var centerPos = new D2DPoint(idxX * SIDE_LEN, idxY * SIDE_LEN);

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
                        var gradPos = new D2DPoint(xo * SIDE_LEN, yo * SIDE_LEN);
                        var dist = centerPos.DistanceTo(gradPos);

                        if (dist <= gradRadius)
                        {
                            var intensity = 1f - Utilities.FactorWithEasing(dist, gradRadius, EasingFunctions.Out.EaseQuad);

                            intensity *= intensityFactor;

                            lightColor.X = Math.Clamp(intensity, 0f, 1f);

                            // Blend the new color.
                            var current = _map[xo, yo];

                            var next = Blend(current, lightColor);

                            _map[xo, yo] = next;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the raw light color at the specified position.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector4 SampleMap(D2DPoint pos)
        {
            var sample = Vector4.Zero;

            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridSize.width && idxY < _gridSize.height)
            {
                sample = _map[idxX, idxY];
            }

            return sample;
        }

        /// <summary>
        /// Sample light color at the specified point and compute the new lighted color from the specified colors.
        /// </summary>
        /// <param name="pos">Position to sample.</param>
        /// <param name="minIntensity">Min intensity range.</param>
        /// <param name="maxIntensity">Max intensity range.</param>
        /// <param name="initColor">Initial un-lighted color.</param>
        /// <returns></returns>
        public D2DColor SampleColor(D2DPoint pos, D2DColor initColor, float minIntensity, float maxIntensity)
        {
            var color = SampleMap(pos);

            // We use the alpha channel to determine the intensity.
            // Clamp the intensity to the specified range.
            var intensity = Utilities.ScaleToRange(color.X, 0f, 1f, minIntensity, maxIntensity);

            // Set the sample color alpha to full as we don't want the sample
            // color to effect the alpha of the initial input color.
            color.X = 1f;

            // Lerp the new color per the intensity.
            var newColor = Vector4.Lerp(initColor.ToVector4(), color, intensity);

            return newColor.ToD2DColor();
        }

        private Vector4 Blend(Vector4 colorA, Vector4 colorB)
        {
            var r = Vector4.Zero;

            r.X = 1f - (1f - colorA.X) * (1f - colorB.X);

            if (r.X < 0.001f)
                return r; // Fully transparent -- R,G,B not important

            var alphaFact = colorA.X / r.X;
            var alphaFactInvert = 1f - alphaFact;

            r.Y = colorA.Y * alphaFact + colorB.Y * alphaFactInvert;
            r.Z = colorA.Z * alphaFact + colorB.Z * alphaFactInvert;
            r.W = colorA.W * alphaFact + colorB.W * alphaFactInvert;

            return r;
        }

        private void ClearMap()
        {
            if (_map != null)
                Array.Clear(_map);
        }

        private void UpdateViewport(D2DRect viewport)
        {
            const int PAD = 5;
            const float MIN_LEN = 30f;
            const float MAX_LEN = 100f;

            // Dynamically change the side length with viewport scale.
            // (Larger side length when zoomed out.)
            _sideLen = Math.Clamp(MathF.Floor(World.ViewPortScaleMulti * 1.5f), MIN_LEN, MAX_LEN);

            var width = (int)MathF.Floor(viewport.Width / SIDE_LEN) + PAD;
            var height = (int)MathF.Floor(viewport.Height / SIDE_LEN) + PAD;

            if (_gridSize.width != width || _gridSize.height != height)
            {
                _gridSize = new D2DSize(width, height);
                _map = new Vector4[width, height];
            }

            _viewport = viewport;
        }

        private void GetGridPos(D2DPoint pos, out int X, out int Y)
        {
            var posOffset = pos - _viewport.Location;

            X = (int)MathF.Floor(posOffset.X / SIDE_LEN);
            Y = (int)MathF.Floor(posOffset.Y / SIDE_LEN);
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
        /// Color of the light to contribute.
        /// </summary>
        /// <returns></returns>
        D2DColor GetLightColor();

        /// <summary>
        /// Intensity factor of the light to contribute. 
        /// </summary>
        float GetIntensityFactor();

        /// <summary>
        /// Position of the light to contribute.
        /// </summary>
        /// <returns></returns>
        D2DPoint GetLightPosition();

        /// <summary>
        /// True if this object is currently contributing light.
        /// </summary>
        bool IsLightEnabled();
    }
}
