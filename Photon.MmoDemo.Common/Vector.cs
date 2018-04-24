// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Vector.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The 3D floating point vector.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace Photon.MmoDemo.Common
{
    /// <summary>
    /// The 3D floating point vector.
    /// </summary>
    public struct Vector
    {
        public const float TOLERANCE = 0.000001f;

        public static Vector Zero;

        public float X { get; set; }

        public float Y { get; set; }

        public Vector(float x, float y) : this()
        {
            X = x;
            Y = y;
        }

        public Vector(Vector v) : this()
        {
            X = v.X;
            Y = v.Y;
        }

        public static Vector operator +(Vector a, Vector b)
        {
            return new Vector {X = a.X + b.X, Y = a.Y + b.Y};
        }

        public static Vector operator /(Vector a, int b)
        {
            return new Vector {X = a.X/b, Y = a.Y/b};
        }

        public static Vector operator /(Vector a, float b)
        {
            return new Vector { X = a.X / b, Y = a.Y / b };
        }

        public static Vector operator *(Vector a, float b)
        {
            return new Vector {X = a.X*b, Y = a.Y*b};
        }

        public static Vector operator *(Vector a, int b)
        {
            return new Vector {X = a.X*b, Y = a.Y*b};
        }

        public static Vector operator -(Vector a, Vector b)
        {
            return new Vector {X = a.X - b.X, Y = a.Y - b.Y};
        }

        public static Vector operator -(Vector a)
        {
            return new Vector {X = -a.X, Y = -a.Y};
        }

        public static Vector Max(Vector value1, Vector value2)
        {
            return new Vector { X = Math.Max(value1.X, value2.X), Y = Math.Max(value1.Y, value2.Y) };
        }

        public static Vector Min(Vector value1, Vector value2)
        {
            return new Vector {X = Math.Min(value1.X, value2.X), Y = Math.Min(value1.Y, value2.Y)};
        }

        public static float Dot(Vector lhs, Vector rhs)
        {
            return (float)((double)lhs.X * (double)rhs.X + (double)lhs.Y * (double)rhs.Y);
        }
        public static Vector Normalized(Vector vec)
        {
            float len = (float)Math.Sqrt(vec.Len2);
            if (len != 0)
            {
                vec.X = vec.X / len;
                vec.Y = vec.Y / len;
            }
            return vec;
        }

        public override string ToString()
        {
            return string.Format("{0}({1:0.00}, {2:0.00})", "V", X, Y);
        }

        public bool IsZero
        {
            get { return Math.Abs(this.X) < TOLERANCE && Math.Abs(this.Y) < TOLERANCE; }
        }

        public float Len2
        {
            get { return X*X + Y*Y; }
        }

 
        public float Len
        {
            get { return (float)Math.Sqrt(X * X + Y * Y); }
        }
    }
}