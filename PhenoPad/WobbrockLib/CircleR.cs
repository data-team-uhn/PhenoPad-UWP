using System;
using System.Collections.Generic;
using System.Text;
using WobbrockLib;

namespace WobbrockLib
{
    /// <summary>
    /// A structure representing a circle with a center and radius.
    /// </summary>
    public struct CircleR
    {
        public static readonly CircleR Empty;
        private PointR _center;
        private double _radius;

        /// <summary>
        /// Constructs a circle from its center x- and y-coordinates and its radius.
        /// </summary>
        /// <param name="x">The x-coordinate of the circle's center.</param>
        /// <param name="y">The y-coordinate of the circle's center.</param>
        /// <param name="radius">The radius of the circle.</param>
        public CircleR(double x, double y, double radius)
        {
            _center = new PointR(x, y);
            _radius = radius;
        }

        /// <summary>
        /// Constructs a circle with the given center and radius.
        /// </summary>
        /// <param name="center">The center point of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        public CircleR(PointR center, double radius)
        {
            _center = center;
            _radius = radius;
        }

        /// <summary>
        /// Copy constructor for a circle.
        /// </summary>
        /// <param name="c">The circle to copy.</param>
        public CircleR(CircleR c)
        {
            _center = c._center;
            _radius = c._radius;
        }

        /// <summary>
        /// Creates a string representation of this circle.
        /// </summary>
        /// <returns>A string representation of this circle.</returns>
        public override string ToString()
        {
            return String.Format("{{X={0}, Y={1}, Radius={2}}}", _center.X, _center.Y, _radius);
        }

        /// <summary>
        /// Constructs a circle from a given string representation.
        /// </summary>
        /// <param name="s">A string representation of a circle.</param>
        /// <returns>A CircleR representing the given string.</returns>
        public static CircleR FromString(string s)
        {
            CircleR c = CircleR.Empty;
            try
            {
                int xpos = s.IndexOf('X'); // "X"
                int comma = s.IndexOf(',', xpos + 2);
                double x = double.Parse(s.Substring(xpos + 2, comma - (xpos + 2)));

                int ypos = s.IndexOf('Y'); // "Y"
                comma = s.IndexOf(',', ypos + 2);
                double y = double.Parse(s.Substring(ypos + 2, comma - (ypos + 2)));

                int rpos = s.IndexOf('R'); // "R"adius
                comma = s.IndexOf('}', rpos + 7);
                double r = double.Parse(s.Substring(rpos + 7, comma - (rpos + 7)));

                c = new CircleR(x, y, r);
            }
            catch { }
            return c;
        }

        /// <summary>
        /// Gets or sets the center point of this circle.
        /// </summary>
        public PointR Center
        {
            get { return _center; }
            set { _center = value; }
        }

        /// <summary>
        /// Gets or sets the radius of this circle.
        /// </summary>
        public double Radius
        {
            get { return _radius; }
            set { _radius = value; }
        }

        /// <summary>
        /// Gets the diameter of this circle.
        /// </summary>
        public double Diameter
        {
            get { return _radius * 2.0; }
        }

        /// <summary>
        /// Gets the circumference of this circle.
        /// </summary>
        public double Circumference
        {
            get { return 2.0 * Math.PI * _radius; }
        }

        /// <summary>
        /// Gets the area of this circle.
        /// </summary>
        public double Area
        {
            get { return Math.PI * _radius * _radius; }
        }

        /// <summary>
        /// Gets the left edge of this circle.
        /// </summary>
        public double Left
        {
            get { return _center.X - _radius; }
        }

        /// <summary>
        /// Gets the top edge of this circle.
        /// </summary>
        public double Top
        {
            get { return _center.Y - _radius; }
        }

        /// <summary>
        /// Gets the right edge of this circle.
        /// </summary>
        public double Right
        {
            get { return _center.X + _radius; }
        }

        /// <summary>
        /// Gets the bottom edge of this circle.
        /// </summary>
        public double Bottom
        {
            get { return _center.Y + _radius; }
        }

        /// <summary>
        /// Gets a rectangle that exactly bounds this circle.
        /// </summary>
        public RectangleR Bounds
        {
            get { return new RectangleR(_center.X - _radius, _center.Y - _radius, _radius * 2.0, _radius * 2.0); }
        }

        /// <summary>
        /// Translates this circle's center by the given amounts.
        /// </summary>
        /// <param name="dx">The amount to translate the circle left or right, using negative or positive values, respectively.</param>
        /// <param name="dy">The amount to translate the circle up or down, using negative or positive values, respectively.</param>
        public void TranslateBy(double dx, double dy)
        {
            _center.X += dx;
            _center.Y += dy;
        }

        /// <summary>
        /// Translates this circle's center to the given x- and y-coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate to which to move this circle's center.</param>
        /// <param name="y">The y-coordinate to which to move this circle's center.</param>
        public void TranslateTo(double x, double y)
        {
            _center.X = x;
            _center.Y = y;
        }

        /// <summary>
        /// Scales this circle by the given scale factor, where 1.00 is no change, values
        /// less than 1.00 are shrinking, and values greater than 1.00 are expanding. The
        /// center of the circle will remain unchanged.
        /// </summary>
        /// <param name="factor">The scale factor to scale by.</param>
        public void ScaleBy(double factor)
        {
            _radius *= factor;
        }

        /// <summary>
        /// Tests whether the given point is contained in this circle.
        /// </summary>
        /// <param name="pt">The point to test.</param>
        /// <returns>True if the given point is contained in this circle; false otherwise.</returns>
        public bool Contains(PointR pt)
        {
            return GeotrigEx.Distance(_center, pt) <= _radius;
        }
        
        /// <summary>
        /// Tests whether the given circle intersects with this circle.
        /// </summary>
        /// <param name="c">The given circle to test for intersection.</param>
        /// <returns>True if the given circle intersects with this circle; false otherwise.</returns>
        public bool IntersectsWith(CircleR c)
        {
            double d = GeotrigEx.Distance(_center, c._center);
            return (d < _radius + c._radius);
        }

        /// <summary>
        /// Equals operator for circles.
        /// </summary>
        /// <param name="c1">The left-hand side of the operator.</param>
        /// <param name="c2">The right-hand side of the operator.</param>
        /// <returns>True if the circles' centers and radii agree; false otherwise.</returns>
        public static bool operator ==(CircleR c1, CircleR c2)
        {
            return (c1._center == c2._center && c1._radius == c2._radius);
        }

        /// <summary>
        /// Not-equals operator for circles.
        /// </summary>
        /// <param name="c1">The left-hand side of the operator.</param>
        /// <param name="c2">The right-hand side of the operator.</param>
        /// <returns>True if either the circles' centers or radii disagree; false otherwise.</returns>
        public static bool operator !=(CircleR c1, CircleR c2)
        {
            return (c1._center != c2._center || c1._radius != c2._radius);
        }

        /// <summary>
        /// Tests whether the given object is equal to this circle.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a circle equal to this circle; false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is CircleR)
            {
                CircleR c = (CircleR) obj;
                return this.Center == c.Center && this.Radius == c.Radius;
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this circle.
        /// </summary>
        /// <returns>A hashcode for this circle.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
