using System.Buffers;
using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{

    public interface ILightMap
    {
        void BeginFrame(D2DRect viewport);
        void EndFrame();
        void AddContribution(GameObject obj);

        Vector4 SampleMap(D2DPoint pos);
        D2DColor SampleColor(D2DPoint pos, D2DColor initColor, float minIntensity, float maxIntensity);

        float SIDE_LEN { get; set; }
    }


    public sealed class LightMap2 : ILightMap
    {
        private float[] A_in = Array.Empty<float>();
        private float[] R_in = Array.Empty<float>();
        private float[] G_in = Array.Empty<float>();
        private float[] B_in = Array.Empty<float>();

        private float[] A_out = Array.Empty<float>();
        private float[] R_out = Array.Empty<float>();
        private float[] G_out = Array.Empty<float>();
        private float[] B_out = Array.Empty<float>();

        //public const float SIDE_LEN = 10f;//10f;

        private ArrayPool<float> _bufPool = ArrayPool<float>.Create();
        
        public float SIDE_LEN
        {
            get { return _sideLen; }

            set { }
        }

        private int _gridWidth;
        private int _gridHeight;

        private int _prevWidth = 0;
        private int _prevHeight = 0;

        private int _gridLen;

        private float _sideLen = 60f;

        private D2DRect _viewport;

        private readonly Vector256<int> _sequence = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
        private readonly Vector256<float> _minAlphaVec = Vector256.Create(0.001f);


        private TimeSpan _mapTime = TimeSpan.Zero;
        private Stopwatch _timer = new Stopwatch();

        private int _numMapped = 0;


        public void BeginFrame(D2DRect viewport)
        {
            _mapTime = TimeSpan.Zero;
            _numMapped = 0;

            UpdateViewport(viewport);
            Clear();

            SwapBuffers();
        }

        public void EndFrame()
        {
            Debug.WriteLine($"[{_numMapped}]  {_mapTime.TotalMilliseconds} ms  {_mapTime.Ticks} ticks");
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
                    //if (USE_QUEUE)
                    //    QueueContribution(contributor);
                    //else
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
                //if (USE_QUEUE)
                //    QueueContribution(contributor);
                //else
                    AddObjContribution(contributor);
            }
        }

        private void AddContribution(ILightMapContributor contributor)
        {
            if (!World.UseLightMap)
                return;

            AddObjContribution(contributor);
        }


        public void AddContribScalar(float posX, float posY, float radius, float initA, float initR, float initG, float initB)
        {
            var sampleNum = (int)(radius / SIDE_LEN);

            GetGridPos(posX, posY, out int idxX, out int idxY);

            var centerPos = new Vector2(idxX * SIDE_LEN, idxY * SIDE_LEN);

            var lightColor = new Vector4(initA, initR, initG, initB);

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
                            var intensity = 1f - EaseQuad(Math.Clamp(dist / radius, 0f, 1f));

                            lightColor.X = Math.Clamp(intensity, 0f, 1f);

                            // Blend the new color.
                            var idx = GetMapIndex(xo, yo);

                            var current = new Vector4(A_in[idx], R_in[idx], G_in[idx], B_in[idx]);

                            var next = Blend(current, lightColor);

                            A_in[idx] = next.X;
                            R_in[idx] = next.Y;
                            G_in[idx] = next.Z;
                            B_in[idx] = next.W;
                        }
                    }
                }
            }
        }


        


        public unsafe void AddObjContribution(ILightMapContributor lightContributor)
        {
            if (!lightContributor.IsLightEnabled())
                return;

            _timer.Restart();


            var radius = lightContributor.GetLightRadius();
            var lightPosition = lightContributor.GetLightPosition();
            var lightColor = lightContributor.GetLightColor();
            var intensityFactor = lightContributor.GetIntensityFactor();

            var sampleNum = (int)(radius / SIDE_LEN);

            GetGridPos(lightPosition.X, lightPosition.Y, out int idxX, out int idxY);

            var initAVec = Vector256.Create(lightColor.a);
            var initRVec = Vector256.Create(lightColor.r);
            var initGVec = Vector256.Create(lightColor.g);
            var initBVec = Vector256.Create(lightColor.b);

            var radiusVec = Vector256.Create(radius);
            var sideLenVec = Vector256.Create(SIDE_LEN);
            var intensityVec = Vector256.Create(intensityFactor);

            var idxXVec = Vector256.Create(idxX);
            var idxYVec = Vector256.Create(idxY);

            var centerPosX = Vector256.Create(idxX * SIDE_LEN);
            var centerPosY = Vector256.Create(idxY * SIDE_LEN);

            fixed (float* ptrA = A_in, ptrR = R_in, ptrG = G_in, ptrB = B_in)
            {
                for (int y = -sampleNum; y <= sampleNum; y++)
                {
                    var yStep = Vector256.Create(y);

                    for (int x = -sampleNum; x <= sampleNum; x += 8)
                    {
                        var idx = GetMapIndex(idxX + x, idxY + y);

                        if (idx >= 0 && idx + 8 <= _gridLen)
                        {
                            var xStep = Vector256.Create(x);

                            var xo = idxXVec + _sequence + xStep;
                            var yo = idxYVec + yStep;

                            // Compute gradient distances.
                            var gradPosX = Vector256.ConvertToSingle(xo) * sideLenVec;
                            var gradPosY = Vector256.ConvertToSingle(yo) * sideLenVec;

                            var distX = centerPosX - gradPosX;
                            var distY = centerPosY - gradPosY;

                            var dist = (distX * distX) + (distY * distY);
                            var distSqrt = Avx.Sqrt(dist);

                            // Quadradic lerp intensity.
                            var gradPct = distSqrt / radiusVec;
                            var pctInv = Vector256<float>.One - gradPct;
                            var intensity = Vector256<float>.One - (Vector256<float>.One - (pctInv * pctInv));

                            intensity *= intensityVec;

                            intensity = Vector256.Clamp(intensity, Vector256<float>.Zero, Vector256<float>.One);

                            // Load current colors. 
                            var curA = Avx.LoadVector256(&ptrA[idx]);
                            var curR = Avx.LoadVector256(&ptrR[idx]);
                            var curG = Avx.LoadVector256(&ptrG[idx]);
                            var curB = Avx.LoadVector256(&ptrB[idx]);

                            // Set initial intensity.
                            initAVec = intensity;

                            // Alpha blend the two colors.
                            var alpha = Vector256<float>.One - (Vector256<float>.One - curA) * (Vector256<float>.One - initAVec);

                            var alphaFact = curA / alpha;
                            var alphaFactInvert = Vector256<float>.One - alphaFact;

                            var resA = alpha;
                            var resR = curR * alphaFact + initRVec * alphaFactInvert;
                            var resG = curG * alphaFact + initGVec * alphaFactInvert;
                            var resB = curB * alphaFact + initBVec * alphaFactInvert;

                            // Discard pixels outside the gradient. (Keep the original color)
                            var distMask = Avx.CompareGreaterThan(distSqrt, radiusVec);
                            resA = Avx.BlendVariable(resA, curA, distMask);
                            resR = Avx.BlendVariable(resR, curR, distMask);
                            resG = Avx.BlendVariable(resG, curG, distMask);
                            resB = Avx.BlendVariable(resB, curB, distMask);

                            // Discard pixels with alpha value below the threshold.
                            var visibleMask = Avx.CompareLessThan(alpha, _minAlphaVec);
                            resA = Avx.BlendVariable(resA, Vector256<float>.Zero, visibleMask);
                            resR = Avx.BlendVariable(resR, Vector256<float>.Zero, visibleMask);
                            resG = Avx.BlendVariable(resG, Vector256<float>.Zero, visibleMask);
                            resB = Avx.BlendVariable(resB, Vector256<float>.Zero, visibleMask);

                            Avx.Store(&ptrA[idx], resA);
                            Avx.Store(&ptrR[idx], resR);
                            Avx.Store(&ptrG[idx], resG);
                            Avx.Store(&ptrB[idx], resB);
                        }
                    }
                }
            }

            _numMapped++;

            _timer.Stop();

            _mapTime += _timer.Elapsed;

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


        public static float EaseQuad(float k)
        {
            return 1f - (1f - k) * (1f - k);
        }

        //public D2DColor SampleMap(D2DPoint pos)
        //{
        //    var sample = D2DColor.Transparent;

        //    GetGridPos(pos, out int idxX, out int idxY);

        //    if (idxX >= 0 && idxY >= 0 && idxX < _gridWidth && idxY < _gridHeight)
        //    {
        //        var idx = GetMapIndex(idxX, idxY);

        //        var a = A_out[idx];
        //        var r = R_out[idx];
        //        var g = G_out[idx];
        //        var b = B_out[idx];

        //        sample = new D2DColor(a, r, g, b);
        //    }

        //    return sample;
        //}

        public Vector4 SampleMap(D2DPoint pos)
        {
            var sample = Vector4.Zero;

            GetGridPos(pos, out int idxX, out int idxY);

            if (idxX >= 0 && idxY >= 0 && idxX < _gridWidth && idxY < _gridHeight)
            {
                var idx = GetMapIndex(idxX, idxY);

                var a = A_out[idx];
                var r = R_out[idx];
                var g = G_out[idx];
                var b = B_out[idx];

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
            Swap(ref A_in, ref A_out);
            Swap(ref R_in, ref R_out);
            Swap(ref G_in, ref G_out);
            Swap(ref B_in, ref B_out);
        }

        private void Swap<T>(ref T[] src, ref T[] dest)
        {
            var tmp = src;
            src = dest;
            dest = tmp;
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

        private int ComputePaddedSize(int len)
        {
            // Radix sort input length must be divisible by this value.
            const int radixMulti = 8;

            if (len < radixMulti)
                return radixMulti;

            int mod = len % radixMulti;
            int padLen = (len - mod) + radixMulti;
            return padLen;
        }

        private void UpdateViewport(D2DRect viewport)
        {
            const int PAD = 5;
            const float MIN_LEN = 30f;
            const float MAX_LEN = 100f;

            _viewport = viewport;

            _sideLen = Math.Clamp(MathF.Floor(World.ViewPortScaleMulti * 1.5f), MIN_LEN, MAX_LEN);

            //var width = (int)MathF.Floor(viewport.Width / SIDE_LEN) + PAD;
            //var height = (int)MathF.Floor(viewport.Height / SIDE_LEN) + PAD;

            var width = ComputePaddedSize((int)MathF.Floor(viewport.Width / SIDE_LEN) + PAD);
            var height = ComputePaddedSize((int)MathF.Floor(viewport.Height / SIDE_LEN) + PAD);

            if (_gridWidth != width || _gridHeight != height)
            {
                //_gridWidth = ComputePaddedSize(width);
                //_gridHeight = ComputePaddedSize(height);

                _gridWidth = width;
                _gridHeight = height;

                var len = _gridWidth * _gridHeight;
                
                
                if (_gridLen != len)
                {
                    _gridLen = len;

                    var newA = _bufPool.Rent(len);
                    var newR = _bufPool.Rent(len);
                    var newG = _bufPool.Rent(len);
                    var newB = _bufPool.Rent(len);


                    CopyBuffer(ref A_in, ref newA, _prevWidth, width, _prevHeight, height);
                    CopyBuffer(ref R_in, ref newR, _prevWidth, width, _prevHeight, height);
                    CopyBuffer(ref G_in, ref newG, _prevWidth, width, _prevHeight, height);
                    CopyBuffer(ref B_in, ref newB, _prevWidth, width, _prevHeight, height);


                    Array.Clear(A_in);
                    Array.Clear(R_in);
                    Array.Clear(G_in);
                    Array.Clear(B_in);

                    _bufPool.Return(A_in);
                    _bufPool.Return(R_in);
                    _bufPool.Return(G_in);
                    _bufPool.Return(B_in);

                    A_in = newA;
                    R_in = newR;
                    G_in = newG;
                    B_in = newB;

                    Array.Clear(A_out);
                    Array.Clear(R_out);
                    Array.Clear(G_out);
                    Array.Clear(B_out);

                    _bufPool.Return(A_out);
                    _bufPool.Return(R_out);
                    _bufPool.Return(G_out);
                    _bufPool.Return(B_out);

                    A_out = _bufPool.Rent(len);
                    R_out = _bufPool.Rent(len);
                    G_out = _bufPool.Rent(len);
                    B_out = _bufPool.Rent(len);

                }



                //Array.Resize(ref A_in, len);
                //Array.Resize(ref R_in, len);
                //Array.Resize(ref G_in, len);
                //Array.Resize(ref B_in, len);

                //Array.Resize(ref A_out, len);
                //Array.Resize(ref R_out, len);
                //Array.Resize(ref G_out, len);
                //Array.Resize(ref B_out, len);


                ////Array.Clear(A_in);
                ////Array.Clear(R_in);
                ////Array.Clear(G_in);
                ////Array.Clear(B_in);



                //Array.Clear(A_out);
                //Array.Clear(R_out);
                //Array.Clear(G_out);
                //Array.Clear(B_out);
            }

            _prevWidth = width;
            _prevHeight = height;
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
                    var scaleX = ScaleToRange(x, 0, oldWidth, 0, newWidth);
                    var scaleY = ScaleToRange(y, 0, oldHeight, 0, newHeight);

                    var ogIdx = GetMapIndex(oldWidth, x, y);
                    var newIdx = GetMapIndex(newWidth, (int)scaleX, (int)scaleY);

                    if (newIdx >= 0 && newIdx < newBuf.Length && ogIdx >= 0 && ogIdx < oldBuf.Length)
                    {
                        newBuf[newIdx] = oldBuf[ogIdx];
                    }
                }
            }
        }

        private void CopyBuffer(ref float[] oldBuf, ref float[] newBuf, int oldWidth, int newWidth, int oldHeight, int newHeight)
        {
            for (int x = 0; x < oldWidth; x++)
            {
                for (int y = 0; y < oldHeight; y++)
                {
                    // Map OG buffer coords to the new dimentions.
                    var scaleX = ScaleToRange(x, 0, oldWidth, 0, newWidth);
                    var scaleY = ScaleToRange(y, 0, oldHeight, 0, newHeight);

                    var ogIdx = GetMapIndex(oldWidth, x, y);
                    var newIdx = GetMapIndex(newWidth, (int)scaleX, (int)scaleY);

                    if (newIdx >= 0 && newIdx < newBuf.Length && ogIdx >= 0 && ogIdx < oldBuf.Length)
                    {
                        newBuf[newIdx] = oldBuf[ogIdx];
                    }
                }
            }
        }

        private float ScaleToRange(float value, float oldMin, float oldMax, float newMin, float newMax)
        {
            var newVal = (((value - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
            newVal = Math.Clamp(newVal, newMin, newMax);

            return newVal;
        }


        private void Clear()
        {
            //Array.Clear(A_in);
            //Array.Clear(R_in);
            //Array.Clear(G_in);
            //Array.Clear(B_in);


            Array.Clear(A_out);
            Array.Clear(R_out);
            Array.Clear(G_out);
            Array.Clear(B_out);
        }
    }
}
