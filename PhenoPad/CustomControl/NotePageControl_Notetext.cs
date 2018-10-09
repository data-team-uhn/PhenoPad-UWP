using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PhenoPad.HWRService;
using PhenoPad.Styles;
using Windows.UI.Input.Inking.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using PhenoPad.PhotoVideoService;
using Windows.UI.Notifications;
using Windows.ApplicationModel.Core;
using System.Threading;
using System.Collections;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using PhenoPad.FileService;
using System.Numerics;
using Windows.UI.Xaml.Hosting;
using Windows.Graphics.Display;
using PhenoPad.LogService;
using MetroLog;

namespace PhenoPad.CustomControl
{
    public class NoopConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }

    public class NoteWord
    {
        public uint id;
        public List<uint> strokeIds;
        public string text;
        public List<string> alternatives;
        public NoteWord(InkAnalysisInkWord word)
        {
            this.id = word.Id;
            this.strokeIds = new List<uint>(word.GetStrokeIds());
            this.text = word.RecognizedText;
            this.alternatives = new List<string>(word.TextAlternates);
        }
    }

    public class NoteLine
    {
        public uint id;
        public List<uint> strokeIds;
        public List<Phenotype> annotatedPhenotypes;
        private string _text;
        public string Text
        {
            get
            {
                return _text;
            }
            private set
            {
                _text = value;
            }
        }
        private List<List<string>> _alternatives;
        public List<List<string>> Alternatives
        {
            get
            {
                return _alternatives;
            }
        }
        // public List<NoteWord> words;
        private List<string> _wordStrings;
        public List<String> WordStrings
        {
            get
            {
                return _wordStrings;
            }

        }
        public List<NCRPhenotype> annotations;
        public List<Phenotype> phenotypes;

        private List<HWRRecognizedText> _hwrResult;

        public List<HWRRecognizedText> HwrResult
        {
            get
            {
                return _hwrResult;
            }
            set
            {
                if (value == null)
                    return;
                int i = 0;
                for (; i < value.Count() && i < _hwrResult.Count(); ++i)
                {
                    // if hwr results of a word not change, restore the candidate selection info
                    if (_hwrResult[i].candidateList.Count() == value[i].candidateList.Count()
                        && _hwrResult[i].candidateList.All(value[i].candidateList.Contains))
                    {
                        // do nothing
                    }
                    else
                    {
                        // update result
                        _hwrResult[i] = value[i];
                    }
                }
                // we still have some result to add
                while (i < value.Count())
                {
                    _hwrResult.Add(value[i]);
                    i++;
                }
                // the new result is shorter, delete extral stored results
                if (i < _hwrResult.Count())
                {
                    _hwrResult.RemoveRange(i, _hwrResult.Count() - i);
                }


                // update 
                _wordStrings = new List<string>();
                _alternatives = new List<List<string>>();
                _text = "";
                foreach (var res in _hwrResult)
                {
                    _wordStrings.Add(res.selectedCandidate);
                    _alternatives.Add(res.candidateList);
                }
                _text = String.Join(" ", _wordStrings);
            }
        }

        public NoteLine(InkAnalysisLine line)
        {
            this.id = line.Id;
            this.strokeIds = new List<uint>(line.GetStrokeIds());
            this._text = "";
            this._hwrResult = new List<HWRRecognizedText>();
            annotatedPhenotypes = new List<Phenotype>();
            // words = new List<NoteWord>();
            _wordStrings = new List<string>();
            _alternatives = new List<List<string>>();
            annotations = new List<NCRPhenotype>();
            phenotypes = new List<Phenotype>();
            /***
            foreach (var word in line.Children)
            {
                if (word.Kind == InkAnalysisNodeKind.InkWord)
                {
                    var aword = (InkAnalysisInkWord)word;
                    words.Add(new NoteWord(aword));
                    wordStrings.Add(aword.RecognizedText.Trim());
                }
            }
            **/
        }


        public void addAnnotations(List<NCRPhenotype> result)
        {
            bool updated = false;
            int i = 0;

            if (annotations.Count() <= result.Count())
            {
                for (; i < annotations.Count(); ++i)
                {
                    if (annotations[i] != result[i])
                    {
                        updated = true;
                        break;
                    }
                }
            }

            if (updated)
            {
                reSetAnnotations(result);
            }
            else
            {
                // append extral result
                while (i < result.Count)
                {
                    addOneAnnotation(result[i]);
                    ++i;
                }
            }
        }

        private void reSetAnnotations(List<NCRPhenotype> result)
        {
            annotations = result;
            // update phenotypes 
            phenotypes = new List<Phenotype>();
            foreach (var ann in annotations)
            {
                Phenotype p = new Phenotype(ann);
                p.sourceType = SourceType.Notes;
                p.state = PhenotypeManager.getSharedPhenotypeManager().getStateByHpid(p.hpId);
                phenotypes.Add(p);
                //var windex = convertStringIndexToWordIndex(ann.start, ann.end);
            }


        }

        private void addOneAnnotation(NCRPhenotype ncr)
        {
            annotations.Add(ncr);
            // update annotations by word
            Phenotype p = new Phenotype(ncr);
            p.sourceType = SourceType.Notes;
            // update saved phenotypes
            phenotypes.Add(p);
            p.state = PhenotypeManager.getSharedPhenotypeManager().getStateByHpid(p.hpId);
            // var windex = convertStringIndexToWordIndex(ncr.start, ncr.end);
            // annotationByWord.Add(Tuple.Create(p, windex.Item1, windex.Item2));
        }

        public void updateHwrResult(int wordind, int selectind, int previousInd)
        {
            this.HwrResult[wordind].selectedIndex = selectind;
            string previous = this.HwrResult[wordind].candidateList[previousInd];
            string selected = this.HwrResult[wordind].candidateList[selectind];
            int index = HWRManager.getSharedHWRManager().abbreviations.IndexOf(previous);
            if (index != -1) {
                HWRManager.getSharedHWRManager().abbreviations[index] = selected;

            }
            this.HwrResult[wordind].selectedCandidate = selected;

            Debug.WriteLine($"\n new alter:{this.HwrResult[wordind].selectedCandidate}");

            // update 
            _wordStrings[wordind] = this.HwrResult[wordind].selectedCandidate;
            _text = String.Join(" ", _wordStrings);
        }

        private (int, int) convertStringIndexToWordIndex(int start, int end)
        {
            int i = 0;
            var result = (-1, -1);
            int wordStart = 0;
            foreach (var word in _wordStrings)
            {
                if (start >= wordStart && start < wordStart + word.Length)
                    result.Item1 = i;
                if (end >= wordStart && end - 1 < wordStart + word.Length)
                {
                    result.Item2 = i;
                    break;
                }
                i++;
                wordStart += word.Length + 1; // blank
            }
            return result;
        }

        /// <summary>
        /// Check whether a line has been updated
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public bool hasUpdated(InkAnalysisLine line)
        {
            return !line.RecognizedText.Equals(this._text);
            /**
            // if number of strokes inside a line has changed, this line has been updated
            if(line.GetStrokeIds().Count() != this.strokeIds.Count())
                return true;
            // if strokes ids inside it have changed, this line has been updated
            if (!this.strokeIds.All(line.GetStrokeIds().Contains))
                return true;
            return false;
            **/
        }
    }
}
