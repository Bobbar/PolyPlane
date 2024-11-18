using NetStack.Quantization;
using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane
{
    public static class World
    {
        public static readonly GameObjectManager ObjectManager;

        public static BoundedRange[] WorldBounds = new BoundedRange[2];
        public static BoundedRange[] VeloBounds = new BoundedRange[2];

        public static bool InterpOn = true;

        public static int PHYSICS_SUB_STEPS => _sub_steps;

        public const bool NET_UPDATE_SKIP_FRAMES = true;
        public const int TARGET_FPS = 60; // Primary FPS target. Change this to match the desired refresh rate.
        public const int NET_SERVER_FPS = 60;
        public const int NET_CLIENT_FPS = TARGET_FPS;

        static World()
        {
            ObjectManager = new GameObjectManager();

            WorldBounds[0] = new BoundedRange(-350000f, 350000, 0.05f);
            WorldBounds[1] = new BoundedRange(-100000f, 1000f, 0.05f);

            VeloBounds[0] = new BoundedRange(-5000f, 5000f, 0.05f);
            VeloBounds[1] = new BoundedRange(-5000f, 5000f, 0.05f);

            _turbulenceNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _turbulenceNoise.SetFrequency(0.0025f);

            TARGET_FRAME_TIME = 1000f / (float)TARGET_FPS;
        }

        public static readonly float TARGET_FRAME_TIME = 16.6f;

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
                _dt = Math.Clamp(value, 0.0045f, 1f);

                // Compute sub DT and number of sub steps.
                var subSteps = (int)Math.Ceiling(_dt / DEFAULT_SUB_DT);
                var subStepFact = _dt / DEFAULT_SUB_DT;

                if (subStepFact < 1f)
                {
                    // Compute a new sub DT once we fall below the default and only 1 sub step is possible.
                    var newSubDT = DEFAULT_SUB_DT * subStepFact;
                    _sub_dt = newSubDT;
                }
                else
                {
                    // Otherwise set to default when there is more than one sub step.
                    _sub_dt = DEFAULT_SUB_DT;
                }

                _sub_steps = subSteps;
            }
        }

        public static float SUB_DT
        {
            get
            {
                return _sub_dt;
            }
        }

        public const float RenderScale = 1f;

        public static float ZoomScale
        {
            get => _zoomScale;

            set
            {
                if (value >= 0.01f && value <= 3f)
                    _zoomScale = value;
            }
        }


        public static D2DSize ViewPortSize { get; set; }
        public static D2DSize ViewPortBaseSize { get; set; }
        public static D2DRect ViewPortRect { get; set; }
        public static D2DRect ViewPortRectUnscaled { get; set; }


        public static float ViewPortScaleMulti
        {
            get
            {
                var multi = 1f / _zoomScale;
                return multi;
            }
        }

        public static bool MissileRegen = true;
        public static bool ShowMissilesOnRadar = false;
        public static bool ShowLeadIndicators = true;
        public static bool ShowAero = false;
        public static bool ShowTracking = false;
        public static bool ShowAITags = false;
        public static bool EnableWind = false;
        public static bool EnableTurbulence = true;
        public static bool ExpireMissilesOnMiss = false;
        public static bool BulletHoleDrag = true;
        public static bool IsPaused = false;
        public static bool IsNetGame = false;
        public static bool IsServer = false;
        public static bool FreeCameraMode = false;
        public static bool UseSkyGradient = false;
        public static bool IsClient
        {
            get { return World.IsNetGame && !World.IsServer; }
        }

        public static bool RespawnAIPlanes = true;
        public static bool GunsOnly = false;

        public const int DEFAULT_FPS = 60;
        public const int DEFAULT_SUB_STEPS = 6;
        public const float DEFAULT_DT = 0.0425f;
        public static readonly float DEFAULT_SUB_DT = DEFAULT_DT / DEFAULT_SUB_STEPS;
        private static float _dt = DEFAULT_DT * ((float)DEFAULT_FPS / (float)TARGET_FPS);
        private static float _sub_dt = DEFAULT_SUB_DT * ((float)DEFAULT_FPS / (float)TARGET_FPS);
        private static int _sub_steps = DEFAULT_SUB_STEPS;

        private static float _zoomScale = 0.11f;
        public const float DEFAULT_DPI = 96f;
        public const float SENSOR_FOV = 60f; // TODO: Not sure this belongs here. Maybe make this unique based on missile/plane types and move it there.
        public const float MAX_ALTITUDE = 100000f; // Max density altitude.  (Air density drops to zero at this altitude)
        public const float MIN_TURB_ALT = 3000f; // Altitude below which turbulence is at maximum.
        public const float MAX_TURB_ALT = 20000f; // Max altitude at which turbulence decreases to zero.
        private const float MIN_TURB = 0.80f;
        private const float MAX_TURB = 1f;
        private const float MAX_WIND_MAG = 100f;
        public const float AirDensity = 1.225f;
        public static D2DPoint Wind = D2DPoint.Zero;
        private static SmoothDouble _serverTimeOffsetSmooth = new SmoothDouble(10);
        private static RandomVariationVector _windVariation = new RandomVariationVector(MAX_WIND_MAG, 10f, 50f);
        private static FastNoiseLite _turbulenceNoise = new FastNoiseLite();

        public static readonly D2DColor HudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static readonly D2DColor DefaultFlameColor = new D2DColor(0.6f, D2DColor.Yellow);
        public static readonly D2DColor BlackSmokeColor = new D2DColor(0.6f, D2DColor.Black);
        public static readonly D2DColor GraySmokeColor = new D2DColor(0.6f, D2DColor.Gray);

        public static readonly D2DPoint Gravity = new D2DPoint(0, 19.6f);
        public static readonly D2DPoint PlaneSpawnRange = new D2DPoint(-250000, 250000);
        public static readonly D2DPoint FieldXBounds = new D2DPoint(-350000, 350000);

        public static uint CurrentObjId = 0;
        public static int CurrentPlayerId = 1000;


        private static GameID ViewObjectID;
        public static GameObject ViewObject;

        public static double ServerTimeOffset
        {
            get { return _serverTimeOffsetSmooth.Current; }
            set { _serverTimeOffsetSmooth.Add(value); }
        }

        public const float MAX_TIMEOFDAY = 24f;
        public const float TOD_RATE = 0.02f;

        public static float TimeOfDay
        {
            get { return _timeOfDay; }

            set
            {
                _timeOfDay = value % MAX_TIMEOFDAY;
            }
        }


        public static float TimeOfDayDir = -1f;

        private static float _timeOfDay = 5f;

        public static D2DColor GetRandomFlameColor()
        {
            var newColor = new D2DColor(DefaultFlameColor.a, 1f, Utilities.Rnd.NextFloat(0f, 0.86f), DefaultFlameColor.b);
            return newColor;
        }

        public static float GetDensityAltitude(D2DPoint position)
        {
            if (position.Y > 0f)
                return AirDensity;

            var alt = Math.Abs(position.Y);
            var fact = 1f - Utilities.FactorWithEasing(alt, MAX_ALTITUDE, EasingFunctions.EaseOutSine);

            return AirDensity * fact;
        }

        public static float SampleNoise(D2DPoint position)
        {
            var noiseRaw = _turbulenceNoise.GetNoise(position.X, position.Y);

            // Noise comes in with the range of -0.9 to 0.9.
            // Change it to the new range of 0 to 1.
            var noise = Utilities.ClampRange(noiseRaw, -1f, 0.8f, 0f, 1f);

            return noise;
        }

        public static float GetTurbulenceForPosition(D2DPoint position)
        {
            if (!EnableTurbulence)
                return MAX_TURB;

            var altOffset = Utilities.PositionToAltitude(position) - World.MIN_TURB_ALT; // Offset the altitude such that turbulence is always at max when below 3000.
            var turbAltFact = Utilities.FactorWithEasing(altOffset, World.MAX_TURB_ALT, EasingFunctions.EaseInCirc);

            // Get noise value for the position and clamp it the desired range.
            var noiseRaw = SampleNoise(position);
            var noise = Utilities.ClampRange(noiseRaw, 0f, 1f, MIN_TURB, MAX_TURB);

            var turb = Utilities.Lerp(noise, 1f, turbAltFact);

            return turb;
        }

        public static void UpdateViewport(Size viewPortSize)
        {
            ViewPortBaseSize = new D2DSize(viewPortSize.Width, viewPortSize.Height);
            ViewPortSize = new D2DSize(viewPortSize.Width * ViewPortScaleMulti, viewPortSize.Height * ViewPortScaleMulti);
            ViewPortRect = new D2DRect(0, 0, viewPortSize.Width * ViewPortScaleMulti, viewPortSize.Height * ViewPortScaleMulti);
            ViewPortRectUnscaled = new D2DRect(0, 0, viewPortSize.Width, viewPortSize.Height);
        }

        public static void UpdateAirDensityAndWind(float dt)
        {
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

            if (TimeOfDay >= MAX_TIMEOFDAY - 0.2f)
                TimeOfDayDir = -1f;

            if (TimeOfDay <= 0.1f)
                TimeOfDayDir = 1f;
        }

        public static uint GetNextObjectId()
        {
            return Interlocked.Increment(ref CurrentObjId);
        }

        public static int GetNextPlayerId()
        {
            return Interlocked.Increment(ref CurrentPlayerId);
        }

        public static long CurrentTime()
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ServerTimeOffset;
            return (long)Math.Floor(now);
        }

        public static FighterPlane GetViewPlane()
        {
            var plane = ObjectManager.GetPlaneByPlayerID(ViewObject.PlayerID);
            return plane;
        }

        public static GameObject GetViewObject()
        {
            var obj = ViewObject;
            ViewObjectID = obj.ID;

            return obj;
        }

        public static void NextViewPlane()
        {
            lock (ObjectManager)
            {
                var nextId = GetNextViewID();
                var plane = ObjectManager.GetPlaneByPlayerID(nextId);

                if (plane != null)
                {
                    ViewObject = plane;
                    ViewObjectID = plane.ID;
                }
            }
        }

        public static void PrevViewPlane()
        {
            lock (ObjectManager)
            {
                var prevId = GetPrevViewID();
                var plane = ObjectManager.GetPlaneByPlayerID(prevId);

                if (plane != null)
                {
                    ViewObject = plane;
                    ViewObjectID = plane.ID;
                }
            }
        }

        private static int GetNextViewID()
        {
            if (ViewObjectID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.First().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == ViewObjectID.PlayerID && i + 1 < ObjectManager.Planes.Count)
                {
                    nextId = ObjectManager.Planes[i + 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == ViewObjectID.PlayerID && i + 1 >= ObjectManager.Planes.Count)
                {
                    nextId = ObjectManager.Planes.First().PlayerID;
                }
            }

            return nextId;
        }

        private static int GetPrevViewID()
        {
            if (ViewObjectID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.Last().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == ViewObjectID.PlayerID && i - 1 >= 0)
                {
                    nextId = ObjectManager.Planes[i - 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == ViewObjectID.PlayerID && i - 1 <= 0)
                {
                    nextId = ObjectManager.Planes.Last().ID.PlayerID;
                }
            }

            return nextId;
        }
    }
}
