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
        List<WordBlockControl> RecognizedNotes;
        int currentIndex;



        Point lastStrokePoint;
        bool recognizing;

        private async void RawStrokeTimer_Tick(object sender = null, object e = null) {
            RawStrokeTimer.Stop();
            recognizing = true;
            InkCanvas temp = new InkCanvas();
            curLineWordsStackPanel.Children.Clear();
            temp.InkPresenter.StrokeContainer.AddStrokes(RawStrokes);
            List<HWRRecognizedText> recognitionResults =await HWRManager.getSharedHWRManager().OnRecognizeAsync(temp.InkPresenter.StrokeContainer, InkRecognitionTarget.All, false);
            if (recognitionResults != null) {
                foreach (HWRRecognizedText recog in recognitionResults)
                {
                    WordBlockControl wb = new WordBlockControl(recog.selectedCandidate, recog.candidateList, currentIndex);
                    RecognizedNoteStackPanel.Children.Add(wb);
                    foreach (Button alternative in wb.GetCurWordCandidates())
                        curLineWordsStackPanel.Children.Add(alternative);
                    ShowAlternativeCanvas();
                    currentIndex++;
                }
                RawStrokes.Clear();
            }

            recognizing = false;
            //inkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private void recognizedResultTextBlock_Tapped(object sender, TappedRoutedEventArgs e) {
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
                if (! lastStrokePoint.Equals(new Point(0,0)))
                {
                    
                    double dist = GetDistance(lastStrokePoint, args.CurrentPoint.Position);
                    Debug.WriteLine($"Stroke dist = {dist}");
                    if (dist > 50 && !recognizing)
                    {
                        RawStrokeTimer_Tick();
                    }
                }

            }

            autosaveDispatcherTimer.Stop();
            //recognizeTimer.Stop();
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
            Rect bound = sender.InkPresenter.StrokeContainer.BoundingRect;
            lastStrokePoint = new Point(bound.X + bound.Width, bound.Y + (bound.Height/2));


        }



        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {//processing strokes inputs
             //dispatcherTimer.Stop();
             //operationDispathcerTimer.Stop(); 
             //RawStrokeTimer.Stop();


                foreach (var s in args.Strokes)
                {

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

                        //if(lastStrokeX != -1) {
                        //    double dist = Math.Abs(lastStrokeX - (s.BoundingRect.X));
                        //    Debug.WriteLine($"Stroke dist = {dist}");
                        //    if (dist > 10 && !recognizing)
                        //    {
                        //        RawStrokeTimer_Tick();
                        //    }
                        //}
                        RawStrokes.Add(s.Clone());
                        //lastStrokeX = s.BoundingRect.X + s.BoundingRect.Width;
                        //OperationLogger.getOpLogger().Log(OperationType.Stroke, s.Id.ToString(), s.StrokeStartedTime.ToString(), s.StrokeDuration.ToString());
                    }
                }

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


    }


}
