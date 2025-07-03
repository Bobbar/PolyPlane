using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    /// <summary>
    /// Provides a fast light color and intensity map for game objects.
    /// </summary>
    public sealed class LightMap : IDisposable
    {
        private Vector4[]? _mapIn = null;
        private Vector4[]? _mapOut = null;

        private ArrayPool<Vector4> _mapPool = ArrayPool<Vector4>.Create();

        private ConcurrentBag<ILightMapContributor> _queue = new ConcurrentBag<ILightMapContributor>();
        private ManualResetEventSlim _runQueueEvent = new ManualResetEventSlim(false);

        private Thread? _queueThread = null;
        private FPSLimiter _queueLimiter = new FPSLimiter();

        public float SIDE_LEN
        {
            get { return _sideLen; }
        }

        const int SAMPLE_NUM = 7;
        const float GRADIENT_RADIUS = 450f;
        const bool USE_QUEUE = true;
        const int QUEUE_FPS = 500;

        private D2DRect _viewport;
        private int _gridWidth = 0;
        private int _gridHeight = 0;
        private int _prevWidth = 0;
        private int _prevHeight = 0;
        private float _sideLen = 60f;
        private bool disposedValue;

        public LightMap() { }

        /// <summary>
        /// Clears and updates the light map to fit the specified viewport and starts the async queue thread.
        /// </summary>
        /// <param name="viewport"></param>
        public void BeginFrame(D2DRect viewport)
        {
            if (!World.UseLightMap)
                return;

            StartQueueLoop();

            UpdateViewport(viewport);
            ClearMap();

            SwapBuffers();

            _runQueueEvent.Set();
        }

        /// <summary>
        /// Stops the async queue and flushes any remaining contributions.
        /// </summary>
        public void EndFrame()
        {
            _runQueueEvent.Reset();

            // Drain queue as needed.
            if (!_queue.IsEmpty)
                DrainQueue();
        }

        private void StartQueueLoop()
        {
            if (!USE_QUEUE)
                return;

            if (_queueThread == null)
            {
                _queueThread = new Thread(QueueLoop);
                _queueThread.IsBackground = true;
                _queueThread.Start();
            }
        }

        private void SwapBuffers()
        {
            var tmp = _mapIn;
            _mapIn = _mapOut;
            _mapOut = tmp;
        }

        private void QueueContribution(ILightMapContributor contributor)
        {
            _queue.Add(contributor);
        }

        private void QueueLoop()
        {
            while (!disposedValue)
            {
                if (!_runQueueEvent.IsSet)
                    _runQueueEvent.Wait();

                DrainQueue();

                _queueLimiter.Wait(QUEUE_FPS);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrainQueue()
        {
            while (!_queue.IsEmpty)
            {
                if (_queue.TryTake(out ILightMapContributor contributor))
                    AddContribution(contributor);
            }
        }

        /// <summary>
        /// Adds light contributions from the specified list of objects which implement <see cref="ILightMapContributor"/>.
        /// </summary>
        /// <param name="objs"></param>
        public void AddContributions(IEnumerable<GameObject> objs)
        {
            if (!World.UseLightMap)
                return;

            foreach (var obj in objs)
            {
                if (obj is ILightMapContributor contributor)
                {
                    if (USE_QUEUE)
                        QueueContribution(contributor);
                    else
                        AddObjContribution(contributor);
                }
            }
        }

        /// <summary>
        /// Adds light contribution from the specified object which implements <see cref="ILightMapContributor"/>.
        /// </summary>
        /// <param name="objs"></param>
        public void AddContribution(GameObject obj)
        {
            if (!World.UseLightMap)
                return;

            if (obj is ILightMapContributor contributor)
            {
                if (USE_QUEUE)
                    QueueContribution(contributor);
                else
                    AddObjContribution(contributor);
            }
        }

        private void AddContribution(ILightMapContributor contributor)
        {
            if (!World.UseLightMap)
                return;

            AddObjContribution(contributor);
        }

        private void AddObjContribution(ILightMapContributor lightContributor)
        {
            if (_mapIn == null)
                return;

            var sampleNum = SAMPLE_NUM;
            var gradRadius = GRADIENT_RADIUS;
            var intensityFactor = 1f;
            var lightColor = Vector4.Zero;
            var lightPosition = D2DPoint.Zero;

            // Query light params for contributor.
            if (lightContributor.IsLightEnabled() == false)
                return;

            intensityFactor = lightContributor.GetIntensityFactor();
            lightColor = lightContributor.GetLightColor().ToVector4();
            lightPosition = lightContributor.GetLightPosition();
            gradRadius = lightContributor.GetLightRadius();

            // Compute the number of samples needed for the current radius.
            sampleNum = (int)(gradRadius / SIDE_LEN);

            GetGridPos(lightPosition, out int idxX, out int idxY);

            var centerPos = new D2DPoint(idxX * SIDE_LEN, idxY * SIDE_LEN);

            // Sample points around the objects to build a light intensity gradient.
            for (int x = -sampleNum; x <= sampleNum; x++)
            {
                for (int y = -sampleNum; y <= sampleNum; y++)
                {
                    var xo = idxX + x;
                    var yo = idxY + y;

                    if (xo >= 0 && yo >= 0 && xo < _gridWidth && yo < _gridHeight)
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
                            var idx = GetMapIndex(xo, yo);

                            var current = _mapIn[idx];

                            var next = Blend(current, lightColor);

                            _mapIn[idx] = next;
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

            if (_mapOut == null)
                return sample;

            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridWidth && idxY < _gridHeight)
            {
                var idx = GetMapIndex(idxX, idxY);
                sample = _mapOut[idx];
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

            if (_gridWidth != width || _gridHeight != height)
            {
                _gridWidth = width;
                _gridHeight = height;

                var len = width * height;

                if (_mapIn == null)
                {
                    _mapIn = _mapPool.Rent(len);
                }

                if (_mapOut == null)
                {
                    _mapOut = _mapPool.Rent(len);
                }

                if (_mapIn.Length != len)
                {
                    // Rent a new input buffer, copy the existing data from the old buffer, then clear & return the old buffer.
                    // This buffer will be swapped to the output, so we need to preserve the existing data to prevent flickering.
                    var newIn = _mapPool.Rent(len);
                    CopyBuffer(ref _mapIn, ref newIn, _prevWidth, width, _prevHeight, height);
                    Array.Clear(_mapIn);
                    _mapPool.Return(_mapIn);
                    _mapIn = newIn;

                    // Just clear and return the output buffer.
                    Array.Clear(_mapOut);
                    _mapPool.Return(_mapOut);
                    _mapOut = _mapPool.Rent(len);
                }
            }

            _prevWidth = width;
            _prevHeight = height;

            _viewport = viewport;
        }

        /// <summary>
        /// Copy existing data from the old buffer to the new one by remapping the coordinate space for the new dimentions.
        /// </summary>
        private void CopyBuffer(ref Vector4[] oldBuf, ref Vector4[] newBuf, int oldWidth, int newWidth, int oldHeight, int newHeight)
        {
            for (int x = 0; x < oldWidth; x++)
            {
                for (int y = 0; y < oldHeight; y++)
                {
                    // Map OG buffer coords to the new dimentions.
                    var scaleX = Utilities.ScaleToRange(x, 0, oldWidth, 0, newWidth);
                    var scaleY = Utilities.ScaleToRange(y, 0, oldHeight, 0, newHeight);

                    var ogIdx = GetMapIndex(oldWidth, x, y);
                    var newIdx = GetMapIndex(newWidth, (int)scaleX, (int)scaleY);

                    if (newIdx >= 0 && newIdx < newBuf.Length && ogIdx >= 0 && ogIdx < oldBuf.Length)
                    {
                        newBuf[newIdx] = oldBuf[ogIdx];
                    }
                }
            }
        }

        private void ClearMap()
        {
            if (_mapOut != null)
                Array.Clear(_mapOut);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetGridPos(D2DPoint pos, out int X, out int Y)
        {
            var posOffset = pos - _viewport.Location;

            X = (int)MathF.Floor(posOffset.X / SIDE_LEN);
            Y = (int)MathF.Floor(posOffset.Y / SIDE_LEN);
        }

        private int GetMapIndex(int x, int y) => GetMapIndex(_gridWidth, x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMapIndex(int width, int x, int y)
        {
            return width * y + x;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                _runQueueEvent.Set();
                _queueThread?.Join();
                _queueLimiter.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
