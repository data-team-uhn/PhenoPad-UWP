using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace WobbrockLib
{
    /// <summary>
    /// A structure representing a two-dimensional point with double-precision floating-point values.
    /// </summary>
    public struct PointR
    {
        public static readonly PointR Empty;
        private double _x;
        private double _y;

        /// <summary>
        /// Constructs a two-dimensional point from its (x,y) components.
        /// </summary>
        /// <param name="x">The x-coordinate of this point.</param>
        /// <param name="y">The y-coordinate of this point.</param>
        public PointR(double x, double y)
        {
            _x = x;
            _y = y;
        }

        /// <summary>
        /// Copy constructor for a point.
        /// </summary>
        /// <param name="pt">The point to copy.</param>
        public PointR(PointR pt)
        {
            _x = pt._x;
            _y = pt._y;
        }

        /// <summary>
        /// Creates a string representation of this point, following System.Drawing.Point.
        /// </summary>
        /// <returns>A string representation of this point.</returns>
        public override string ToString()
        {
            return String.Format("{{X={0}, Y={1}}}", _x, _y);
        }

        /// <summary>
        /// Constructs a point based on a string representation.
        /// </summary>
        /// <param name="s">The string representation of the point.</param>
        /// <returns>A PointR based on the given string.</returns>
        public static PointR FromString(string s)
        {
            PointR pt = PointR.Empty;
            try
            {
                int xpos = s.IndexOf('X'); // "X"
                int comma = s.IndexOf(',', xpos + 2);
                double x = double.Parse(s.Substring(xpos + 2, comma - (xpos + 2)));

                int ypos = s.IndexOf('Y'); // "Y"
                comma = s.IndexOf('}', ypos + 2);
                double y = double.Parse(s.Substring(ypos + 2, comma - (ypos + 2)));

                pt = new PointR(x, y);
            }
            catch { }
            return pt;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of this point.
        /// </summary>
        public double X
        {
            get { return _x; }
            set { _x = value; }
        }

        /// <summary>
        /// Gets or sets the y-coordinate of this point.
        /// </summary>
        public double Y
        {
            get { return _y; }
            set { _y = value; }
        }

        /// <summary>
        /// Explicit cast from a PointR to a PointF.
        /// </summary>
        /// <param name="pt">The PointR to cast.</param>
        /// <returns>A PointF that is a PointR truncated from double-precision to single-precision.</returns>
        public static explicit operator PointF(PointR pt)
        {
            return new PointF((float) pt._x, (float) pt._y);
        }

        /// <summary>
        /// Implicit cast from a PointF to a PointR.
        /// </summary>
        /// <param name="pt">The PointF to cast.</param>
        /// <returns>A PointR that is a PointF extended from single-precision to double-precision.</returns>
        public static implicit operator PointR(PointF pt)
        {
            return new PointR(pt.X, pt.Y);
        }

        /// <summary>
        /// Convert a list of PointR's to a list of PointF's.
        /// </summary>
        /// <param name="pts">The PointR's to convert.</param>
        /// <returns>A list of PointF's that have the same spatial components as the given PointR's,
        /// truncated from double-precision to single-precision floating-point values.</returns>
        public static List<PointF> ConvertList(List<PointR> pts)
        {
            List<PointF> list = new List<PointF>(pts.Count);
            foreach (PointR pt in pts)
            {
                list.Add((PointF) pt); // explicit conversion
            }
            return list;
        }

        /// <summary>
        /// Convert a list of PointF's to a list of PointR's.
        /// </summary>
        /// <param name="pts">The PointF's to convert.</param>
        /// <returns>A list of PointR's that have the same spatial components as the given PointF's.</returns>
        public static List<PointR> ConvertList(List<PointF> pts)
        {
            List<PointR> list = new List<PointR>(pts.Count);
            foreach (PointF pt in pts)
            {
                list.Add(pt); // implicit conversion
            }
            return list;
        }

        /// <summary>
        /// Equality operator for PointR. 
        /// </summary>
        /// <param name="p1">The lefthand side of the operator.</param>
        /// <param name="p2">The righthand side of the operator.</param>
        /// <returns>True if the X and Y components of these points are equal; false otherwise.</returns>
        public static bool operator ==(PointR p1, PointR p2)
        {
            return (p1._x == p2._x && p1._y == p2._y);
        }

        /// <summary>
        /// Not-equal operator for PointR.
        /// </summary>
        /// <param name="p1">The lefthand side of the operator.</param>
        /// <param name="p2">The righthand side of the operator.</param>
        /// <returns>True if the X or Y components of the points are unequal; false otherwise.</returns>
        public static bool operator !=(PointR p1, PointR p2)
        {
            return (p1._x != p2._x || p1._y != p2._y);
        }

        /// <summary>
        /// Tests whether the given object is equal to this point.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a point equal to this point; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PointR)
            {
                PointR pt = (PointR) obj;
                return (this.X == pt.X && this.Y == pt.Y);
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this point.
        /// </summary>
        /// <returns>A hashcode for this point.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
