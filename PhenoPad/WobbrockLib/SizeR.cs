using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace WobbrockLib
{
    /// <summary>
    /// A structure representing a two-dimensional size with double-precision floating-point values.
    /// </summary>
    public struct SizeR
    {
        public static readonly SizeR Empty;
        private double _width;
        private double _height;

        /// <summary>
        /// Constructs a size with the given width and height.
        /// </summary>
        /// <param name="width">The width component of this size.</param>
        /// <param name="height">The height component of this size.</param>
        public SizeR(double width, double height)
        {
            _width = width;
            _height = height;
        }

        /// <summary>
        /// Copy constructor for a size.
        /// </summary>
        /// <param name="sz">The size to copy.</param>
        public SizeR(SizeR sz)
        {
            _width = sz._width;
            _height = sz._height;
        }

        /// <summary>
        /// Creates a string representation of this size, following System.Drawing.Size.
        /// </summary>
        /// <returns>The string representation of this size.</returns>
        public override string ToString()
        {
            return String.Format("{{Width={0}, Height={1}}}", _width, _height);
        }

        /// <summary>
        /// Constructs a size from the given string representation.
        /// </summary>
        /// <param name="s">The string representation of a size.</param>
        /// <returns>The SizeR instance representing the given string.</returns>
        public static SizeR FromString(string s)
        {
            SizeR sz = SizeR.Empty;
            try
            {
                int wpos = s.IndexOf('W'); // "W"idth
                int comma = s.IndexOf(',', wpos + 6);
                double width = double.Parse(s.Substring(wpos + 6, comma - (wpos + 6)));

                int hpos = s.IndexOf('H'); // "H"eight
                comma = s.IndexOf('}', hpos + 7);
                double height = double.Parse(s.Substring(hpos + 7, comma - (hpos + 7)));

                sz = new SizeR(width, height);
            }
            catch { }
            return sz;
        }

        /// <summary>
        /// Gets or sets the width component of this size.
        /// </summary>
        public double Width
        {
            get { return _width; }
            set { _width = value; }
        }

        /// <summary>
        /// Gets or sets the height component of this size.
        /// </summary>
        public double Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>
        /// Explicit cast from a SizeR to a SizeF.
        /// </summary>
        /// <param name="sz">The SizeR to cast.</param>
        /// <returns>The SizeF version of a SizeR truncated from double-precision to single-precision.</returns>
        public static explicit operator SizeF(SizeR sz)
        {
            return new SizeF((float) sz._width, (float) sz._height);
        }

        /// <summary>
        /// Implicit cast from a SizeF to a SizeR.
        /// </summary>
        /// <param name="sz">The SizeF to cast.</param>
        /// <returns>A SizeR version of a SizeF extended from single-precision to double-precision.</returns>
        public static implicit operator SizeR(SizeF sz)
        {
            return new SizeR(sz.Width, sz.Height);
        }

        /// <summary>
        /// Equals operator for SizeR.
        /// </summary>
        /// <param name="z1">The lefthand side of the operator.</param>
        /// <param name="z2">The righthand side of the operator.</param>
        /// <returns>True if the width and height of the sizes agree; false otherwise.</returns>
        public static bool operator ==(SizeR z1, SizeR z2)
        {
            return (z1._width == z2._width && z1._height == z2._height);
        }

        /// <summary>
        /// Not-equals operator for SizeR.
        /// </summary>
        /// <param name="z1">The lefthand side of the operator.</param>
        /// <param name="z2">The righthand side of the operator.</param>
        /// <returns>True if the width or height of the sizes disagree; false otherwise.</returns>
        public static bool operator !=(SizeR z1, SizeR z2)
        {
            return (z1._width != z2._width || z1._height != z2._height);
        }

        /// <summary>
        /// Tests whether the given object is equal to this size.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a size equal to this size; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is SizeR)
            {
                SizeR sz = (SizeR) obj;
                return (this.Width == sz.Width && this.Height == sz.Height);
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this size.
        /// </summary>
        /// <returns>A hashcode for this size.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
