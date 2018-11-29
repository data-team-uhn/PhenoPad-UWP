using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;

namespace WobbrockLib
{
    /// <summary>
    /// Useful utility functions dealing with randomly generated values.
    /// </summary>
    public static class RandomEx
    {
        /// <summary>
        /// Private static member variable -- random number generator.
        /// </summary>
        private static readonly Random _rand = new Random();

        /// <summary>
        /// Gets a random number between low and high, inclusive.
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public static int Integer(int low, int high)
        {
            return _rand.Next(low, high + 1);
        }

        /// <summary>
        /// Gets a random double-precision floating point value between low and high, inclusive.
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public static double Double(double low, double high)
        {
            double d = _rand.NextDouble();
            return GeotrigEx.MapValue(d, 0.0, 1.0, low, high);
        }

        /// <summary>
        /// Gets a random single-precision floating point value between low and high, inclusive.
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public static float Single(float low, float high)
        {
            return (float) Double(low, high);
        }

        /// <summary>
        /// Gets a random boolean value from a fair coin toss.
        /// </summary>
        /// <returns>True or false.</returns>
        public static bool Boolean()
        {
            return Boolean(0.50);
        }

        /// <summary>
        /// Gets a random boolean value from a coin toss, where the coin toss is biased towards 
        /// "heads" (true) with the given chance.
        /// </summary>
        /// <param name="chanceTrue">The bias, or prior probability, of getting a true result.
        /// Allowable values range from 0.00 to 1.00. Values outside of this range will throw
        /// an ArgumentOutOfRangeException.</param>
        /// <returns>True or false.</returns>
        public static bool Boolean(double chanceTrue)
        {
            if (!(0.00 <= chanceTrue && chanceTrue <= 1.00))
                throw new ArgumentOutOfRangeException("chanceTrue");
            double d = Double(0.00, 1.00 - double.Epsilon);
            return (d < chanceTrue);
        }

        /// <summary>
        /// Gets multiple random numbers between low and high, inclusive. The
        /// numbers may be requested to be all distinct.
        /// </summary>
        /// <param name="low">The low end of the number range from which to draw, inclusive.</param>
        /// <param name="high">The high end of the number range from which to draw, inclusive.</param>
        /// <param name="num">The number of random numbers to return. If distinct numbers are requested,
        /// this value must be less than or equal to (<i>high</i> - <i>low</i> + 1).</param>
        /// <param name="requireDistinct">If true, the numbers in the returned array will be distinct,
        /// unless the function cannot succeed because more distinct numbers are requested than possible
        /// distinct values.</param>
        /// <returns>An array of random numbers, possibly distinct if requested, which can be useful for 
        /// determining orders, or <b>null</b> if distinctness is requested but 
        /// (<i>high</i> - <i>low</i> + 1 &lt; <i>num</i>).</returns>
        /// <remarks>
        /// The number of distinct values in the inclusive range is <i>high</i> - <i>low</i> + 1.
        /// The number of values the client is requesting is <i>num</i>. Thus, if <i>num</i> is
        /// greater than the distinct values, the function cannot succeed and returns <b>null</b>.
        /// </remarks>
        public static int[] Array(int low, int high, int num, bool requireDistinct)
        {
            if (requireDistinct && num > high - low + 1)
                return null; // can't succeed

            int[] array = new int[num];
            for (int i = 0; i < num; i++)
            {
                array[i] = _rand.Next(low, high + 1);
                if (requireDistinct)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (array[i] == array[j])
                        {
                            i--; // redo i
                            break;
                        }
                    }
                }
            }
            return array;
        }

        /// <summary>
        /// Gets a random color in RGB space, with RGB values bounded by [low..high].
        /// Note that both values should range from [0..255], and low should not be
        /// greater than high.
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <returns></returns>
        public static Color Color(int low, int high)
        {
            return System.Drawing.Color.FromArgb(
                Integer(low, high),
                Integer(low, high),
                Integer(low, high)
                );
        }

        /// <summary>
        /// Gets a random string of alphanumeric characters at a random length between
        /// a minimum and maximum prescribed length, inclusive.
        /// </summary>
        /// <param name="minlen">The minimum length of the desired string, inclusive.</param>
        /// <param name="maxlen">The maximum length of the desired string, inclusive.</param>
        /// <returns>A random alphanumeric string.</returns>
        public static string String(int minlen, int maxlen)
        {
            int len = RandomEx.Integer(minlen, maxlen); // pick a random length between (minlen, maxlen), inclusive

            string s = System.String.Empty;
            while (s.Length < len)
            {
                s += Path.GetRandomFileName(); // tack on random characters until the chosen len is exceeded
                int loc = s.IndexOf('.'); // be sure to remove the period from the random filename
                s = s.Remove(loc, 1);
            }

            return s.Substring(0, len); // we know we have at least len chars; return the first 'len' amount
        }
    }
}
