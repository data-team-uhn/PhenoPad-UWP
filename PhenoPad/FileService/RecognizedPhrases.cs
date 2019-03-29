using PhenoPad.CustomControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace PhenoPad.FileService
{
    /// <summary>
    /// Represents a recognized phrase in the note
    /// </summary>
    public class RecognizedPhrases
    {
        public string current;//selected candidate
        public string candidate2;
        public string candidate3;
        public string candidate4;
        public string candidate5;

        public int word_index;
        public int phrase_index;
        public int line_index;


        /// <summary>
        /// Empty construtor for serialization.
        /// </summary>
        public RecognizedPhrases()
        {
        }

        public RecognizedPhrases(int lineNum, int phrase, int index, string selected,List<string>candidates) {
            line_index = lineNum;
            phrase_index = phrase;
            word_index = index;
            current = selected;
            candidates.Remove(selected);
            candidate2 = candidates[0];
            candidate3 = candidates[1];
            candidate4 = candidates[2];
            candidate5 = candidates[3];
        }
    }
}
