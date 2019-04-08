using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Serialization;
using PhenoPad.HWRService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class NotePhraseControl : UserControl
    {
        public double canvasLeft;
        public double canvasTop;
        public double width;
        public int lineIndex;
        public float LINE_HEIGHT = 50;
        public List<WordBlockControl> words;

        public NotePhraseControl(int lineNum, List<WordBlockControl> words = null, double left = 0,double width = 0)
        {
            InitializeComponent();
            lineIndex = lineNum;
            this.width = width;
            this.words = new List<WordBlockControl>();
            canvasLeft = left;
            if (words != null)
            {
                foreach (WordBlockControl wb in words)
                    AddWord(wb);
            }
        }

        public List<string> GetStringAsList() {
            List<string> str = new List<string>();
            foreach (WordBlockControl s in words)
                str.Add(s.current);
            return str;
        }

        public string GetString() {
            string text = "";
            foreach (WordBlockControl s in words) {
                text += s.current.Trim('(').Trim(')') + " ";
                if (s.abbr_current != "")
                    text += s.abbr_current.Trim('(').Trim(')')+" ";
            }
            return text;
        }

        public void SetPhrasePosition(double left, double top) {
            canvasLeft = left;
            canvasTop = top;
        }

        public void AddWord(WordBlockControl word) {
            words.Add(word);
            RecognizedPhrase.Children.Add(word);
        }

        internal void UpdateRecognition(List<HWRRecognizedText> updated)
        {
            if (updated != null) {
                RecognizedPhrase.Children.Clear();
                var dict = HWRManager.getSharedHWRManager().getDictionary();
                words.Clear();
                for (int i = 0; i < updated.Count; i++)
                {
                    HWRRecognizedText recognized = updated[i];
                    //not an abbreviation
                    if (dict.ContainsKey(recognized.selectedCandidate.ToLower()))
                    {
                        var extended = updated[i + 1];
                        extended.candidateList.Insert(0, recognized.selectedCandidate);
                        WordBlockControl wb2 = new WordBlockControl(lineIndex, 0, i, extended.selectedCandidate, extended.candidateList);
                        wb2.is_abbr = true;
                        AddWord(wb2);
                        i++;
                    }
                    else {
                        WordBlockControl wb = new WordBlockControl(lineIndex, 0, i, recognized.selectedCandidate, recognized.candidateList);
                        AddWord(wb);
                    }
                }

            }

        }


    }
}
