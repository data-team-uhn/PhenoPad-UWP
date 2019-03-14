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
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.UI.Xaml.Media.Animation;

namespace PhenoPad.CustomControl
{
    /**
     * 
     * This is the control for note pages
     * It contains all the strokes, add-in images
     * We also perform handwriting recognitiona and natural language processing
     * 
    **/

    public sealed partial class NotePageControl : UserControl
    {
        DispatcherTimer RawStrokeTimer;
        List<InkStroke> RawStrokes;
        InkAnalyzer strokeAnalyzer;
        List<NotePhraseControl> NotePhrases;
        int currentIndex;

        Point lastStrokePoint;
        Rect lastStrokeBound;
        bool recognizing;

        private async void RawStrokeTimer_Tick(object sender = null, object e = null) {

            Debug.WriteLine("tick");

            RawStrokeTimer.Stop();
            List<WordBlockControl> words = new List<WordBlockControl>();
            recognizing = true;
            InkCanvas temp = new InkCanvas();
            temp.InkPresenter.StrokeContainer.AddStrokes(RawStrokes);
            foreach (var s in inkCan.InkPresenter.StrokeContainer.GetStrokes()) {
                if (RawStrokes.Where(x => x.StrokeStartedTime == s.StrokeStartedTime).Count() > 0)
                {
                    s.Selected = true;
                }
            }

            List<HWRRecognizedText> recognitionResults =await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCan.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, false);

            if (recognitionResults == null)
                return;

            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();

            foreach (HWRRecognizedText recog in recognitionResults)
            {
                curLineWordsStackPanel.Children.Clear();
                WordBlockControl wb = new WordBlockControl(recog.selectedCandidate, recog.candidateList, currentIndex);
                words.Add(wb);
                foreach (Button alternative in wb.GetCurWordCandidates())
                    curLineWordsStackPanel.Children.Add(alternative);
                ShowAlternativeCanvas();
                currentIndex++;
            }

            int lineNum = (int)Math.Floor(lastStrokePoint.Y / LINE_HEIGHT);

            NotePhraseControl np = NotePhrases.Where(x => x.lineIndex == lineNum && Math.Abs(lastStrokePoint.X - (x.canvasLeft + x.width)) < 200).FirstOrDefault();
            if (np == null)
            {
                np = new NotePhraseControl();
                Debug.WriteLine(temp.InkPresenter.StrokeContainer.BoundingRect.Width + ")))");
                np.InitializePhrase(words, RawStrokes, lineNum,temp.InkPresenter.StrokeContainer.BoundingRect.Width);
                double left = lastStrokeBound.X;
                np.ShowPhraseAt( left , (lineNum) * LINE_HEIGHT);
                PhraseControlCanvas.Children.Add(np);
                NotePhrases.Add(np);

                Debug.WriteLine("added new phrase control");
            }
            else {
                np.AddWords(words);
                np.AddStrokes(RawStrokes);
                Debug.WriteLine("extended to new phrase");
            }
            RawStrokes.Clear();
            np.UpdateLayout();
            lastStrokeBound = new Rect(0,0,5,5);
            recognizing = false;

        }

        private void recognizedResultTextBlock_Tapped(object sender, TappedRoutedEventArgs e) {
        }

        public void HideCurLineStackPanel() {
            curLineWordsStackPanel.Children.Clear();
            curLineResultPanel.Visibility = Visibility.Collapsed;
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            ClearSelectionAsync();
            curLineResultPanel.Visibility = Visibility.Collapsed;
            curLineWordsStackPanel.Children.Clear();
            RawStrokes.Clear();
            //operationDispathcerTimer.Stop();
            foreach (var stroke in args.Strokes)
            {                                      
                inkAnalyzer.RemoveDataForStroke(stroke.Id);
            }
            //operationDispathcerTimer.Start();
            //dispatcherTimer.Start();
            autosaveDispatcherTimer.Start();
            lastStrokePoint = new Point(0,0);
        }

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            if (!leftLasso)
            {
                //ClearSelection();
                // dispatcherTimer.Stop();
                //operationDispathcerTimer.Stop();
                //RawStrokeTimer.Stop();
                inkOperationAnalyzer.ClearDataForAllStrokes();
                if (lastStrokePoint.Equals(new Point(0, 0)))
                    lastStrokePoint = args.CurrentPoint.Position;
                //if (! lastStrokePoint.Equals(new Point(0,0)))
                //{

                //    double dist = GetDistance(lastStrokePoint, args.CurrentPoint.Position);
                //    Debug.WriteLine($"Stroke dist = {dist}");
                //    if (dist > 50 && !recognizing)
                //    {
                //        RawStrokeTimer_Tick();
                //    }
                //}
            }

            autosaveDispatcherTimer.Stop();
            //recognizeTimer.Stop();
            if (lastStrokePoint.Equals(new Point(0,0)))
                lastStrokePoint = new Point(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y );

        }

        private void ShowAlternativeCanvas() {
            double y = (Math.Floor(lastStrokePoint.Y / LINE_HEIGHT) - 1) * LINE_HEIGHT;
            Canvas.SetTop(curLineResultPanel, y);
            Canvas.SetLeft(curLineResultPanel, lastStrokePoint.X);
            curLineWordsStackPanel.Visibility = Visibility.Visible;
            curLineResultPanel.Visibility = Visibility.Visible;
            UpdateLayout();


        }

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
            //RawStrokeTimer.Start();

            //recognizeTimer.Start();
            lastStrokePoint = new Point(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y );
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {//processing strokes inputs
             //dispatcherTimer.Stop();
             //operationDispathcerTimer.Stop(); 
                foreach (var s in args.Strokes)
                {
                    if (lastStrokeBound.Equals(new Rect(0, 0, 5, 5)))
                        lastStrokeBound = s.BoundingRect;
                    //Process strokes that excess maximum height for recognition
                    if (s.BoundingRect.Height > MAX_WRITING)
                    {
                        inkOperationAnalyzer.AddDataForStroke(s);
                        try
                        {
                            await RecognizeInkOperation();
                        }
                        catch (Exception e)
                        {
                            MetroLogger.getSharedLogger().Error($"InkPresenter_StrokesCollectedAsync in NotePageControl:{e}|{e.Message}");
                        }
                    }
                    //Instantly analyze ink inputs
                    else
                    {
                        s.Selected = false;
                        strokeAnalyzer.AddDataForStroke(s);
                        var result = await strokeAnalyzer.AnalyzeAsync();
                        if (result.Status == InkAnalysisStatus.Updated) {
                            var wordNodes = strokeAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
                            Debug.WriteLine(wordNodes.Count+"***");
                            //check if node is >= 2 and parse only the first word then updates 
                            foreach (var node in wordNodes) {
                                var ids = node.GetStrokeIds();
                                foreach (var id in ids) {
                                    var st = inkCan.InkPresenter.StrokeContainer.GetStrokeById(id);
                                    st.Selected = true;
                                }
                                //strokeAnalyzer.RemoveDataForStrokes(ids);
                            }
                            //List<WordBlockControl> words = new List<WordBlockControl>();
                            //InkCanvas temp = new InkCanvas();
                            //inkCan.InkPresenter.StrokeContainer.CopySelectedToClipboard();
                            //temp.InkPresenter.StrokeContainer.PasteFromClipboard(new Point(0,0));

                            //List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCan.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, false);

                            //if (recognitionResults == null)
                            //    return;


                            //foreach (HWRRecognizedText recog in recognitionResults)
                            //{
                            //    curLineWordsStackPanel.Children.Clear();
                            //    WordBlockControl wb = new WordBlockControl(recog.selectedCandidate, recog.candidateList, currentIndex);
                            //    words.Add(wb);
                            //    foreach (Button alternative in wb.GetCurWordCandidates())
                            //        curLineWordsStackPanel.Children.Add(alternative);
                            //    ShowAlternativeCanvas();
                            //    currentIndex++;
                            //}

                            //int lineNum = (int)Math.Floor(lastStrokePoint.Y / LINE_HEIGHT);

                            //NotePhraseControl np = NotePhrases.Where(x => x.lineIndex == lineNum && Math.Abs(lastStrokePoint.X - (x.canvasLeft + x.width)) < 200).FirstOrDefault();
                            //if (np == null)
                            //{
                            //    np = new NotePhraseControl();
                            //    Debug.WriteLine(temp.InkPresenter.StrokeContainer.BoundingRect.Width + ")))");
                            //    np.InitializePhrase(words, temp.InkPresenter.StrokeContainer.GetStrokes().ToList(), lineNum, temp.InkPresenter.StrokeContainer.BoundingRect.Width);
                            //    double left = lastStrokeBound.X;
                            //    np.ShowPhraseAt(left, (lineNum) * LINE_HEIGHT);
                            //    PhraseControlCanvas.Children.Add(np);
                            //    NotePhrases.Add(np);

                            //    Debug.WriteLine("added new phrase control");
                            //}
                            //else
                            //{
                            //    np.AddWords(words);
                            //    np.AddStrokes(temp.InkPresenter.StrokeContainer.GetStrokes().ToList());
                            //    Debug.WriteLine("extended to new phrase");
                            //}

                            //np.UpdateLayout();
                            //lastStrokeBound = new Rect(0, 0, 5, 5);
                            //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                        }
                    }
                }

                //RawStrokeTimer.Start();

            }
            else
            {//processing strokes selected with left mouse lasso strokes
                leftLossoStroke = args.Strokes;
                foreach (var s in args.Strokes)
                {
                    //TODO: 
                }
            }
        }

        private double GetDistance(Point p1, Point p2) {
            //last stroke not yet recorded
            if (p1.Equals(new Point(0,0))) {
                return 0;
            }

            //Returns the eucliedean distance between 2 points
            double absoluteX_dist = Math.Abs(p1.X - p2.X);
            double euclidean_dist = Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
            return Math.Min(absoluteX_dist, euclidean_dist);
        }

        private void ShowWordCandidate(object sender, RoutedEventArgs args) {
            TextBlock block = (TextBlock)sender;
            Debug.Write(block.Text);
        }

        public string ParseNoteText() {
            string text = "";
            int totalLine = (int)(inkCan.ActualHeight / LINE_HEIGHT);

            for (int i = 0; i < totalLine; i++) {
                var phrases = NotePhrases.Where(p => p.lineIndex == i).ToList();
                phrases = phrases.OrderBy( p=> p.canvasLeft).ToList();
                foreach (var p in phrases)
                    text += p.GetString();
                text += Environment.NewLine;

            }
            return text;
        }


    }


}
