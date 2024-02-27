using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane
{
    public class Radar
    {
        public D2DPoint Position { get; set; } = D2DPoint.Zero;
        public GameObject HostObj;

        private float _sweepAngle = 0f;
        private float _maxRange = 40000f;
        private float _maxAge = 2f;
        private readonly float SWEEP_RATE = 200f;
        private float _radius = 100f;
        private List<List<GameObject>> _sources = new List<List<GameObject>>();
        private List<PingObj> _pings = new List<PingObj>();

        public Radar(GameObject hostObj, params List<GameObject>[] sources)
        {
            HostObj = hostObj;

            foreach (var source in sources)
            {
                _sources.Add(source);
            }
        }

        public void Update(float dt)
        {
            _sweepAngle += SWEEP_RATE * dt;
            _sweepAngle = Helpers.ClampAngle(_sweepAngle);

            foreach (var src in _sources)
            {
                foreach (var obj in src)
                {
                    if (obj is Decoy)
                        continue;

                    if (obj.IsExpired)
                        continue;

                    if (IsInFOV(obj, _sweepAngle, 10f))
                    {
                        var dist = this.HostObj.Position.DistanceTo(obj.Position);
                        var angle = (this.HostObj.Position - obj.Position).Angle(true);
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

            PrunePings();

            _pings.ForEach(p => p.Update(dt));
        }

        public void Render(D2DGraphics gfx, D2DColor color)
        {
            // Background
            var bgColor = new D2DColor(color.a * 0.05f, color);
            gfx.FillEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), bgColor);

            // Draw icons.
            foreach (var p in _pings)
            {
                var ageFact = 1f - Helpers.Factor(p.Age, _maxAge);
                var pColor = new D2DColor(ageFact, color);

                if (p.Obj is Plane plane)
                {
                    if (plane.IsDamaged)
                        gfx.FillEllipse(new D2DEllipse(p.RadarPos, new D2DSize(3f, 3f)), pColor);
                    else
                        gfx.FillRectangle(new D2DRect(p.RadarPos, new D2DSize(6f, 6f)), pColor);
                }

                if (p.Obj is Missile missile)
                {
                    if (p.Obj.Owner.ID != this.HostObj.ID)
                        gfx.DrawTriangle(p.RadarPos, pColor, D2DColor.Red, 1f);
                    else
                        gfx.DrawTriangle(p.RadarPos, pColor, pColor, 1f);

                }
            }

            // Sweep line, direction line and FOV cone.
            var sweepLine = Helpers.AngleToVectorDegrees(_sweepAngle, _radius);
            gfx.DrawLine(this.Position, this.Position + sweepLine, color, 1f, D2DDashStyle.Dot);
         
            DrawFOVCone(gfx, color);

            // Draw the current target.
            // TODO:  This logic is repeated in multiple places. Need to find a global solution.
            var mostCentered = FindMostCentered();
            if (mostCentered != null)
            {
                gfx.DrawCrosshair(mostCentered.RadarPos, 2f, color, 0, 10f);

                var dist = this.HostObj.Position.DistanceTo(mostCentered.Obj.Position);

                var distPos = this.Position + new D2DPoint(0f, 120f);
                var dRect = new D2DRect(distPos, new D2DSize(60, 30));
                gfx.FillRectangle(dRect, new D2DColor(0.5f, D2DColor.Black));
                gfx.DrawTextCenter(Math.Round(dist, 0).ToString(), color, "Consolas", 15f, dRect);
            }

            // Draw range rings.
            const int N_RANGES = 4;
            var step = _radius / (float)N_RANGES;
            for (int i = 0 ; i < N_RANGES; i++)
            {
                gfx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(step * i, step * i)), color, 1f, D2DDashStyle.Dot);

            }

            // Border
            gfx.DrawEllipse(new D2DEllipse(this.Position, new D2DSize(_radius, _radius)), color);
        }

        private void DrawFOVCone(D2DGraphics gfx, D2DColor color)
        {
            var fov = World.SENSOR_FOV * 0.5f;

            var centerLine = Helpers.AngleToVectorDegrees(this.HostObj.Rotation, _radius);
            var cone1 = Helpers.AngleToVectorDegrees(this.HostObj.Rotation + (fov * 0.5f), _radius);
            var cone2 = Helpers.AngleToVectorDegrees(this.HostObj.Rotation - (fov * 0.5f), _radius);

            gfx.DrawLine(this.Position, this.Position + cone1, color);
            gfx.DrawLine(this.Position, this.Position + cone2, color);

            gfx.DrawLine(this.Position, this.Position + centerLine, color, 1f, D2DDashStyle.DashDot);

        }

        private PingObj? FindMostCentered()
        {
            float minFov = float.MaxValue;
            PingObj minFovObj = null;

            foreach (var p in _pings)
            {
                if (p.Obj is not Plane plane)
                    continue;

                if (plane.IsDamaged)
                    continue;

                var fov = this.HostObj.FOVToObject(p.Obj);
                if (fov < minFov && fov <= (World.SENSOR_FOV * 0.5f))
                {
                    minFov = fov;
                    minFovObj = p;
                }
            }

            return minFovObj;
        }

        private bool IsInFOV(GameObject obj, float sweepAngle, float fov)
        {
            var dir = obj.Position - this.HostObj.Position;

            var angle = dir.Angle(true);
            var diff = Helpers.AngleDiff(sweepAngle, angle);

            return diff <= (fov * 0.5f);
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
