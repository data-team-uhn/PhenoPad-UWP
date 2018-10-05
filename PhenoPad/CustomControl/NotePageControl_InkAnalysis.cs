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
    public sealed partial class NotePageControl : UserControl
    {   
        // ================================= INK RECOGNITION ==============================================                      
        
        /// <summary>
        /// select and recognize a line by its id
        /// </summary>
        private async Task<List<HWRRecognizedText>> RecognizeLine(uint lineid)
        {
            // only one thread is allowed to use select and recognize
            await selectAndRecognizeSemaphoreSlim.WaitAsync();
            try
            {
                // clear selection
                var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                foreach (var stroke in strokes)
                {
                    stroke.Selected = false;
                }

                // select storkes of this line
                var lines = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
                var thisline = lines.Where(x => x.Id == lineid).FirstOrDefault();
                if (thisline != null)
                {
                    foreach (var sid in thisline.GetStrokeIds())
                    {
                        inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                    }
                }

                //recognize selection
                List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer,
                    InkRecognitionTarget.Selected);
                return recognitionResults;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to recognize line ({lineid}): {ex.Message}");
            }
            finally
            {
                selectAndRecognizeSemaphoreSlim.Release();
            }
            return null;
        }

        /// <summary>
        /// Handwrting recognition on selected strokes.
        /// </summary>
        private async Task<string> recognizeSelection()
        {
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync
                    (inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected);

                string str = "";
                if (recognitionResults != null)
                {
                    recognizedText.Clear();
                    recognitionResults.ToList().ForEach(recognizedText.Add);

                    foreach (var rt in recognitionResults)
                    {
                        str += rt.selectedCandidate + " ";
                    }
                }

                return str;
            }
            return String.Empty;
        }

        /// <summary>
        /// Get line number of a line node,it is given by line number of most strokes.
        /// </summary>
        private int getLineNumOfLine(IInkAnalysisNode node)
        {
            int l1 = -1; int count1 = 0;
            int l2 = -1; int count2 = 0;
            int loopnum = -1;
            foreach (uint s in node.GetStrokeIds())
            {
                var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(s);
                int l = getLineNumByRect(stroke.BoundingRect);
                if (l1 == -1 || l1 == l)
                {
                    l1 = l;
                    count1++;
                }
                else if (l2 == -1 || l2 == l)
                {
                    l2 = l;
                    count2++;
                }
                loopnum++;
                if (loopnum == 10)
                    break;
            }

            return count1 > count2 ? l1 : l2;
        }

        /// <summary>
        /// Get line number by using a rectangle object.
        /// </summary>
        private int getLineNumByRect(Rect rect)
        {
            return (int)((rect.Y + rect.Height / 2) / (LINE_HEIGHT));
        }

        /// <summary>
        /// Recognize strokes selected 
        /// </summary>
        private async void RecognizeSelection()
        {
            selectionCanvas.Children.Clear();

            if (boundingRect.Width <= 0 || boundingRect.Height <= 0)
            {
                return;
            }

            /***
            var rectangle = new Rectangle()
            {
                Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
                Width = boundingRect.Width,
                Height = boundingRect.Height
            };

            Canvas.SetLeft(rectangle, boundingRect.X);
            Canvas.SetTop(rectangle, boundingRect.Y);
            selectionCanvas.Children.Add(rectangle);
            ***/

            //recogPhenoFlyout.ShowAt(rectangle);
            string str = await recognizeSelection();
            if (!str.Equals(String.Empty) && !str.Equals(""))
            {
                PopupCommandBar.Visibility = Visibility.Visible;
                recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
                Canvas.SetLeft(PopupCommandBar, boundingRect.X);
                Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height);
                PopupCommandBar.Width = Math.Max(boundingRect.Width, PopupCommandBar.MinWidth);
                //Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
                //Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);

                selectionCanvas.Children.Add(PopupCommandBar);
                selectionCanvas.Children.Add(recognizedPhenoBriefPanel);



                selectionRectangle.Width = boundingRect.Width;
                selectionRectangle.Height = boundingRect.Height;
                //selectionRectangleTranform = new TranslateTransform();
                //selectionRectangle.RenderTransform = this.selectionRectangleTranform;

                selectionCanvas.Children.Add(selectionRectangle);
                Canvas.SetLeft(selectionRectangle, boundingRect.Left);
                Canvas.SetTop(selectionRectangle, boundingRect.Top);
                //TestC.Children.Add(selectionRectangle);


                var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
                recognizedResultTextBlock.Text = str;
                recogPhenoFlyout.ShowAt(selectionRectangle);
                searchPhenotypes(str);
            }
        }




        /// <summary>
        /// Recognize a set of strokes as whether a shape or just drawing and handles each case
        /// accordingly.
        /// </summary>
        private async Task<int> RecognizeInkOperation()
        {
            var result = await inkOperationAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //first need to clear all previous selections to filter out strokes that don't want to be deleted
                //ClearSelectionAsync();
                foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                //Gets all strokes from inkoperationanalyzer
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                {
                    //user has drawn a square/rectangle for adding an add-in
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                    {
                        foreach (var dstroke in drawNode.GetStrokeIds())
                        {
                            var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                            //adds a new add-in control 
                            string name = FileManager.getSharedFileManager().CreateUniqueName();
                            NewAddinControl(name, false, left: stroke.BoundingRect.X, top: stroke.BoundingRect.Y,
                                            widthOrigin: stroke.BoundingRect.Width, heightOrigin: stroke.BoundingRect.Height);
                            stroke.Selected = true;
                        }
                        //dispose the strokes as don't need them anymore
                        inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
                        inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                    }
                    else
                    {
                        //you need this else statement for debugging reasons...
                        Debug.WriteLine(drawNode.DrawingKind);
                    }
                }
                // delete all strokes that are too large, like drawings
                foreach (var sid in inkOperationAnalyzer.AnalysisRoot.GetStrokeIds())
                {
                    //ShowToastNotification("Write smallers", "Write smaller please");
                    //rootPage.NotifyUser("Write smaller", NotifyType.ErrorMessage, 2);
                    //inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                    inkOperationAnalyzer.RemoveDataForStroke(sid);
                }
                //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                return 1;
            }
            return -1;
        }

        private void SelectWithLineWithThickness(Point p1, Point p2, double thickness)
        {
            double x1 = p1.X;
            double y1 = p1.Y;
            double x2 = p2.X;
            double y2 = p2.Y;

            double temp = Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));
            double x11 = x1 + (y1 - y2) * thickness / temp;
            double y11 = y1 + (x2 - x1) * thickness / temp;
            double x12 = x1 - (y1 - y2) * thickness / temp;
            double y12 = y1 - (x2 - x1) * thickness / temp;

            double x22 = x2 + (y2 - y1) * thickness / temp;
            double y22 = y2 + (x1 - x2) * thickness / temp;
            double x21 = x2 - (y2 - y1) * thickness / temp;
            double y21 = y2 - (x1 - x2) * thickness / temp;

            double minX = Math.Min(Math.Min(Math.Min(x11, x12), x21), x22);
            double minY = Math.Min(Math.Min(Math.Min(y11, y12), y21), y22);
            double maxX = Math.Max(Math.Max(Math.Max(x11, x12), x21), x22);
            double maxY = Math.Max(Math.Max(Math.Max(y11, y12), y21), y22);

            /** for visualizaiton
            var rectangle = new Polyline()
            {
                Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 }
                //Width = maxX - minX,
                //Height = maxY - minY
            };
            rectangle.Points.Add(new Point(x11, y11));
            rectangle.Points.Add(p1);
            rectangle.Points.Add(new Point(x12, y12));
            rectangle.Points.Add(new Point(x22, y22));
            rectangle.Points.Add(p2);
            rectangle.Points.Add(new Point(x21, y21));
            rectangle.Points.Add(new Point(x11, y11));

            selectionCanvas.Children.Add(rectangle);
            **/

            Rect outboundOfLineRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
            {
                if (!RectHelper.Intersect(s.BoundingRect, outboundOfLineRect).Equals(Rect.Empty))
                {
                    foreach (var p in s.GetInkPoints())
                    {
                        // check if inside the rotated rectangle
                        if ((isLeftOfLine(p.Position, x11, y11, x21, y21) != isLeftOfLine(p.Position, x12, y12, x22, y22))
                            && (isLeftOfLine(p.Position, x11, y11, x12, y12) != isLeftOfLine(p.Position, x21, y21, x22, y22)))
                        {
                            s.Selected = true;
                            boundingRect.Union(s.BoundingRect);
                        }
                    }
                }
            }
        }

        private bool isLeftOfLine(Point p, double x1, double y1, double x2, double y2)
        {
            return ((x2 - x1) * (p.Y - y1) - (y2 - y1) * (p.X - x1)) > 0;
        }

        /// <summary>
        /// set up for current line, i.e. hwr, show recognition results and show recognized phenotypes
        /// </summary>
        private async void recognizeAndSetUpUIForLine(InkAnalysisLine line, bool indetails = false)
        {
            if (line == null)
                return;
            // set current line id
            // switch to another line, clear result of current line
            if (line.Id != showingResultOfLine)
            {
                curLineCandidatePheno.Clear();
                curLineWordsStackPanel.Children.Clear();
                curWordPhenoControlGrid.Margin = new Thickness(0);

                showingResultOfLine = line.Id;
                curLineObject = line;
            }


            if (idToNoteLine.ContainsKey(line.Id))
            {  // existing line
                NoteLine nl = idToNoteLine[line.Id];
                var hwrresult = await RecognizeLine(line.Id);
                nl.HwrResult = hwrresult;
            }
            else
            {
                //new line
                NoteLine nl = new NoteLine(line);
                // hwr
                var hwrresult = await RecognizeLine(line.Id);
                nl.HwrResult = hwrresult;
                idToNoteLine[line.Id] = nl;
            }

            // HWR result UI
            setUpCurrentLineResultUI(line);

            // annotation and UI
            annotateCurrentLineAndUpdateUI(line);

            if (indetails)
            {
                string str = idToNoteLine[line.Id].Text;
                PopupCommandBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefPanel.Visibility = Visibility.Visible;
                Canvas.SetLeft(PopupCommandBar, boundingRect.X);
                Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height);
                PopupCommandBar.Width = Math.Max(boundingRect.Width, PopupCommandBar.MinWidth);
                Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
                Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);

                selectionCanvas.Children.Add(PopupCommandBar);
                selectionCanvas.Children.Add(recognizedPhenoBriefPanel);



                selectionRectangle.Width = boundingRect.Width;
                selectionRectangle.Height = boundingRect.Height;
                //selectionRectangleTranform = new TranslateTransform();
                //selectionRectangle.RenderTransform = this.selectionRectangleTranform;

                selectionCanvas.Children.Add(selectionRectangle);
                Canvas.SetLeft(selectionRectangle, boundingRect.Left);
                Canvas.SetTop(selectionRectangle, boundingRect.Top);
                //TestC.Children.Add(selectionRectangle);


                //var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
                //recognizedResultTextBlock.Text = str;
                //recogPhenoFlyout.ShowAt(selectionRectangle);
                searchPhenotypesAndSetUpBriefView(str);
            }
        }

        private void recognizeLine(int line)
        {
            if (line < 0)
                return;
            SelectAndAnnotateByLineNum(line);
        }

        public async void SelectAndAnnotateByLineNum(int line)
        {
            await deleteSemaphoreSlim.WaitAsync();
            try
            {
                var xFrom = 0;
                var xTo = inkCanvas.ActualWidth;
                var yFrom = line * LINE_HEIGHT - 0.2 * LINE_HEIGHT;
                yFrom = yFrom < 0 ? 0 : yFrom;
                var yTo = (line + 1) * LINE_HEIGHT + 1.0 * LINE_HEIGHT;
                Polyline lasso = new Polyline();
                lasso.Points.Add(new Point(xFrom, yFrom));
                lasso.Points.Add(new Point(xFrom, yTo));
                lasso.Points.Add(new Point(xTo, yTo));
                lasso.Points.Add(new Point(xTo, yFrom));
                lasso.Points.Add(new Point(xFrom, yFrom));
                Rect selectRect = Rect.Empty;

                selectRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(lasso.Points);

                //double panelLeft = 0;
                if (!selectRect.Equals(Rect.Empty))
                {
                    var shouldRecognize = false;
                    foreach (var ss in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                        if (ss.Selected)
                        {
                            if (ss.BoundingRect.Top > (line + 1) * LINE_HEIGHT || ss.BoundingRect.Bottom < line * LINE_HEIGHT)
                            {
                                ss.Selected = false;
                            }
                            else
                            {
                                shouldRecognize = true;
                                //panelLeft = panelLeft > (ss.BoundingRect.X + ss.BoundingRect.Width) ? panelLeft : (ss.BoundingRect.X + ss.BoundingRect.Width);
                            }
                        }
                    if (shouldRecognize)
                    {
                        List<HWRRecognizedText> recognitionResults = await HWRService.HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected);

                        //ClearSelection();
                        if (recognitionResults != null)
                        {
                            string str = "";
                            foreach (var rt in recognitionResults)
                            {
                                str += rt.selectedCandidate + " ";
                            }

                            if (!str.Equals(String.Empty))
                            {
                                string pname = "";
                                if (stringsOfLines.ContainsKey(line) && stringsOfLines[line].Equals(str) && phenotypesOfLines.ContainsKey(line))
                                {
                                    pname = phenotypesOfLines[line].ElementAt(0).name;
                                    // if (PhenotypeManager.getSharedPhenotypeManager().checkIfSaved(phenotypesOfLines[line].ElementAt(0)))
                                    //ifSaved = true;
                                }
                                else
                                {
                                    List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
                                    if (result != null && result.Count > 0)
                                    {
                                        phenotypesOfLines[line] = result;
                                        stringsOfLines[line] = str;
                                        pname = result.ElementAt(0).name;
                                        // if (PhenotypeManager.getSharedPhenotypeManager().checkIfSaved(result.ElementAt(0)))
                                        // ifSaved = true;
                                    }
                                    else
                                        return;
                                }
                                /**
                                HoverPhenoPanel.Visibility = Visibility.Visible;
                                HoverPhenoPopupText.Text = pname;
                                Canvas.SetTop(HoverPhenoPanel, ((double)line-0.4) * LINE_HEIGHT);
                                Canvas.SetLeft(HoverPhenoPanel, 10);
                                HoverPhenoPopupText.Tapped += HoverPhenoPopupText_Tapped;

                                if (ifSaved)
                                    HoverPhenoPanel.BorderThickness = new Thickness(2);
                                else
                                    HoverPhenoPanel.BorderThickness = new Thickness(0);
                                **/

                            }
                        }
                    }
                    else
                    {
                        if (stringsOfLines.ContainsKey(line))
                            stringsOfLines.Remove(line);
                        if (phenotypesOfLines.ContainsKey(line))
                            phenotypesOfLines.Remove(line);
                    }
                }
            }
            finally
            {
                deleteSemaphoreSlim.Release();
            }
        }

        // Draw a polygon on the recognitionCanvas.
        private void DrawPolygon(InkAnalysisInkDrawing shape)
        {
            var points = shape.Points;
            Polygon polygon = new Polygon();

            foreach (var point in points)
            {
                polygon.Points.Add(point);
            }

            var brush = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(255, 0, 0, 255));
            polygon.Stroke = brush;
            polygon.StrokeThickness = 2;
            //recognitionCanvas.Children.Add(polygon);
        }

        // extract text from a paragraph perserving line breaks
        private string ExtractTextFromParagraph(InkAnalysisParagraph paragraph)
        {
            // The paragraph.RecognizedText property also returns the text,
            // but manually walking through the lines allows us to preserve
            // line breaks.
            var lines = new List<string>();
            foreach (var child in paragraph.Children)
            {
                if (child.Kind == InkAnalysisNodeKind.Line)
                {
                    var line = (InkAnalysisLine)child;
                    lines.Add(line.RecognizedText);
                }
                else if (child.Kind == InkAnalysisNodeKind.ListItem)
                {
                    var listItem = (InkAnalysisListItem)child;
                    lines.Add(listItem.RecognizedText);
                }
            }
            return String.Join("\n", lines);
        }
        // Find line by hitting position
        private InkAnalysisLine FindHitLine(Point pt)
        {
            var lines = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
            foreach (var line in lines)
            {
                // To support ink written with angle, RotatedBoundingRect should be used in hit testing.
                var xFrom = line.BoundingRect.X;
                var xTo = xFrom + line.BoundingRect.Width;
                var yFrom = line.BoundingRect.Y;
                var yTo = yFrom + line.BoundingRect.Height;
                //if (RectHelper.Contains(line.BoundingRect, pt))
                if (pt.X > xFrom && pt.X < xTo && pt.Y > yFrom && pt.Y < yTo)
                {
                    return (InkAnalysisLine)line;
                }
            }
            return null;
        }
        // Find word by hitting position
        private InkAnalysisInkWord FindHitWord(Point pt)
        {
            var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
            foreach (var word in words)
            {
                // To support ink written with angle, RotatedBoundingRect should be used in hit testing.
                if (RectHelper.Contains(word.BoundingRect, pt))
                {
                    return (InkAnalysisInkWord)word;
                }
            }
            return null;
        }
        // Find paragraph by hitting position
        private InkAnalysisParagraph FindHitParagraph(Point pt)
        {
            var paragraphs = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Paragraph);
            foreach (var paragraph in paragraphs)
            {
                // To support ink written with angle, RotatedBoundingRect should be used in hit testing.
                if (RectHelper.Contains(paragraph.BoundingRect, pt))
                {
                    return (InkAnalysisParagraph)paragraph;
                }
            }
            return null;
        }


        // =============================== ANALYZE INKS ==============================================//

        /// <summary>
        /// Called upon page creation/page switch, will re-analyze everything on the current page and
        /// change phenotype candidates accordingly.
        /// </summary>
        public async void initialAnalyze()
        {
            rootPage.NotifyUser("Analyzing current page ...", NotifyType.StatusMessage, 2);
            Debug.WriteLine("changed page, will analyze strokes");
            inkAnalyzer = new InkAnalyzer();
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            bool result = false;
            while (!result)
                result = await analyzeInk();
            Debug.WriteLine("done initial analyze, result = " + result);
        }

        /// <summary>
        ///  Analyze ink strokes contained in inkAnalyzer and add phenotype candidates
        ///  from fetching API
        /// </summary>
        private async Task<bool> analyzeInk(InkStroke lastStroke = null)
        {
            //if (lastStroke == null) { 
            //    PhenoMana.phenotypesCandidates.Clear();
            //}
            dispatcherTimer.Stop();
            Debug.WriteLine("analyzing...");
            if (inkAnalyzer.IsAnalyzing)
            {
                Debug.WriteLine("already analyzing...");
                // inkAnalyzer is being used 
                // try again after some time by dispatcherTimer 
                dispatcherTimer.Start();
                return false;
            }
            // analyze 
            var result = await inkAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //int line = linesToAnnotate.Dequeue();
                IReadOnlyList<IInkAnalysisNode> lineNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);

                foreach (InkAnalysisLine line in lineNodes)
                {
                    // only focus on current line
                    if (lastStroke != null)
                    {
                        // current line
                        if (line.GetStrokeIds().Contains(lastStroke.Id))
                        {
                            // set up for current line
                            recognizeAndSetUpUIForLine(line);
                        }
                    }
                    // recognize all lines
                    else
                    {
                        Debug.WriteLine("Analyzing all lines...");
                        if (!idToNoteLine.ContainsKey(line.Id))
                        {
                            //new line
                            NoteLine nl = new NoteLine(line);
                            // hwr
                            var hwrresult = await RecognizeLine(line.Id);
                            nl.HwrResult = hwrresult;
                            idToNoteLine[line.Id] = nl;
                            Debug.WriteLine("fetching from API...");
                            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(idToNoteLine.GetValueOrDefault(line.Id).Text);
                            Debug.WriteLine(annoResult.Count);
                            if (annoResult != null && annoResult.Count != 0)
                            {
                                int lineNum = getLineNumByRect(line.BoundingRect);
                                if (!annotatedLines.Contains(lineNum))
                                {
                                    annotatedLines.Add(lineNum);
                                    Rectangle rect = new Rectangle
                                    {
                                        Fill = Application.Current.Resources["Button_Background"] as SolidColorBrush,
                                        Width = 5,
                                        Height = LINE_HEIGHT - 20
                                    };
                                    sideCanvas.Children.Add(rect);
                                    Canvas.SetTop(rect, lineNum * LINE_HEIGHT + 10);
                                    Canvas.SetLeft(rect, 5);
                                    if (!lineToRect.ContainsKey(lineNum))
                                        lineToRect.Add(lineNum, rect);
                                }

                                foreach (var pp in annoResult.Values)
                                {
                                    pp.sourceType = SourceType.Notes;
                                    PhenoMana.addPhenotypeCandidate(pp, SourceType.Notes);
                                }
                            }
                        }
                    }


                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Dynamic programming for caluculating best alignment of two string list
        /// http://www.biorecipes.com/DynProgBasic/code.html
        /// </summary>
        /// <param name="newList"></param>
        /// <param name="oldList"></param>
        private (List<int>, List<int>) alignTwoStringList(List<string> newList, List<string> oldList)
        {
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
            Debug.WriteLine("Alignment results: ");
            string newString = "";
            string oldString = "";
            for (i = 0; i < newIndex.Count(); i++)
            {
                oldString += oldIndex[i] == -1 ? "_\t" : oldList[oldIndex[i]] + "\t";
                newString += newIndex[i] == -1 ? "_\t" : newList[newIndex[i]] + "\t";
            }
            Debug.WriteLine("Old string:    " + oldString);
            Debug.WriteLine("New String:    " + newString);
            return (newIndex, oldIndex);

        }

        private void setUpCurrentLineResultUI(InkAnalysisLine line)
        {
            var wordlist = idToNoteLine.GetValueOrDefault(line.Id).WordStrings;
            {
                // align wordlist and textblocks in curLineResultPanel
                /***
                var oldWordList = new List<string>();
                foreach (TextBlock tb in curLineWordsStackPanel.Children)
                    oldWordList.Add(tb.Text);

                var alignResult = alignTwoStringList(wordlist, oldWordList);
                var newIndex = alignResult.Item1;
                var oldIndex = alignResult.Item2;

                int insertIndex = oldWordList.Count();

                for (int i = oldIndex.Count() - 1; i >= 0; --i)
                {
                    // gap
                    if (oldIndex[i] == -1)
                    {
                        TextBlock tb = new TextBlock();
                        tb.VerticalAlignment = VerticalAlignment.Center;
                        tb.FontSize = 16;
                        tb.Text = wordlist[newIndex[i]];
                        if (insertIndex >= curLineWordsStackPanel.Children.Count())
                            curLineWordsStackPanel.Children.Add(tb);
                        else
                            curLineWordsStackPanel.Children.Insert(insertIndex, tb);
                    }
                    // aligment
                    else if (newIndex[i] != -1) 
                    {
                        insertIndex--;
                        if (oldWordList[oldIndex[i]] != wordlist[newIndex[i]])
                            (curLineWordsStackPanel.Children[oldIndex[i]] as TextBlock).Text = wordlist[newIndex[i]];
                    }
                }
                for (int i = oldIndex.Count() - 1; i >= 0; --i)
                {
                    // delete
                    if (newIndex[i] == -1) 
                    {
                        curLineWordsStackPanel.Children.RemoveAt(oldIndex[i]);
                    }
                }
                **/
            }
            curLineWordsStackPanel.Children.Clear();
            //sets a text block for each recognized word and adds event handler to click event
            foreach (var word in wordlist)
            {
                TextBlock tb = new TextBlock();
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.FontSize = 16;
                tb.Text = word;
                curLineWordsStackPanel.Children.Add(tb);
                tb.Tapped += ((object sender, TappedRoutedEventArgs e) => {
                    int wi = curLineWordsStackPanel.Children.IndexOf((TextBlock)sender);
                    var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
                    showAlterOfWord = wi;
                    alternativeListView.ItemsSource = idToNoteLine[showingResultOfLine].HwrResult[wi].candidateList;
                    alterFlyout.ShowAt((FrameworkElement)sender);

                });
            }
            curLineResultPanel.Visibility = Visibility.Visible;
            Canvas.SetLeft(curLineResultPanel, line.BoundingRect.Left);
            int lineNum = getLineNumByRect(line.BoundingRect);
            Canvas.SetTop(curLineResultPanel, (lineNum - 1) * LINE_HEIGHT);
            /***
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () => {
                
            });
            ***/


        }

        private async void annotateCurrentLineAndUpdateUI(InkAnalysisLine line)
        {
            // after get annotation, recognized text has also changed
            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(idToNoteLine.GetValueOrDefault(line.Id).Text);
            if (annoResult != null && annoResult.Count != 0)
            {
                // update global annotations
                foreach (var anno in annoResult.ToList())
                {
                    if (cachedAnnotation.ContainsKey(anno.Key))
                        cachedAnnotation[anno.Key] = anno.Value;
                    else
                        cachedAnnotation.Add(anno.Key, anno.Value);
                    // add to global candidate list
                    anno.Value.sourceType = SourceType.Notes;
                    PhenoMana.addPhenotypeCandidate(anno.Value, SourceType.Notes);
                }

                // update current line annotation
                idToNoteLine.GetValueOrDefault(line.Id).phenotypes = annoResult.Values.ToList();

                /**
                foreach (TextBlock tb in curLineWordsStackPanel.Children)
                {
                    var match = cachedAnnotation.Keys.Where(x => x.Split(' ').Contains(tb.Text)).FirstOrDefault();
                    if (match != null)
                    {
                        tb.Foreground = Application.Current.Resources["WORD_DARK"] as SolidColorBrush;
                    }
                }
                **/

                if (curLineCandidatePheno.Count == 0 || curWordPhenoControlGrid.Margin.Top == 0)
                {
                    curWordPhenoControlGrid.Margin = new Thickness(0, -100, 0, 0);
                    curWordPhenoAnimation.Begin();
                }


                foreach (var pheno in annoResult.Values.ToList())
                {
                    var temp = curLineCandidatePheno.Where(x => x == pheno).FirstOrDefault();
                    pheno.state = PhenotypeManager.getSharedPhenotypeManager().getStateByHpid(pheno.hpId);
                    if (temp == null)
                    {
                        curLineCandidatePheno.Add(pheno);
                    }
                    else
                    {
                        if (temp.state != pheno.state)
                        {
                            var ind = curLineCandidatePheno.IndexOf(temp);
                            curLineCandidatePheno.Remove(temp);
                            curLineCandidatePheno.Insert(ind, pheno);

                        }
                    }
                }

                if (curLineCandidatePheno.Count != 0)
                {
                    int lineNum = getLineNumByRect(line.BoundingRect);
                    if (!annotatedLines.Contains(lineNum))
                    {
                        annotatedLines.Add(lineNum);
                        if (!lineToRect.ContainsKey(lineNum))
                        {

                            Rectangle rect = new Rectangle
                            {
                                Fill = Application.Current.Resources["Button_Background"] as SolidColorBrush,
                                Width = 5,
                                Height = LINE_HEIGHT - 20
                            };
                            sideCanvas.Children.Add(rect);
                            Canvas.SetTop(rect, lineNum * LINE_HEIGHT + 10);
                            Canvas.SetLeft(rect, sideScrollView.ActualWidth / 2);
                            lineToRect.Add(lineNum, rect);
                        }

                    }
                }
            }
            else
            {
                curWordPhenoControlGrid.Margin = new Thickness(0, 0, 0, 0);
                int lineNum = getLineNumByRect(line.BoundingRect);
                if (lineToRect.ContainsKey(lineNum))
                {
                    try
                    {
                        sideCanvas.Children.Remove(lineToRect[lineNum]);
                        lineToRect.Remove(lineNum);
                    }
                    catch (Exception E)
                    {
                        logger.Error(E.Message);
                    }

                }
                //curWordPhenoHideAnimation.Begin();
            }


        }
        private async void searchPhenotypesAndSetUpBriefView(string str)
        {
            List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
            if (result != null && result.Count > 0)
            {
                foreach (var pp in result)
                    pp.sourceType = SourceType.Notes;

                recognizedPhenoBriefListView.ItemsSource = result;

                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Visible;

            }
            else
            {
                recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Collapsed;

                rootPage.NotifyUser("No phenotypes found in: " + str, NotifyType.ErrorMessage, 2);
            }

        }

        /// <summary>
        /// Search Phenotypes by a given string input.
        /// </summary>
        /// <param name="str"></param>
        private async void searchPhenotypes(string str)
        {
            List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
            if (result != null && result.Count > 0)
            {
                foreach (var pp in result)
                    pp.sourceType = SourceType.Notes;
                //recognizedPhenotypes.Clear();
                //recognizedPhenotypes = new ObservableCollection<Phenotype>(result);
                //result.ToList().ForEach(recognizedPhenotypes.Add);
                recognizedPhenoListView.ItemsSource = result;
                recognizedPhenoBriefListView.ItemsSource = result;

                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Visible;

                phenoProgressRing.Visibility = Visibility.Collapsed;
                recognizedPhenoListView.Visibility = Visibility.Visible;
            }
            else
            {
                recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
                // ClearSelection();
                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Collapsed;
                phenoProgressRing.Visibility = Visibility.Collapsed;
                recognizedPhenoListView.Visibility = Visibility.Collapsed;
                rootPage.NotifyUser("No phenotypes recognized by the selected strokes.", NotifyType.ErrorMessage, 2);
            }



        }


    }
}
