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

        private static SmoothDouble _serverTimeOffsetSmooth = new SmoothDouble(30);
        private static FastNoiseLite _turbulenceNoise = new FastNoiseLite();

        public static int PHYSICS_SUB_STEPS => _sub_steps;


        static World()
        {
            ObjectManager = new GameObjectManager();

            WorldBounds[0] = new BoundedRange(-350000f, 350000, 0.05f);
            WorldBounds[1] = new BoundedRange(-100000f, 1000f, 0.05f);

            VeloBounds[0] = new BoundedRange(-5000f, 5000f, 0.05f);
            VeloBounds[1] = new BoundedRange(-5000f, 5000f, 0.05f);

            _turbulenceNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _turbulenceNoise.SetFrequency(NOISE_FREQUENCY);

            TARGET_FRAME_TIME = 1000f / (float)TARGET_FPS;
        }

        public static double LAST_FRAME_TIME = 16.6d;
        public static float TARGET_FRAME_TIME = 16.6f;
        public static readonly float TARGET_FRAME_TIME_NET = TARGET_FRAME_TIME * 2f;
        public const float DEFAULT_FRAME_TIME = 1000f / 60f;

        public static float CurrentDT = TargetDT;

        public static float TargetDT
        {
            get
            {
                return _dt;
            }

            set
            {
                _dt = Math.Clamp(value, 0.0045f, 1f);

                SetSubDT(_dt);
            }
        }

        /// <summary>
        /// Computes the dynamic delta time based on the specified elapsed frame time and sets fixed sub DT and sub steps.
        /// </summary>
        /// <param name="elapFrameTime"></param>
        /// <returns>Returns the new delta time.</returns>
        public static float SetDynamicDT(double elapFrameTime)
        {
            if (elapFrameTime > 250f)
                elapFrameTime = 250f;

            var dt = (float)(World.TargetDT * (elapFrameTime / World.DEFAULT_FRAME_TIME));

            CurrentDT = dt;

            SetSubDT(dt);

            return dt;
        }

        /// <summary>
        /// Sets the fixed-ish sub DT and number of sub steps used for physics.
        /// </summary>
        /// <param name="dt"></param>
        public static void SetSubDT(float dt)
        {
            // Compute sub DT and number of sub steps.
            var subSteps = (int)Math.Ceiling(dt / DEFAULT_SUB_DT);

            _sub_dt = dt / subSteps;
            _sub_steps = subSteps;
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

        public static bool FastPrimitives = true;
        public static bool DrawLightMap = false;
        public static bool DrawNoiseMap = false;
        public static bool UseLightMap = true;
        public static bool MissileRegen = true;
        public static bool ShowMissilesOnRadar = false;
        public static bool ShowLeadIndicators = true;
        public static bool ShowAero = false;
        public static bool ShowTracking = false;
        public static bool ShowAITags = false;
        public static bool EnableTurbulence = true;
        public static bool BulletHoleDrag = true;
        public static bool IsPaused = false;
        public static bool IsNetGame = false;
        public static bool FreeCameraMode = false;
        public static bool UseSkyGradient = false;

        public static bool IsClient
        {
            get { return IsNetGame && !IsServer; }
        }

        public static bool IsServer
        {
            get { return IsNetGame && _isServer; }

            set { _isServer = value; }
        }

        private static bool _isServer = false;

        public static bool RespawnAIPlanes = true;
        public static bool GunsOnly = false;

        public const int DEFAULT_FPS = 60;
        public const int DEFAULT_SUB_STEPS = 6;
        public const float DEFAULT_DT = 0.0425f;
        public const float DEFAULT_SUB_DT = DEFAULT_DT / DEFAULT_SUB_STEPS;

        private static float _dt = DEFAULT_DT * ((float)DEFAULT_FPS / (float)TARGET_FPS);
        private static float _sub_dt = DEFAULT_SUB_DT * ((float)DEFAULT_FPS / (float)TARGET_FPS);
        private static int _sub_steps = DEFAULT_SUB_STEPS;
        private static float _zoomScale = 0.11f;

        public const int TARGET_FPS = 60; // Primary FPS target. Change this to match the desired refresh rate.
        public const int NET_SERVER_FPS = 240;
        public const int NET_CLIENT_FPS = TARGET_FPS;
        public const float NET_INTERP_AMOUNT = 70f; // Amount of time in milliseconds for the interpolation buffer.

        public const float FAST_PRIMITIVE_MIN_SIZE = 2f;
        public const float SCREEN_SHAKE_G = 9f; // Amount of g-force before screen shake effect.
        public const float INERTIA_MULTI = 20f; // Mass is multiplied by this value for interia calculations.
        public const float DEFAULT_DPI = 96f;
        public const float SENSOR_FOV = 60f; // TODO: Not sure this belongs here. Maybe make this unique based on missile/plane types and move it there.
        public const float MAX_ALTITUDE = 100000f; // Max density altitude.  (Air density drops to zero at this altitude)
        public const float MIN_TURB_ALT = 3000f; // Altitude below which turbulence is at maximum.
        public const float MAX_TURB_ALT = 20000f; // Max altitude at which turbulence decreases to zero.
        public const float CLOUD_SCALE = 5f;
        public const float CLOUD_MOVE_RATE = 40f;
        public const float CLOUD_MAX_X = 400000f;
        public const float MAX_AIR_DENSITY = 1.225f;
        private const float NOISE_FLOOR = -1f;
        private const float NOISE_CEILING = 0.8f;
        private const float NOISE_FREQUENCY = 0.0025f;
        private const float MIN_TURB = 0.80f;
        private const float MAX_TURB = 1f;

        public static readonly D2DColor DefaultHudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static D2DColor HudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static readonly D2DColor DefaultFlameColor = new D2DColor(0.6f, D2DColor.Yellow);
        public static readonly D2DColor BlackSmokeColor = new D2DColor(0.6f, D2DColor.Black);
        public static readonly D2DColor GraySmokeColor = new D2DColor(0.6f, D2DColor.Gray);

        public static readonly D2DPoint Gravity = new D2DPoint(0, 19.6f);
        public static readonly D2DPoint FieldPlaneXBounds = new D2DPoint(-350000, 350000);
        public static readonly D2DPoint FieldXBounds = new D2DPoint(-400000, 400000);
        public static readonly D2DPoint CloudRangeY = new D2DPoint(-30000, -2000);
        public static readonly D2DPoint PlaneSpawnRange = new D2DPoint(-250000, 250000);
        public static readonly float PlaneSpawnVelo = 500f;

        public static readonly D2DColor[] TimeOfDayPallet =
        [
            new D2DColor(1f, 0.33f, 0.35f, 0.49f),
            new D2DColor(1f, 0.33f, 0.35f, 0.49f),
            new D2DColor(1f, 0.64f, 0.52f, 0.66f),
            new D2DColor(1f, 0.64f, 0.52f, 0.66f),
            new D2DColor(1f, 1f, 0.67f, 0f),
            new D2DColor(1f, 1f, 0.47f, 0f),
            new D2DColor(1f, 1f, 0f, 0.08f),
            new D2DColor(1f, 1f, 0f, 0.49f),
            new D2DColor(1f, 0.86f, 0f, 1f),
            new D2DColor(1f, 0.64f, 0.52f, 0.66f),
            new D2DColor(1f, 0.33f, 0.35f, 0.49f),
            new D2DColor(1f, 0.37f, 0.4f, 0.54f),
            new D2DColor(1f, 0.71f, 0.77f, 0.93f),
            new D2DColor(0.5f, 0f, 0f, 0f),
            new D2DColor(0.5f, 0f, 0f, 0f)
        ];

        public static uint CurrentObjId = 0;
        public static int CurrentPlayerId = 1000;

        private static long _lastFrameTimeTicks = 0;

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

        public static float GetAltitudeDensity(D2DPoint position)
        {
            var alt = Utilities.PositionToAltitude(position);
            var fact = 1f - Utilities.FactorWithEasing(alt, MAX_ALTITUDE, EasingFunctions.Out.EaseSine);

            return MAX_AIR_DENSITY * fact;
        }

        public static float SampleNoise(D2DPoint position)
        {
            var noiseRaw = _turbulenceNoise.GetNoise(position.X, position.Y);

            // Noise values come in with the range of around -0.9 to 0.9.
            // Clamp the noise value to a new range such that -0.9 equals zero, and 0.9 equals one.
            var noise = Utilities.ScaleToRange(noiseRaw, NOISE_FLOOR, NOISE_CEILING, 0f, 1f);

            return noise;
        }

        public static float GetTurbulenceForPosition(D2DPoint position)
        {
            if (!EnableTurbulence)
                return MAX_TURB;

            var altOffset = Utilities.PositionToAltitude(position) - World.MIN_TURB_ALT; // Offset the altitude such that turbulence is always at max when below 3000.
            var turbAltFact = Utilities.FactorWithEasing(altOffset, World.MAX_TURB_ALT, EasingFunctions.In.EaseCircle);

            // Get noise value for the position and clamp it the desired range.
            var noiseRaw = SampleNoise(position);
            var noise = Utilities.ScaleToRange(noiseRaw, 0f, 1f, MIN_TURB, MAX_TURB);

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

        public static void Update()
        {
            // Compute elapsed time since the last frame
            // and use it to compute a dynamic delta time
            // for the next frame.
            // This should allow for more correct movement
            // when the FPS drops below the target.
            var nowTicks = CurrentTimeTicks();

            if (_lastFrameTimeTicks == 0)
                _lastFrameTimeTicks = nowTicks;

            var elapFrameTimeTicks = nowTicks - _lastFrameTimeTicks;
            _lastFrameTimeTicks = nowTicks;

            var elapFramTimeMs = TimeSpan.FromTicks(elapFrameTimeTicks).TotalMilliseconds;

            var dt = SetDynamicDT(elapFramTimeMs);

            LAST_FRAME_TIME = elapFramTimeMs;

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

        /// <summary>
        /// Current UTC time in milliseconds for net games.
        /// 
        /// Includes computed server time offset.
        /// </summary>
        /// <returns></returns>
        public static double CurrentNetTimeMs()
        {
            var nowTicks = CurrentNetTimeTicks();
            return TimeSpan.FromTicks(nowTicks).TotalMilliseconds;
        }

        /// <summary>
        /// Current UTC time in ticks for net games.
        /// 
        /// Includes computed server time offset.
        /// </summary>
        /// <returns></returns>
        public static long CurrentNetTimeTicks()
        {
            var now = CurrentTimeTicks();
            var time = now + ServerTimeOffset;

            return (long)time;
        }

        /// <summary>
        /// Current UTC time in milliseconds.
        /// </summary>
        /// <returns></returns>
        public static double CurrentTimeMs()
        {
            var now = CurrentTimeTicks();
            var time = now / (double)TimeSpan.TicksPerMillisecond;

            return time;
        }

        /// <summary>
        /// Current UTC time in ticks.
        /// </summary>
        /// <returns></returns>
        public static long CurrentTimeTicks()
        {
            var now = DateTimeOffset.UtcNow.Ticks;
            return now;
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

            if (nextId == -1 && ObjectManager.Planes.Count > 0)
                nextId = ObjectManager.Planes.First().PlayerID;

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

            if (nextId == -1 && ObjectManager.Planes.Count > 0)
                nextId = ObjectManager.Planes.First().PlayerID;

            return nextId;
        }
    }
}
