using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Photon.MmoDemo.Common;

namespace Photon.MmoDemo.Server.GameSpecific
{
    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    public class MathHelper
    {
        //public static bool Intersects(Vector circlePos, float circleRad, Rect rect)
        //{
        //    Vector circleDistance = new Vector();
        //    circleDistance.X = Math.Abs(circlePos.X - rect.x);
        //    circleDistance.Y = Math.Abs(circlePos.Y - rect.y);

        //    if (circleDistance.X > (rect.width / 2 + circleRad)) { return false; }
        //    if (circleDistance.Y > (rect.height / 2 + circleRad)) { return false; }

        //    if (circleDistance.X <= (rect.width / 2)) { return true; }
        //    if (circleDistance.Y <= (rect.height / 2)) { return true; }

        //    float cornerDistance_sq = (circleDistance.X - rect.width / 2) ^ 2 +
        //                         (circleDistance.Y - rect.height / 2) ^ 2;

        //    return (cornerDistance_sq <= (circleRad ^ 2));
        //}

        // line segment circle intersection segment length can be passed and and this sped up by a lot.
        public static bool Intersects(Vector segA, Vector segB, Vector circlePos, float radius2, ref Vector offset,
            float radius, float? segmentLength, bool returnOFfset)
        {
            Vector segV = segB - segA;
            Vector ptV = circlePos - segA;

            float segVLen;
            if (segmentLength != null)
                segVLen = (float)segmentLength;
            else
                segVLen = segV.Len;

            Vector segVunit = segV / segVLen;
            // closest point on segment
            float proj = Vector.Dot(ptV, segVunit);
            Vector closest;
            Vector projV;
            if (proj <= 0)
                closest = segA;
            else if (proj >= segVLen)
                closest = segB;
            else
            {
                projV = segVunit * proj;
                closest = projV + segA;
            }

            Vector distV = circlePos - closest;

            float distVLen2 = distV.Len2;
            if (distVLen2 < radius2)
            {
                if (returnOFfset)
                {
                    float distVLen = (float)Math.Sqrt(distVLen2);
                    offset =  ( distV  / distVLen) * (radius - distVLen); 
                }
                return true;
            }
            return false;

            
        }

        // segment length can be passed and and this sped up by a lot.
        public static Vector ClosestPointOnSeg(Vector segA, Vector segB, Vector circlePos)
        {
            Vector segV = segB - segA;
            Vector ptV = circlePos - segA;
            float segVLen = segV.Len;

            Vector segVunit = segV / segVLen;
            // closest point on segment
            float proj = Vector.Dot(ptV, segVunit);
            if (proj <= 0)
                return segA;
            if (proj >= segVLen)
                return segB;

            Vector projV = segVunit * proj;
            Vector closest = projV + segA;
            return closest;
        }
    }
}
