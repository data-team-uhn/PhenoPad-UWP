using System;
using System.Collections;
using System.Collections.Generic;

namespace WobbrockLib
{
    /// <summary>
    /// Implements a set of static utility functions designed to work with time series data.
    /// </summary>
    public static class SeriesEx
    {
        /// <summary>
        /// Resamples an ordered set of points such that <i>n</i> spatially equidistant points  
        /// will compose the resampled path.
        /// </summary>
        /// <param name="points">The points to resample in space.</param>
        /// <param name="px">The incremental pixel distance to use between points in the new path. This distance 
        /// will be met for all intervals except possibly the last one, depending on the remainder that
        /// results from dividing the path length by the interval length.</param>
        /// <returns>A list of time points resembling the new path.</returns>
        public static List<TimePointR> ResampleInSpace(List<TimePointR> points, double px)
        {
            List<TimePointR> srcPts = new List<TimePointR>(points); // copy source points so we can insert into them
            List<TimePointR> dstPts = new List<TimePointR>(); // create the destination points that we return
            dstPts.Add(srcPts[0]); // add the first source point as the first destination point

            double D = 0.0; // accumulated distance
            for (int i = 1; i < srcPts.Count; i++)
            {
                TimePointR pt1 = srcPts[i - 1];
                TimePointR pt2 = srcPts[i];
                double dxy = GeotrigEx.Distance((PointR) pt1, (PointR) pt2); // distance in space
                
                if ((D + dxy) >= px) // would enough space be traversed in this step?
                {
                    double pct = (px - D) / dxy;
                    double qx = pt1.X + pct * (pt2.X - pt1.X); // interpolate X
                    double qy = pt1.Y + pct * (pt2.Y - pt1.Y); // interpolate Y
                    double qt = pt1.Time + pct * (pt2.Time - pt1.Time); // interpolate Time
                    TimePointR q = new TimePointR(qx, qy, (long) qt);
                   
                    dstPts.Add(q); // append new point 'q'
                    srcPts.Insert(i, q); // insert 'q' at position i in points s.t. 'q' will be the next i
                    D = 0.0; // reset
                }
                else D += dxy; // accumulator
            }
            // unless px divides evenly into the path length (not likely), we will have some accumulation
            // left over, so just add the last point as the last point, which will not be the full interval.
            if (D > 0.0)
            {
                dstPts.Add(srcPts[srcPts.Count - 1]);
            }

            return dstPts;
        }

        /// <summary>
        /// Resamples an ordered set of points at a given frequency; that is, such that the
        /// new points are equidistant in time at the given frequency.
        /// </summary>
        /// <param name="points">The points to be resampled.</param>
        /// <param name="hertz">The cycles per second; that is, the number of point samples per second
        /// that should result. The time gap (in ms) between successive points will therefore be 
        /// 1000.0 / <i>hertz</i>.
        /// </param>
        /// <returns>A list of time points resembling the new path.</returns>
        public static List<TimePointR> ResampleInTime(List<TimePointR> points, int hertz)
        {
            double I = 1000.0 / hertz; // interval in time -- points should be spaced by this much time (ms)
            double T = 0.0; // accumulated time

            List<TimePointR> srcPts = new List<TimePointR>(points); // copy source points so we can insert into them
            int n = (int) Math.Ceiling((points[points.Count - 1].Time - points[0].Time) / I); // we will need this many points
            List<TimePointR> dstPts = new List<TimePointR>(n); // create the destination points that we return

            dstPts.Add(srcPts[0]); // add the first point
            for (int i = 1; i < srcPts.Count; i++)
            {
                TimePointR pt1 = srcPts[i - 1];
                TimePointR pt2 = srcPts[i];
                double dt = pt2.Time - pt1.Time; // distance in time
                
                if ((T + dt) >= I) // would enough time be traversed in this step?
                {
                    double pct = (I - T) / dt;
                    double qx = pt1.X + pct * (pt2.X - pt1.X); // interpolate X
                    double qy = pt1.Y + pct * (pt2.Y - pt1.Y); // interpolate Y
                    double qt = pt1.Time + (I - T); // interpolate Time
                    TimePointR q = new TimePointR(qx, qy, (long) qt);

                    dstPts.Add(q); // append new point 'q'
                    srcPts.Insert(i, q); // insert 'q' at position i in points s.t. 'q' will be the next i
                    T = 0.0; // reset
                }
                else T += dt; // accumulator
            }
            // somtimes we fall a rounding-error short of adding the last point, so add it if so
            if (dstPts.Count == n - 1)
            {
                dstPts.Add(srcPts[srcPts.Count - 1]);
            }

            return dstPts;
        }

        /// <summary>
        /// Find the maximum Y-value in a data series and return the index.
        /// </summary>
        /// <param name="series">A series, for example, a time series where X-values are time and Y-values are
        /// some quantity of interest.</param>
        /// <param name="start">The starting index, inclusive, at which to look for the maximum.</param>
        /// <param name="count">The number of values over which to look for the maximum, or -1 for all.</param>
        /// <returns>The index of the maximum value. If the same value exists more than once,
        /// the first index at which it appears is returned.</returns>
        public static int Max(List<PointR> series, int start, int count)
        {
            int max = start;
            int ubound = count > 0 ? Math.Min(start + count, series.Count) : series.Count;

            for (int i = start; i < ubound; i++)
            {
                PointR p0 = series[max];
                PointR p1 = series[i];
                if (p1.Y > p0.Y)
                {
                    max = i;
                }
            }
            return max;
        }

        /// <summary>
        /// Find the minimum Y-value in a data series and return the index.
        /// </summary>
        /// <param name="series">A series, for example, a time series where X-values are time and Y-values are
        /// some quantity of interest.</param>
        /// <param name="start">The starting index, inclusive, at which to look for the minimum.</param>
        /// <param name="count">The number of values over which to look for the minimum, or -1 for all.</param>
        /// <returns>The index of the minimum value. If the same value exists more than once,
        /// the last index at which it appears is returned.</returns>
        public static int Min(List<PointR> series, int start, int count)
        {
            int min = start;
            int ubound = count > 0 ? Math.Min(start + count, series.Count) : series.Count;

            for (int i = start; i < ubound; i++)
            {
                PointR p0 = series[min];
                PointR p1 = series[i];
                if (p1.Y <= p0.Y)
                {
                    min = i;
                }
            }
            return min;
        }

        /// <summary>
        /// Find the indices of all local maxima Y-values in a data series.
        /// </summary>
        /// <param name="series">A series, for example, a time series where X-values are time and Y-values are
        /// some quantity of interest.</param>
        /// <param name="start">The starting index, inclusive, at which to look for maxima.</param>
        /// <param name="count">The number of values over which to look for the maxima, or -1 for all.</param>
        /// <returns>The indices of all local maxima in the range given.</returns>
        /// <remarks>Series endpoints are not eligible maxima. Plateaus produce one local maxima at their start 
        /// from left to right, but no more.</remarks>
        public static int[] Maxima(List<PointR> series, int start, int count)
        {
            List<int> indices = new List<int>(series.Count);
            int ubound = count > 0 ? Math.Min(start + count, series.Count) : series.Count;

            for (int i = start + 1; i < ubound - 1; i++)
            {
                PointR p0 = series[i - 1];
                PointR p1 = series[i];
                PointR p2 = series[i + 1];
                if (p0.Y < p1.Y && p1.Y >= p2.Y)
                {
                    indices.Add(i);
                }
            }
            return indices.ToArray();
        }

        /// <summary>
        /// Find the indices of all local minima Y-values in a data series.
        /// </summary>
        /// <param name="series">A series, for example, a time series where X-values are time and Y-values are
        /// some quantity of interest.</param>
        /// <param name="start">The starting index, inclusive, at which to look for the minima.</param>
        /// <param name="count">The number of values over which to look for the minima, or -1 for all.</param>
        /// <returns>The indices of all local minima in the range given.</returns>
        /// <remarks>Series endpoints are not eligible minima. Plateaus produce one local minima at their end 
        /// from left to right, but no more.</remarks>
        public static int[] Minima(List<PointR> series, int start, int count)
        {
            List<int> indices = new List<int>(series.Count);
            int ubound = count > 0 ? Math.Min(start + count, series.Count) : series.Count;

            for (int i = start + 1; i < ubound - 1; i++)
            {
                PointR p0 = series[i - 1];
                PointR p1 = series[i];
                PointR p2 = series[i + 1];
                if (p0.Y >= p1.Y && p1.Y < p2.Y)
                {
                    indices.Add(i);
                }
            }
            return indices.ToArray();
        }

        /// <summary>
        /// From the given series, creates two output series which contain points for when the original series
        /// is rising and falling.
        /// </summary>
        /// <param name="series">The original series whose points to examine.</param>
        /// <param name="rising">A list of series whose points are from where the original series is rising.</param>
        /// <param name="falling">A list of series whose points are from where the original series is falling.</param>
        /// <remarks>Rising and falling sections are based on the SeriesEx.Minima and SeriesEx.Maxima functions.
        /// See those functions for descriptions of how flat plateaus are handled. The number of return points in
        /// the rising and falling series should always sum to the the number in the original series (i.e., no 
        /// points are omitted).</remarks>
        public static void GetRisingAndFalling(List<PointR> series, out List<List<PointR>> rising, out List<List<PointR>> falling)
        {
            int[] minima = Minima(series, 0, -1);
            int[] maxima = Maxima(series, 0, -1);
            GetRisingAndFalling(series, minima, maxima, out rising, out falling);
        }

        /// <summary>
        /// From the given series, creates two output series which contain points for when the original series
        /// is rising and falling.
        /// </summary>
        /// <param name="series">The original series whose points to examine.</param>
        /// <param name="minima">An array of indices at which local minima occur; use SeriesEx.Minima.</param>
        /// <param name="maxima">An array of indices at which local maxima occur; use SeriesEx.Maxima.</param>
        /// <param name="rising">A list of series whose points are from where the original series is rising.</param>
        /// <param name="falling">A list of series whose points are from where the original series is falling.</param>
        /// <remarks>Rising and falling sections are based on the minima and maxima provided by SeriesEx.Minima 
        /// and SeriesEx.Maxima, respectively. See those functions for descriptions of how flat plateaus are handled.
        /// The number of return points in the rising and falling series should always sum to the the number in the 
        /// original series (i.e., no points are omitted).</remarks>
        public static void GetRisingAndFalling(List<PointR> series, int[] minima, int[] maxima, out List<List<PointR>> rising, out List<List<PointR>> falling)
        {
            rising = new List<List<PointR>>();
            falling = new List<List<PointR>>();

            // iterate over the minima and maxima and add points to the rising and falling series accordingly
            int from, to = -1;
            for (int i = 0; i < Math.Max(minima.Length, maxima.Length); i++)
            {
                if (0 <= i && i < maxima.Length)
                {
                    from = to + 1;
                    to = maxima[i];
                    rising.Add(series.GetRange(from, to - from + 1)); // rising
                }
                if (0 <= i && i < minima.Length)
                {
                    from = to + 1;
                    to = minima[i];
                    falling.Add(series.GetRange(from, to - from + 1)); // falling
                }
            }
            // series endpoints are not part of the minima, maxima arrays. so we need to handle the 
            // endpoint to determine if this series ends while rising or while falling. it is even
            // possible that the *whole* series is rising or falling and so no minima or maxima exist.
            if (minima.Length == 0 && maxima.Length == 0)
            {
                from = 0;
                to = series.Count - 1;
                if (series[from].Y <= series[to].Y)
                    rising.Add(series.GetRange(from, to - from + 1)); // all rising (or flat)
                else
                    falling.Add(series.GetRange(from, to - from + 1)); // all falling
            }
            else if ((minima.Length > 0 && maxima.Length == 0) // there was only minima but no maxima, or
                ||
                (minima.Length > 0 && maxima.Length > 0 && minima[minima.Length - 1] > maxima[maxima.Length - 1])) // a minima occurred last, so we're still rising
            {
                from = minima[minima.Length - 1] + 1;
                to = series.Count - 1;
                rising.Add(series.GetRange(from, to - from + 1)); // rising
            }
            else if ((maxima.Length > 0 && minima.Length == 0) // there was only maxima but no minima, or
                ||
                (maxima.Length > 0 && minima.Length > 0 && maxima[maxima.Length - 1] > minima[minima.Length - 1])) // a maxima occurred last, so we're still falling
            {
                from = maxima[maxima.Length - 1] + 1;
                to = series.Count - 1;
                falling.Add(series.GetRange(from, to - from + 1)); // falling
            }
        }

        /// <summary>
        /// Tests all or part of a time series for stretches that contain the given value plus-or-minus the given
        /// tolerance. The result is an array of bits whose true bits indicate values matching, within the tolerance
        /// limit, the given value.
        /// </summary>
        /// <param name="series">A time series of Y-values to test. The X-values are regarded as time.</param>
        /// <param name="value">The value to test.</param>
        /// <param name="tolerance">The tolerance around that value, i.e., value +/- tolerance.</param>
        /// <param name="start">The start index within the series to begin testing.</param>
        /// <param name="count">The number of values to test, or -1 to test the whole series.</param>
        /// <returns>A bit array whose on bits identify series values that are within +/-tolerance of the given value.</returns>
        public static BitArray ValuesWithinTolerance(List<PointR> series, float value, float tolerance, int start, int count)
        {
            BitArray bits = new BitArray(series.Count);
            int ubound = (count > 0) ? Math.Min(start + count, series.Count) : series.Count;

            for (int i = start; i < ubound; i++)
            {
                bits[i] = (Math.Abs(series[i].Y - value) <= tolerance);
            }
            return bits;
        }

        /// <summary>
        /// Given a bit array whose on bits identify values of interest, this method returns
        /// index pairs to indicate the spans of on-values in the bit array.
        /// </summary>
        /// <param name="bits">The bit array whose on bits identify values of interest.</param>
        /// <param name="start">The start index within the bit array to begin.</param>
        /// <param name="count">The number of bits to examine, or -1 to examine the whole bit array.</param>
        /// <returns>A list of index pairs, where even index i is the start of a range and odd index i+1 is the end.</returns>
        public static List<int> Bits2Indices(BitArray bits, int start, int count)
        {
            List<int> ranges = new List<int>();
            int ubound = (count > 0) ? Math.Min(start + count, bits.Count) : bits.Count;
            int ifrom = -1;

            for (int i = start; i < ubound; i++)
            {
                if (bits[i])
                {
                    if (ifrom == -1)
                    {
                        ifrom = i; // start of range
                    }
                }
                else if (ifrom != -1) // not true but we were in a range
                {
                    ranges.Add(ifrom);
                    ranges.Add(i - 1); // end of range
                    ifrom = -1; // reset
                }
            }
            if (ifrom != -1) // if we're still in the range after loop, add last index
            {
                ranges.Add(ifrom);
                ranges.Add(bits.Count - 1); // end of range
            }

            return ranges;
        }

        /// <summary>
        /// Turns a list of index pairs identifying number ranges into a bit array whose on bits identifying the same.
        /// </summary>
        /// <param name="ranges">Index pairs that identify ranges of on bits. Even index i is the start of a range
        /// while odd index i+1 is the end of a range.</param>
        /// <param name="count">The number of bits in the bit array. The caller must ensure that the count is large
        /// enough to cover whatever indices are given in <i>ranges</i>.</param>
        /// <returns>A bit array with <i>count</i> bits whose on bits were identified by the index pairs in <i>ranges</i>.</returns>
        public static BitArray Indices2Bits(List<int> indices, int count)
        {
            BitArray bits = new BitArray(count);
            for (int i = 1; i < indices.Count; i += 2)
            {
                for (int j = indices[i - 1]; j <= indices[i]; j++)
                {
                    bits[j] = true;
                }
            }
            return bits;
        }

        /// <summary>
        /// Given a series of 2-D values (x,y) over time (t), returns the 1-D series representing
        /// this series' derivative. The derivative is the Euclidean change in (x,y) position over the
        /// Time-value intervals.
        /// </summary>
        /// <param name="twoDSeries">A series, for example, of (x,y) points over time.</param>
        /// <returns>The derivative series of the one given. The first point is assumed to be at (0,0).</returns>
        public static List<PointR> Derivative(List<TimePointR> twoDSeries)
        {
            List<PointR> result = new List<PointR>(twoDSeries.Count);
            for (int i = 1; i < twoDSeries.Count; i++)
            {
                double t = twoDSeries[i].Time - twoDSeries[0].Time; // timestamp relative to start
                double dt = twoDSeries[i].Time - twoDSeries[i - 1].Time; // the last time interval
                double dx = GeotrigEx.Distance((PointR) twoDSeries[i - 1], (PointR) twoDSeries[i]); // last distance covered
                double v = dx / dt; // e.g., pixels/ms
                result.Add(new PointR(t, v));
            }
            result.Insert(0, PointR.Empty); // add initial point at (0,0)
            return result;
        }

        /// <summary>
        /// Given a series of 1-D values (y) over time (x), returns the 1-D series representing this series'
        /// derivative. The derivative is the change in Y-values over the X-value intervals.
        /// </summary>
        /// <param name="series">A series, for example, a time series where X-values are time and Y-values are
        /// some quantity of interest.</param>
        /// <returns>The derivative series of the one given.  The first point is assumed to be at (0,0).</returns>
        /// <example>In a time series, X-values will be time stamps equally spaced due
        /// to prior resampling, and Y-values will be a quantity of interest, for example, the
        /// distance moved between successive points in a movement path. In this case, the return
        /// series is the velocity plot.</example>
        public static List<PointR> Derivative(List<PointR> oneDSeries)
        {
            List<PointR> result = new List<PointR>(oneDSeries.Count);
            for (int i = 1; i < oneDSeries.Count; i++)
            {
                double x = oneDSeries[i].X - oneDSeries[0].X; // x-coord (relative to start)
                double dx = oneDSeries[i].X - oneDSeries[i - 1].X; // the last interval
                double dy = oneDSeries[i].Y - oneDSeries[i - 1].Y; // value change over interval
                double v = dy / dx; // rate of change
                result.Add(new PointR(x, v));
            }
            result.Insert(0, PointR.Empty); // add initial point at (0,0)
            return result;
        }

        /// <summary>
        /// Applies a 1D kernel filter to the given series of points. For example, filtering can be used to 
        /// create a smoothed time series.
        /// </summary>
        /// <param name="series">The series to be filtered. It is customary for the points to represent a time series
        /// where the X-coordinate is along a time axis and the Y-coordinate will be the value at that time. The points 
        /// should be evenly spaced along the X-axis, regardless of the interpretation of the axis.</param>
        /// <param name="filter">The kernel used to do the filtering.</param>
        /// <returns>A series of filtered points. The X-coordinates of the points remain unchanged from
        /// the original series.</returns>
        public static List<PointR> Filter(List<PointR> series, double[] filter)
        {
            double[] newy = new double[series.Count]; // for storing new y-values

            // let i be the center of the filter. it runs the length of the series.
            for (int i = 0; i < series.Count; i++)
            {
                // let j move in the series array over the range overlapped by 
                // the filter. let k be the index within the filter itself.
                for (int k = 0, j = i - filter.Length / 2; j <= i + filter.Length / 2; k++, j++)
                {
                    if (0 <= j && j < series.Count) // only can use filter slots that overlap the series array
                    {
                        newy[i] += series[j].Y * filter[k];
                    }
                }
            }

            // copy the newy values into new points y-values while retaining 
            // the same x-values (time coords) as the original series.
            List<PointR> newpts = new List<PointR>(newy.Length);
            for (int i = 0; i < newy.Length; i++)
            {
                newpts.Add(new PointR(series[i].X, newy[i]));
            }
            return newpts;
        }

        /// <summary>
        /// Creates a discretized Gaussian kernel with the given standard deviation. A discretized
        /// Gaussian reaches near zero about three standard deviations from its center, so the size
        /// of the array kernel will be (3 * <i>stdev</i> * 2 + 1). The "2" multiplier is for a two-sided
        /// Gaussian, and the "+ 1" is for the central value.
        /// </summary>
        /// <param name="stdev">The standard deviation of the Gaussian normal distribution. Greater standard
        /// deviations result in a flatter (and wider) Gaussian distribution.</param>
        /// <returns>A discretized Gaussian distribution kernel.</returns>
        /// <url>http://homepages.inf.ed.ac.uk/rbf/HIPR2/gsmooth.htm</url>
        public static double[] GaussianKernel(int stdev)
        {
            int size = 3 * stdev * 2 + 1;
            double[] kernel = new double[size];

            // create the kernel values using the Gaussian distribution equation.
            for (int i = -kernel.Length / 2, j = 0; i <= kernel.Length / 2; i++, j++)
            {
                kernel[j] = (1.0 / (Math.Sqrt(2.0 * Math.PI) * stdev)) * Math.Pow(Math.E, -(i * i) / (2.0 * stdev * stdev));
            }

            return kernel;
        }
    }
}
