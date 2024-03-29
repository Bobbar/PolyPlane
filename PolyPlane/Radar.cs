﻿using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane
{
    public class Radar : GameObject
    {
        public D2DPoint Position { get; set; } = D2DPoint.Zero;
        public Plane HostPlane;

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
        private readonly float SWEEP_FOV = 10f; // How wide the radar beam is?
        private readonly float AIM_FOV = 10f; // How wide the radar beam is?

        private float _sweepAngle = 0f;
        private float _maxRange = 40000f;
        private float _maxAge = 2f;
        private readonly float SWEEP_RATE = 300f;
        private float _radius = 150f;//100f;
        private bool _hostIsAI = false;
        private D2DColor _color = D2DColor.Green;
        private List<List<GameObject>> _sources = new List<List<GameObject>>();
        private List<PingObj> _pings = new List<PingObj>();
        private GameTimer _lockTimer = new GameTimer(2f);
        private GameTimer _lostLockTimer = new GameTimer(10f);
        private GameTimer _AIUpdateRate = new GameTimer(1f);

        public Radar(Plane hostPlane, D2DColor renderColor, params List<GameObject>[] sources)
        {
            HostPlane = hostPlane;
            _color = renderColor;

            if (HostPlane.IsAI)
            {
                _hostIsAI = true;
                _AIUpdateRate.Restart();
            }

            foreach (var source in sources)
            {
                _sources.Add(source);
            }

            _lockTimer.TriggerCallback = () =>
            {
                SwitchLock();
                Log.Msg("Lock complete!");
            };

            _lostLockTimer.TriggerCallback = () => ClearLock();
        }

        public override void Update(float dt, D2DSize viewport, float renderScale)
        {
            base.Update(dt, viewport, renderScale);

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
                _sweepAngle = Helpers.ClampAngle(_sweepAngle);

                // Check all sources and add pings if they are within the FOV of the current sweep.
                foreach (var src in _sources)
                {
                    foreach (var obj in src)
                    {
                        if (obj is Decoy)
                            continue;

                        if (obj.IsExpired)
                            continue;

                        if (obj.ID == HostPlane.ID) // Really needed?
                            continue;

                        if (_hostIsAI)
                        {
                            var dist = this.HostPlane.Position.DistanceTo(obj.Position);
                            var angle = (this.HostPlane.Position - obj.Position).Angle(true);
                            var radDist = (_radius / _maxRange) * dist;
                            var radPos = this.Position - Helpers.AngleToVectorDegrees(angle, radDist);

                            if (dist > _maxRange)
                                radPos = this.Position - Helpers.AngleToVectorDegrees(angle, _radius);

                            var pObj = new PingObj(obj, radPos);

                            AddIfNotExists(pObj);
                            RefreshPing(pObj);
                        }
                        else
                        {
                            if (IsInFOV(obj, _sweepAngle, SWEEP_FOV))
                            {
                                var dist = this.HostPlane.Position.DistanceTo(obj.Position);
                                var angle = (this.HostPlane.Position - obj.Position).Angle(true);
                                var radDist = (_radius / _maxRange) * dist;
                                var radPos = this.Position - Helpers.AngleToVectorDegrees(angle, radDist);

                                if (dist > _maxRange)
                                    radPos = this.Position - Helpers.AngleToVectorDegrees(angle, _radius);

                                var pObj = new PingObj(obj, radPos);

                                AddIfNotExists(pObj);
                                RefreshPing(pObj);
                            }
                        }


                    }
                }

                _AIUpdateRate.Restart();
            }


            PrunePings();

            _pings.ForEach(p => p.Update(dt));

            CheckForLock();
            NotifyLocks();
        }


        public override void Render(RenderContext ctx)
        {
            var gfx = ctx.Gfx;

            // Background
            var bgColor = new D2DColor(_color.a * 0.05f, _color);
            gfx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), bgColor);

            // Draw icons.
            foreach (var p in _pings)
            {
                var ageFact = 1f - Helpers.Factor(p.Age, _maxAge);
                var pColor = new D2DColor(ageFact, _color);

                if (p.Obj is Plane plane)
                {
                    if (plane.IsDamaged)
                        gfx.FillEllipse(new D2DEllipse(p.RadarPos, new D2DSize(3f, 3f)), pColor);
                    else
                        gfx.FillRectangle(new D2DRect(p.RadarPos, new D2DSize(6f, 6f)), pColor);
                }

                if (p.Obj is Missile missile)
                {
                    if (p.Obj.Owner.ID != this.HostPlane.ID)
                        gfx.DrawTriangle(p.RadarPos, pColor, D2DColor.Red, 1f);
                    else
                        gfx.DrawTriangle(p.RadarPos, pColor, pColor, 1f);

                }
            }

            // Sweep line, direction line and FOV cone.
            var sweepLine = Helpers.AngleToVectorDegrees(_sweepAngle, _radius);
            gfx.DrawLine(this.Position, this.Position + sweepLine, _color, 1f, D2DDashStyle.Dot);

            DrawFOVCone(gfx, _color);

            // Draw crosshairs on aimed at obj.
            if (_aimedAtPingObj != null)
            {
                gfx.DrawCrosshair(_aimedAtPingObj.RadarPos, 2f, _color, 0, 10f);

                var dist = this.HostPlane.Position.DistanceTo(_aimedAtPingObj.Obj.Position);
                var distPos = this.Position + new D2DPoint(-210f, 100f);
                var dRect = new D2DRect(distPos, new D2DSize(100, 40));
                gfx.FillRectangle(dRect, new D2DColor(0.5f, D2DColor.Black));
                var info = $"D:{Math.Round(dist, 0)}\nA:{Math.Round(_aimedAtPingObj.Obj.Altitude, 0)}";
                gfx.DrawTextCenter(info, _color, "Consolas", 15f, dRect);
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

            // Border
            gfx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), _color);
        }

        private void NotifyLocks()
        {
            if (_lockedPingObj != null)
            {
                if (_lockedPingObj.Obj is Plane plane)
                    plane.IsLockedOnto();
            }
        }

        private void SwitchLock()
        {
            if (_aimedAtPingObj != null)
            {
                _lockedPingObj = _aimedAtPingObj;
                HasLock = true;

                if (_lockedPingObj.Obj is Plane plane)
                    plane.IsLockedOnto();

            }
        }

        private void ClearLock()
        {
            HasLock = false;
            _lockTimer.Stop();
            _lockedPingObj = null;

            Log.Msg("Lost radar lock...");
        }

        private void CheckForLock()
        {
            var mostCentered = FindMostCentered();

            if (LockedObj != null && (LockedObj is Plane plane && (plane.IsExpired || plane.IsDamaged || plane.HasCrashed)))
            {
                ClearLock();
            }

            if (mostCentered != null)
            {
                _lostLockTimer.Stop();
                _aimedAtPingObj = mostCentered;

                if (HasLock)
                {
                    if (mostCentered.Obj.ID != _lockedPingObj.Obj.ID)
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

            var centerLine = Helpers.AngleToVectorDegrees(this.HostPlane.Rotation, _radius);
            var cone1 = Helpers.AngleToVectorDegrees(this.HostPlane.Rotation + (fov * 0.5f), _radius);
            var cone2 = Helpers.AngleToVectorDegrees(this.HostPlane.Rotation - (fov * 0.5f), _radius);

            gfx.DrawLine(this.Position, this.Position + cone1, color);
            gfx.DrawLine(this.Position, this.Position + cone2, color);

            gfx.DrawLine(this.Position, this.Position + centerLine, color, 1f, D2DDashStyle.DashDot);
        }

        public Plane FindRandomPlane()
        {
            var planes = _pings.Where(p =>
            p.Obj is Plane plane
            && !plane.IsDamaged
            && !plane.HasCrashed).ToList();

            if (planes.Count == 0)
                return null;

            var rndPlane = planes[Helpers.Rnd.Next(planes.Count)].Obj as Plane;
            return rndPlane;
        }

        public Plane FindNearestPlane()
        {
            var planes = _pings.Where(p =>
            p.Obj is Plane plane
            && !plane.IsDamaged
            && !plane.HasCrashed).ToList();

            planes = planes.OrderBy(p => this.HostPlane.Position.DistanceTo(p.Obj.Position)).ToList();

            if (planes.Count == 0)
                return null;

            return planes.First().Obj as Plane;
        }

        //private PingObj? FindMostCentered()
        //{
        //    //var distSort = _pings.Where(p =>
        //    //p.Obj is Plane plane
        //    //&& !plane.IsDamaged && !plane.HasCrashed
        //    //&& this.HostPlane.FOVToObject(p.Obj) <= (World.SENSOR_FOV * 0.5f))
        //    //.OrderBy(p => p.Obj.Position.DistanceTo(HostPlane.Position)).ToList();

        //    var distSort = _pings.Where(p =>
        //   p.Obj is Plane plane
        //   && !plane.IsDamaged && !plane.HasCrashed
        //   && this.HostPlane.FOVToObject(p.Obj) <= AIM_FOV)
        //   .OrderBy(p => p.Obj.Position.DistanceTo(HostPlane.Position)).ToList();

        //    var fovSort = distSort.OrderBy(p => this.HostPlane.FOVToObject(p.Obj));

        //    var mostCentered = fovSort.FirstOrDefault();

        //    return mostCentered;
        //}

        private PingObj? FindMostCentered()
        {
            PingObj? mostCentered = null;
            var minFov = float.MaxValue;
            var minDist = float.MaxValue;

            foreach (var p in _pings)
            {
                if (p.Obj is Plane plane && !plane.IsDamaged && !plane.HasCrashed)
                {
                    var fov = this.HostPlane.FOVToObject(plane);
                    var dist = this.HostPlane.Position.DistanceTo(plane.Position);

                    if (fov <= (World.SENSOR_FOV * 0.5f) && fov < minFov && dist < minDist)
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


            var threats = _pings.Where(p => p.Obj is GuidedMissile missile
            && !missile.IsDistracted && !missile.MissedTarget
            && missile.Target == HostPlane
            && missile.ClosingRate(HostPlane) > 0f
            && Helpers.ImpactTime(HostPlane, missile) <= MIN_IMPACT_TIME).ToList();

            // var threats = _pings.Where(p => p.Obj is GuidedMissile missile
            //&& !missile.IsDistracted && !missile.MissedTarget
            //&& missile.ClosingRate(HostPlane) > 0f
            //&& Helpers.ImpactTime(HostPlane, missile) <= MIN_IMPACT_TIME).ToList();

            threats.OrderBy(p => Helpers.ImpactTime(HostPlane, p.Obj as Missile)).ToList();

            if (threats.Count == 0)
                return nearest;

            return threats.First().Obj as GuidedMissile;
        }

        private bool IsInFOV(GameObject obj, float sweepAngle, float fov)
        {
            var dir = obj.Position - this.HostPlane.Position;

            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(sweepAngle, angle);

            return diff <= (fov * 0.5f);
        }

        private float FOVToSweep(GameObject obj)
        {
            var dir = obj.Position - this.Position;
            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(_sweepAngle, angle);

            return diff;
        }

        private void PrunePings()
        {
            for (int i = 0; i < _pings.Count; i++)
            {
                var p = _pings[i];

                if (p.Age >= _maxAge)
                    _pings.RemoveAt(i);
            }
        }

        private void AddIfNotExists(PingObj pingObj)
        {
            bool exists = false;
            for (int i = 0; i < _pings.Count; i++)
            {
                var p = _pings[i];

                if (p.Obj.ID == pingObj.Obj.ID)
                    exists = true;
            }

            if (!exists)
                _pings.Add(pingObj);
        }

        private void RefreshPing(PingObj pingObj)
        {
            for (int i = 0; i < _pings.Count; i++)
            {
                var ping = _pings[i];

                if (ping.Obj.ID == pingObj.Obj.ID)
                {
                    _pings[i].Refresh(pingObj.RadarPos);
                }
            }
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
