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

        public string GetString() {
            string text = " ";
            foreach (WordBlockControl s in words)
                text += s.current + " ";
            return text;
        }

        public void ShowPhraseAt(double left, double top) {
            canvasLeft = left;
            canvasTop = top;
        }

        public void AddStrokes(List<InkStroke> strokes, double boundWidth) {
            foreach (var s in strokes) {
                RawStrokes.InkPresenter.StrokeContainer.AddStroke(s.Clone());
                //this.width += Math.Abs((s.BoundingRect.X + s.BoundingRect.Width) - (canvasLeft + width));                
            }
            width += boundWidth;
        }

        public void AddWord(WordBlockControl word) {
            words.Add(word);
            RecognizedPhrase.Children.Add(word);
        }

        public void ToggleTextView(object sender, DoubleTappedRoutedEventArgs args)
        {
            RawStrokes.Visibility = Visibility.Collapsed;
            RecognizedPhrase.Visibility = Visibility.Visible;

        }

        internal void UpdateRecognition(List<HWRRecognizedText> updated)
        {
            RecognizedPhrase.Children.Clear();
            words.Clear();
            for (int i = 0; i < updated.Count; i++) {
                HWRRecognizedText recognized = updated[i];
                WordBlockControl wb = new WordBlockControl(lineIndex, 0, i, recognized.selectedCandidate, recognized.candidateList);
                AddWord(wb);
            }
            UpdateLayout();
        }

        public void ToggleRawView(object sender, DoubleTappedRoutedEventArgs args)
        {
            RawStrokes.Visibility = Visibility.Visible;
            RecognizedPhrase.Visibility = Visibility.Collapsed;
        }

    }
}
