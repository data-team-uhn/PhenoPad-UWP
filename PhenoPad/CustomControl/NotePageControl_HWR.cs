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
        List<HWRRecognizedText> RecognizedNotes;
        double lastStrokeX = -1;
        bool recognizing;

        private async void RawStrokeTimer_Tick(object sender = null, object e = null) {
            RawStrokeTimer.Stop();
            recognizing = true;
            InkCanvas temp = new InkCanvas();
            curLineWordsStackPanel.Children.Clear();
            temp.InkPresenter.StrokeContainer.AddStrokes(RawStrokes);
            List<HWRRecognizedText> recognitionResults =await HWRManager.getSharedHWRManager().OnRecognizeAsync(temp.InkPresenter.StrokeContainer, InkRecognitionTarget.All, false);
            if (recognitionResults != null) {
                RecognizedNotes.AddRange(recognitionResults);
                foreach (HWRRecognizedText recog in recognitionResults)
                {
                    TextBlock block = new TextBlock();
                    block.FontSize = 22;
                    block.Text = recog.selectedCandidate;
                    RecognizedNoteStackPanel.Children.Add(block);

                    foreach (string candidate in recog.candidateList) {
                        Debug.WriteLine(candidate + "\n");
                        Button tb = new Button();
                        tb.FontSize = 16;
                        tb.Content = candidate;
                        curLineWordsStackPanel.Children.Add(tb);
                    }

                    Canvas.SetTop(curLineResultPanel, 200);
                    curLineWordsStackPanel.Visibility = Visibility.Visible;
                    curLineResultPanel.Visibility = Visibility.Visible;
                    UpdateLayout();

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
            lastStrokeX = -1;
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
                if (lastStrokeX != -1)
                {
                    
                    double dist = Math.Abs(lastStrokeX - (args.CurrentPoint.Position.X));
                    Debug.WriteLine($"Stroke dist = {dist}");
                    if (dist > 30 && !recognizing)
                    {
                        RawStrokeTimer_Tick();
                    }
                }

            }

            autosaveDispatcherTimer.Stop();
            //recognizeTimer.Stop();
        }


        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
            //RawStrokeTimer.Start();
            //recognizeTimer.Start();
            lastStrokeX = args.CurrentPoint.Position.X;

        }

        //private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        //{
        //    if (!leftLasso)
        //    {//processing strokes inputs
        //        //dispatcherTimer.Stop();
        //        //operationDispathcerTimer.Stop();            
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
        //                RawStrokeTimer.Start();
        //                RawStrokes.Add(s.Clone());

        //                inkAnalyzer.AddDataForStroke(s);
        //                inkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
        //                //marking the current stroke for later server recognition
        //                curStroke = s;
        //                //here we need instant call to analyze ink for the specified line input
        //                await analyzeInk(s);
        //                OperationLogger.getOpLogger().Log(OperationType.Stroke, s.Id.ToString(), s.StrokeStartedTime.ToString(), s.StrokeDuration.ToString());
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



    }


}
