using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace WobbrockLib
{
    /// <summary>
    /// 
    /// </summary>
    public struct TimePointR
    {
        public static readonly TimePointR Empty;
        private double _x;
        private double _y;
        private long _t;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="ms"></param>
        public TimePointR(double x, double y, long ms)
        {
            _x = x;
            _y = y;
            _t = ms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="ms"></param>
        public TimePointR(PointR pt, long ms)
        {
            _x = pt.X;
            _y = pt.Y;
            _t = ms;
        }

        /// <summary>
        /// Copy constructor for a timepoint.
        /// </summary>
        /// <param name="pt">The point to copy.</param>
        public TimePointR(TimePointR pt)
        {
            _x = pt.X;
            _y = pt.Y;
            _t = pt.Time;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{{X={0}, Y={1}, Time={2}}}", _x, _y, _t);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static TimePointR FromString(string s)
        {
            TimePointR tp = TimePointR.Empty;
            try
            {
                int xpos = s.IndexOf('X'); // "X"
                int comma = s.IndexOf(',', xpos + 2);
                double x = double.Parse(s.Substring(xpos + 2, comma - (xpos + 2)));

                int ypos = s.IndexOf('Y'); // "Y"
                comma = s.IndexOf(',', ypos + 2);
                double y = double.Parse(s.Substring(ypos + 2, comma - (ypos + 2)));

                int tpos = s.IndexOf('T'); // "T"ime
                comma = s.IndexOf('}', tpos + 5);
                long t = long.Parse(s.Substring(tpos + 5, comma - (tpos + 5)));

                tp = new TimePointR(x, y, t);
            }
            catch { }
            return tp;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of this timepoint.
        /// </summary>
        public double X
        {
            get { return _x; }
            set { _x = value; }
        }

        /// <summary>
        /// Gets or sets the y-coordinate of this timepoint.
        /// </summary>
        public double Y
        {
            get { return _y; }
            set { _y = value; }
        }

        /// <summary>
        /// Gets or sets the timestamp of this timepoint, in milliseconds.
        /// </summary>
        public long Time
        {
            get { return _t; }
            set { _t = value; }
        }

        /// <summary>
        /// Explicit cast from a TimePointR to a PointR.
        /// </summary>
        /// <param name="pt">The TimePointR to cast.</param>
        /// <returns>A PointR that represents the spatial components of the TimePointR. The timestamp is discarded.</returns>
        public static explicit operator PointR(TimePointR pt)
        {
            return new PointR(pt.X, pt.Y);
        }

        /// <summary>
        /// Implicit cast from a PointR to a TimePointR.
        /// </summary>
        /// <param name="pt">The PointR to cast.</param>
        /// <returns>A TimePointR that takes its spatial components from the PointR. The timestamp is 
        /// initialized to zero.</returns>
        public static implicit operator TimePointR(PointR pt)
        {
            return new TimePointR(pt.X, pt.Y, 0L);
        }

        /// <summary>
        /// Explicit cast from a TimePOintR to a PointF.
        /// </summary>
        /// <param name="pt">The TimePointR to cast.</param>
        /// <returns>A PointF that represents the spatial components of the TimePointR, truncated from double-precision
        /// to single-precision floating point values. The timestamp is discarded.</returns>
        public static explicit operator PointF(TimePointR pt)
        {
            return new PointF(
                (float) pt.X,
                (float) pt.Y
                );
        }

        /// <summary>
        /// Implicit cast from a PointF to a TimePointR.
        /// </summary>
        /// <param name="pt">The PointF to cast.</param>
        /// <returns>A TimePointR that takes its spatial components from the PointF. The timestamp is 
        /// initialized to zero.</returns>
        public static implicit operator TimePointR(PointF pt)
        {
            return new TimePointR(pt.X, pt.Y, 0L);
        }

        /// <summary>
        /// Convert a list of TimePointR's to a list of PointR's.
        /// </summary>
        /// <param name="pts">The TimePointR's to convert.</param>
        /// <returns>A list of PointR's that have the same spatial components as the given TimePointR's.
        /// All timestamps are discarded.</returns>
        public static List<PointR> ConvertList(List<TimePointR> pts)
        {
            List<PointR> list = new List<PointR>(pts.Count);
            foreach (TimePointR pt in pts)
            {
                list.Add((PointR) pt); // explicit conversion
            }
            return list;
        }

        /// <summary>
        /// Convert a list of PointR's to a list of TimePointR's.
        /// </summary>
        /// <param name="pts">The PointR's to convert.</param>
        /// <returns>A list of TimePointR's that have the same spatial components as the given PointR's.
        /// All timestamps are initialized to zero.</returns>
        public static List<TimePointR> ConvertList(List<PointR> pts)
        {
            List<TimePointR> list = new List<TimePointR>(pts.Count);
            foreach (PointR pt in pts)
            {
                list.Add(pt); // implicit conversion
            }
            return list;
        }

        /// <summary>
        /// Equals operator for TimePointR.
        /// </summary>
        /// <param name="tp1">The lefthand side of the operator.</param>
        /// <param name="tp2">The righthand side of the operator.</param>
        /// <returns>True if the spatial and temporal components of the timepoints agree; false otherwise.</returns>
        /// <remarks>To compare just the spatial components ignoring timestamps, use EqualsIgnoreTime.</remarks>
        public static bool operator ==(TimePointR tp1, TimePointR tp2)
        {
            return (tp1.X == tp2.X && tp1.Y == tp2.Y && tp1.Time == tp2.Time);
        }

        /// <summary>
        /// Not-equals operator for TimePointR.
        /// </summary>
        /// <param name="tp1">The lefthand side of the operator.</param>
        /// <param name="tp2">The righthand side of the operator.</param>
        /// <returns>True if the spatial or temporal components of the timepoints disagree; false otherwise.</returns>
        /// <remarks>To compare just the spatial components ignoring timestamps, use EqualsIgnoreTime.</remarks>
        public static bool operator !=(TimePointR tp1, TimePointR tp2)
        {
            return (tp1.X != tp2.X || tp1.Y != tp2.Y || tp1.Time != tp2.Time);
        }

        /// <summary>
        /// Tests whether the given timepoint's spatial coordinates are the same as this timepoint.
        /// Timestamps are ignored for this comparison.
        /// </summary>
        /// <param name="tp">The timestamp to test.</param>
        /// <returns>True if the spatial components of these timestamps are equal; false otherwise.</returns>
        public bool EqualsIgnoreTime(TimePointR tp)
        {
            return (this.X == tp.X && this.Y == tp.Y);
        }

        /// <summary>
        /// Tests whether the given object is equal to this range.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a timepoint equal to this timepoint; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is TimePointR)
            {
                TimePointR tp = (TimePointR) obj;
                return (this.X == tp.X && this.Y == tp.Y && this.Time == tp.Time);
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this timepoint.
        /// </summary>
        /// <returns>A hashcode for this timepoint.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

    }
}
