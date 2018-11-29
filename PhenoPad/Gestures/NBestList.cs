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
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PhenoPad.Gestures
{
    public class NBestList
    {
        #region NBestResult Inner Class

        public class NBestResult : IComparable
        {
            public static NBestResult Empty = new NBestResult(String.Empty, -1d, -1d, 0d);

            private string _name;
            private double _score;
            private double _distance;
            private double _angle;

            // constructor
            public NBestResult(string name, double score, double distance, double angle)
            {
                _name = name;
                _score = score;
                _distance = distance;
                _angle = angle;
            }

            public string Name { get { return _name; } }
            public double Score { get { return _score; } }
            public double Distance { get { return _distance; } }
            public double Angle { get { return _angle; } }
            public bool IsEmpty { get { return _score == -1d; } }

            // sorts in descending order of Score
            public int CompareTo(object obj)
            {
                if (obj is NBestResult)
                {
                    NBestResult r = (NBestResult) obj;
                    if (_score < r._score)
                        return 1;
                    else if (_score > r._score)
                        return -1;
                    return 0;
                }
                else throw new ArgumentException("object is not an NBestResult");
            }
        }

        #endregion

        #region Fields

        public static NBestList Empty = new NBestList();
        private ArrayList _nBestList;

        #endregion

        #region Constructor & Methods

        public NBestList()
        {
            _nBestList = new ArrayList();
        }

        public bool IsEmpty
        {
            get
            {
                return _nBestList.Count == 0;
            }
        }

        public void AddResult(string name, double score, double distance, double angle)
        {
            NBestResult r = new NBestResult(name, score, distance, angle);
            _nBestList.Add(r);
        }

        public void SortDescending()
        {
            _nBestList.Sort();
        }

        #endregion

        #region Top Result

        /// <summary>
        /// Gets the gesture name of the top result of the NBestList.
        /// </summary>
        public string Name
        {
            get
            {
                if (_nBestList.Count > 0)
                {
                    NBestResult r = (NBestResult) _nBestList[0];
                    return r.Name;
                }
                return String.Empty;
            }
        }

        /// <summary>
        /// Gets the [0..1] matching score of the top result of the NBestList.
        /// </summary>
        public double Score
        {
            get
            {
                if (_nBestList.Count > 0)
                {
                    NBestResult r = (NBestResult) _nBestList[0];
                    return r.Score;
                }
                return -1.0;
            }
        }

        /// <summary>
        /// Gets the average pixel distance of the top result of the NBestList.
        /// </summary>
        public double Distance
        {
            get
            {
                if (_nBestList.Count > 0)
                {
                    NBestResult r = (NBestResult) _nBestList[0];
                    return r.Distance;
                }
                return -1.0;
            }
        }

        /// <summary>
        /// Gets the average pixel distance of the top result of the NBestList.
        /// </summary>
        public double Angle
        {
            get
            {
                if (_nBestList.Count > 0)
                {
                    NBestResult r = (NBestResult) _nBestList[0];
                    return r.Angle;
                }
                return 0.0;
            }
        }

        #endregion

        #region All Results

        public NBestResult this[int index]
        {
            get
            {
                if (0 <= index && index < _nBestList.Count)
                {
                    return (NBestResult) _nBestList[index];
                }
                return null;
            }
        }

        public string[] Names
        {
            get
            {
                string[] s = new string[_nBestList.Count];
                if (_nBestList.Count > 0)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        s[i] = ((NBestResult) _nBestList[i]).Name;
                    }
                }
                return s;
            }
        }

        public string NamesString
        {
            get
            {
                string s = String.Empty;
                if (_nBestList.Count > 0)
                {
                    foreach (NBestResult r in _nBestList)
                    {
                        s += String.Format("{0},", r.Name);
                    }
                }
                return s.TrimEnd(new char[1] { ',' });
            }
        }

        public double[] Scores
        {
            get
            {
                double[] s = new double[_nBestList.Count];
                if (_nBestList.Count > 0)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        s[i] = ((NBestResult) _nBestList[i]).Score;
                    }
                }
                return s;
            }
        }

        public string ScoresString
        {
            get
            {
                string s = String.Empty;
                if (_nBestList.Count > 0)
                {
                    foreach (NBestResult r in _nBestList)
                    {
                        s += String.Format("{0:F3},", Math.Round(r.Score, 3));
                    }
                }
                return s.TrimEnd(new char[1] { ',' });
            }
        }

        #endregion

    }
}
