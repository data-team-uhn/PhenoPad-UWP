using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Windows.UI.Input.Inking;

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

        [XmlArray("strokes")]
        [XmlArrayItem("stroke")]
        public List<DateTime> strokes;

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
        }

        public RecognizedPhrases(string noteId, string pageId,int lineNum, int index, string selected,List<string>candidates, List<InkStroke> strokes,bool isCorrected,bool isAbbr) {
            line_index = lineNum;
            word_index = index;
            is_corrected = isCorrected;
            is_abbr = isAbbr;
            current = selected;
            candidate_list = candidates;
            this.strokes = new List<DateTime>();
            foreach (var s in strokes)
                this.strokes.Add(s.StrokeStartedTime.Value.DateTime);
        }
    }
}
