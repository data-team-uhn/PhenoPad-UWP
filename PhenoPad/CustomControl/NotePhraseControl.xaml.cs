using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        public NotePhraseControl()
        {
            this.InitializeComponent();
            words = new List<WordBlockControl>();           
        }

        public void InitializePhrase(List<WordBlockControl> words,List<InkStroke> strokes,int lineNum, double width) {
            this.words = words;
            lineIndex = lineNum;
            foreach(var word in words)
                RecognizedPhrase.Children.Add(word);
            this.width = width;
            foreach (var s in strokes) {
                RawStrokes.InkPresenter.StrokeContainer.AddStroke(s.Clone());
            }
            
        }

        public string GetString() {
            string text = " ";
            foreach (WordBlockControl s in words)
                text += s.current + " ";
            return text;
        }

        public void ShowPhraseAt(double left, double top) {
            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);
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

        public void AddWords(List<WordBlockControl> words) {
            this.words.AddRange(words);
            foreach(var word in words)
                RecognizedPhrase.Children.Add(word);
        }

        public void ToggleTextView(object sender, DoubleTappedRoutedEventArgs args)
        {
            RawStrokes.Visibility = Visibility.Collapsed;
            RecognizedPhrase.Visibility = Visibility.Visible;

        }

        public void ToggleRawView(object sender, DoubleTappedRoutedEventArgs args)
        {
            RawStrokes.Visibility = Visibility.Visible;
            RecognizedPhrase.Visibility = Visibility.Collapsed;
        }

    }
}
