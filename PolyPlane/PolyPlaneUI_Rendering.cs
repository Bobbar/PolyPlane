using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane
{
    public partial class PolyPlaneUI : Form
    {
        private void DrawPlaneAndObjects(RenderContext ctx, Plane plane)
        {
            ctx.Gfx.PushTransform();

            var zAmt = World.ZoomScale;
            var pos = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            pos *= zAmt;

            var offset = new D2DPoint(-plane.Position.X, -plane.Position.Y);
            offset *= zAmt;

            ctx.Gfx.ScaleTransform(VIEW_SCALE, VIEW_SCALE, plane.Position);
            ctx.Gfx.TranslateTransform(offset.X, offset.Y);
            ctx.Gfx.TranslateTransform(pos.X, pos.Y);

            var viewPortRect = new D2DRect(plane.Position, new D2DSize((World.ViewPortSize.width / VIEW_SCALE), World.ViewPortSize.height / VIEW_SCALE));
            ctx.PushViewPort(viewPortRect);

            // Draw the ground.
            ctx.Gfx.FillRectangle(new D2DRect(new D2DPoint(plane.Position.X, 2000f), new D2DSize(this.Width * World.ViewPortScaleMulti, 4000f)), D2DColor.DarkGreen);

            _decoys.ForEach(o => o.Render(ctx));
            _missiles.ForEach(o => o.Render(ctx));
            _missileTrails.ForEach(o => o.Render(ctx));

            _planes.ForEach(o =>
            {
                if (o is Plane tplane && !tplane.ID.Equals(plane.ID))
                {
                    o.Render(ctx);
                    ctx.Gfx.DrawEllipse(new D2DEllipse(tplane.Position, new D2DSize(80f, 80f)), _hudColor, 2f);
                }
            });

            plane.Render(ctx);

            _bullets.ForEach(o => o.Render(ctx));
            _explosions.ForEach(o => o.Render(ctx));

            //DrawNearObj(_ctx.Gfx, plane);

            ctx.PopViewPort();
            ctx.Gfx.PopTransform();
        }

        private void DrawHud(RenderContext ctx, D2DSize viewportsize, Plane viewPlane)
        {
            DrawAltimeter(ctx.Gfx, viewportsize, viewPlane);
            DrawSpeedo(ctx.Gfx, viewportsize, viewPlane);
            DrawGMeter(ctx.Gfx, viewportsize, viewPlane);
            DrawThrottle(ctx.Gfx, viewportsize, viewPlane);
            DrawStats(ctx.Gfx, viewportsize, viewPlane);

            if (!viewPlane.IsDamaged)
            {
                if (viewPlane.IsAI == false)
                {
                    DrawGuideIcon(ctx.Gfx, viewportsize);
                }

                DrawHudMessage(ctx.Gfx, viewportsize);
                DrawPlanePointers(ctx.Gfx, viewportsize, viewPlane);
                DrawMissilePointers(ctx.Gfx, viewportsize, viewPlane);
            }

            DrawRadar(ctx, viewportsize, viewPlane);

        }

        private void DrawGuideIcon(D2DGraphics gfx, D2DSize viewportsize)
        {
            const float DIST = 300f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            var mouseAngle = _guideAngle;
            var mouseVec = Helpers.AngleToVectorDegrees(mouseAngle, DIST);
            gfx.DrawEllipse(new D2DEllipse(pos + mouseVec, new D2DSize(5f, 5f)), _hudColor, 2f);

            var planeAngle = _playerPlane.Rotation;
            var planeVec = Helpers.AngleToVectorDegrees(planeAngle, DIST);
            gfx.DrawCrosshair(pos + planeVec, 2f, _hudColor, 5f, 20f);
        }

        private void DrawHudMessage(D2DGraphics gfx, D2DSize viewportsize)
        {
            if (_hudMessageTimeout.IsRunning && !string.IsNullOrEmpty(_hudMessage))
            {
                var pos = new D2DPoint(viewportsize.width * 0.5f, 200f);
                var rect = new D2DRect(pos, new D2DSize(250, 50));
                gfx.FillRectangle(rect, D2DColor.Gray);
                gfx.DrawTextCenter(_hudMessage, _hudMessageColor, _defaultFontName, 40f, rect);
            }

            if (!_hudMessageTimeout.IsRunning)
                _hudMessage = string.Empty;
        }

        private void DrawThrottle(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 80f;
            const float yPos = 80f;
            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));

            gfx.PushTransform();

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawTextCenter("THR", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));

            var throtRect = new D2DRect(pos.X - (W * 0.5f), pos.Y - (H * 0.5f), W, (H * plane.ThrustAmount));
            gfx.RotateTransform(180f, pos);
            gfx.FillRectangle(throtRect, _hudColor);

            gfx.PopTransform();
        }

        private void DrawStats(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float W = 20f;
            const float H = 50f;
            const float xPos = 80f;
            const float yPos = 110f;
            var pos = new D2DPoint(xPos, (viewportsize.height * 0.5f) + yPos);

            var rect = new D2DRect(pos, new D2DSize(W, H));

            gfx.PushTransform();

            gfx.DrawTextCenter($"{plane.Hits}/{Plane.MAX_HITS}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 40f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"{plane.NumMissiles}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 70f), new D2DSize(50f, 20f)));
            gfx.DrawTextCenter($"{plane.NumBullets}", _hudColor, _defaultFontName, 15f, new D2DRect(pos + new D2DPoint(0, 100f), new D2DSize(50f, 20f)));

            gfx.PopTransform();
        }

        private void DrawGMeter(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float xPos = 80f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(50, 20));

            gfx.DrawText($"G {Math.Round(plane.GForce, 1)}", _hudColor, _defaultFontName, 15f, rect);
        }

        private void DrawAltimeter(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_ALT = 3000f;
            const float W = 80f;
            const float H = 400f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 100f;
            const float xPos = 200f;
            var pos = new D2DPoint(viewportsize.width - xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var alt = plane.Altitude;
            var startAlt = alt - (alt % MARKER_STEP) + MARKER_STEP;
            var altWarningColor = new D2DColor(0.2f, D2DColor.Red);

            var highestAlt = startAlt + MARKER_STEP;
            var lowestAlt = startAlt - (MARKER_STEP * 2f);

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            if (highestAlt <= MIN_ALT || lowestAlt <= MIN_ALT)
            {
                var s = new D2DPoint(pos.X - HalfW, (pos.Y + (alt - MIN_ALT)));

                if (s.Y < pos.Y - HalfH)
                    s.Y = pos.Y - HalfH;

                gfx.FillRectangle(new D2DRect(s.X, s.Y, W, (pos.Y + (H * 0.5f)) - s.Y), altWarningColor);
            }

            for (float y = 0; y < H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var start = new D2DPoint(pos.X - HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (alt % MARKER_STEP));
                    var end = new D2DPoint(pos.X + HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (alt % MARKER_STEP));

                    var div = y / MARKER_STEP;
                    var altMarker = startAlt + (-HalfH + (div * MARKER_STEP));

                    gfx.DrawLine(start, end, _hudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawTextCenter(altMarker.ToString(), _hudColor, _defaultFontName, 15f, textRect);
                }
            }

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(alt, 0).ToString(), _hudColor, _defaultFontName, 15f, actualRect);
        }


        private void DrawSpeedo(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float W = 80f;
            const float H = 400f;
            const float HalfW = W * 0.5f;
            const float HalfH = H * 0.5f;
            const float MARKER_STEP = 50f;//100f;
            const float xPos = 200f;
            var pos = new D2DPoint(xPos, viewportsize.height * 0.5f);
            var rect = new D2DRect(pos, new D2DSize(W, H));
            var spd = plane.Velocity.Length();
            var startSpd = (spd) - (spd % (MARKER_STEP)) + MARKER_STEP;

            gfx.DrawRectangle(rect, _hudColor);
            gfx.DrawLine(new D2DPoint(pos.X - HalfW, pos.Y), new D2DPoint(pos.X + HalfW, pos.Y), D2DColor.GreenYellow, 1f, D2DDashStyle.Solid);

            for (float y = 0; y < H; y += MARKER_STEP)
            {
                if (y % MARKER_STEP == 0)
                {
                    var start = new D2DPoint(pos.X - HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));
                    var end = new D2DPoint(pos.X + HalfW, (pos.Y - y + HalfH - MARKER_STEP) + (spd % MARKER_STEP));

                    var div = y / MARKER_STEP;
                    var altMarker = startSpd + (-HalfH + (div * MARKER_STEP));

                    gfx.DrawLine(start, end, _hudColor, 1f, D2DDashStyle.Dash);
                    var textRect = new D2DRect(start - new D2DPoint(25f, 0f), new D2DSize(60f, 30f));
                    gfx.DrawTextCenter(altMarker.ToString(), _hudColor, _defaultFontName, 15f, textRect);
                }
            }

            var actualRect = new D2DRect(new D2DPoint(pos.X, pos.Y + HalfH + 20f), new D2DSize(60f, 20f));
            gfx.DrawTextCenter(Math.Round(spd, 0).ToString(), _hudColor, _defaultFontName, 15f, actualRect);
        }

        private void DrawPlanePointers(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_DIST = 600f;
            const float MAX_DIST = 6000f;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _planes.Count; i++)
            {
                var target = _planes[i];

                if (target == null)
                    continue;

                if (target.IsDamaged)
                    continue;

                var dist = D2DPoint.Distance(plane.Position, target.Position);

                if (dist < MIN_DIST || dist > MAX_DIST)
                    continue;

                var dir = target.Position - plane.Position;
                var angle = dir.Angle(true);
                var vec = Helpers.AngleToVectorDegrees(angle);

                if (plane.ClosingRate(target) > 0f)
                    gfx.DrawArrow(pos + (vec * 270f), pos + (vec * 250f), _hudColor, 2f);
                else
                    gfx.DrawArrow(pos + (vec * 250f), pos + (vec * 270f), _hudColor, 2f);
            }

            if (plane.Radar.HasLock)
            {
                var lockPos = pos + new D2DPoint(0f, -200f);
                var lRect = new D2DRect(lockPos, new D2DSize(120, 30));
                gfx.DrawTextCenter("LOCKED", _hudColor, _defaultFontName, 25f, lRect);

            }
        }

        private void DrawRadar(RenderContext ctx, D2DSize viewportsize, Plane plane)
        {
            var pos = new D2DPoint(viewportsize.width * 0.8f, viewportsize.height * 0.8f);
            plane.Radar.Position = pos;
            plane.Radar.Render(ctx);
        }

        private void DrawMissilePointers(D2DGraphics gfx, D2DSize viewportsize, Plane plane)
        {
            const float MIN_DIST = 3000f;
            const float MAX_DIST = 20000f;

            bool warningMessage = false;
            var pos = new D2DPoint(viewportsize.width * 0.5f, viewportsize.height * 0.5f);

            for (int i = 0; i < _missiles.Count; i++)
            {
                var missile = _missiles[i] as GuidedMissile;

                if (missile == null)
                    continue;

                if (missile.Owner.ID.Equals(plane.ID))
                    continue;

                if (!missile.Target.ID.Equals(plane.ID))
                    continue;

                var dist = D2DPoint.Distance(plane.Position, missile.Position);

                var dir = missile.Position - plane.Position;
                var angle = dir.Angle(true);
                var color = D2DColor.Red;
                var vec = Helpers.AngleToVectorDegrees(angle);
                var pos1 = pos + (vec * 200f);
                var pos2 = pos1 + (vec * 20f);
                var distFact = 1f - Helpers.Factor(dist, MIN_DIST * 10f);

                if (missile.IsDistracted)
                    color = D2DColor.Yellow;

                // Display warning if impact time is less than 10 seconds?
                const float MIN_IMPACT_TIME = 20f;
                if (MissileIsImpactThreat(plane, missile, MIN_IMPACT_TIME))
                    warningMessage = true;

                if (dist < MIN_DIST / 2f || dist > MAX_DIST)
                    continue;

                if (!missile.MissedTarget)
                    gfx.DrawArrow(pos1, pos2, color, (distFact * 30f) + 1f);
            }

            if (warningMessage)
            {
                var rect = new D2DRect(pos - new D2DPoint(0, -200), new D2DSize(120, 30));
                gfx.DrawTextCenter("MISSILE", D2DColor.Red, _defaultFontName, 30f, rect);
            }

            if (plane.HasRadarLock)
            {
                var lockRect = new D2DRect(pos - new D2DPoint(0, -160), new D2DSize(120, 30));
                gfx.DrawTextCenter("LOCK", D2DColor.Red, _defaultFontName, 30f, lockRect);

            }
        }

        private bool MissileIsImpactThreat(Plane plane, Missile missile, float minImpactTime)
        {
            var navigationTime = Helpers.ImpactTime(plane, missile);
            var closingRate = plane.ClosingRate(missile);

            // Is it going to hit soon, and has positive closing rate and is actively targeting us?
            return (navigationTime < minImpactTime && closingRate > 0f && missile.Target == plane);
        }

        private void DrawOverlays(RenderContext ctx)
        {
            if (_showInfo)
                DrawInfo(ctx.Gfx, _infoPosition);

            //var center = new D2DPoint(World.ViewPortSize.width * 0.5f, World.ViewPortSize.height * 0.5f);
            //var angVec = Helpers.AngleToVectorDegrees(_testAngle);
            //gfx.DrawLine(center, center + (angVec * 100f), D2DColor.Red);


            if (World.EnableTurbulence || World.EnableWind)
                DrawWindAndTurbulenceOverlay(ctx);


            if (_playerPlane.IsDamaged)
                ctx.Gfx.FillRectangle(World.ViewPortRect, new D2DColor(0.2f, D2DColor.Red));

            //DrawFPSGraph(ctx);
            //DrawGrid(gfx);

            //DrawRadial(ctx.Gfx, _radialPosition);
        }

        private void DrawFPSGraph(RenderContext ctx)
        {
            var pos = new D2DPoint(300, 300);
            _fpsGraph.Render(ctx.Gfx, pos, 1f);
        }


        private float _guideAngle = 0f;
        private void DrawRadial(D2DGraphics ctx, D2DPoint pos)
        {
            const float radius = 300f;
            const float step = 10f;

            float angle = 0f;

            while (angle < 360f)
            {
                var vec = Helpers.AngleToVectorDegrees(angle);
                vec = pos + (vec * radius);

                ctx.DrawLine(pos, vec, D2DColor.DarkGray, 1, D2DDashStyle.Dash);

                ctx.DrawText(angle.ToString(), D2DColor.White, _defaultFontName, 12f, new D2DRect(vec.X, vec.Y, 100f, 30f));

                angle += step;
            }

            ctx.DrawEllipse(new D2DEllipse(pos, new D2DSize(radius, radius)), D2DColor.White);


            float testDiff = 200f;
            float testFact = 0.6f;
            float angle1 = _guideAngle;
            float angle2 = _guideAngle + testDiff;

            ctx.DrawLine(pos, pos + Helpers.AngleToVectorDegrees(angle1) * (radius), D2DColor.Green);


            //        if (!_isPaused)
            //_testAngle = Helpers.ClampAngle(_testAngle + 1f);
        }

        private void DrawSky(RenderContext ctx, Plane viewPlane)
        {
            const float barH = 20f;
            const float MAX_ALT = 50000f;

            var plrAlt = Math.Abs(viewPlane.Position.Y);
            if (viewPlane.Position.Y >= 0)
                plrAlt = 0f;


            var color1 = new D2DColor(0.5f, D2DColor.SkyBlue);
            var color2 = new D2DColor(0.5f, D2DColor.Black);
            var rect = new D2DRect(new D2DPoint(this.Width * 0.5f, 0), new D2DSize(this.Width, barH));
            plrAlt += this.Height / 2f;

            for (float y = 0; y < this.Height; y += barH)
            {
                var posY = (plrAlt - y);
                var color = Helpers.LerpColor(color1, color2, (posY / MAX_ALT));

                rect.Y = y;
                ctx.Gfx.FillRectangle(rect, color);
            }
        }

        private void DrawMovingBackground(RenderContext ctx, Plane viewPlane)
        {
            float spacing = 75f;
            const float size = 4f;
            var d2dSz = new D2DSize(size, size);
            var color = new D2DColor(0.4f, D2DColor.Gray);

            var plrPos = viewPlane.Position;
            plrPos /= World.ViewPortScaleMulti;
            var roundPos = new D2DPoint((plrPos.X) % spacing, (plrPos.Y) % spacing);
            roundPos *= 4f;

            var rect = new D2DRect(0, 0, this.Width, this.Height);

            int hits = 0;
            int miss = 0;

            for (float x = 0 - (spacing * 3f); x < this.Width + roundPos.X; x += spacing)
            {
                for (float y = 0 - (spacing * 3f); y < this.Height + roundPos.Y; y += spacing)
                {
                    var pos = new D2DPoint(x, y);
                    pos -= roundPos;

                    if (rect.Contains(pos))
                    {
                        ctx.Gfx.FillRectangle(new D2DRect(pos, d2dSz), color);
                        hits++;
                    }
                    else
                        miss++;
                }
            }
        }

        private void DrawWindAndTurbulenceOverlay(RenderContext ctx)
        {
            var pos = new D2DPoint(this.Width - 100f, 100f);

            ctx.FillEllipse(new D2DEllipse(pos, new D2DSize(World.AirDensity * 10f, World.AirDensity * 10f)), D2DColor.SkyBlue);

            ctx.DrawLine(pos, pos + (World.Wind * 2f), D2DColor.White, 2f);
        }



        //private void DrawAIPlanesOverlay(RenderContext ctx)
        //{
        //    if (_aiPlaneViewIdx < 0 || _aiPlaneViewIdx > _aiPlanes.Count - 1)
        //        return;

        //    var plane = _aiPlanes[_aiPlaneViewIdx];

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(3000f, 3000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    ctx.Gfx.PushTransform();

        //    var offset = new D2DPoint(-plane.Position.X, -plane.Position.Y);
        //    offset *= zAmt;

        //    ctx.Gfx.ScaleTransform(scale, scale, plane.Position);
        //    ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //    ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //    var vp = new D2DRect(plane.Position, World.ViewPortSize);
        //    ctx.PushViewPort(vp);

        //    var test = vp.Contains(plane.Position);

        //    _targets.ForEach(t =>
        //    {
        //        if (t is Decoy d)
        //            d.Render(ctx);
        //    });

        //    _missiles.ForEach(m => m.Render(ctx));

        //    plane.Render(ctx);

        //    _flames.ForEach(f => f.Render(ctx));

        //    ctx.DrawText(plane.Altitude.ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 80), new D2DSize(100, 20)));
        //    ctx.DrawText(plane.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(plane.Position + new D2DPoint(20, 90), new D2DSize(100, 20)));

        //    ctx.PopViewPort();

        //    ctx.Gfx.PopTransform();
        //    ctx.Gfx.PopLayer();
        //}

        //private void DrawMissileTargetOverlays(RenderContext ctx)
        //{
        //    var plrMissiles = _missiles.Where(m => m.Owner.ID.Equals(_playerPlane.ID)).ToArray();
        //    if (plrMissiles.Length == 0)
        //        return;

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    for (int m = 0; m < plrMissiles.Length; m++)
        //    {
        //        var missile = plrMissiles[m] as GuidedMissile;
        //        var target = missile.Target as Plane;

        //        if (target == null)
        //            continue;

        //        if (!missile.Owner.ID.Equals(_playerPlane.ID))
        //            continue;

        //        ctx.Gfx.PushTransform();

        //        var offset = new D2DPoint(-target.Position.X, -target.Position.Y);
        //        offset *= zAmt;

        //        ctx.Gfx.ScaleTransform(scale, scale, target.Position);
        //        ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //        ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //        target.Render(ctx);

        //        //for (int t = 0; t < _targets.Count; t++)
        //        //    _targets[t].Render(gfx);

        //        //missile.Render(gfx);

        //        _targets.ForEach(t =>
        //        {
        //            if (t is Decoy d)
        //                d.Render(ctx);

        //        });

        //        var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

        //        //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
        //        ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
        //        ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

        //        ctx.Gfx.PopTransform();
        //    }

        //    ctx.Gfx.PopLayer();
        //}

        //private void DrawMissileOverlays(RenderContext ctx)
        //{
        //    var plrMissiles = _missiles.Where(m => m.Owner.ID.Equals(_playerPlane.ID)).ToArray();
        //    if (plrMissiles.Length == 0)
        //        return;

        //    var scale = 5f;
        //    var zAmt = World.ZoomScale;
        //    var pos = new D2DPoint(World.ViewPortSize.width * 0.85f, World.ViewPortSize.height * 0.20f);
        //    pos *= zAmt;

        //    ctx.Gfx.PushLayer(_missileOverlayLayer, new D2DRect(pos * World.ViewPortScaleMulti, new D2DSize(1000f, 1000f)));
        //    ctx.Gfx.Clear(_missileOverlayColor);

        //    for (int m = 0; m < plrMissiles.Length; m++)
        //    {
        //        var missile = plrMissiles[m] as GuidedMissile;

        //        if (!missile.Owner.ID.Equals(_playerPlane.ID))
        //            continue;


        //        var vp = new D2DRect(missile.Position, World.ViewPortSize);
        //        ctx.PushViewPort(vp);

        //        ctx.Gfx.PushTransform();

        //        var offset = new D2DPoint(-missile.Position.X, -missile.Position.Y);
        //        offset *= zAmt;

        //        ctx.Gfx.ScaleTransform(scale, scale, missile.Position);
        //        ctx.Gfx.TranslateTransform(offset.X, offset.Y);
        //        ctx.Gfx.TranslateTransform(pos.X, pos.Y);

        //        for (int t = 0; t < _targets.Count; t++)
        //            _targets[t].Render(ctx);

        //        missile.Render(ctx);

        //        var dist = D2DPoint.Distance(missile.Position, missile.Target.Position);

        //        //gfx.DrawText(missile.Velocity.Length().ToString(), D2DColor.White, _defaultFontName, 20f, new D2DRect(pos - new D2DPoint(0,0),new D2DSize(500,500)));
        //        ctx.DrawText(Math.Round(missile.Velocity.Length(), 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position + new D2DPoint(80, 80), new D2DSize(50, 20)));
        //        ctx.DrawText(Math.Round(dist, 1).ToString(), D2DColor.White, _defaultFontName, 10f, new D2DRect(missile.Position - new D2DPoint(60, -80), new D2DSize(50, 20)));

        //        ctx.PopViewPort();
        //        ctx.Gfx.PopTransform();
        //    }

        //    ctx.Gfx.PopLayer();
        //}

    }
}
