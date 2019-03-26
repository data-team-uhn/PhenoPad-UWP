﻿using PhenoPad.PhenotypeService;
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
        Dictionary<int, List<InkStroke>> strokeRecords;
        int currentIndex;
        int lastWordCount;
        bool mergedWord = false;
        Point lastStrokePoint;
        Rect lastStrokeBound;
        bool recognizing;

        private async void RawStrokeTimer_Tick(object sender = null, object e = null) {

            RawStrokeTimer.Stop();

            var result = await strokeAnalyzer.AnalyzeAsync();
            if (result.Status == InkAnalysisStatus.Updated)
            {
                var wordNodes = strokeAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
                Debug.WriteLine(wordNodes.Count + "***");
                foreach (var node in wordNodes)
                {
                    var ids = node.GetStrokeIds();
                    int lineNum = (int)Math.Floor(node.BoundingRect.Y / LINE_HEIGHT);
                    UpdateRecognizedWordStyle(lineNum, ids.ToList());
                    lastStrokeBound = node.BoundingRect;
                    strokeAnalyzer.ClearDataForAllStrokes();
                }
                    //    //List<WordBlockControl> words = new List<WordBlockControl>();
                    //    //InkCanvas temp = new InkCanvas();
                    //    //inkCan.InkPresenter.StrokeContainer.CopySelectedToClipboard();
                    //    //temp.InkPresenter.StrokeContainer.PasteFromClipboard(new Point(0, 0));

                    //    //List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCan.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, server: false);

                    //    //if (recognitionResults == null)
                    //    //    return;

                    //    //foreach (HWRRecognizedText recog in recognitionResults)
                    //    //{
                    //    //    curLineWordsStackPanel.Children.Clear();
                    //    //    WordBlockControl wb = new WordBlockControl(recog.selectedCandidate, recog.candidateList, currentIndex);
                    //    //    words.Add(wb);
                    //    //    foreach (Button alternative in wb.GetCurWordCandidates())
                    //    //        curLineWordsStackPanel.Children.Add(alternative);
                    //    //    ShowAlternativeCanvas(new Point(lastStrokeBound.X, lastStrokeBound.Y));
                    //    //    currentIndex++;
                    //    //}
                    //    //int lineNum = (int)Math.Floor(lastStrokeBound.Y / LINE_HEIGHT);

                    //    //NotePhraseControl np = NotePhrases.Where(x => x.lineIndex == lineNum).FirstOrDefault();
                    //    //Debug.WriteLine($"line does not match = {np == null}");

                    //    //if (np == null)
                    //    //{
                    //    //    np = new NotePhraseControl();
                    //    //    np.InitializePhrase(words, temp.InkPresenter.StrokeContainer.GetStrokes().ToList(), lineNum, lastStrokeBound.Width);
                    //    //    double left = lastStrokeBound.X;
                    //    //    np.ShowPhraseAt(left, (lineNum) * LINE_HEIGHT);
                    //    //    PhraseControlCanvas.Children.Add(np);
                    //    //    NotePhrases.Add(np);

                    //    //    Debug.WriteLine("added new phrase control");
                    //    //}
                    //    //else
                    //    //{
                    //    //    np = NotePhrases.Where(x => Math.Abs((lastStrokeBound.X) - (x.canvasLeft + x.width)) <= 300).FirstOrDefault();
                    //    //    Debug.WriteLine($"X axis exceeding = {np == null}");
                    //    //    if (np == null)
                    //    //        return;
                    //    //    np.AddWords(words);
                    //    //    np.AddStrokes(temp.InkPresenter.StrokeContainer.GetStrokes().ToList(), lastStrokeBound.Width);
                    //    //    Debug.WriteLine("extended to new phrase");
                    //    //}
                    //    //np.UpdateLayout();
                    //    //lastStrokeBound = new Rect(0, 0, 5, 5);
                    //}
                    strokeAnalyzer.ClearDataForAllStrokes();
            }
        }

        private void UpdateRecognizedWordStyle(int lineNum, List<uint> strokeIds)
        {//updates the color of recognized word on inkcanvas and removed data from inkanalyzer
            foreach (var id in strokeIds)
            {
                //finds the corresponding stroke object given the info in stroke record
                var rst = strokeRecords[lineNum].Where(x => x.Id == id).FirstOrDefault();
                var st = inkCan.InkPresenter.StrokeContainer.GetStrokes().Where(x => x.StrokeStartedTime == rst.StrokeStartedTime).FirstOrDefault();
                if (st != null)
                {
                    st.Selected = true;
                    InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
                    drawingAttributes.Color = Color.FromArgb(255, 255, 255, 255);
                    st.DrawingAttributes = drawingAttributes;
                    //strokeRecords[lineNum].Remove(rst);
                    //strokeAnalyzer.RemoveDataForStroke(id);
                }
            }
            lastWordCount = 0;
        }

        private void recognizedResultTextBlock_Tapped(object sender, TappedRoutedEventArgs e) {
        }

        public void HideCurLineStackPanel() {
            curLineWordsStackPanel.Children.Clear();
            curLineResultPanel.Visibility = Visibility.Collapsed;
        }

        //private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        //{
        //    ClearSelectionAsync();
        //    curLineResultPanel.Visibility = Visibility.Collapsed;
        //    curLineWordsStackPanel.Children.Clear();
        //    RawStrokes.Clear();
        //    //operationDispathcerTimer.Stop();
        //    foreach (var stroke in args.Strokes)
        //    {                                      
        //        inkAnalyzer.RemoveDataForStroke(stroke.Id);
        //    }
        //    //operationDispathcerTimer.Start();
        //    //dispatcherTimer.Start();
        //    autosaveDispatcherTimer.Start();
        //    lastStrokePoint = new Point(0,0);
        //}

        //private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        //{
        //    if (!leftLasso)
        //    {
        //        //ClearSelection();
        //        // dispatcherTimer.Stop();
        //        //operationDispathcerTimer.Stop();
        //        //RawStrokeTimer.Stop();
        //        inkOperationAnalyzer.ClearDataForAllStrokes();
        //        if (lastStrokePoint.Equals(new Point(0, 0)))
        //            lastStrokePoint = args.CurrentPoint.Position;
        //    }

        //    autosaveDispatcherTimer.Stop();
        //    //recognizeTimer.Stop();
        //    RawStrokeTimer.Stop();
        //    if (lastStrokePoint.Equals(new Point(0,0)))
        //        lastStrokePoint = new Point(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y );

        //}

        //private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        //{
        //    autosaveDispatcherTimer.Start();
        //    //RawStrokeTimer.Start();

        //    //recognizeTimer.Start();
        //    lastStrokePoint = new Point(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y );
        //}

        //private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        //{
        //    if (!leftLasso)
        //    {//processing strokes inputs
        //        foreach (var s in args.Strokes)
        //        {
        //            //Process strokes that excess maximum height for recognition
        //            if (s.BoundingRect.Height > MAX_WRITING)
        //            {
        //                inkOperationAnalyzer.AddDataForStroke(s);
        //                try
        //                {
        //                    await RecognizeInkOperation();
        //                }
        //                catch (Exception e)
        //                {
        //                    MetroLogger.getSharedLogger().Error($"InkPresenter_StrokesCollectedAsync in NotePageControl:{e}|{e.Message}");
        //                }
        //            }
        //            //Instantly analyze ink inputs
        //            else
        //            {
        //                int lineNum = (int)Math.Floor(s.BoundingRect.Y / LINE_HEIGHT);
        //                if (!strokeRecords.ContainsKey(lineNum))
        //                {
        //                    strokeRecords.Add(lineNum, new List<InkStroke>());
        //                }
        //                strokeRecords[lineNum].Add(s.Clone());

        //                if (!strokeAnalyzer.IsAnalyzing)
        //                {
        //                    strokeAnalyzer.ClearDataForAllStrokes();
        //                    strokeAnalyzer.AddDataForStrokes(strokeRecords[lineNum]);
        //                    var result = await strokeAnalyzer.AnalyzeAsync();
        //                    var wordNodes = strokeAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);

        //                    if (result.Status == InkAnalysisStatus.Updated)
        //                    {
        //                        Debug.WriteLine($"line = {lineNum}, number of words = {wordNodes.Count}");

        //                        if (wordNodes.Count == lastWordCount - 1)
        //                        {
        //                            var ids = wordNodes[0].GetStrokeIds().ToList();
        //                            UpdateRecognizedWordStyle(lineNum, ids.ToList());
        //                            UpdateLayout();

        //                        }
        //                        else if (wordNodes.Count > 1)
        //                        {
        //                            Point p1 = new Point(wordNodes[wordNodes.Count - 2].BoundingRect.X + wordNodes[wordNodes.Count - 2].BoundingRect.Width, wordNodes[0].BoundingRect.Y);
        //                            Point p2 = new Point(wordNodes[wordNodes.Count - 1].BoundingRect.X, wordNodes[wordNodes.Count - 1].BoundingRect.Y);
        //                            double dist = GetDistance(p1, p2);
        //                            Debug.WriteLine(dist + "---");

        //                            if (dist > 20) {
        //                                Debug.WriteLine("greater than 20 dist, will parse");
        //                                var ids = wordNodes[wordNodes.Count - 2].GetStrokeIds().ToList();
        //                                UpdateRecognizedWordStyle(lineNum, ids);
        //                                UpdateLayout();
        //                            }
                                    
        //                        }

        //                        //if (wordNodes.Count > 1 && wordNodes.Count > lastWordCount) {

        //                        //    Point p1 = new Point(wordNodes[wordNodes.Count - 2].BoundingRect.X + wordNodes[0].BoundingRect.Width, wordNodes[0].BoundingRect.Y);
        //                        //    Point p2 = new Point(wordNodes[wordNodes.Count - 1].BoundingRect.X , wordNodes[1].BoundingRect.Y);
        //                        //    double dist = GetDistance(p1, p2);
        //                        //    Debug.WriteLine(dist+"---");
        //                        //    if (dist > 20) {
        //                        //        var ids = wordNodes[wordNodes.Count -2 ].GetStrokeIds();
        //                        //        foreach (var id in ids)
        //                        //        {
        //                        //            var st = inkCan.InkPresenter.StrokeContainer.GetStrokeById(id);
        //                        //            if (st != null)
        //                        //                st.Selected = true;

        //                        //            //var rst = strokeRecords[lineNum].Where(x => x.StrokeStartedTime == st.StrokeStartedTime).FirstOrDefault();
        //                        //            //if (rst != null)
        //                        //            //    strokeRecords[lineNum].Remove(rst);

        //                        //        }
        //                        //        inkCan.InkPresenter.StrokeContainer.MoveSelected(new Point(9999, 9999));
        //                        //    }
        //                        //}

        //                        //if (wordNodes.Count == 3) {
        //                        //    var ids = wordNodes[0].GetStrokeIds().ToList();
        //                        //    ids.AddRange(wordNodes[1].GetStrokeIds().ToList());

        //                        //    foreach (var id in ids)
        //                        //    {
        //                        //        var st = inkCan.InkPresenter.StrokeContainer.GetStrokeById(id);
        //                        //        if (st != null)
        //                        //            st.Selected = true;

        //                        //    }
        //                        //    inkCan.InkPresenter.StrokeContainer.MoveSelected(new Point(9999, 9999));
        //                        //}

        //                        lastWordCount = wordNodes.Count;

        //                    }

        //                }

        //                RawStrokeTimer.Start();


        //                //strokeAnalyzer.AddDataForStroke(s);
        //                //if (strokeAnalyzer.IsAnalyzing)
        //                //    return;
        //                //var result = await strokeAnalyzer.AnalyzeAsync();
        //                //if (result.Status == InkAnalysisStatus.Updated) {
        //                //    var wordNodes = strokeAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
        //                //    if (wordNodes.Count > 1) {
        //                //        var ids = wordNodes[0].GetStrokeIds();
        //                //        foreach (var id in ids)
        //                //        {
        //                //            var st = inkCan.InkPresenter.StrokeContainer.GetStrokeById(id);
        //                //            if ( st != null)
        //                //                st.Selected = true;

        //                //        }
        //                //        lastStrokeBound = wordNodes[0].BoundingRect;
        //                //        List<WordBlockControl> words = new List<WordBlockControl>();
        //                //        InkCanvas temp = new InkCanvas();
        //                //        inkCan.InkPresenter.StrokeContainer.CopySelectedToClipboard();
        //                //        temp.InkPresenter.StrokeContainer.PasteFromClipboard(new Point(0, 0));

        //                //        List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, server:false);

        //                //        if (recognitionResults == null)
        //                //            return;

        //                //        foreach (HWRRecognizedText recog in recognitionResults)
        //                //        {
        //                //            curLineWordsStackPanel.Children.Clear();
        //                //            WordBlockControl wb = new WordBlockControl(recog.selectedCandidate, recog.candidateList, currentIndex);
        //                //            words.Add(wb);
        //                //            foreach (Button alternative in wb.GetCurWordCandidates())
        //                //                curLineWordsStackPanel.Children.Add(alternative);
        //                //            ShowAlternativeCanvas(new Point(lastStrokeBound.X, lastStrokeBound.Y));
        //                //            currentIndex++;
        //                //        }

        //                //        NotePhraseControl np = NotePhrases.Where(x => x.lineIndex == lineNum).FirstOrDefault();
        //                //        Debug.WriteLine($"line does not match = {np==null}");

        //                //        if (np == null)
        //                //        {
        //                //            np = new NotePhraseControl();
        //                //            np.InitializePhrase(words, temp.InkPresenter.StrokeContainer.GetStrokes().ToList(), lineNum, lastStrokeBound.Width);
        //                //            double left = lastStrokeBound.X;
        //                //            np.ShowPhraseAt(left, (lineNum) * LINE_HEIGHT);
        //                //            PhraseControlCanvas.Children.Add(np);
        //                //            NotePhrases.Add(np);

        //                //            Debug.WriteLine("added new phrase control");
        //                //        }
        //                //        else
        //                //        {
        //                //            np = NotePhrases.Where(x => x.lineIndex == lineNum && Math.Abs((lastStrokeBound.X) - (x.canvasLeft + x.width)) <= 300).FirstOrDefault();
        //                //            Debug.WriteLine($"X axis over limit = {np == null}");

        //                //            if (np == null) {
        //                //                return;
        //                //            }

        //                //            np.AddWords(words);
        //                //            np.AddStrokes(temp.InkPresenter.StrokeContainer.GetStrokes().ToList(), lastStrokeBound.Width);
        //                //            Debug.WriteLine("extended to new phrase");
        //                //        }
        //                //        np.UpdateLayout();
        //                //        //lastStrokeBound = new Rect(0, 0, 5, 5);
        //                //        strokeAnalyzer.RemoveDataForStrokes(ids);
        //                //    }
        //                //    //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
        //                //    //RawStrokeTimer.Start();
        //                //}
        //            }
        //        }



        //    }
        //    else
        //    {//processing strokes selected with left mouse lasso strokes
        //        leftLossoStroke = args.Strokes;
        //        foreach (var s in args.Strokes)
        //        {
        //            //TODO: 
        //        }
        //    }
        //}

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
        private void ShowAlternativeCanvas(Point p)
        {
            double y = (Math.Floor(p.Y / LINE_HEIGHT) - 1) * LINE_HEIGHT;
            Canvas.SetTop(curLineResultPanel, y);
            Canvas.SetLeft(curLineResultPanel, p.X);
            curLineWordsStackPanel.Visibility = Visibility.Visible;
            curLineResultPanel.Visibility = Visibility.Visible;
            UpdateLayout();
        }


    }


}
