﻿using NetStack.Quantization;
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

        public const int PHYSICS_SUB_STEPS = 6;

        public const bool NET_UPDATE_SKIP_FRAMES = true;
        public const int NET_SERVER_FPS = 60;
        public const int NET_CLIENT_FPS = 60;

        static World()
        {
            ObjectManager = new GameObjectManager();

            WorldBounds[0] = new BoundedRange(-350000f, 350000, 0.05f);
            WorldBounds[1] = new BoundedRange(-100000f, 1000f, 0.05f);

            VeloBounds[0] = new BoundedRange(-5000f, 5000f, 0.05f);
            VeloBounds[1] = new BoundedRange(-5000f, 5000f, 0.05f);
        }

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
                return DT / PHYSICS_SUB_STEPS;
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

        public static bool ShowLeadIndicators = true;
        public static bool ShowAero = false;
        public static bool ShowTracking = false;
        public static bool EnableWind = false;
        public static bool EnableTurbulence = true;
        public static bool ExpireMissilesOnMiss = false;
        public static bool IsNetGame = false;
        public static bool IsServer = false;
        public static bool IsClient
        {
            get { return World.IsNetGame && !World.IsServer; }
        }

        public static bool RespawnAIPlanes = true;
        public static bool GunsOnly = false;

        private static float _zoomScale = 0.11f;
        private static float _dt = DEFAULT_DT;

        public const float DEFAULT_DT = 0.0425f;
        public const float DEFAULT_DPI = 96f;
        public const float SENSOR_FOV = 60f; // TODO: Not sure this belongs here. Maybe make this unique based on missile/plane types and move it there.
        public const float MAX_ALTITUDE = 60000f; // Max density altitude.  (Air density drops to zero at this altitude)
        private const float MIN_TURB = 0.7f;
        private const float MAX_TURB = 1f;
        private const float MAX_WIND_MAG = 100f;
        public const float AirDensity = 1.225f;
        public static float Turbulence = 1f;
        public static D2DPoint Wind = D2DPoint.Zero;
        private static SmoothDouble _serverTimeOffsetSmooth = new SmoothDouble(10);
        private static RandomVariationFloat _turbulenceVariation = new RandomVariationFloat(MIN_TURB, MAX_TURB, 0.05f, 0.3f);
        private static RandomVariationVector _windVariation = new RandomVariationVector(MAX_WIND_MAG, 10f, 50f);

        public static readonly D2DColor HudColor = new D2DColor(0.3f, D2DColor.GreenYellow);
        public static readonly D2DPoint Gravity = new D2DPoint(0, 19.6f);
        public static readonly D2DPoint PlaneSpawnRange = new D2DPoint(-250000, 250000);
        public static readonly D2DPoint FieldXBounds = new D2DPoint(-350000, 350000);

        public static int CurrentObjId = 0;
        public static int CurrentPlayerId = 1000;

        public static GameID ViewPlaneID;

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

        public static float GetDensityAltitude(D2DPoint position)
        {
            if (position.Y > 0)
                return AirDensity;

            var alt = Math.Abs(position.Y);
            var fact = 1f - Utilities.FactorWithEasing(alt, MAX_ALTITUDE, EasingFunctions.EaseInSine);

            return AirDensity * fact;
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

            if (EnableTurbulence)
            {
                _turbulenceVariation.Update(dt);
                Turbulence = _turbulenceVariation.Value;
            }
            else
            {
                Turbulence = 1f;
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

            if (TimeOfDay >= MAX_TIMEOFDAY - 0.2f)
                TimeOfDayDir = -1f;

            if (TimeOfDay <= 0.1f)
                TimeOfDayDir = 1f;
        }

        public static int GetNextObjectId()
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
            var plane = ObjectManager.GetPlaneByPlayerID(ViewPlaneID.PlayerID);
            return plane;
        }

        public static void NextViewPlane()
        {
            lock (ObjectManager)
            {
                var nextId = GetNextViewID();
                var plane = ObjectManager.GetPlaneByPlayerID(nextId);

                if (plane != null)
                    ViewPlaneID = plane.ID;
            }
        }

        public static void PrevViewPlane()
        {
            lock (ObjectManager)
            {
                var prevId = GetPrevViewID();
                var plane = ObjectManager.GetPlaneByPlayerID(prevId);

                if (plane != null)
                    ViewPlaneID = plane.ID;
            }
        }

        private static int GetNextViewID()
        {
            if (ViewPlaneID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.First().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == ViewPlaneID.PlayerID && i + 1 < ObjectManager.Planes.Count)
                {
                    nextId = ObjectManager.Planes[i + 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == ViewPlaneID.PlayerID && i + 1 >= ObjectManager.Planes.Count)
                {
                    nextId = ObjectManager.Planes.First().PlayerID;
                }
            }

            return nextId;
        }

        private static int GetPrevViewID()
        {
            if (ViewPlaneID.PlayerID == -1 && ObjectManager.Planes.Count > 0)
                return ObjectManager.Planes.Last().ID.PlayerID;

            int nextId = -1;
            for (int i = 0; i < ObjectManager.Planes.Count; i++)
            {
                var plane = ObjectManager.Planes[i];

                if (plane.ID.PlayerID == ViewPlaneID.PlayerID && i - 1 >= 0)
                {
                    nextId = ObjectManager.Planes[i - 1].ID.PlayerID;
                }
                else if (plane.ID.PlayerID == ViewPlaneID.PlayerID && i - 1 <= 0)
                {
                    nextId = ObjectManager.Planes.Last().ID.PlayerID;
                }
            }

            return nextId;
        }
    }
}
