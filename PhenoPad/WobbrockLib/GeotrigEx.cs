using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using WobbrockLib;

namespace WobbrockLib
{
    /// <summary>
    /// Geometric and trigonometric utility functions.
    /// </summary>
    public static class GeotrigEx
    {
        /// <summary>
        /// Compute the Euclidean distance between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The Euclidean planar distance between <i>p1</i> and <i>p2</i>.</returns>
        public static double Distance(PointR p1, PointR p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Compute the centroid of the given points. The centroid is defined as the
        /// average x and average y values, i.e., (x_bar, y_bar).
        /// </summary>
        /// <param name="points">A list of points.</param>
        /// <returns>The centroid point of the set of points.</returns>
        public static PointR Centroid(List<PointR> points)
        {
            double xsum = 0.0;
            double ysum = 0.0;

            foreach (PointR p in points)
            {
                xsum += p.X;
                ysum += p.Y;
            }
            return new PointR(xsum / points.Count, ysum / points.Count);
        }

        /// <summary>
        /// Compute the weighted centroid of the given points. The weighted centroid is
        /// a weighted average of x and y values. Weighting allows points, e.g., farther
        /// from the geometric centroid to matter less, or more.
        /// </summary>
        /// <param name="points">A list of points.</param>
        /// <param name="w">The array of weights.</param>
        /// <remarks>This function can be thought of as a two-dimensional weighted mean.</remarks>
        public static PointR Centroid(List<PointR> points, double[] w)
        {
            double wxsum = 0.0; // weighted sum of x values
            double wysum = 0.0; // weighted sum of y values
            double sumw = 0.0; // sum of used weights
            for (int i = 0; i < points.Count; i++)
            {
                wxsum += points[i].X * w[i];
                wysum += points[i].Y * w[i];
                sumw += w[i]; // keep sum of used weights
            }
            return new PointR(wxsum / sumw, wysum / sumw);
        }

        /// <summary>
        /// Compute the midpoint, or nonparametric centroid, of the points given. The
        /// points' x and y values are ranked, and the midpoint in each ranked list
        /// is used to create the return value.
        /// </summary>
        /// <param name="points">A list of points.</param>
        /// <returns>The midpoint of the set of points.</returns>
        public static PointR MidPoint(List<PointR> points)
        {
            double[] xs = new double[points.Count];
            double[] ys = new double[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                xs[i] = points[i].X;
                ys[i] = points[i].Y;
            }
            Array.Sort<double>(xs); // sort ascending
            Array.Sort<double>(ys); // sort ascending

            if ((points.Count % 2) == 1) // odd number
            {
                return new PointR(xs[points.Count / 2], ys[points.Count / 2]);
            }
            else // even number
            {
                return new PointR(
                    (xs[points.Count / 2 - 1] + xs[points.Count / 2]) / 2.0,
                    (ys[points.Count / 2 - 1] + ys[points.Count / 2]) / 2.0
                    );
            }
        }

        /// <summary>
        /// Maps a value from range (min1, max1) to its proportional value in range (min2, max2). Note that
        /// the function works even when max1 > min1, or max2 > min2.
        /// </summary>
        /// <param name="value">The value to map. It does not have to be within [min1, max1].</param>
        /// <param name="min1">The minimum value of the range from which to map.</param>
        /// <param name="max1">The maximum value of the range from which to map.</param>
        /// <param name="min2">The minimum value of the range to which to map.</param>
        /// <param name="max2">The maximum value of the range to which to map.</param>
        /// <returns>The given value mapped from [min1,max1] into [min2,max2]. If [min1,max1] constitutes
        /// a null range (i.e., min1 == min2), then a value halfway between min2 and max2 will be
        /// returned.</returns>
        public static double MapValue(double value, double min1, double max1, double min2, double max2)
        {
            if (min1 != max1)
            {
                return min2 + ((value - min1) / (max1 - min1)) * (max2 - min2);
            }
            else
            {
                // given that we're mapping from a null range, the best we can do
                // is split the midpoint of the range we're mapping to.
                return min2 + (max2 - min2) / 2.0;
            }
        }

        /// <summary>
        /// Maps a value from range (min1, max1) to its proportional value in range (min2, max2). Note that
        /// the function works even when max1 > min1, or max2 > min2.
        /// </summary>
        /// <param name="value">The value to map. It does not have to be within [min1, max1]; however,
        /// if <i>pinToRange</i> is true, then the value will be confined to this range, inclusive.</param>
        /// <param name="min1">The minimum value of the range from which to map.</param>
        /// <param name="max1">The maximum value of the range from which to map.</param>
        /// <param name="min2">The minimum value of the range to which to map.</param>
        /// <param name="max2">The maximum value of the range to which to map.</param>
        /// <param name="pinToRange">If true, the mapped value is pinned to either endpoint of the range
        /// in the event that it falls outside either range endpoint.</param>
        /// <returns>The given value mapped from [min1,max1] into [min2,max2]. If [min1,max1] constitutes
        /// a null range (i.e., min1 == min2), then a value halfway between min2 and max2 will be
        /// returned.</returns>
        public static double MapValue(double value, double min1, double max1, double min2, double max2, bool pinToRange)
        {
            double d = MapValue(value, min1, max1, min2, max2);
            if (pinToRange)
            {
                d = PinValue(d, min2, max2); // pin to range
            }
            return d;
        }

        /// <summary>
        /// Pins the given value to the range identified by (min, max), inclusive, if the value
        /// falls outside that range. The values given for the range do not have to actually reflect 
        /// the minimum and maximum, i.e., they can be swapped or identical.
        /// </summary>
        /// <param name="value">The value to pin.</param>
        /// <param name="min">The minimum possible value.</param>
        /// <param name="max">The maximum possible value.</param>
        /// <returns>The value itself if it is within the range, or the pinned value if it fell
        /// outside the range.</returns>
        public static double PinValue(double value, double min, double max)
        {
            double truemin = Math.Min(min, max);
            double truemax = Math.Max(min, max);
            return Math.Max(truemin, Math.Min(value, truemax));
        }

        /// <summary>
        /// Maps a point from one coordinate plane to the same relative position in another 
        /// coordinate plane.
        /// </summary>
        /// <param name="p">The point to map.</param>
        /// <param name="rFrom">The plane in which <i>p</i> originally resides.</param>
        /// <param name="rTo">The new plane into which <i>p</i> will be mapped.</param>
        /// <returns>The given point mapped into <i>rTo</i>.</returns>
        public static PointR MapPoint(PointR p, RectangleR rFrom, RectangleR rTo)
        {
            double x = rTo.Left + ((p.X - rFrom.Left) / rFrom.Width) * rTo.Width;
            double y = rTo.Top + ((p.Y - rFrom.Top) / rFrom.Height) * rTo.Height;
            return new PointR(x, y);
        }

        /// <summary>
        /// Gets the length of the path traversed by the given points.
        /// </summary>
        /// <param name="points">The points to traverse.</param>
        /// <returns>The length of the path.</returns>
        public static double PathLength(List<PointR> points)
        {
            double dx = 0.0;
            for (int i = 1; i < points.Count; i++)
            {
                dx += Distance(points[i - 1], points[i]);
            }
            return dx;
        }

        /// <summary>
        /// Determine where an infinite line extrapolated from a segment from <i>p0</i> to <i>p1</i> 
        /// intersects the boundary of rectangle <i>r</i>.
        /// </summary>
        /// <param name="p0">The anchor point of the vector.</param>
        /// <param name="p1">The point to which point <i>p0</i> connects.</param>
        /// <param name="r">The rectangle at whose boundary we're finding where vector (p0, p1) intersects.</param>
        /// <returns>The point of intersection.</returns>
        public static PointR LineIntersectsRectangle(PointR p0, PointR p1, RectangleR r)
        {
            if (p1.X != p0.X)
            {
                if (p1.Y != p0.Y)
                {
                    double m = (p1.Y - p0.Y) / (p1.X - p0.X); // compute the slope
                    double b = p0.Y - m * p0.X; // compute the y-intercept using p0

                    // all of the plane's edges if extended to infinity are hit somewhere.
                    // Compute where by using the formulae [x = (y - b) / m, y = mx + b].
                    PointR leftPt = new PointR(r.Left, m * r.Left + b);
                    PointR topPt = new PointR((r.Top - b) / m, r.Top);
                    PointR rightPt = new PointR(r.Right, m * r.Right + b);
                    PointR bottomPt = new PointR((r.Bottom - b) / m, r.Bottom);

                    // but only two of those edges are hit *inside* the plane's boundary
                    bool isLeftIn = (r.Top <= leftPt.Y && leftPt.Y <= r.Bottom);
                    bool isTopIn = (r.Left <= topPt.X && topPt.X <= r.Right);
                    bool isRightIn = (r.Top <= rightPt.Y && rightPt.Y <= r.Bottom);
                    bool isBottomIn = (r.Left <= bottomPt.X && bottomPt.X <= r.Right);

                    // figure out directionality and return the edge point
                    if (isLeftIn && p1.X < p0.X)
                        return leftPt; // headed to left edge
                    else if (isTopIn && p1.Y < p0.Y)
                        return topPt; // headed to top edge
                    else if (isRightIn && p0.X < p1.X)
                        return rightPt; // headed to right edge
                    else if (isBottomIn && p0.Y < p1.Y)
                        return bottomPt; // headed to bottom edge

                    // if we reach here, we have an error. this should never happen.
                    //System.Diagnostics.Debug.Fail("Unable to determine border intersection point.");
                    return PointR.Empty; // satisfies compiler
                }
                else // straight horizontal
                {
                    return (p1.X > p0.X ? new PointR(r.Right, p1.Y) : new PointR(r.Left, p1.Y));
                }
            }
            else // straight vertical
            {
                return (p1.Y > p0.Y ? new PointR(p1.X, r.Bottom) : new PointR(p1.X, r.Top));
            }
        }

        /// <summary>
        /// Calculates the convex hull of the points given and returns the result as
        /// an ordered sequence of indices into the given points list. The ordered sequence
        /// represents a closed hull, so the last index will always be the first.
        /// </summary>
        /// <param name="points">The points from which to compute a convex hull.</param>
        /// <returns>An ordered list of indices into the given points list that represents
        /// a closed convex hull.</returns>
        /// <remarks>Uses the Jarvis March algorithm, also known as the gift-wrapping
        /// algorithm.</remarks>
        /// <see cref="http://www.chrisharrison.net/projects/convexHull/jarvis.cpp"/>
        /// "http://www.chrisharrison.net/projects/convexHull/jarvis.cpp"</see>
        public static List<int> ConvexHull(List<PointR> points)
        {
            List<int> used = new List<int>(); // used indices

            int maxIdx = 0;
            int maxAngle = 0;
            int minAngle = 0;

            // Find the index of the maximum y-value point in the set
            int maxPoint = 0;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].Y > points[maxPoint].Y)
                    maxPoint = i;
            }

            // Find the index of the minimum y-value point in the set
            int minPoint = 0;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].Y < points[minPoint].Y)
                    minPoint = i;
            }
            used.Add(minPoint);

            int currPoint = minPoint;
            while (currPoint != maxPoint) // build left-hand side of convex hull
            {
                maxAngle = currPoint;

                for (int j = 0; j < points.Count; j++) // find point with lowest relative angle
                {
                    if (HullFindAngle(points[currPoint], points[maxAngle]) < HullFindAngle(points[currPoint], points[j])
                        && (!used.Contains(j) || j == maxPoint)
                        && HullFindAngle(points[currPoint], points[j]) <= 180.0)
                    {
                        maxAngle = j;
                    }
                }
                currPoint = maxAngle;
                used.Add(currPoint);
            }
            maxIdx = used.Count - 1; // mark the transition between left-side and right-side

            currPoint = minPoint;
            while (currPoint != maxPoint) // build right-hand side of convex hull
            {
                minAngle = maxPoint;

                for (int j = 0; j < points.Count; j++) // find the point with greatest relative angle
                {
                    if (HullFindAngle(points[currPoint], points[minAngle]) > HullFindAngle(points[currPoint], points[j])
                        && (!used.Contains(j) || j == maxPoint)
                        && HullFindAngle(points[currPoint], points[j]) >= 0.0)
                    {
                        minAngle = j;
                    }
                }
                currPoint = minAngle;
                used.Insert(maxIdx + 1, currPoint);
            }

            used.Add(used[0]); // close the hull
            used.RemoveAt(maxIdx); // each half was built separately, so we have a duplicate at the seam. remove it.

            return used;
        }

        /// <summary>
        /// Helper function for the convex hull method to find the angle between two points. If the points are
        /// identical, then -1 is returned.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        /// <returns>The angle between points in the range required for the convex hull method.</returns>
        private static double HullFindAngle(PointR p1, PointR p2)
        {
            return (p1 == p2) ? -1.0 : Radians2Degrees(Angle(p1, p2, true));
        }

        /// <summary>
        /// Determines the angle, in radians, between two points. The angle is defined 
        /// by the circle centered on the start point with a radius to the end point, 
        /// where 0 radians is straight right from start (+x-axis) and PI/2 radians is
        /// straight down (+y-axis).
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="positiveOnly"></param>
        /// <returns></returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static double Angle(PointR start, PointR end, bool positiveOnly)
        {
            double radians = 0.0;
            if (start.X != end.X)
            {
                radians = Math.Atan2(end.Y - start.Y, end.X - start.X);
            }
            else // pure vertical movement
            {
                if (end.Y < start.Y)
                    radians = -Math.PI / 2.0; // -90 degrees is straight up
                else if (end.Y > start.Y)
                    radians = +Math.PI / 2.0; // 90 degrees is straight down
            }
            if (positiveOnly && radians < 0.0)
            {
                radians += Math.PI * 2.0;
            }
            return radians;
        }

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        public static double Radians2Degrees(double radians)
        {
            return (radians * 180.0 / Math.PI);
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        public static double Degrees2Radians(double degrees)
        {
            return (degrees * Math.PI / 180.0);
        }

        /// <summary>
        /// Rotate the points by the given radians about their centroid.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="radians"></param>
        /// <returns></returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static List<PointR> RotatePoints(List<PointR> points, double radians)
        {
            List<PointR> newPoints = new List<PointR>(points.Count);

            PointR c = Centroid(points);
            double cx = c.X;
            double cy = c.Y;

            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            for (int i = 0; i < points.Count; i++)
            {
                double dx = points[i].X - cx;
                double dy = points[i].Y - cy;

                PointR q = PointR.Empty;
                q.X = dx * cos - dy * sin + cx;
                q.Y = dx * sin + dy * cos + cy;

                newPoints.Add(q);
            }
            return newPoints;
        }

        /// <summary>
        /// Rotate a point 'p' around a point 'c' by the given radians.
        /// Rotation (around the origin) amounts to a 2x2 matrix of the form:
        ///
        ///		[ cos A		-sin A	] [ p.x ]
        ///		[ sin A		cos A	] [ p.y ]
        ///
        /// </summary>
        /// <param name="p"></param>
        /// <param name="c"></param>
        /// <param name="radians"></param>
        /// <returns></returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static PointR RotatePoint(PointR p, PointR c, double radians)
        {
            PointR q = PointR.Empty;
            q.X = (p.X - c.X) * Math.Cos(radians) - (p.Y - c.Y) * Math.Sin(radians) + c.X;
            q.Y = (p.X - c.X) * Math.Sin(radians) + (p.Y - c.Y) * Math.Cos(radians) + c.Y;
            return q;
        }

        /// <summary>
        /// Gets the end point of the vector extending from 'start' with a given length at a given angle.
        /// </summary>
        /// <param name="start">The start point for the vector.</param>
        /// <param name="length">The length of the vector extending from the start point.</param>
        /// <param name="radians">The angle at which the vector extends from the start point at the given length.</param>
        /// <returns></returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static PointR PointFromVector(PointR start, double length, double radians)
        {
            PointR p = new PointR(start.X + length, start.Y); // straight right of 'start'
            return RotatePoint(p, start, radians);
        }

        /// <summary>
        /// Gets the smallest angle between two angles, in degrees. The two angles can 
        /// range from -Infinity to +Infinity; they do not need to be confined to [0..360).
        /// The angle between is the smallest (non-obtuse) angle, with range [0..180].
        /// </summary>
        /// <param name="deg1">An angle in degrees.</param>
        /// <param name="deg2">An angle in degrees.</param>
        /// <returns>The smallest angle between, ranging from [0..180].</returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static double DegreesBetween(double deg1, double deg2)
        {
            return Math.Abs((Math.Abs(180.0 - deg1 + deg2) % 360.0) - 180.0);
        }

        /// <summary>
        /// Gets the smallest angle between two angles, in radians. The two angles can 
        /// range from -Infinity to +Infinity; they do not need to be confined to [0..2PI).
        /// The angle between is the smallest (non-obtuse) angle, with range [0..PI].
        /// </summary>
        /// <param name="rad1">An angle in radians.</param>
        /// <param name="rad2">An angle in radians.</param>
        /// <returns>The smallest angle between, ranging from [0..PI].</returns>
        /// <remarks>
        /// Note that the C# Math coordinate system has +x-axis stright right and
        /// +y-axis straight down. Rotation is clockwise such that from +x-axis to
        /// +y-axis is +90 degrees, from +x-axis to -x-axis is +180 degrees, and 
        /// from +x-axis to -y-axis is -90 degrees. Thus, the map looks like this:
        /// <pre>
        ///                                   -y
        ///                                   (270)
        ///                          (225)    -90     (315)
        ///                          -135             -45
        ///
        ///               -x    +/-180                     0 (360)   +x
        ///
        ///                          135              45
        ///                          (-225)   90      (-315) 
        ///                                   (-270)
        ///                                   +y
        /// </pre>
        /// </remarks>
        public static double RadiansBetween(double rad1, double rad2)
        {
            return Math.Abs((Math.Abs(Math.PI - rad1 + rad2) % (2.0 * Math.PI)) - Math.PI);
        }

        /// <summary>
        /// Finds the minimum sized bounding box for the points given.
        /// </summary>
        /// <param name="points">The points whose bounding box to find.</param>
        /// <returns>The bounding box of the given points.</returns>
        public static RectangleR FindBoundingBox(List<PointR> points)
        {
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
		
			foreach (PointR p in points)
			{
				if (p.X < minX)
					minX = p.X;
				if (p.X > maxX)
					maxX = p.X;
			
				if (p.Y < minY)
					minY = p.Y;
				if (p.Y > maxY)
					maxY = p.Y;
			}
		
			return new RectangleR(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Scales a set of points so that their bounding box occupies the size dimensions given.
        /// </summary>
        /// <param name="points">The points to scale.</param>
        /// <param name="size">The size dimension to scale the points to.</param>
        /// <returns>The points scaled to the given size.</returns>
        public static List<PointR> ScaleTo(List<PointR> points, SizeR size)
        {
            List<PointR> newPoints = new List<PointR>(points.Count);
            RectangleR r = FindBoundingBox(points);
            for (int i = 0; i < points.Count; i++)
            {
                PointR p = points[i];
                if (r.Width != 0.0)
                    p.X *= (size.Width / r.Width);
                if (r.Height != 0.0)
                    p.Y *= (size.Height / r.Height);
                newPoints.Add(p);
            }
            return newPoints;
        }

        /// <summary>
        /// Scales a set of points by a percentage amount in each of the (x,y) dimensions.
        /// </summary>
        /// <param name="points">The points to scale by the given percentages.</param>
        /// <param name="percents">The x and y percents. Values of 1.0 result in the identity scale (no change).</param>
        /// <returns>The points scaled by the given percentages.</returns>
        public static List<PointR> ScaleBy(List<PointR> points, SizeR percents)
        {
            List<PointR> newPoints = new List<PointR>(points.Count);
            RectangleR r = FindBoundingBox(points);
            for (int i = 0; i < points.Count; i++)
            {
                PointR p = points[i];
                p.X *= percents.Width;
                p.Y *= percents.Height;
                newPoints.Add(p);
            }
            return newPoints;
        }

        /// <summary>
        /// Translates the given points to the point given, which can refer to the top-left corner
        /// of the points' bounding box or the centroid of the points.
        /// </summary>
        /// <param name="points">The points to translate.</param>
        /// <param name="toPt">The destination to where to translate the points given.</param>
        /// <param name="centroid">If true, the points are translated so that their centroid lines up
        /// with the 'toPt' point. If false, the points line up so their top-left corner matches the 'toPt' point.</param>
        public static List<PointR> TranslateTo(List<PointR> points, PointR toPt, bool centroid)
        {
            List<PointR> newPoints = new List<PointR>(points.Count);
            if (centroid)
            {
                PointR ctr = Centroid(points);
                for (int i = 0; i < points.Count; i++)
                {
                    PointR p = points[i];
                    p.X += (toPt.X - ctr.X);
                    p.Y += (toPt.Y - ctr.Y);
                    newPoints.Add(p);
                }
            }
            else // line up top-left corner
            {
                RectangleR r = FindBoundingBox(points);
                for (int i = 0; i < points.Count; i++)
                {
                    PointR p = points[i];
                    p.X += (toPt.X - r.Left);
                    p.Y += (toPt.Y - r.Top);
                    newPoints.Add(p);
                }
            }
            return newPoints;
        }

        /// <summary>
        /// Translates the given points by the given deltas in each of the (x,y) dimensions.
        /// </summary>
        /// <param name="points">The points to translate.</param>
        /// <param name="deltas">The (x,y) deltas to translate the points by.</param>
        /// <returns>The translated points.</returns>
        public static List<PointR> TranslateBy(List<PointR> points, SizeR deltas)
        {
            List<PointR> newPoints = new List<PointR>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                PointR p = points[i];
                p.X += deltas.Width;
                p.Y += deltas.Height;
                newPoints.Add(p);
            }
            return newPoints;
        }
    }
}
