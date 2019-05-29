using System.Collections.Generic;
using System.Xml.Serialization;

namespace PhenoPad.FileService
{
    /// <summary>
    /// Represents a recognized phrase in the note
    /// </summary>
    /// 
    public class RecognizedPhrases
    {
        public string current;//selected candidate

        [XmlArray("alternatives")]
        [XmlArrayItem("candidate")]
        public List<string> candidate_list;

        public bool is_corrected;
        public int word_index;
        public int line_index;
        public double canvasLeft;
        public string pageId;
        public string noteId;
        public bool is_abbr;


        /// <summary>
        /// Empty construtor for serialization.
        /// </summary>
        public RecognizedPhrases()
        {
            current = "";
            candidate_list = new List<string>();
        }
        public RecognizedPhrases(string noteId, string pageId,int lineNum, double canvasLeft, int index, string selected,List<string>candidates, bool isCorrected,bool isAbbr) {
            line_index = lineNum;
            this.canvasLeft = canvasLeft;
            word_index = index;
            is_corrected = isCorrected;
            is_abbr = isAbbr;
            current = selected;
            candidate_list = candidates;
        }
    }
}
