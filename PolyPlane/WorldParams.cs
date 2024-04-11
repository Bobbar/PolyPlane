using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane
{
    public static class World
    {

        public static bool InterpOn = true;

        public const int PHYSICS_STEPS = 8;//8;

        public const bool NET_UPDATE_SKIP_FRAMES = true;
        public const int NET_SERVER_FPS = 60;
        public const int NET_CLIENT_FPS = 60;

        public static float SERVER_TICK_RATE
        {
            get
            {
                if (NET_UPDATE_SKIP_FRAMES)
                    return NET_SERVER_FPS / 2;
                else
                    return NET_SERVER_FPS;
            }
        }

        public static float DT
        {
            get
            {
                if (World.IsServer)
                    return _dt / (NET_SERVER_FPS / NET_CLIENT_FPS);
                else
                    return _dt;
            }

            set
            {
                _dt = Math.Clamp(value, 0.0004f, 1f);
            }
        }

        public static float SUB_DT
        {
            get
            {
                return DT / PHYSICS_STEPS;
            }
        }

        public static float RenderScale { get; set; } = 1f;

        public static float ZoomScale
        {
            get => _zoomScale;

            set
            {
                if (value >= 0.01f && value <= 3f)
                    _zoomScale = value;

                Log.Msg($"Zoom: {_zoomScale}");
            }
        }


        public static D2DSize ViewPortSize { get; set; }
        public static D2DSize ViewPortBaseSize { get; set; }
        public static D2DRect ViewPortRect { get; set; }

        public static float ViewPortScaleMulti
        {
            get
            {
                var multi = 1f / _zoomScale;
                return multi;
            }
        }


        public static bool ShowAero = false;
        public static bool ShowMissileCloseup = false;
        public static bool ShowTracking = false;
        public static bool EnableWind = false;
        public static bool EnableTurbulence = false;
        public static bool ExpireMissilesOnMiss = false;
        public static bool IsNetGame = false;
        public static bool IsServer = false;

        private static float _zoomScale = 0.11f;
        private static float _dt = 0.0325f;

        public const float MIN_COLLISION_DIST = 250000f;// Minimum distance (squared) for collisions to be considered.
        public const float SENSOR_FOV = 60f; // TODO: Not sure this belongs here. Maybe make this unique based on missile/plane types and move it there.

        private const float MIN_TURB_DENS = 0.6f;
        private const float MAX_TURB_DENS = 1.225f;
        private const float MAX_WIND_MAG = 100f;
        public static float AirDensity = 1.225f;
        public static D2DPoint Wind = D2DPoint.Zero;

        private static RandomVariationFloat _airDensVariation = new RandomVariationFloat(MIN_TURB_DENS, MAX_TURB_DENS, 0.2f, 5f);
        private static RandomVariationVector _windVariation = new RandomVariationVector(MAX_WIND_MAG, 10f, 50f);

        public static D2DPoint Gravity = new D2DPoint(0, 9.8f);
        public static long CurrentObjId = 0;
        public static int CurrentPlayerId = 1000;

        public static GameID ViewID;
        public static double ServerTimeOffset = 0;

        public const float MAX_TIMEOFDAY = 20f;
        public const float TOD_RATE = 0.03f;
        public static float TimeOfDay = 0.1f;
        public static float TimeOfDayDir = 1f;

        public static float GetDensityAltitude(D2DPoint position)
        {
            const float MAX_ALT = 60000f;

            if (position.Y > 0)
                return AirDensity;

            var alt = Math.Abs(position.Y);
            var fact = 1f - Helpers.Factor(alt, MAX_ALT);

            return AirDensity * fact;
        }


        public static void UpdateViewport(Size viewPortSize)
        {
            ViewPortBaseSize = new D2DSize(viewPortSize.Width, viewPortSize.Height);
            ViewPortSize = new D2DSize(viewPortSize.Width * ViewPortScaleMulti, viewPortSize.Height * ViewPortScaleMulti);
            ViewPortRect = new D2DRect(0, 0, viewPortSize.Width * ViewPortScaleMulti, viewPortSize.Height * ViewPortScaleMulti);
        }

        public static void UpdateAirDensityAndWind(float dt)
        {
            if (EnableTurbulence)
            {
                _airDensVariation.Update(dt);
                AirDensity = _airDensVariation.Value;
            }
            else
            {
                AirDensity = MAX_TURB_DENS;
            }

            if (EnableWind)
            {
                _windVariation.Update(dt);
                Wind = _windVariation.Value;
            }
            else
            {
                Wind = D2DPoint.Zero;
            }

            UpdateTOD(dt);
        }

        private static void UpdateTOD(float dt)
        {
            TimeOfDay += TimeOfDayDir * (TOD_RATE * dt);

            if (TimeOfDay >= MAX_TIMEOFDAY)
                TimeOfDayDir = -1f;

            if (TimeOfDay <= 0.1f)
                TimeOfDayDir = 1f;
        }

        public static long GetNextObjectId()
        {
            return Interlocked.Increment(ref CurrentObjId);
        }

        public static int GetNextPlayerId()
        {
            return Interlocked.Increment(ref CurrentPlayerId);
        }

        public static double CurrentTime()
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ServerTimeOffset;
            return now;

            //var now = DateTime.UtcNow.TimeOfDay.TotalMilliseconds + ServerTimeOffset;
            //return now;

        }
    }
}
