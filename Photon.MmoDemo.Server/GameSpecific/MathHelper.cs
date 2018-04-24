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

            // line segment circle intersection
        public static bool Intersects(Vector segA, Vector segB, Vector circlePos, float radius2)
        {
            Vector segV = segB - segA;
            Vector ptV = circlePos - segA;
            float segVLen = segV.Len;

            // closest point on segment
            float proj = Vector.Dot(ptV, segV / segVLen);

            Vector projV = (segV / segVLen) * proj;
            Vector closest = segA + projV;

            Vector distV = circlePos - closest;
            if (distV.Len2 < radius2)
                return true;
            return false;

            
        }
    }
}
