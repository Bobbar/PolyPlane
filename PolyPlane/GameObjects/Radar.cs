﻿using PolyPlane.GameObjects;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane
{
    public class Radar : GameObject
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

        private readonly float MIN_IMPACT_TIME = 20f; // Min time before defending.
        private float SWEEP_FOV = 10f; // How wide the radar beam is?
        private readonly float AIM_FOV = 10f; // How wide the radar beam is?
        private readonly float SWEEP_RATE = 300f;

        private float _sweepAngle = 0f;
        private float _maxRange = 40000f;
        private float _maxAge = 2f;
        private float _radius = 150f;
        private bool _hostIsAI = false;
        private D2DColor _color = World.HudColor;
        private Dictionary<GameID, PingObj> _pings = new Dictionary<GameID, PingObj>();

        private GameTimer _lockTimer = new GameTimer(2f);
        private GameTimer _lostLockTimer = new GameTimer(10f);
        private GameTimer _AIUpdateRate = new GameTimer(1f);

        private D2DLayer _groundClipLayer = null;

        public Radar(FighterPlane hostPlane)
        {
            HostPlane = hostPlane;

            if (HostPlane.IsAI)
            {
                _hostIsAI = true;
                _AIUpdateRate.Restart();
            }

            _lockTimer.TriggerCallback = () =>
            {
                SwitchLock();
            };

            _lostLockTimer.TriggerCallback = () => ClearLock();
        }

        public override void Update(float dt, float renderScale)
        {
            // Increase sweep FOV as needed to ensure we don't skip over any objects?
            if (SWEEP_RATE * World.DT > SWEEP_FOV)
                SWEEP_FOV = (SWEEP_RATE * World.DT) * 1.2f;

            base.Update(dt, renderScale);

            _lockTimer.Update(dt);
            _lostLockTimer.Update(dt);
            _AIUpdateRate.Update(dt);

            bool timeForUpdate = true;

            if (_hostIsAI && _AIUpdateRate.IsRunning)
            {
                timeForUpdate = false;
            }

            if (timeForUpdate)
            {
                _sweepAngle += SWEEP_RATE * dt;
                _sweepAngle = Utilities.ClampAngle(_sweepAngle);

                // Check all sources and add pings if they are within the FOV of the current sweep.

                foreach (var missile in World.ObjectManager.Missiles)
                    DoSweep(missile);

                foreach (var plane in World.ObjectManager.Planes)
                    DoSweep(plane);

                _AIUpdateRate.Restart();
            }


            PrunePings();

            foreach (var ping in _pings.Values)
            {
                ping.Update(dt);
            }

            CheckForLock();
            NotifyLocks();
        }

        private void DoSweep(GameObject obj)
        {
            if (obj is Decoy)
                return;

            if (obj.IsExpired)
                return;

            if (obj.Equals(HostPlane)) // Really needed?
                return;

            if (_hostIsAI)
            {
                var dist = this.HostPlane.Position.DistanceTo(obj.Position);
                var angle = (this.HostPlane.Position - obj.Position).Angle(true);
                var radDist = (_radius / _maxRange) * dist;
                var radPos = this.Position - Utilities.AngleToVectorDegrees(angle, radDist);

                if (dist > _maxRange)
                    radPos = this.Position - Utilities.AngleToVectorDegrees(angle, _radius);

                var pObj = new PingObj(obj, radPos);

                AddOrRefresh(pObj);
            }
            else
            {
                if (IsInFOV(obj, _sweepAngle, SWEEP_FOV))
                {
                    var dist = this.HostPlane.Position.DistanceTo(obj.Position);
                    var angle = (this.HostPlane.Position - obj.Position).Angle(true);
                    var radDist = (_radius / _maxRange) * dist;
                    var radPos = this.Position - Utilities.AngleToVectorDegrees(angle, radDist);

                    if (dist > _maxRange)
                        radPos = this.Position - Utilities.AngleToVectorDegrees(angle, _radius);

                    var pObj = new PingObj(obj, radPos);

                    AddOrRefresh(pObj);
                }
            }

        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            var gfx = ctx.Gfx;

            // Background
            var bgColor = new D2DColor(_color.a * 0.05f, _color);
            gfx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), bgColor);

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
                        gfx.FillRectangle(new D2DRect(p.RadarPos, new D2DSize(6f, 6f)), pColor);
                }

                if (p.Obj is GuidedMissile missile)
                {
                    if (!p.Obj.Owner.Equals(this.HostPlane))
                        gfx.DrawTriangle(p.RadarPos, pColor, D2DColor.Red, 1f);
                    else
                        gfx.DrawTriangle(p.RadarPos, pColor, pColor, 1f);
                }
            }

            // Sweep line, direction line and FOV cone.
            var sweepLine = Utilities.AngleToVectorDegrees(_sweepAngle, _radius);
            gfx.DrawLine(this.Position, this.Position + sweepLine, _color, 1f, D2DDashStyle.Dot);

            DrawFOVCone(gfx, _color);

            // Draw crosshairs on aimed at obj.
            if (_aimedAtPingObj != null)
            {
                gfx.DrawCrosshair(_aimedAtPingObj.RadarPos, 2f, _color, 0, 10f);

                // Draw target info.
                var aimedAtPlane = _aimedAtPingObj.Obj as FighterPlane;

                if (aimedAtPlane != null)
                {
                    var dist = this.HostPlane.Position.DistanceTo(aimedAtPlane.Position);
                    var distPos = this.Position + new D2DPoint(-240f, 100f);
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
            var step = _radius / (float)N_RANGES;
            for (int i = 0; i < N_RANGES; i++)
            {
                gfx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(step * i, step * i)), _color, 1f, D2DDashStyle.Dot);
            }

            // Draw ground indicator.
            DrawGround(ctx);

            // Border
            gfx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), _color);

            // Lock icon.
            if (this.HasLock)
            {
                var color = Utilities.LerpColor(World.HudColor, D2DColor.WhiteSmoke, 0.3f);
                var lockPos = this.Position + new D2DPoint(0f, -130f);
                var lRect = new D2DRect(lockPos, new D2DSize(80, 20));
                ctx.Gfx.DrawTextCenter("LOCKED", color, "Consolas", 15f, lRect);
                ctx.Gfx.FillRectangle(lRect, color.WithAlpha(0.1f));
            }
        }

        private void DrawGround(RenderContext ctx)
        {
            if (_groundClipLayer == null)
                _groundClipLayer = ctx.Device.CreateLayer();

            // Calculate ground position relative to the plane.
            var groundDist = this.HostPlane.Position.DistanceTo(new D2DPoint(this.HostPlane.Position.X, 0f));
            var radDist = (_radius / _maxRange) * groundDist;
            var radPos = this.Position + Utilities.AngleToVectorDegrees(90f, radDist);

            if (groundDist > _maxRange)
                radPos = this.Position + Utilities.AngleToVectorDegrees(90f, _radius);

            radPos += new D2DPoint(0f, _radius);

            // Draw a clipped rectangle to represent the ground.
            using (var clipGeo = ctx.Device.CreatePathGeometry())
            {
                var start = new D2DPoint(this.Position.X - _radius, this.Position.Y);
                var end = new D2DPoint(this.Position.X + _radius, this.Position.Y);
                var groundRectSize = new D2DSize(_radius * 2f, _radius * 2f);
                var groundRect = new D2DRect(radPos, groundRectSize);

                // Build an inverted semi-circular path.
                clipGeo.SetStartPoint(start);
                clipGeo.AddArc(end, new D2DSize(_radius, _radius), 0f, D2DArcSize.Small, D2DSweepDirection.CounterClockwise);
                clipGeo.ClosePath();

                ctx.Gfx.PushLayer(_groundClipLayer, new D2DRect(this.Position, groundRectSize), clipGeo);

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

            if (this.HostPlane.IsDisabled)
            {
                ClearLock();
                return;
            }

            var mostCentered = FindMostCenteredAndClosest();

            if (LockedObj != null && (LockedObj is FighterPlane plane && (plane.IsExpired || plane.IsDisabled || plane.HasCrashed)))
            {
                ClearLock();
            }

            if (mostCentered != null)
            {
                _lostLockTimer.Stop();
                _aimedAtPingObj = mostCentered;

                var dist = _aimedAtPingObj.Obj.Position.DistanceTo(this.HostPlane.Position);
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

        private void DrawFOVCone(D2DGraphics gfx, D2DColor color)
        {
            var fov = World.SENSOR_FOV * 0.5f;

            var centerLine = Utilities.AngleToVectorDegrees(this.HostPlane.Rotation, _radius);
            var cone1 = Utilities.AngleToVectorDegrees(this.HostPlane.Rotation + (fov * 0.5f), _radius);
            var cone2 = Utilities.AngleToVectorDegrees(this.HostPlane.Rotation - (fov * 0.5f), _radius);

            gfx.DrawLine(this.Position, this.Position + cone1, color);
            gfx.DrawLine(this.Position, this.Position + cone2, color);

            gfx.DrawLine(this.Position, this.Position + centerLine, color, 1f, D2DDashStyle.DashDot);
        }

        public FighterPlane FindNearestPlane()
        {
            var planes = _pings.Values.Where(p =>
            p.Obj is FighterPlane plane
            && !plane.IsDisabled
            && !plane.HasCrashed);

            planes = planes.OrderBy(p => this.HostPlane.Position.DistanceTo(p.Obj.Position));

            if (planes.Count() == 0)
                return null;

            return planes.FirstOrDefault().Obj as FighterPlane;
        }

        private PingObj? FindMostCenteredAndClosest()
        {
            const float MAX_DIST = 90000f;

            PingObj? mostCentered = null;
            var minFov = float.MaxValue;
            var minDist = float.MaxValue;

            foreach (var p in _pings.Values)
            {
                if (p.Obj is FighterPlane plane && !plane.IsDisabled && !plane.HasCrashed)
                {
                    var fov = this.HostPlane.FOVToObject(plane);
                    var dist = this.HostPlane.Position.DistanceTo(plane.Position);

                    if (fov <= (World.SENSOR_FOV * 0.25f) && fov < minFov && dist < minDist)
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

            var threats = _pings.Values.Where(p => p.Obj is GuidedMissile missile
            && !missile.MissedTarget
            && missile.Target.Equals(HostPlane)
            && Utilities.ImpactTime(HostPlane, missile) <= MIN_IMPACT_TIME);

            if (threats.Count() == 0)
                return nearest;

            threats = threats.OrderBy(p => Utilities.ImpactTime(HostPlane, p.Obj as Missile));

            var first = threats.FirstOrDefault();

            if (first != null && first.Obj != null)
                nearest = first.Obj as GuidedMissile;

            return nearest;
        }

        private bool IsInFOV(GameObject obj, float sweepAngle, float fov)
        {
            var dir = obj.Position - this.HostPlane.Position;

            var angle = dir.Angle(true);
            var diff = Utilities.AngleDiff(sweepAngle, angle);

            return diff <= (fov * 0.5f);
        }

        private float FOVToSweep(GameObject obj)
        {
            var dir = obj.Position - this.Position;
            var angle = dir.Angle(true);
            var diff = Utilities.AngleDiff(_sweepAngle, angle);

            return diff;
        }

        private void PrunePings()
        {
            foreach (var ping in _pings.Values)
            {
                if (ping.Age > _maxAge)
                    _pings.Remove(ping.Obj.ID);
            }
        }

        private void AddOrRefresh(PingObj pingObj)
        {
            if (_pings.TryGetValue(pingObj.Obj.ID, out var ping))
                ping.Refresh(pingObj.RadarPos);
            else
                _pings.Add(pingObj.Obj.ID, pingObj);
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
