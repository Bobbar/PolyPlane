using PolyPlane.GameObjects;
using PolyPlane.GameObjects.Managers;
using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane
{
    public static class World
    {
        public static CommandLineOptions? LaunchOptions = null;
        public static readonly GameObjectManager ObjectManager;
        public static GameObject ViewObject;

        public static double LastFrameTimeMs => _lastFrameTimeMs;

        public static float GameSpeed
        {
            get => _gameSpeed;

            set
            {
                _gameSpeed = Math.Clamp(value, 0.01f, 3f);
            }
        }

        public static float SUB_DT
        {
            get => _sub_dt;
        }

        public static float ZoomScale
        {
            get => _zoomScale;

            set
            {
                if (value >= 0.01f && value <= 3f)
                    _zoomScale = value;
            }
        }

        public static float TimeOfDay
        {
            get => _timeOfDay;

            set
            {
                _timeOfDay = value % MAX_TIMEOFDAY;
            }
        }

        public static bool IsClient
        {
            get { return IsNetGame && !IsServer; }
        }

        public static bool IsServer
        {
            get { return IsNetGame && _isServer; }

            set { _isServer = value; }
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

        public static bool FreeCameraMode
        {
            get { return ViewObject != null && ViewObject is FreeCamera; }
        }

        public static bool FastPrimitives = true;
        public static bool DrawLightMap = false;
        public static bool DrawNoiseMap = false;
        public static bool UseLightMap = true;
        public static bool MissileRegen = true;
        public static bool ShowMissilesOnRadar = false;
        public static bool ShowLeadIndicators = true;
        public static bool ShowPointerLine = true;
        public static bool ShowAero = false;
        public static bool ShowTracking = false;
        public static bool ShowAITags = false;
        public static bool EnableTurbulence = true;
        public static bool BulletHoleDrag = true;
        public static bool IsPaused = false;
        public static bool IsNetGame = false;
        public static bool UseSkyGradient = false;
        public static bool RespawnAIPlanes = true;
        public static bool GunsOnly = false;

        public static float CurrentDT => _currentDT;
        public static uint CurrentObjId = 0;
        public static int CurrentPlayerId = 1000;
        public static float TimeOfDayDir = -1f;
        public static double ServerTimeOffset = 0;

        public const float TARGET_FRAME_TIME = 16.666666666f;
        public const float TARGET_FRAME_TIME_NET = TARGET_FRAME_TIME * 2f;
        public const double SERVER_FRAME_TIME = 1000d / NET_SERVER_FPS;

        public const float MAX_TIMEOFDAY = 24f;
        public const float TOD_RATE = 0.02f;

        public const int PHYSICS_SUB_STEPS = 8;
        public static readonly int MUTLI_THREAD_COUNT = 8;

        public const float DEFAULT_DT = 0.0425f;
        public const float DEFAULT_SUB_DT = DEFAULT_DT / PHYSICS_SUB_STEPS;

        public static int TARGET_FPS
        {
            get { return _targetFPS; }
            set { _targetFPS = Math.Clamp(value, 1, 480); }
        }

        public const int NET_SERVER_FPS = 240;
        public const float NET_INTERP_AMOUNT = 70f; // Amount of time in milliseconds for the interpolation buffer.

        public const int SPATIAL_GRID_SIDELEN = 9;
        public const float FAST_PRIMITIVE_MIN_SIZE = 1.5f;
        public const float MIN_ELLIPSE_RENDER_SIZE = 0.5f; // Ellipses with a final radius less than this value are not rendered. 
        public const float MIN_RENDER_ALPHA = 0.0023f; // Skip rendering if the alpha is below this value.
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
        public const float MAX_ROT_SPD = 3000f; // Max rotation speed allowed for game objects.

        public static readonly D2DColor DefaultHudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static D2DColor HudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static readonly D2DColor DefaultFlameColor = new D2DColor(0.6f, D2DColor.Yellow);
        public static readonly D2DColor BlackSmokeColor = new D2DColor(0.6f, D2DColor.Black);
        public static readonly D2DColor GraySmokeColor = new D2DColor(0.6f, D2DColor.Gray);

        public static readonly D2DPoint Gravity = new D2DPoint(0, 19.6f);
        public static readonly D2DPoint CloudRangeY = new D2DPoint(-30000, -2000);
        public static readonly float FieldPlaneXBounds = 350000f;
        public static readonly float FieldXBounds = 400000f;
        public static float PlaneSpawnRange = 250000f;
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

        private const float GAME_SPEED_MULTI = DEFAULT_DT / TARGET_FRAME_TIME;
        private const float NOISE_FLOOR = -1f;
        private const float NOISE_CEILING = 0.8f;
        private const float NOISE_FREQUENCY = 0.0025f;
        private const float MIN_TURB = 0.80f;
        private const float MAX_TURB = 1f;

        private static long _lastFrameTimeTicks = 0;
        private static double _lastFrameTimeMs = 0;
        private static float _timeOfDay = 5f;
        private static bool _isServer = false;
        private static float _currentDT = DEFAULT_DT;
        private static float _gameSpeed = 1f;
        private static int _targetFPS = 60;
        private static float _sub_dt = DEFAULT_SUB_DT;
        private static float _zoomScale = 0.11f;
        private static GameID _viewObjectID;
        private static FastNoiseLite _turbulenceNoise = new FastNoiseLite();

        static World()
        {
            ObjectManager = new GameObjectManager();

            _turbulenceNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _turbulenceNoise.SetFrequency(NOISE_FREQUENCY);

            MUTLI_THREAD_COUNT = Environment.ProcessorCount;
        }

        /// <summary>
        /// Computes the dynamic delta time based on the specified elapsed frame time.
        /// </summary>
        /// <param name="elapFrameTime"></param>
        /// <returns>Returns the new delta time.</returns>
        public static float SetDynamicDT(double elapFrameTime)
        {
            if (elapFrameTime > 250f)
                elapFrameTime = 250f;

            var gameSpeedFactor = _gameSpeed * GAME_SPEED_MULTI;
            var dt = (float)(elapFrameTime * gameSpeedFactor);

            _currentDT = dt;
            _sub_dt = dt / PHYSICS_SUB_STEPS;

            return dt;
        }

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
            // Reset profiler stats.
            Profiler.ResetAll();

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

            var elapFrameTimeMs = TimeSpan.FromTicks(elapFrameTimeTicks).TotalMilliseconds;

            var dt = SetDynamicDT(elapFrameTimeMs);

            _lastFrameTimeMs = elapFrameTimeMs;

            if (!IsPaused)
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

        public static GameObject GetViewPlane()
        {
            var plane = ObjectManager.GetPlaneByPlayerID(ViewObject.PlayerID);

            if (plane == null)
                return new FreeCamera(ViewObject.Position);

            return plane;
        }

        public static GameObject GetViewObject()
        {
            var obj = ViewObject;
            _viewObjectID = obj.ID;

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
                    _viewObjectID = plane.ID;
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
                    _viewObjectID = plane.ID;
                }
            }
        }

        private static int GetNextViewID()
        {
            if (_viewObjectID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.First().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == _viewObjectID.PlayerID && i + 1 < ObjectManager.Planes.Count)
                {
                    nextId = ObjectManager.Planes[i + 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _viewObjectID.PlayerID && i + 1 >= ObjectManager.Planes.Count)
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
            if (_viewObjectID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.Last().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == _viewObjectID.PlayerID && i - 1 >= 0)
                {
                    nextId = ObjectManager.Planes[i - 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == _viewObjectID.PlayerID && i - 1 <= 0)
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
