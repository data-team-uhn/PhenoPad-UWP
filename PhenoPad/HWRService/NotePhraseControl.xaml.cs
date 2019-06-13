using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PhenoPad.HWRService;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using System;

namespace PhenoPad.CustomControl
{
    public sealed partial class NotePhraseControl : UserControl
    {
        public int lineIndex;
        public int phraseIndex;
        public float LINE_HEIGHT = 50;
        public List<WordBlockControl> words;

        public NotePhraseControl(int lineNum, List<WordBlockControl> words = null)
        {
            InitializeComponent();
            lineIndex = lineNum;
            this.words = new List<WordBlockControl>();
            if (words != null)
                this.words = words;
            UpdatePhraseLayout();
        }

        public void UpdatePhraseLayout() {
            //this guarantees list will have at least one word
            if (words.Count == 0)
                return;
            try
            {
                PhraseCanvas.Children.Clear();
                words = words.OrderBy(w => w.left).ToList();
                StackPanel sp = InitNewBlockPanel();
                sp.Children.Add(words[0]);
                //Debug.WriteLine($"WBC left = {words[0].left}");
                for (int i = 1; i < words.Count; i++)
                {
                    //same block
                    if (words[i].left == words[i - 1].left)
                    {
                        sp.Children.Add(words[i]);
                    }
                    //new block
                    else if (words[i].left > words[i - 1].left)
                    {
                        Debug.WriteLine($"new block WBC left = {words[i].left}");

                        PhraseCanvas.Children.Add(sp);
                        Canvas.SetLeft(sp, words[i - 1].left);
                        Canvas.SetTop(sp, 0);
                        sp = InitNewBlockPanel();
                        sp.Children.Add(words[i]);
                    }
                }
                if (sp.Children.Count > 0)
                {
                    PhraseCanvas.Children.Add(sp);
                    Canvas.SetLeft(sp, words[words.Count - 1].left);
                    Canvas.SetTop(sp, 0);
                }

                UpdateLayout();
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }
        }

        public StackPanel InitNewBlockPanel() {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Spacing = 10;
            sp.Height = LINE_HEIGHT;
            return sp;
        }

        public List<string> GetStringAsList() {
            List<string> str = new List<string>();
            foreach (WordBlockControl s in words)
                str.Add(s.current.Trim('(').Trim(')'));
            return str;
        }

        private List<string> ParseStringAsList(List<WordBlockControl> updated)
        {
            List<string> str = new List<string>();
            foreach (var s in updated)
                str.Add(s.current.Trim('(').Trim(')'));
            return str;
        }

        public string GetString() {
            string text = "";
            foreach (WordBlockControl s in words) {
                text += s.current.Trim('(').Trim(')') + " ";
            }
            return text.Trim();
        }

        internal List<WordBlockControl> MergeNewResultToOld(List<WordBlockControl> updated) {
            List<WordBlockControl> merged = new List<WordBlockControl>();

            var old = GetStringAsList();
            var _new = ParseStringAsList(updated);

            var indexes = alignTwoStringList(_new, old);
            var old_index = indexes.Item2;
            var new_index = indexes.Item1;

            int total = new_index.Count;

            for(int i = 0; i < total; i++) {

                if (old_index[i] == -1)
                {
                    merged.Add(updated[new_index[i]]);
                }
                else if (new_index[i] == -1)
                {
                    //dont do anything
                }
                else {
                    if (words[old_index[i]].corrected)
                        merged.Add(words[old_index[i]]);
                    else
                        merged.Add(updated[new_index[i]]);
                }
            }

            return merged;
        }

        private (List<int>, List<int>) alignTwoStringList(List<string> newList, List<string> oldList)
        {
            // Dynamic programming for caluculating best alignment of two string list
            // http://www.biorecipes.com/DynProgBasic/code.html

            // score matrix
            int gap_score = 0;
            int mismatch_score = 0;
            int match_score = 1;

            int newLen = newList.Count();
            int oldLen = oldList.Count();
            int[,] scoreMatrix = new int[newLen + 1, oldLen + 1];
            int[,] tracebackMatrix = new int[newLen + 1, oldLen + 1];

            // base condition
            scoreMatrix[0, 0] = 0;
            int ind = 0;
            for (ind = 0; ind <= oldLen; ++ind)
            {
                scoreMatrix[0, ind] = 0;
                tracebackMatrix[0, ind] = 1;
            }

            for (ind = 0; ind <= newLen; ++ind)
            {
                scoreMatrix[ind, 0] = 0;
                tracebackMatrix[ind, 0] = -1;
            }
            tracebackMatrix[0, 0] = 0;

            // recurrence 
            int i;
            int j;
            for (i = 1; i <= newLen; ++i)
                for (j = 1; j <= oldLen; ++j)
                {
                    // align i and j
                    scoreMatrix[i, j] = newList[i - 1] == oldList[j - 1] ? scoreMatrix[i - 1, j - 1] + match_score : scoreMatrix[i - 1, j - 1] + mismatch_score;
                    tracebackMatrix[i, j] = 0;
                    // insert gap to old 
                    if (scoreMatrix[i - 1, j] + gap_score > scoreMatrix[i, j])
                        tracebackMatrix[i, j] = -1;
                    // insert gap to new 
                    if (scoreMatrix[i, j - 1] + gap_score > scoreMatrix[i, j])
                        tracebackMatrix[i, j] = 1;
                }

            // trace back
            List<int> newIndex = new List<int>();
            List<int> oldIndex = new List<int>();

            i = newLen;
            j = oldLen;
            while (i >= 0 && j >= 0)
            {
                switch (tracebackMatrix[i, j])
                {
                    case -1:
                        newIndex.Insert(0, i - 1);
                        oldIndex.Insert(0, -1); // gap
                        i--;
                        break;
                    case 1:
                        newIndex.Insert(0, -1); // gap
                        oldIndex.Insert(0, j - 1);
                        j--;
                        break;
                    case 0:
                        newIndex.Insert(0, i - 1);
                        oldIndex.Insert(0, j - 1);
                        i--;
                        j--;
                        break;

                }

            }

            // remove fake element at beginning
            newIndex.RemoveAt(0);
            oldIndex.RemoveAt(0);
            if (newIndex.Count() != oldIndex.Count())
                Debug.WriteLine("Alignment error!");

            // use newIndex as base line
            string newString = "";
            string oldString = "";
            for (i = 0; i < newIndex.Count(); i++)
            {
                oldString += oldIndex[i] == -1 ? "_\t" : oldList[oldIndex[i]] + "\t";
                newString += newIndex[i] == -1 ? "_\t" : newList[newIndex[i]] + "\t";
            }

            //Debug.WriteLine("Alignment results: ");
            //Debug.WriteLine("Old string:    " + oldString);
            //foreach (var k in oldIndex)
            //    Debug.Write(k + " ");
            //Debug.WriteLine("\n--");
            //Debug.WriteLine("New String:    " + newString);
            //foreach (var kk in newIndex)
            //    Debug.Write(kk + " ");
            //Debug.WriteLine("\n");

            return (newIndex, oldIndex);

        }

        internal void UpdateRecognition(List<WordBlockControl> new_w, bool fromServer = false)
        {
            if (new_w != null && new_w.Count > 0)
            {
                var merged_new = MergeNewResultToOld(new_w);
                words.Clear();
                int new_index = 0;
                foreach (var w in merged_new)
                {
                    w.word_index = new_index;
                    words.Add(w);
                    new_index++;
                }
                UpdatePhraseLayout();
                if (MainPage.Current != null)
                {
                    MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: lineIndex);
                }
                Debug.WriteLine($"====== updated line {lineIndex}, server {fromServer}, word count={words.Count} ");
                UpdateLayout();
            }
        }

        public List<TextBlock> GetCurLineHWR() {
            List<TextBlock> result = new List<TextBlock>();
            for (int i = 0; i < words.Count; i++) {
                var tb = words[i].GetCurWordTextBlock();

                if (i == words.Count - 1) {
                    tb.Foreground = new SolidColorBrush(Colors.SkyBlue);
                    tb.UpdateLayout();                  
                }
                result.Add(tb);
            }
            return result;
        }
    }
}
