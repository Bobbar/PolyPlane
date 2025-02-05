using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public class Radar
    {
        public D2DPoint Position { get; set; } = D2DPoint.Zero;
        public FighterPlane HostPlane;

        public bool HasLock = false;
        public GameObject LockedObj
        {
            get
            {
                if (_lockedPingObj != null)
                    return _lockedPingObj.Obj;
                else
                    return null;
            }
        }

        private PingObj _lockedPingObj = null;
        private PingObj _aimedAtPingObj = null;

        private readonly float _radarFOV = World.SENSOR_FOV * 0.25f;
        private const float MIN_IMPACT_TIME = 20f; // Min time before defending.
        private const float _maxRange = 60000f;
        private const float _maxAge = 1.4f;
        private const float _radius = 150f;

        private D2DColor _color = World.HudColor;
        private Dictionary<GameID, PingObj> _pings = new Dictionary<GameID, PingObj>();

        private GameTimer _lockTimer = new GameTimer(2f);
        private GameTimer _lostLockTimer = new GameTimer(10f);
        private GameTimer _updateTimer = new GameTimer(0.5f, true);

        private D2DLayer _groundClipLayer = null;

        public Radar(FighterPlane hostPlane)
        {
            HostPlane = hostPlane;

            _lockTimer.TriggerCallback = () =>
            {
                SwitchLock();
            };

            _lostLockTimer.TriggerCallback = () => ClearLock();

            _updateTimer.TriggerCallback = DoSweeps;
            _updateTimer.Start();
        }

        public void Update(float dt)
        {
            _lockTimer.Update(dt);
            _lostLockTimer.Update(dt);
            _updateTimer.Update(dt);

            PrunePings();

            foreach (var ping in _pings.Values)
            {
                ping.Update(dt);
            }

            NotifyLocks();
        }

        private void DoSweeps()
        {
            // Check all sources and add pings if they are within the FOV of the current sweep.

            foreach (var missile in World.ObjectManager.Missiles)
                DoSweep(missile);

            foreach (var plane in World.ObjectManager.Planes)
                DoSweep(plane);

            CheckForLock();
        }

        private void DoSweep(GameObject obj)
        {
            if (obj is Decoy)
                return;

            if (obj.IsExpired)
                return;

            if (obj.Equals(HostPlane)) // Really needed?
                return;

            var dist = HostPlane.Position.DistanceTo(obj.Position);
            var angle = (HostPlane.Position - obj.Position).Angle();
            var radDist = _radius / _maxRange * dist;
            var radPos = D2DPoint.Zero - Utilities.AngleToVectorDegrees(angle, radDist);

            if (dist > _maxRange)
                radPos = D2DPoint.Zero - Utilities.AngleToVectorDegrees(angle, _radius);

            AddOrRefresh(obj, radPos);
        }

        public void Render(RenderContext ctx)
        {
            var gfx = ctx.Gfx;

            // Background
            var bgColor = new D2DColor(_color.a * 0.05f, _color);
            gfx.FillEllipse(new D2DEllipse(D2DPoint.Zero, new D2DSize(_radius, _radius)), bgColor);

            // Draw icons.
            foreach (var p in _pings.Values)
            {
                var ageFact = 1f - Utilities.Factor(p.Age, _maxAge);
                var pColor = new D2DColor(ageFact, _color);

                if (p.Obj is FighterPlane plane)
                {
                    if (plane.IsDisabled)
                        gfx.DrawEllipse(new D2DEllipse(p.RadarPos, new D2DSize(4f, 4f)), pColor);
                    else
                    {
                        // Draw direction line.
                        gfx.DrawLine(p.RadarPos, p.RadarPos + Utilities.AngleToVectorDegrees(p.Obj.Velocity.Angle(), 7f), pColor);

                        gfx.FillRectangle(new D2DRect(p.RadarPos, new D2DSize(6f, 6f)), pColor);
                    }

                }

                if (World.ShowMissilesOnRadar)
                {
                    if (p.Obj is GuidedMissile missile)
                    {
                        if (!p.Obj.Owner.Equals(HostPlane))
                            gfx.DrawTriangle(p.RadarPos, pColor, D2DColor.Red, 1f);
                        else
                            gfx.DrawTriangle(p.RadarPos, pColor, pColor, 1f);
                    }
                }
            }

            // Direction line and FOV cone.
            DrawFOVCone(gfx, _color);

            // Draw crosshairs on aimed at obj.
            if (_aimedAtPingObj != null)
            {
                gfx.DrawCrosshair(_aimedAtPingObj.RadarPos, 2f, _color, 0, 10f);

                // Draw target info.
                var aimedAtPlane = _aimedAtPingObj.Obj as FighterPlane;

                if (aimedAtPlane != null)
                {
                    var dist = HostPlane.Position.DistanceTo(aimedAtPlane.Position);
                    var distPos = new D2DPoint(-240f, 100f);
                    var dRect = new D2DRect(distPos, new D2DSize(180, 80));
                    gfx.FillRectangle(dRect, new D2DColor(0.5f, D2DColor.Black));
                    var info = $"D:{Math.Round(dist / 1000f, 0)}\nA:{Math.Round(aimedAtPlane.Altitude / 1000f, 0)}\n{aimedAtPlane.PlayerName}";
                    gfx.DrawTextCenter(info, _color, "Consolas", 20f, dRect);
                }

            }

            // Draw lock circle around locked on obj.
            if (_lockedPingObj != null && HasLock)
                gfx.DrawEllipse(new D2DEllipse(_lockedPingObj.RadarPos, new D2DSize(10f, 10f)), _color);

            // Draw range rings.
            const int N_RANGES = 4;
            var step = _radius / N_RANGES;
            for (int i = 0; i < N_RANGES; i++)
            {
                gfx.DrawEllipse(new D2DEllipse(D2DPoint.Zero, new D2DSize(step * i, step * i)), _color, 1f, D2DDashStyle.Dot);
            }

            // Draw ground indicator.
            DrawGround(ctx);

            // Border
            gfx.DrawEllipse(new D2DEllipse(D2DPoint.Zero, new D2DSize(_radius, _radius)), _color);

            // Lock icon.
            if (HasLock)
            {
                var color = Utilities.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);
                var lockPos = new D2DPoint(0f, -130f);
                var lRect = new D2DRect(lockPos, new D2DSize(80, 20));
                ctx.Gfx.DrawTextCenter("LOCKED", color, "Consolas", 15f, lRect);
                ctx.Gfx.FillRectangle(lRect, color.WithAlpha(0.1f));
            }
        }

        private void DrawFOVCone(D2DGraphics gfx, D2DColor color)
        {
            var fov = _radarFOV;

            var centerLine = Utilities.AngleToVectorDegrees(HostPlane.Rotation, _radius);
            var cone1 = Utilities.AngleToVectorDegrees(HostPlane.Rotation + fov, _radius);
            var cone2 = Utilities.AngleToVectorDegrees(HostPlane.Rotation - fov, _radius);

            gfx.DrawLine(Position, Position + cone1, color);
            gfx.DrawLine(Position, Position + cone2, color);

            gfx.DrawLine(Position, Position + centerLine, color, 1f, D2DDashStyle.DashDot);
        }

        private void DrawGround(RenderContext ctx)
        {
            if (_groundClipLayer == null)
                _groundClipLayer = ctx.Device.CreateLayer();

            // Calculate ground position relative to the plane.
            var groundDist = HostPlane.Position.DistanceTo(new D2DPoint(HostPlane.Position.X, 0f));
            var radDist = _radius / _maxRange * groundDist;
            var radPos = D2DPoint.UnitY * radDist;

            if (groundDist > _maxRange)
                radPos = D2DPoint.UnitY * _radius;

            radPos += new D2DPoint(0f, _radius);

            // Draw a clipped rectangle to represent the ground.
            using (var clipGeo = ctx.Device.CreatePathGeometry())
            {
                var start = new D2DPoint(-_radius, 0f);
                var end = new D2DPoint(_radius, 0f);
                var groundRectSize = new D2DSize(_radius * 2f, _radius * 2f);
                var groundRect = new D2DRect(radPos, groundRectSize);

                // Build an inverted semi-circular path.
                clipGeo.SetStartPoint(start);
                clipGeo.AddArc(end, new D2DSize(_radius, _radius), 0f, D2DArcSize.Small, D2DSweepDirection.CounterClockwise);
                clipGeo.ClosePath();

                ctx.Gfx.PushLayer(_groundClipLayer, new D2DRect(D2DPoint.Zero, groundRectSize), clipGeo);

                ctx.Gfx.FillRectangle(groundRect, _color.WithAlpha(0.05f));

                ctx.Gfx.PopLayer();
            }
        }

        private void NotifyLocks()
        {
            if (_lockedPingObj != null)
            {
                if (_lockedPingObj.Obj is FighterPlane plane)
                    plane.IsLockedOnto();
            }
        }

        private void SwitchLock()
        {
            if (_aimedAtPingObj != null)
            {
                _lockedPingObj = _aimedAtPingObj;
                HasLock = true;

                if (_lockedPingObj.Obj is FighterPlane plane)
                    plane.IsLockedOnto();

            }
        }

        private void ClearLock()
        {
            HasLock = false;
            _lockTimer.Stop();
            _lockedPingObj = null;
        }

        private void CheckForLock()
        {
            const float MAX_LOCK_DIST = 90000f;

            if (HostPlane.IsDisabled)
            {
                _aimedAtPingObj = null;
                ClearLock();
                return;
            }

            var mostCentered = FindMostCenteredAndClosest();

            if (LockedObj != null && LockedObj is FighterPlane plane && (plane.IsExpired || plane.IsDisabled || plane.HasCrashed))
            {
                ClearLock();
            }

            if (mostCentered != null)
            {
                _lostLockTimer.Stop();
                _aimedAtPingObj = mostCentered;

                var dist = _aimedAtPingObj.Obj.Position.DistanceTo(HostPlane.Position);
                if (dist > MAX_LOCK_DIST)
                    return;

                if (HasLock)
                {
                    if (!mostCentered.Obj.Equals(_lockedPingObj.Obj))
                    {

                        if (!_lockTimer.IsRunning)
                            _lockTimer.Restart();
                    }
                }
                else
                {
                    if (!_lockTimer.IsRunning)
                        _lockTimer.Restart();
                }
            }
            else
            {
                _aimedAtPingObj = null;
                _lockTimer.Stop();

                if (!_lostLockTimer.IsRunning)
                    _lostLockTimer.Restart();
            }
        }

        public FighterPlane FindNearestPlane()
        {
            var planes = _pings.Values.Where(p =>
            p.Obj is FighterPlane plane
            && !plane.IsDisabled
            && !plane.HasCrashed);

            planes = planes.OrderBy(p => HostPlane.Position.DistanceTo(p.Obj.Position));

            if (planes.Count() == 0)
                return null;

            return planes.FirstOrDefault().Obj as FighterPlane;
        }

        private PingObj? FindMostCenteredAndClosest()
        {
            PingObj? mostCentered = null;
            var minFov = float.MaxValue;
            var minDist = float.MaxValue;

            foreach (var p in _pings.Values)
            {
                if (p.Obj is FighterPlane plane && !plane.IsDisabled && !plane.HasCrashed)
                {
                    var fov = HostPlane.FOVToObject(plane);
                    var dist = HostPlane.Position.DistanceTo(plane.Position);

                    if (fov <= _radarFOV && fov < minFov && dist < minDist)
                    {
                        minFov = fov;
                        minDist = dist;
                        mostCentered = p;
                    }
                }
            }

            return mostCentered;
        }

        public GuidedMissile FindNearestThreat()
        {
            GuidedMissile nearest = null;

            var threats = _pings.Values.Where(p =>
            {
                if (p.Obj is GuidedMissile missile)
                {
                    if (!missile.MissedTarget && missile.Target.Equals(HostPlane))
                    {
                        var impactTime = Utilities.ImpactTime(HostPlane, missile);

                        if (impactTime > 0f && impactTime <= MIN_IMPACT_TIME)
                            return true;
                    }
                }

                return false;
            });

            if (threats.Count() == 0)
                return nearest;

            threats = threats.OrderBy(p => Utilities.ImpactTime(HostPlane, p.Obj as GuidedMissile));

            var first = threats.FirstOrDefault();

            if (first != null && first.Obj != null)
                nearest = first.Obj as GuidedMissile;

            return nearest;
        }

        private void PrunePings()
        {
            foreach (var ping in _pings.Values)
            {
                if (ping.Age > _maxAge)
                    _pings.Remove(ping.Obj.ID);
            }
        }

        private void AddOrRefresh(GameObject obj, D2DPoint radarPos)
        {
            if (_pings.TryGetValue(obj.ID, out var ping))
                ping.Refresh(radarPos);
            else
                _pings.Add(obj.ID, new PingObj(obj, radarPos));
        }


        private class PingObj
        {
            public GameObject Obj;
            public D2DPoint RadarPos;
            public float Age = 0f;

            public PingObj(GameObject obj)
            {
                Obj = obj;
            }

            public PingObj(GameObject obj, D2DPoint pos)
            {
                Obj = obj;
                RadarPos = pos;
            }

            public void Update(float dt)
            {
                Age += dt;
            }

            public void Refresh(D2DPoint pos)
            {
                Age = 0f;
                RadarPos = pos;
            }
        }
    }
}
