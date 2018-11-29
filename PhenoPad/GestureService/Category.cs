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
using System.Diagnostics;

namespace PhenoPad.GestureService
{
    public class Category
    {
        private string _name;
        private List<Unistroke> _prototypes;

        public Category(string name)
        {
            _name = name;
            _prototypes = null;
        }

        public Category(string name, Unistroke firstExample)
        {
            _name = name;
            _prototypes = new List<Unistroke>();
            AddExample(firstExample);
        }
        
        public Category(string name, List<Unistroke> examples)
        {
            _name = name;
            _prototypes = new List<Unistroke>(examples.Count);
            for (int i = 0; i < examples.Count; i++)
            {
                AddExample(examples[i]);
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public int NumExamples
        {
            get
            {
                return _prototypes.Count;
            }
        }

        /// <summary>
        /// Indexer that returns the prototype at the given index within
        /// this gesture category, or null if the gesture does not exist.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Unistroke this[int i]
        {
            get
            {
                if (0 <= i && i < _prototypes.Count)
                {
                    return _prototypes[i];
                }
                else
                {
                    return null;
                }
            }
        }

        public void AddExample(Unistroke p)
        {
            bool success = true;
            try
            {
                // first, ensure that p's name is right
                string name = ParseName(p.Name);
                if (name != _name)
                    throw new ArgumentException("Prototype name does not equal the name of the category to which it was added.");

                // second, ensure that it doesn't already exist
                for (int i = 0; i < _prototypes.Count; i++)
                {
                    if (p.Name == _prototypes[i].Name)
                        throw new ArgumentException("Prototype name was added more than once to its category.");
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                success = false;
            }
            if (success)
            {
                _prototypes.Add(p);
            }
        }

        /// <summary>
        /// Pulls the category name from the gesture name, e.g., "circle" from "circle03".
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ParseName(string s)
        {
            string category = String.Empty;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (!Char.IsDigit(s[i]))
                {
                    category = s.Substring(0, i + 1);
                    break;
                }
            }
            return category;
        }

    }
}
