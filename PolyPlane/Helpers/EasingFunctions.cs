﻿namespace PolyPlane
{
    public static class EasingFunctions
    {
        public static class In
        {
            public static float EaseCubic(float k)
            {
                return k * k * k;
            }

            public static float EaseCircle(float k)
            {
                return 1f - (float)Math.Sqrt(1f - Math.Pow(k, 2f));
            }

            public static float EaseQuintic(float k)
            {
                return k * k * k * k * k;
            }

            public static float EaseQuad(float k)
            {
                return k * k;
            }

            public static float EaseSine(float k)
            {
                return 1f - (float)Math.Cos((k * Math.PI) / 2f);
            }

            public static float EaseExpo(float k)
            {
                return k == 0f ? 0f : (float)Math.Pow(2f, 10f * k - 10f);
            }
        }

        public static class Out
        {
            public static float EaseBack(float k)
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;

                return (float)(1f + c3 * Math.Pow(k - 1f, 3f) + c1 * Math.Pow(k - 1f, 2f));
            }

            public static float EaseBounce(float k)
            {
                const float n1 = 7.5625f;
                const float d1 = 2.75f;

                if (k < 1f / d1)
                {
                    return n1 * k * k;
                }
                else if (k < 2f / d1)
                {
                    return n1 * (k -= 1.5f / d1) * k + 0.75f;
                }
                else if (k < 2.5 / d1)
                {
                    return n1 * (k -= 2.25f / d1) * k + 0.9375f;
                }
                else
                {
                    return n1 * (k -= 2.625f / d1) * k + 0.984375f;
                }
            }

            public static float EaseElastic(float k)
            {
                const float c4 = (2f * (float)Math.PI) / 3f;

                return k == 0f ? 0f : k == 1f ? 1f : (float)Math.Pow(2f, -10f * k) * (float)Math.Sin((k * 10f - 0.75f) * c4) + 1f;
            }

            public static float EaseSine(float k)
            {
                return (float)Math.Sin((k * Math.PI) / 2f);
            }

            public static float EaseExpo(float k)
            {
                return k == 1f ? 1f : 1f - (float)Math.Pow(2f, -10f * k);
            }

            public static float EaseQuintic(float k)
            {
                return 1f + ((k -= 1f) * (float)Math.Pow(k, 4));
            }

            public static float EaseQuad(float k)
            {
                return 1f - (1f - k) * (1f - k);
            }

            public static float EaseCircle(float k)
            {
                return (float)Math.Sqrt(1f - Math.Pow(k - 1f, 2f));
            }

            public static float EaseCubic(float k)
            {
                return 1f - (float)Math.Pow(1f - k, 3f);
            }
        }

        public static class InOut
        {
            public static float EaseBack(float k)
            {
                const float c1 = 1.70158f;
                const float c2 = c1 * 1.525f;

                return k < 0.5
                  ? (float)(Math.Pow(2f * k, 2f) * ((c2 + 1f) * 2f * k - c2)) / 2f
                  : (float)(Math.Pow(2f * k - 2f, 2f) * ((c2 + 1f) * (k * 2f - 2f) + c2) + 2f) / 2f;
            }

            public static float EaseQuart(float k)
            {
                return k < 0.5f ? 8f * k * k * k * k : 1f - (float)Math.Pow(-2f * k + 2f, 4f) / 2f;
            }
        }

        public static float EaseLinear(float k)
        {
            return k;
        }
    }
}
