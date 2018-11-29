using System;
using System.Collections.Generic;
using System.Text;

namespace WobbrockLib
{
    /// <summary>
    /// A structure representing a value range from minimum to maximum. The range can be inclusive
    /// or exclusive at either endpoint.
    /// </summary>
    public struct RangeR
    {
        private const double EPSILON = 1e-12; // 1 trillionth
        public static readonly RangeR Empty;
        private double _min;
        private double _max;
        private bool _excludeMin;
        private bool _excludeMax;

        /// <summary>
        /// Constructs a range from minimum to maximum, inclusive of the endpoints.
        /// </summary>
        /// <param name="min">The minimum endpoint of this range.</param>
        /// <param name="max">The maximum endpoint of this range.</param>
        public RangeR(double min, double max)
            : this(min, max, false, false)
        {
            // do nothing
        }

        /// <summary>
        /// Constructs a range from minimum to maximum, either inclusive or exclusive at either endpoint.
        /// </summary>
        /// <param name="min">The minimum endpoint of this range.</param>
        /// <param name="max">The maximum endpoint of this range.</param>
        /// <param name="excludeMin">If true, the minimum is excluded from the range. If false, the minimum is included in the range.</param>
        /// <param name="excludeMax">If true, the maximum is excluded from the range. If false, the maximum is included in the range.</param>
        /// <remarks>In mathematical notation, an included endpoint is written with a bracket, e.g.,
        /// [5,10]. An excluded endpoint is written with a parenthesis, e.g., (5,10).</remarks>
        public RangeR(double min, double max, bool excludeMin, bool excludeMax)
        {
            _min = Math.Min(min, max);
            _max = Math.Max(min, max);
            _excludeMin = excludeMin;
            _excludeMax = excludeMax;
        }

        /// <summary>
        /// Gets or sets the minimum value for the range. If the value given is
        /// greater than the maximum, the former maximum will become the new
        /// minimum, and the value will become the new maximum.
        /// </summary>
        public double Min
        {
            get { return _min; }
            set
            {
                _min = Math.Min(value, _max);
                _max = Math.Max(value, _max);
            }
        }

        /// <summary>
        /// Gets or sets the maximum value for the range. If the value given is
        /// less than the minimum, the former minimum will become the new
        /// maximum, and the value will become the new minimum.
        /// </summary>
        public double Max
        {
            get { return _max; }
            set
            {
                _max = Math.Max(_min, value);
                _min = Math.Min(_min, value);
            }
        }

        /// <summary>
        /// Gets or sets whether to exclude the minimum in the range from the range itself.
        /// </summary>
        /// <remarks>In mathematical notation, an included endpoint is written with a bracket, e.g.,
        /// [5,10]. An excluded endpoint is written with a parenthesis, e.g., (5,10).</remarks>
        public bool ExcludeMin
        {
            get { return _excludeMin; }
            set { _excludeMin = value; }
        }

        /// <summary>
        /// Gets or sets whether to exclude the maximum in the range from the range itself.
        /// </summary>
        /// <remarks>In mathematical notation, an included endpoint is written with a bracket, e.g.,
        /// [5,10]. An excluded endpoint is written with a parenthesis, e.g., (5,10).</remarks>
        public bool ExcludeMax
        {
            get { return _excludeMax; }
            set { _excludeMax = value; }
        }

        /// <summary>
        /// Gets the expanse, or distance, covered by this range. If either endpoint is excluded,
        /// that endpoint is moved inward by a tiny constant EPISLON. The value double.Epsilon is not
        /// used because it is so small, it is not effective in changing the values.
        /// </summary>
        public double Range
        {
            get
            {
                double min = _excludeMin ? _min + EPSILON : _min; // possibly increase min by epsilon
                double max = _excludeMax ? _max - EPSILON : _max; // possible decrease max by epsilon
                return max - min;
            }
        }

        /// <summary>
        /// Creates a string representation of this range.
        /// </summary>
        /// <returns>A string representation of this range.</returns>
        public override string ToString()
        {
            return String.Format("{{Min={0}, Max={1}, ExcludeMin={2}, ExcludeMax={3}}}", _min, _max, _excludeMin, _excludeMax);
        }

        /// <summary>
        /// Constructs a range from a given string representation.
        /// </summary>
        /// <param name="s">A string representation of a range.</param>
        /// <returns>A RangeR representing the given string.</returns>
        public static RangeR FromString(string s)
        {
            RangeR r = RangeR.Empty;
            try
            {
                int minpos = s.IndexOf("Min"); // "Min"
                int comma = s.IndexOf(',', minpos + 4);
                double min = double.Parse(s.Substring(minpos + 4, comma - (minpos + 4)));

                int maxpos = s.IndexOf("Max"); // "Max"
                comma = s.IndexOf(',', maxpos + 4);
                double max = double.Parse(s.Substring(maxpos + 4, comma - (maxpos + 4)));

                int exminpos = s.IndexOf("ExcludeMin"); // "ExcludeMin"
                comma = s.IndexOf(',', exminpos + 11);
                bool excludemin = bool.Parse(s.Substring(exminpos + 11, comma - (exminpos + 11)));

                int exmaxpos = s.IndexOf("ExcludeMax"); // "ExcludeMax"
                comma = s.IndexOf('}', exmaxpos + 11);
                bool excludemax = bool.Parse(s.Substring(exmaxpos + 11, comma - (exmaxpos + 11)));

                r = new RangeR(min, max, excludemin, excludemax);
            }
            catch { }
            return r;
        }

        /// <summary>
        /// Equals operator for a range.
        /// </summary>
        /// <param name="r1">The lefthand side of the operator.</param>
        /// <param name="r2">The righthand side of the operator.</param>
        /// <returns>True if the minimum and maximum values of this range are equal, and their endpoint exclusion 
        /// statuses are equal; false otherwise.</returns>
        /// <remarks>For comparing without regard to endpoint exclusion statuses, use EqualsIgnoreExclusion.</remarks>
        public static bool operator ==(RangeR r1, RangeR r2)
        {
            return (r1._min == r2._min
                && r1._max == r2._max
                && r1._excludeMin == r2._excludeMin
                && r1._excludeMax == r2._excludeMax
                );
        }

        /// <summary>
        /// Not-equals operator for a range.
        /// </summary>
        /// <param name="r1">The lefthand side of the operator.</param>
        /// <param name="r2">The righthand side of the operator.</param>
        /// <returns>True if either the minimum or maximum values of the ranges differ, or either of their exclusion statuses.</returns>
        /// <remarks>For comparing without regard to endpoint exclusion statuses, use EqualsIgnoreExclusion.</remarks>
        public static bool operator !=(RangeR r1, RangeR r2)
        {
            return (r1._min != r2._min
                || r1._max != r2._max
                || r1._excludeMin != r2._excludeMin
                || r1._excludeMax != r2._excludeMax
                );
        }

        /// <summary>
        /// Tests whether the endpoint values of the given range are the same as those for this range.
        /// The exclusion statuses of the endpoints are ignored, and only their values are considered.
        /// </summary>
        /// <param name="r">The given range to test.</param>
        /// <returns>True if the endpoint values of the given range are the same as those for this range;
        /// false otherwise.</returns>
        public bool EqualsIgnoreExclusion(RangeR r)
        {
            return (this.Min == r.Min && this.Max == r.Max);
        }

        /// <summary>
        /// Tests whether the given object is equal to this range.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns>True if the given object is a range equal to this range including the endpoint exclusion statuses; 
        /// false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is RangeR)
            {
                RangeR r = (RangeR) obj;
                return (this.Min == r.Min 
                    && this.Max == r.Max 
                    && this.ExcludeMin == r.ExcludeMin 
                    && this.ExcludeMax == r.ExcludeMax);
            }
            return false;
        }

        /// <summary>
        /// Gets a hashcode for this range.
        /// </summary>
        /// <returns>A hashcode for this range.</returns>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
