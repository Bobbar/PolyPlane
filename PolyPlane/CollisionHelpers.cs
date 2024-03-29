﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyPlane.GameObjects;
using unvell.D2DLib;

namespace PolyPlane
{
    /// <summary>
    /// Credit: https://www.habrador.com/tutorials/math/5-line-line-intersection/
    /// </summary>
    public static class CollisionHelpers
    {
        //Check if the lines are interesecting in 2d space

        public static bool IsIntersecting(D2DPoint a1, D2DPoint b1,  D2DPoint a2, D2DPoint b2, out D2DPoint pos)
        {
            Vector2 l1_start = new Vector2(a1.X, a1.Y);
            Vector2 l1_end = new Vector2(b1.X, b1.Y);

            Vector2 l2_start = new Vector2(a2.X, a2.Y);
            Vector2 l2_end = new Vector2(b2.X, b2.Y);

            //Direction of the lines
            Vector2 l1_dir = (l1_end - l1_start).Normalized();
            Vector2 l2_dir = (l2_end - l2_start).Normalized();

            //If we know the direction we can get the normal vector to each line
            Vector2 l1_normal = new Vector2(-l1_dir.Y, l1_dir.X);
            Vector2 l2_normal = new Vector2(-l2_dir.Y, l2_dir.X);


            //Step 1: Rewrite the lines to a general form: Ax + By = k1 and Cx + Dy = k2
            //The normal vector is the A, B
            float A = l1_normal.X;
            float B = l1_normal.Y;

            float C = l2_normal.X;
            float D = l2_normal.Y;

            //To get k we just use one point on the line
            float k1 = (A * l1_start.X) + (B * l1_start.Y);
            float k2 = (C * l2_start.X) + (D * l2_start.Y);

            //Step 2: are the lines parallel? -> no solutions
            if (IsParallel(l1_normal, l2_normal))
            {
                pos = D2DPoint.Zero;
                return false;
            }

            //Step 3: are the lines the same line? -> infinite amount of solutions
            //Pick one point on each line and test if the vector between the points is orthogonal to one of the normals
            if (IsOrthogonal(l1_start - l2_start, l1_normal))
            {
                //Return false anyway
                pos = D2DPoint.Zero;
                return false;
            }

            //Step 4: calculate the intersection point -> one solution
            float x_intersect = (D * k1 - B * k2) / (A * D - B * C);
            float y_intersect = (-C * k1 + A * k2) / (A * D - B * C);

            Vector2 intersectPoint = new Vector2(x_intersect, y_intersect);

            //Step 5: but we have line segments so we have to check if the intersection point is within the segment
            if (IsBetween(l1_start, l1_end, intersectPoint) && IsBetween(l2_start, l2_end, intersectPoint))
            {
                pos = intersectPoint;
                return true;
            }

            pos = D2DPoint.Zero;
            return false;
        }

        //Are 2 vectors parallel?
        public static bool IsParallel(Vector2 v1, Vector2 v2)
        {
            //2 vectors are parallel if the angle between the vectors are 0 or 180 degrees
            if (v1.AngleTo(v2) == 0f || v1.AngleTo(v2) == 180f)
            {
                return true;
            }

            return false;
        }

        //Are 2 vectors orthogonal?
        public static bool IsOrthogonal(Vector2 v1, Vector2 v2)
        {
            //2 vectors are orthogonal is the dot product is 0
            //We have to check if close to 0 because of floating numbers
            if (Math.Abs(Vector2.Dot(v1, v2)) < 0.000001f)
            {
                return true;
            }

            return false;
        }

        //Is a point c between 2 other points a and b?
        public static bool IsBetween(Vector2 a, Vector2 b, Vector2 c)
        {
            bool isBetween = false;

            //Entire line segment
            Vector2 ab = b - a;
            //The intersection and the first point
            Vector2 ac = c - a;

            //Need to check 2 things: 
            //1. If the vectors are pointing in the same direction = if the dot product is positive
            //2. If the length of the vector between the intersection and the first point is smaller than the entire line
            if (Vector2.Dot(ab, ac) > 0f && ab.LengthSquared() >= ac.LengthSquared())
            {
                isBetween = true;
            }

            return isBetween;
        }

        public static bool EllipseContains(D2DEllipse ellipse, float ellipseRotation, D2DPoint pos)
        {
            var mat = Matrix3x2.CreateRotation(-ellipseRotation * (float)(Math.PI / 180f), ellipse.origin);
            var transPos = D2DPoint.Transform(pos, mat);

            var p = (Math.Pow(transPos.X - ellipse.origin.X, 2f) / Math.Pow(ellipse.radiusX, 2f)) + (Math.Pow(transPos.Y - ellipse.origin.Y, 2f) / Math.Pow(ellipse.radiusY, 2f));

            return p <= 1f;
        }

    }
}
