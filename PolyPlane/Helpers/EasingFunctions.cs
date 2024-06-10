namespace PolyPlane
{
    public static class EasingFunctions
    {
        public static float EaseInOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;

            return k < 0.5
              ? (float)(Math.Pow(2f * k, 2f) * ((c2 + 1f) * 2f * k - c2)) / 2f
              : (float)(Math.Pow(2f * k - 2f, 2f) * ((c2 + 1f) * (k * 2f - 2f) + c2) + 2f) / 2f;
        }


        public static float EaseOutQuintic(float k)
        {
            return 1f + ((k -= 1f) * (float)Math.Pow(k, 4));
        }

        public static float EaseOutCirc(float k)
        {
            return (float)Math.Sqrt(1f - Math.Pow(k - 1f, 2f));
        }

        public static float EaseInQuintic(float k)
        {
            return k * k * k * k * k;
        }

        public static float EaseInCubic(float k)
        {
            return k * k * k;
        }

        public static float EaseOutCubic(float k)
        {
            return 1f - (float)Math.Pow(1f - k, 3f);
        }

        public static float EaseInSine(float k)
        {
            return 1f - (float)Math.Cos((k * Math.PI) / 2f);
        }

        public static float EaseOutSine(float k)
        {
            return (float)Math.Sin((k * Math.PI) / 2f);
        }

        public static float EaseOutElastic(float k)
        {
            const float c4 = (2f * (float)Math.PI) / 3f;

            return k == 0f ? 0f : k == 1f ? 1f : (float)Math.Pow(2f, -10f * k) * (float)Math.Sin((k * 10f - 0.75f) * c4) + 1f;
        }

        public static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            return (float)(1f + c3 * Math.Pow(k - 1f, 3f) + c1 * Math.Pow(k - 1f, 2f));
        }
    }
}
