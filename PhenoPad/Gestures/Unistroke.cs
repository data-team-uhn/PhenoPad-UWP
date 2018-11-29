/**
 * The $1 Unistroke Recognizer (C# version)
 *
 *		Jacob O. Wobbrock, Ph.D.
 * 		The Information School
 *		University of Washington
 *		Mary Gates Hall, Box 352840
 *		Seattle, WA 98195-2840
 *		wobbrock@u.washington.edu
 *
 *		Andrew D. Wilson, Ph.D.
 *		Microsoft Research
 *		One Microsoft Way
 *		Redmond, WA 98052
 *		awilson@microsoft.com
 *
 *		Yang Li, Ph.D.
 *		Department of Computer Science and Engineering
 * 		University of Washington
 *		The Allen Center, Box 352350
 *		Seattle, WA 98195-2840
 * 		yangli@cs.washington.edu
 *
 * The Protractor enhancement was published by Yang Li and programmed here by 
 * Jacob O. Wobbrock.
 *
 *	Li, Y. (2010). Protractor: A fast and accurate gesture 
 *	  recognizer. Proceedings of the ACM Conference on Human 
 *	  Factors in Computing Systems (CHI '10). Atlanta, Georgia
 *	  (April 10-15, 2010). New York: ACM Press, pp. 2169-2172.
 * 
 * This software is distributed under the "New BSD License" agreement:
 * 
 * Copyright (c) 2007-2011, Jacob O. Wobbrock, Andrew D. Wilson and Yang Li.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *    * Redistributions of source code must retain the above copyright
 *      notice, this list of conditions and the following disclaimer.
 *    * Redistributions in binary form must reproduce the above copyright
 *      notice, this list of conditions and the following disclaimer in the
 *      documentation and/or other materials provided with the distribution.
 *    * Neither the names of the University of Washington nor Microsoft,
 *      nor the names of its contributors may be used to endorse or promote 
 *      products derived from this software without specific prior written
 *      permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
 * IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL Jacob O. Wobbrock OR Andrew D. Wilson
 * OR Yang Li BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
 * OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
 * OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
**/

using System;
using System.Collections.Generic;
using System.Drawing;
using WobbrockLib;

namespace PhenoPad.Gestures
{
	public class Unistroke : IComparable
	{
        public string Name;
        public List<TimePointR> RawPoints; // raw points (for drawing) -- read in from XML
        public List<PointR> Points;        // pre-processed points (for matching) -- created when loaded
        public List<double> Vector;        // vector representation -- for Protractor

        /// <summary>
        /// Constructor of a unistroke gesture. A unistroke is comprised of a set of points drawn
        /// out over time in a sequence.
        /// </summary>
        /// <param name="name">The name of the unistroke gesture.</param>
        /// <param name="timepoints">The array of points supplied for this unistroke.</param>
        public Unistroke(string name, List<TimePointR> timepoints)
		{
			this.Name = name;
            this.RawPoints = new List<TimePointR>(timepoints); // copy (saved for drawing)
            double I = GeotrigEx.PathLength(TimePointR.ConvertList(timepoints)) / (Recognizer.NumPoints - 1); // interval distance between points
            this.Points = TimePointR.ConvertList(SeriesEx.ResampleInSpace(timepoints, I));
            double radians = GeotrigEx.Angle(GeotrigEx.Centroid(this.Points), this.Points[0], false);
            this.Points = GeotrigEx.RotatePoints(this.Points, -radians);
            this.Points = GeotrigEx.ScaleTo(this.Points, Recognizer.SquareSize);
            this.Points = GeotrigEx.TranslateTo(this.Points, Recognizer.Origin, true);
            this.Vector = Vectorize(this.Points); // vectorize resampled points (for Protractor)
		}



        /// <summary>
        /// Vectorize the unistroke according to the algorithm by Yang Li for use in the Protractor extension to $1.
        /// </summary>
        /// <param name="points">The resampled points in the gesture to vectorize.</param>
        /// <returns>A vector of cosine distances.</returns>
        /// <seealso cref="http://yangl.org/protractor/"/>
        public static List<double> Vectorize(List<PointR> points)
        {
            double sum = 0.0;
            List<double> vector = new List<double>(points.Count * 2);
            for (int i = 0; i < points.Count; i++)
            {
                vector.Add(points[i].X);
                vector.Add(points[i].Y);
                sum += points[i].X * points[i].X + points[i].Y * points[i].Y;
            }
            double magnitude = Math.Sqrt(sum);
            for (int i = 0; i < vector.Count; i++)
            {
                vector[i] /= magnitude;
            }
            return vector;
        }

        /// <summary>
        /// Gets the duration in milliseconds of this gesture.
        /// </summary>
        public long Duration
        {
            get { return (RawPoints.Count >= 2) ? RawPoints[RawPoints.Count - 1].Time - RawPoints[0].Time : 0L; }
        }

        /// <summary>
        /// Sort comparator in descending order of score.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (obj is Unistroke)
            {
                Unistroke g = (Unistroke) obj;
                return this.Name.CompareTo(g.Name);
            }
            else throw new ArgumentException("object is not a Gesture");
        }

        /// <summary>
        /// Pulls the gesture name from the file name, e.g., "circle03" from "C:\gestures\circles\circle03.xml".
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ParseName(string filename)
        {
            int start = filename.LastIndexOf('\\');
            int end = filename.LastIndexOf('.');
            return filename.Substring(start + 1, end - start - 1);
        }

    }
}
