using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class LightMap : IDisposable
    {
        private MapBuffer _mapIn = new MapBuffer();
        private MapBuffer _mapOut = new MapBuffer();

        private ConcurrentBag<ILightMapContributor> _queue = new ConcurrentBag<ILightMapContributor>();
        private ManualResetEventSlim _runQueueEvent = new ManualResetEventSlim(false);

        private Thread? _queueThread = null;
        private FPSLimiter _queueLimiter = new FPSLimiter();
        const int QUEUE_FPS = 500;
        const float MIN_ALPHA = 0.001f;

        public float SIDE_LEN
        {
            get { return _sideLen; }
        }

        private D2DRect _viewport;
        private int _gridWidth;
        private int _gridHeight;
        private int _prevWidth = 0;
        private int _prevHeight = 0;
        private int _gridLen;
        private float _sideLen = 60f;
        private bool disposedValue;

        private static readonly Vector256<int> X_SEQUENCE = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
        private static readonly Vector256<float> MIN_ALPHA_VEC = Vector256.Create(MIN_ALPHA);
        private static readonly Vector256<float> ONE_F8 = Vector256<float>.One;
        private static readonly Vector256<float> ZERO_F8 = Vector256<float>.Zero;

        private Vector256<float> _sideLenVec = ZERO_F8;
        private Vector256<float> _gridWidthVec = ZERO_F8;

        public void BeginFrame(D2DRect viewport)
        {
            StartQueueLoop();

            UpdateViewport(viewport);
            Clear();

            SwapBuffers();

            _runQueueEvent.Set();
        }

        public void EndFrame()
        {
            _runQueueEvent.Reset();

            // Drain queue as needed.
            if (!_queue.IsEmpty)
                DrainQueue();
        }

        private void StartQueueLoop()
        {
            if (_queueThread == null)
            {
                _queueThread = new Thread(QueueLoop);
                _queueThread.IsBackground = true;
                _queueThread.Start();
            }
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

        private void QueueContribution(ILightMapContributor contributor)
        {
            if (contributor.IsLightEnabled())
                _queue.Add(contributor);
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
                QueueContribution(contributor);
            }
        }

        private void AddContribution(ILightMapContributor contributor)
        {
            if (!World.UseLightMap)
                return;

            if (Avx.IsSupported)
            {
                AddContributionAvx(contributor);
            }
            else
            {
                AddContributionScalar(contributor);
            }
        }

        public unsafe void AddContributionAvx(ILightMapContributor lightContributor)
        {
            if (!lightContributor.IsLightEnabled())
                return;

            var radius = lightContributor.GetLightRadius();
            var lightPosition = lightContributor.GetLightPosition();
            var lightColor = lightContributor.GetLightColor();
            var intensityFactor = lightContributor.GetIntensityFactor();
            var sampleNum = (int)(radius / SIDE_LEN);

            GetGridPos(lightPosition.X, lightPosition.Y, out int idxX, out int idxY);

            // Create vectors for the input parameters.
            var inputAVec = Vector256.Create(lightColor.a);
            var inputRVec = Vector256.Create(lightColor.r);
            var inputGVec = Vector256.Create(lightColor.g);
            var inputBVec = Vector256.Create(lightColor.b);

            var radiusVec = Vector256.Create(radius);
            var intensityFactorVec = Vector256.Create(intensityFactor);

            // Center position of the gradient.
            var idxXVec = Vector256.Create(idxX);
            var idxYVec = Vector256.Create(idxY);

            var centerPosX = Vector256.Create(idxX * SIDE_LEN);
            var centerPosY = Vector256.Create(idxY * SIDE_LEN);

            fixed (float* ptrA = _mapIn.A, ptrR = _mapIn.R, ptrG = _mapIn.G, ptrB = _mapIn.B)
            {
                for (int y = -sampleNum; y <= sampleNum; y++)
                {
                    var yOffset = idxY + y;
                    if (yOffset < 0 || yOffset >= _gridHeight)
                        continue;

                    var yStep = Vector256.Create(y);

                    for (int x = -sampleNum; x <= sampleNum; x += 8)
                    {
                        var xOffset = idxX + x;
                        if (xOffset < 0 || xOffset >= _gridWidth)
                            continue;

                        var idx = GetMapIndex(xOffset, yOffset);
                        var xStep = Vector256.Create(x);

                        // Compute the gradient for 8 sequential pixels.
                        var xOffsetVec = Vector256.ConvertToSingle(idxXVec + X_SEQUENCE + xStep);
                        var yOffsetVec = Vector256.ConvertToSingle(idxYVec + yStep);

                        // Gradient distances.
                        var gradPosX = xOffsetVec * _sideLenVec;
                        var gradPosY = yOffsetVec * _sideLenVec;

                        var distX = centerPosX - gradPosX;
                        var distY = centerPosY - gradPosY;

                        var dist = (distX * distX) + (distY * distY);
                        var distSqrt = Avx.Sqrt(dist);

                        // Quadradic lerp intensity for each of the 8 pixels.
                        var gradPct = distSqrt / radiusVec;
                        var pctInv = ONE_F8 - gradPct;
                        var intensity = ONE_F8 - (ONE_F8 - (pctInv * pctInv));

                        // Apply intensity factor.
                        intensity = Vector256.Clamp(intensity * intensityFactorVec, ZERO_F8, ONE_F8);

                        // Compute a mask for the pixels we need to load.
                        // Skip pixels outside the gradient or out of range on the X axis.
                        var obDistMask = Avx.CompareGreaterThan(distSqrt, radiusVec);
                        var obRightMask = Avx.CompareGreaterThanOrEqual(xOffsetVec, _gridWidthVec);
                        var loadMask = obDistMask | obRightMask;

                        // Selectively load current colors within the bounds. 
                        var curAVec = Avx.MaskLoad(&ptrA[idx], ~loadMask);
                        var curRVec = Avx.MaskLoad(&ptrR[idx], ~loadMask);
                        var curGVec = Avx.MaskLoad(&ptrG[idx], ~loadMask);
                        var curBVec = Avx.MaskLoad(&ptrB[idx], ~loadMask);

                        // Alpha blend the two color series.
                        var alpha = ONE_F8 - (ONE_F8 - curAVec) * (ONE_F8 - intensity);

                        var alphaFact = curAVec / alpha;
                        var alphaFactInvert = ONE_F8 - alphaFact;

                        var resA = alpha;
                        var resR = curRVec * alphaFact + inputRVec * alphaFactInvert;
                        var resG = curGVec * alphaFact + inputGVec * alphaFactInvert;
                        var resB = curBVec * alphaFact + inputBVec * alphaFactInvert;

                        // Discard mask for pixels with alpha value below the threshold.
                        var invisibleMask = Avx.CompareLessThan(alpha, MIN_ALPHA_VEC);

                        // Selectively store the new values with the mask.
                        var storeMask = loadMask | invisibleMask;
                        Avx.MaskStore(&ptrA[idx], ~storeMask, resA);
                        Avx.MaskStore(&ptrR[idx], ~storeMask, resR);
                        Avx.MaskStore(&ptrG[idx], ~storeMask, resG);
                        Avx.MaskStore(&ptrB[idx], ~storeMask, resB);
                    }
                }
            }
        }

        public void AddContributionScalar(ILightMapContributor lightContributor)
        {
            var radius = lightContributor.GetLightRadius();
            var lightPosition = lightContributor.GetLightPosition();
            var lightColor = lightContributor.GetLightColor().ToVector4();
            var intensityFactor = lightContributor.GetIntensityFactor();

            var sampleNum = (int)(radius / SIDE_LEN);

            GetGridPos(lightPosition.X, lightPosition.Y, out int idxX, out int idxY);

            var centerPos = new Vector2(idxX * SIDE_LEN, idxY * SIDE_LEN);

            for (int y = -sampleNum; y <= sampleNum; y++)
            {
                for (int x = -sampleNum; x <= sampleNum; x++)
                {
                    var xo = idxX + x;
                    var yo = idxY + y;

                    if (xo >= 0 && yo >= 0 && xo < _gridWidth && yo < _gridHeight)
                    {
                        // Compute the gradient from distance to center.
                        var gradPos = new D2DPoint(xo * SIDE_LEN, yo * SIDE_LEN);
                        var dist = Vector2.Distance(centerPos, gradPos);

                        if (dist <= radius)
                        {
                            var intensity = 1f - Utilities.FactorWithEasing(dist, radius, EasingFunctions.Out.EaseQuad);

                            lightColor.X = Math.Clamp(intensity, 0f, 1f);

                            // Blend the new color.
                            var idx = GetMapIndex(xo, yo);

                            var current = new Vector4(_mapIn.A[idx], _mapIn.R[idx], _mapIn.G[idx], _mapIn.B[idx]);

                            var next = Blend(current, lightColor);

                            _mapIn.A[idx] = next.X;
                            _mapIn.R[idx] = next.Y;
                            _mapIn.G[idx] = next.Z;
                            _mapIn.B[idx] = next.W;
                        }
                    }
                }
            }
        }

        private Vector4 Blend(Vector4 colorA, Vector4 colorB)
        {
            var r = Vector4.Zero;

            var alpha = 1f - (1f - colorA.X) * (1f - colorB.X);

            if (alpha < 0.001f)
                return r;

            var alphaFact = colorA.X / alpha;
            var alphaFactInvert = 1f - alphaFact;

            r = colorA * alphaFact + colorB * alphaFactInvert;
            r.X = alpha;

            return r;
        }

        public Vector4 SampleMap(D2DPoint pos)
        {
            var sample = Vector4.Zero;

            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridWidth && idxY < _gridHeight)
            {
                var idx = GetMapIndex(idxX, idxY);

                var a = _mapOut.A[idx];
                var r = _mapOut.R[idx];
                var g = _mapOut.G[idx];
                var b = _mapOut.B[idx];

                sample = new Vector4(a, r, g, b);
            }

            return sample;
        }

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

        private void SwapBuffers()
        {
            var tmp = _mapIn;
            _mapIn = _mapOut;
            _mapOut = tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetGridPos(Vector2 pos, out int X, out int Y)
        {
            GetGridPos(pos.X, pos.Y, out X, out Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetGridPos(float posX, float posY, out int X, out int Y)
        {
            var posOffset = new D2DPoint(posX - _viewport.Location.X, posY - _viewport.Location.Y);

            X = (int)MathF.Floor(posOffset.X / SIDE_LEN);
            Y = (int)MathF.Floor(posOffset.Y / SIDE_LEN);
        }

        private int GetMapIndex(int x, int y) => GetMapIndex(_gridWidth, x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMapIndex(int width, int x, int y)
        {
            return width * y + x;
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

                var len = _gridWidth * _gridHeight;

                if (_gridLen != len)
                {
                    _gridLen = len;

                    _mapIn.Resize(len, _prevWidth, width, _prevHeight, height, copy: true);
                    _mapOut.Resize(len, _prevWidth, width, _prevHeight, height);
                }

                _gridWidthVec = Vector256.Create((float)_gridWidth);
            }

            _sideLenVec = Vector256.Create(_sideLen);
            _prevWidth = width;
            _prevHeight = height;
            _viewport = viewport;
        }


        private void Clear()
        {
            _mapOut.Clear();
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
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Manages the ARGB buffers.
        /// </summary>
        private sealed class MapBuffer
        {
            public float[] A = Array.Empty<float>();
            public float[] R = Array.Empty<float>();
            public float[] G = Array.Empty<float>();
            public float[] B = Array.Empty<float>();

            private ArrayPool<float> _bufPool = ArrayPool<float>.Create();

            public void Clear()
            {
                Array.Clear(A);
                Array.Clear(R);
                Array.Clear(G);
                Array.Clear(B);
            }

            /// <summary>
            /// Return the current map to the buffer pool and rent new buffers with the specified length.
            /// </summary>
            /// <param name="len">Length of the new buffer.</param>
            /// <param name="oldWidth"></param>
            /// <param name="newWidth"></param>
            /// <param name="oldHeight"></param>
            /// <param name="newHeight"></param>
            /// <param name="copy">True if the old buffer will be copied to the new one.</param>
            public void Resize(int len, int oldWidth, int newWidth, int oldHeight, int newHeight, bool copy = false)
            {
                var newA = _bufPool.Rent(len);
                var newR = _bufPool.Rent(len);
                var newG = _bufPool.Rent(len);
                var newB = _bufPool.Rent(len);

                if (copy)
                    CopyBuffer(ref newA, ref newR, ref newG, ref newB, oldWidth, newWidth, oldHeight, newHeight);

                _bufPool.Return(A, clearArray: true);
                _bufPool.Return(R, clearArray: true);
                _bufPool.Return(G, clearArray: true);
                _bufPool.Return(B, clearArray: true);

                A = newA;
                R = newR;
                G = newG;
                B = newB;
            }

            /// <summary>
            /// Copy existing data from the old buffer to the new one by remapping the coordinate space for the new dimentions.
            /// </summary>
            private unsafe void CopyBuffer(ref float[] newA, ref float[] newR, ref float[] newG, ref float[] newB, int oldWidth, int newWidth, int oldHeight, int newHeight)
            {
                fixed (float* ptrOldA = A, ptrOldR = R, ptrOldG = G, ptrOldB = B)
                fixed (float* ptrNewA = newA, ptrNewR = newR, ptrNewG = newG, ptrNewB = newB)
                {
                    for (int y = 0; y < oldHeight; y++)
                    {
                        for (int x = 0; x < oldWidth; x += 8)
                        {
                            // Map OG buffer coords to the new dimentions.
                            var scaleX = ScaleToRange(x, 0, oldWidth, 0, newWidth);
                            var scaleY = ScaleToRange(y, 0, oldHeight, 0, newHeight);

                            var ogIdx = GetMapIndex(oldWidth, x, y);
                            var newIdx = GetMapIndex(newWidth, (int)scaleX, (int)scaleY);

                            if (newIdx >= 0 && newIdx < newA.Length && ogIdx >= 0 && ogIdx < A.Length)
                            {
                                var curA = Avx.LoadVector256(&ptrOldA[ogIdx]);
                                var curR = Avx.LoadVector256(&ptrOldR[ogIdx]);
                                var curG = Avx.LoadVector256(&ptrOldG[ogIdx]);
                                var curB = Avx.LoadVector256(&ptrOldB[ogIdx]);

                                Avx.Store(&ptrNewA[newIdx], curA);
                                Avx.Store(&ptrNewR[newIdx], curR);
                                Avx.Store(&ptrNewG[newIdx], curG);
                                Avx.Store(&ptrNewB[newIdx], curB);
                            }
                        }
                    }
                }
            }

            private int GetMapIndex(int width, int x, int y)
            {
                return width * y + x;
            }

            private float ScaleToRange(float value, float oldMin, float oldMax, float newMin, float newMax)
            {
                var newVal = (((value - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
                newVal = Math.Clamp(newVal, newMin, newMax);

                return newVal;
            }
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
