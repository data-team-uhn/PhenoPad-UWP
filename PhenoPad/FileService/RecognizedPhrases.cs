using System.Collections.Generic;

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

        public bool is_corrected;

        public int word_index;
        public double left;
        public int line_index;

        public int pageId;


        /// <summary>
        /// Empty construtor for serialization.
        /// </summary>
        public RecognizedPhrases()
        {
            current = "";
            candidate2 = "";
            candidate3 = "";
            candidate4 = "";
            candidate5 = "";

        }

        public RecognizedPhrases(int lineNum, double left, int index, string selected,List<string>candidates, bool isCorrected) {
            line_index = lineNum;
            this.left = left;
            word_index = index;
            current = "";
            candidate2 = "";
            candidate3 = "";
            candidate4 = "";
            candidate5 = "";

            is_corrected = isCorrected;

            current = selected;
            if (candidates.Contains(selected))
                candidates.Remove(selected);

            foreach (var s in candidates) {
                if (candidate2.Length == 0)
                    candidate2 = s;
                else if (candidate3.Length == 0)
                    candidate3 = s;
                else if (candidate4.Length == 0)
                    candidate4 = s;
                else if (candidate5.Length == 0)
                    candidate5 = s;
            }
        }
    }
}
