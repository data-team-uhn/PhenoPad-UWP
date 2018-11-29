using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace WobbrockLib
{
    /// <summary>
    /// A structure representing a rectangle with double-precision left, top, width, and height components.
    /// </summary>
    public struct RectangleR
    {
        public static readonly RectangleR Empty;
        private double _left;
        private double _top;
        private double _width;
        private double _height;

        /// <summary>
        /// Constructs a rectangle from a topleft location and a size.
        /// </summary>
        /// <param name="location">The topleft corner of the rectangle.</param>
        /// <param name="size">The size of the rectangle.</param>
        public RectangleR(PointR location, SizeR size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
            // do nothing
        }

        /// <summary>
        /// Constructs a rectangle from its four components.
        /// </summary>
        /// <param name="left">The left edge of the rectangle.</param>
        /// <param name="top">The top edge of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public RectangleR(double left, double top, double width, double height)
        {
            _left = left;
            _top = top;
            _width = width;
            _height = height;
        }

        /// <summary>
        /// Copy constructor for a rectangle.
        /// </summary>
        /// <param name="r">The rectangle to copy.</param>
        public RectangleR(RectangleR r)
        {
            _left = r._left;
            _top = r._top;
            _width = r._width;
            _height = r._height;
        }

        /// <summary>
        /// Creates a string representation of this rectangle, following System.Drawing.Rectangle.
        /// </summary>
        /// <returns>A string representation of this rectangle.</returns>
        public override string ToString()
        {
            return String.Format("{{X={0}, Y={1}, Width={2}, Height={3}}}", _left, _top, _width, _height);
        }

        /// <summary>
        /// Constructs a rectangle from a given string representation.
        /// </summary>
        /// <param name="s">A string representation of a rectangle.</param>
        /// <returns>A RectangleR structure representing the given string.</returns>
        public static RectangleR FromString(string s)
        {
            RectangleR r = RectangleR.Empty;
            try
            {
                int Xpos = s.IndexOf('X'); // "X"
                int comma = s.IndexOf(',', Xpos + 2);
                double x = double.Parse(s.Substring(Xpos + 2, comma - (Xpos + 2)));

                int Ypos = s.IndexOf('Y'); // "Y"
                comma = s.IndexOf(',', Ypos + 2);
                double y = double.Parse(s.Substring(Ypos + 2, comma - (Ypos + 2)));

                int Wpos = s.IndexOf('W'); // "W"idth
                comma = s.IndexOf(',', Wpos + 6);
                double width = double.Parse(s.Substring(Wpos + 6, comma - (Wpos + 6)));

                int Hpos = s.IndexOf('H'); // "H"eight
                comma = s.IndexOf('}', Hpos + 7);
                double height = double.Parse(s.Substring(Hpos + 7, comma - (Hpos + 7)));

                r = new RectangleR(x, y, width, height);
            }
            catch { }
            return r;
        }

        /// <summary>
        /// Gets or sets the left edge of this rectangle.
        /// </summary>
        public double Left
        {
            get { return _left; }
            set { _left = value; }
        }

        /// <summary>
        /// Gets or sets the top edge of this rectangle.
        /// </summary>
        public double Top
        {
            get { return _top; }
            set { _top = value; }
        }

        /// <summary>
        /// Gets the X-coordinate of this rectangle, which is the
        /// same as its Left property.
        /// </summary>
        public double X
        {
            get { return this.Left; }
        }

        /// <summary>
        /// Gets the Y-coordinate of this rectangle, which is the
        /// same as its Top property.
        /// </summary>
        public double Y
        {
            get { return this.Top;  }
        }

        /// <summary>
        /// Gets or sets the width of this rectangle.
        /// </summary>
        public double Width
        {
            get { return _width; }
            set { _width = value; }
        }

        /// <summary>
        /// Gets or sets the height of this rectangle.
        /// </summary>
        public double Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>
        /// Gets the right edge of this rectangle.
        /// </summary>
        public double Right
        {
            get { return _left + _width; }
        }

        /// <summary>
        ///Gets the bottom edge of this rectangle.
        /// </summary>
        public double Bottom
        {
            get { return _top + _height; }
        }

        /// <summary>
        /// Gets the center point of this rectangle.
        /// </summary>
        public PointR Center
        {
            get { return new PointR(_left + _width / 2.0, _top + _height / 2.0); }
        }

        /// <summary>
        /// Gets the topleft corner of this rectangle.
        /// </summary>
        public PointR TopLeft
        {
            get { return new PointR(_left, _top); }
        }

        /// <summary>
        /// Gets the topright corner of this rectangle.
        /// </summary>
        public PointR TopRight
        {
            get { return new PointR(_left + _width, _top); }
        }

        /// <summary>
        /// Gets the bottomleft corner of this rectangle.
        /// </summary>
        public PointR BottomLeft
        {
            get { return new PointR(_left, _top + _height); }
        }

        /// <summary>
        /// Gets the bottomright corner of this rectangle.
        /// </summary>
        public PointR BottomRight
        {
            get { return new PointR(_left + _width, _top + _height); }
        }

        /// <summary>
        /// Tests whether the given point is contained within this rectangle.
        /// </summary>
        /// <param name="pt">The point to test.</param>
        /// <returns>True if the point is contained; false otherwise.</returns>
        public bool Contains(PointR pt)
        {
            return (_left <= pt.X
                && _top <= pt.Y 
                && pt.X < _left + _width
                && pt.Y < _top + _height);
        }

        /// <summary>
        /// Tests whether this rectangle intersects with a given rectangle.
        /// </summary>
        /// <param name="r">The given rectangle to test for intersection.</param>
        /// <returns>True if this rectangle intersects with the given rectangle; false otherwise.</returns>
        public bool IntersectsWith(RectangleR r)
        {
            RectangleF rF = (RectangleF) r;
            RectangleF thisF = (RectangleF) this;
            return thisF.IntersectsWith(rF);
        }

        /// <summary>
        /// Produces the rectangle that is the result of intersecting this rectangle with
        /// the given rectangle. If there is no intersection, then RectangleR.Empty will be the
        /// result.
        /// </summary>
        /// <param name="r">The given rectangle to intersect with this rectangle.</param>
        /// <remarks>This method results in a RectangleR whose components will be truncated to 
        /// single-precision floating-point values. However, they will still be stored in double-precision
        /// floating-point variables.</remarks>
        public void Intersect(RectangleR r)
        {
            RectangleF rF = (RectangleF) r;
            RectangleF thisF = (RectangleF) this;
            thisF.Intersect(rF); // do the intersection
            _left = thisF.Left;
            _top = thisF.Top;
            _width = thisF.Width;
            _height = thisF.Height;
        }

        /// <summary>
        /// Produces the rectangle that is the union of this rectangle and the given rectangle.
        /// The union is a rectangle that minimally bounds both component rectangles.
        /// </summary>
        /// <param name="r">The rectangle to union with this rectangle.</param>
        public void Union(RectangleR r)
        {
            double minX = Math.Min(_left, r.Left);
            double minY = Math.Min(_top, r.Top);
            double maxX = Math.Max(_left + _width, r.Right);
            double maxY = Math.Max(_top + _height, r.Bottom);
            _left = minX;
            _top = minY;
            _width = maxX - minX;
            _height = maxY - minY;
        }

        /// <summary>
        /// Inflates this RectangleR to the nearest whole integer coordinates. The resulting
        /// RectangleR is guaranteed to encompass the one prior to inflating. The center
        /// of the rectangle is not guaranteed to remain in place.
        /// </summary>
        public void Inflate()
        {
            double right = Math.Ceiling(_left + _width);
            double bottom = Math.Ceiling(_top + _height);
            _left = Math.Floor(_left);
            _top = Math.Floor(_top);
            _width = right - _left;
            _height = bottom - _top;
        }

        /// <summary>
        /// Inflates this RectangleR by the amounts given in each dimension. Inflating
        /// keeps the center of the rectangle in place. Inflation can be positive or negative,
        /// resulting in expansion or shrinking, respectively.
        /// </summary>
        /// <param name="dx">The distance the rectangle should expand or contract in the x-direction.</param>
        /// <param name="dy">The distance the rectangle should expand or contract in the y-direction.</param>
        public void Inflate(double dx, double dy)
        {
            _left -= dx / 2.0;
            _top -= dy / 2.0;
            _width += dx / 2.0;
            _height += dy / 2.0;
        }

        /// <summary>
        /// Gets the inflated version of this rectangle; that is, one that is enlarged to whole-number
        /// coordinates. This property does not change the current state of this rectangle.
        /// </summary>
        public Rectangle Inflated
        {
            get
            {
                RectangleR rc = new RectangleR(this); // copy ourself
                rc.Inflate();
                return new Rectangle((int) rc.Left, (int) rc.Top, (int) rc.Width, (int) rc.Height);
            }
        }

        /// <summary>
        /// Scales this RectangleR by the given scale factors, where values less than 1.00 indicate
        /// shrinking in a given dimension, and values greater than 1.00 indicate growing
        /// in a given dimension.
        /// </summary>
        /// <param name="sx">The multiplier to scale by in the x-dimension. The scale factor is such that
        /// 1.00 = 100% (no change).</param>
        /// <param name="sy">The multiplier to scale by in the y-dimension. The scale factor is such that
        /// 1.00 = 100% (no change).</param>
        /// <remarks>The center of this rectangle is preserved under this transformation.</remarks>
        public void ScaleBy(double sx, double sy)
        {
            double cx = _left + _width / 2.0; // center X
            double cy = _top + _height / 2.0; // center Y
            _width *= sx; // scale X
            _height *= sy; // scale Y
            _left = cx - _width / 2.0; // restore center X
            _top = cy - _height / 2.0; // restore center Y
        }

        /// <summary>
        /// Scales this RectangleR uniformly by the given percent, where values less than 1.00 indicate
        /// shrinking and values greater than 1.00 indicate growing.
        /// </summary>
        /// <param name="factor">The scale factor to use, where 1.00 = 100% (no change).</param>
        /// <remarks>The center of this rectangle is preserved under this transformation.</remarks>
        public void ScaleBy(double factor)
        {
            this.ScaleBy(factor, factor);
        }

        /// <summary>
        /// Explicit cast from a RectangleR to a RectangleF.
        /// </summary>
        /// <param name="r">The RectangleR to cast.</param>
        /// <returns>A RectangleF representing a RectangleR whose components have been truncated from 
        /// double-precision to single-precision floating-point values.</returns>
        public static explicit operator RectangleF(RectangleR r)
        {
            return new RectangleF((float) r._left, (float) r._top, (float) r._width, (float) r._height);
        }

        /// <summary>
        /// Implicit cast from a RectangleF to a RectangleR.
        /// </summary>
        /// <param name="r">The RectangleF to cast.</param>
        /// <returns>A RectangleR representing a RectangleF whose components have been extended from
        /// single-precision to double-precision floating-point values.</returns>
        public static implicit operator RectangleR(RectangleF r)
        {
            return new RectangleR(r.Left, r.Top, r.Width, r.Height);
        }

        /// <summary>
        /// Explicit cast from a RectangleR to a Rectangle.
        /// </summary>
        /// <param name="r">The RectangleR to cast.</param>
        /// <returns>A Rectangle representing a RectangleR whose components have been possibly inflated
        /// slightly to ensure that the Rectangle covers whatever the former RectangleR would have
        /// covered. It is possible that it covers slightly more due to this inflation.</returns>
        /// <remarks>The returned Rectangle is guaranteed to cover the coordinates occupied by 
        /// the RectagleR.</remarks>
        public static explicit operator Rectangle(RectangleR r)
        {
            int right = (int) Math.Ceiling(r.Right);
            int bottom = (int) Math.Ceiling(r.Bottom);
            int left = (int) Math.Floor(r.Left);
            int top = (int) Math.Floor(r.Top);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Implicit cast from a Rectangle to a RectangleR.
        /// </summary>
        /// <param name="r">The Rectangle to cast.</param>
        /// <returns>A RectangleR representing a Rectangle whose components have been extended from
        /// integer values to double-precision floating-point values.</returns>
        public static implicit operator RectangleR(Rectangle r)
        {
            return new RectangleR(r.Left, r.Top, r.Width, r.Height);
        }

        /// <summary>
        /// Equals operator for RectangleR.
        /// </summary>
        /// <param name="r1">The lefthand side of the operator.</param>
        /// <param name="r2">The righthand side of the operator.</param>
        /// <returns>True if the four components of the rectangles agree; false otherwise.</returns>
        public static bool operator ==(RectangleR r1, RectangleR r2)
        {
            return (
                   r1._left == r2._left
                && r1._top == r2._top
                && r1._width == r2._width
                && r1._height == r2._height
                );
        }

        /// <summary>
        /// Not-equals operator for RectangleR.
        /// </summary>
        /// <param name="r1">The lefthand side of the operator.</param>
        /// <param name="r2">The righthand side of the operator.</param>
        /// <returns>True if any of the four components of the rectangles disagree; false otherwise.</returns>
        public static bool operator !=(RectangleR r1, RectangleR r2)
        {
            return (
                   r1._left != r2._left
                || r1._top != r2._top
                || r1._width != r2._width
                || r1._height != r2._height
                );
        }

        /// <summary>
        /// Tests whether the given object is equal to this rectangle.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a rectangle equal to this rectangle; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is RectangleR)
            {
                RectangleR r = (RectangleR) obj;
                return (this.Left == r.Left && this.Top == r.Top && this.Width == r.Width && this.Height == r.Height);
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this rectangle.
        /// </summary>
        /// <returns>A hashcode for this rectangle.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
