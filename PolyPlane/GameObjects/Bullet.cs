﻿using PolyPlane.GameObjects.Tools;
using PolyPlane.Helpers;
using PolyPlane.Rendering;
using unvell.D2DLib;

namespace PolyPlane.GameObjects
{
    public sealed class Bullet : GameObjectPoly, ILightMapContributor
    {
        public const float SPEED = 800f;
        public float Lifetime = 10f;

        private static readonly D2DColor _lightMapColor = new D2DColor(1f, 1f, 0.98f, 0.54f);

        private static readonly D2DPoint[] _poly = new D2DPoint[]
        {
            new D2DPoint(7,0),
            new D2DPoint(4,-3),
            new D2DPoint(0,-4),
            new D2DPoint(-8,-4),
            new D2DPoint(-8,4),
            new D2DPoint(0,4),
            new D2DPoint(4,3),
        };

        public Bullet() : base()
        {
            this.Polygon = new RenderPoly(this, _poly);
            this.RenderOrder = 4;
            this.Flags = GameObjectFlags.SpatialGrid;
        }

        public Bullet(FighterPlane plane) : this()
        {
            this.ObjectID = World.GetNextObjectId();
            this.PlayerID = plane.PlayerID;
            this.IsExpired = false;
            this.IsNetObject = false;
            this.Age = 0f;

            this.Position = plane.GunPosition;
            this.Rotation = plane.Rotation;
            this.Owner = plane;

            var velo = (Utilities.AngleToVectorDegrees(plane.Rotation, Bullet.SPEED));
            this.Velocity = velo + Utilities.PointVelocity(plane, plane.Gun.Position);

            this.Rotation = this.Velocity.Angle();
            this.Polygon.Update();
        }

        public Bullet(D2DPoint pos, D2DPoint velo, float rotation) : this()
        {
            this.IsExpired = false;
            this.IsNetObject = true;
            this.Age = 0f;

            this.Position = pos;
            this.Velocity = velo;
            this.Rotation = rotation;

            this.Polygon.Update();
        }

        public override void DoUpdate(float dt)
        {
            base.DoUpdate(dt);

            if (this.Age >= Lifetime)
                this.IsExpired = true;
        }

        public override void Render(RenderContext ctx)
        {
            base.Render(ctx);

            ctx.DrawPolygon(this.Polygon, D2DColor.Black, 1f, D2DColor.Yellow);
        }

        float ILightMapContributor.GetLightRadius()
        {
            const float LIGHT_RADIUS = 350f;

            return LIGHT_RADIUS;
        }

        float ILightMapContributor.GetIntensityFactor()
        {
            return 1.3f;
        }

        bool ILightMapContributor.IsLightEnabled()
        {
            return !this.IsExpired;
        }

        D2DPoint ILightMapContributor.GetLightPosition()
        {
            return this.Position;
        }

        D2DColor ILightMapContributor.GetLightColor()
        {
            return _lightMapColor;
        }
    }
}
