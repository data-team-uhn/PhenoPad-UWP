using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using PhenoPad.HWRService;
using System.Diagnostics;
using System.Threading.Tasks;
using PhenoPad.FileService;
using PhenoPad.LogService;
using Windows.UI.Xaml.Media.Animation;
using Windows.System;

namespace PhenoPad.CustomControl
{
    public sealed partial class NotePageControl : UserControl
    {
        DispatcherTimer recognizeTimer;
        public DispatcherTimer RawStrokeTimer;
        DispatcherTimer EraseTimer;
        List<InkStroke> RawStrokes;
        InkAnalyzer strokeAnalyzer;
        int lastWordCount;
        int lastWordIndex;
        Point lastWordPoint;
        Rect lastStrokeBound;
        List<int> linesErased = new List<int>();
        private Dictionary<int, NotePhraseControl> phrases = new Dictionary<int, NotePhraseControl>();


        #region stroke event handlers




        private async void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    ClearSelectionAsync();
                    EraseTimer.Stop();

                    curLineResultPanel.Visibility = Visibility.Collapsed;
                    curLineWordsStackPanel.Children.Clear();

                    linesErased.Clear();
                    foreach (var stroke in args.Strokes)
                    {
                        inkAnalyzer.RemoveDataForStroke(stroke.Id);
                        if (stroke.BoundingRect.Height <= MAX_WRITING)
                        {
                            int line = getLineNumByRect(stroke.BoundingRect);
                            if (!linesErased.Contains(line))
                                linesErased.Add(line);
                        }
                    }

                    autosaveDispatcherTimer.Start();
                    EraseTimer.Start();

                }
            );
        }

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            if (!leftLasso)
            {
                //ClearSelection();
                // dispatcherTimer.Stop();
                //operationDispathcerTimer.Stop();
                inkOperationAnalyzer.ClearDataForAllStrokes();
                if (RawStrokeTimer.IsEnabled && Math.Floor(args.CurrentPoint.Position.Y / LINE_HEIGHT) != showingResultOfLine) {
                    Debug.WriteLine("started writing on new line,will tick timer");
                    RawStrokeTimer_Tick();
                }
                else
                    RawStrokeTimer.Stop();
            }
            autosaveDispatcherTimer.Stop();
            recognizeTimer.Stop();
        }

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
            recognizeTimer.Start();
            //lastStrokePoint = new Point(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y);
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {//processing strokes inputs
                //dispatcherTimer.Stop();
                //operationDispathcerTimer.Stop();            
                foreach (var s in args.Strokes)
                {
                    //Process strokes that excess maximum height for shape recognition (addin control)
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
                        inkAnalyzer.AddDataForStroke(s);
                        inkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
                        //marking the current stroke for later server recognition
                        curStroke = s;
                        //here we need instant call to analyze ink for the specified line input
                        await analyzeInk(s,serverFlag:MainPage.Current.abbreviation_enabled);
                        RawStrokeTimer.Start();
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

        #endregion

        #region Analysis timer tick event handlers

        private async void EraseTimer_Tick(object sender = null, object e = null)
        {
            EraseTimer.Stop();
            await inkAnalyzer.AnalyzeAsync();
            if (inkCan.InkPresenter.StrokeContainer.GetStrokes().Count == 0)
            {
                ClearAllParsedText();
            }
            else {
                foreach (int line in linesErased)
                {
                    List<HWRRecognizedText> updated = await RecognizeLine(line, wholeline: true);

                    //while (updated == null)
                    //{
                    //    await Task.Delay(TimeSpan.FromSeconds(1));
                    //    updated = await RecognizeLine(line, wholeline: true);
                    //}
                    NotePhraseControl npc = phrases.Where(x => x.Key == line).FirstOrDefault().Value;
                    npc.UpdateRecognition(updated,false);
                    Canvas.SetLeft(npc, lastStrokeBound.X);
                    npc.SetPhrasePosition(lastStrokeBound.X, npc.canvasTop);
                }
            }
            linesErased.Clear();
        }

        private void RawStrokeTimer_Tick(object sender = null, object e = null)
        {
            RawStrokeTimer.Stop();
            recognizeAndSetUpUIForLine(curLineObject,timerFlag: true);                       
        }

        private void LineAnalysisDispatcherTimer_Tick(object sender, object e)
        {
            /// Analyze the currently hovering line.
            /// PS:10/9/2018 this method is currently not used as the timer tick is commented out

            var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
            var x = pointerPosition.X - Window.Current.Bounds.X;
            var y = pointerPosition.Y - Window.Current.Bounds.Y;
            int curLine = (int)y / (int)LINE_HEIGHT;
            if (curLine != hoveringLine)
            {
                hoveringLine = curLine;
                recognizeLine(hoveringLine);
            }
        }

        public void UpdateRecognition(int line, List<HWRRecognizedText> result) {
            NotePhraseControl npc = phrases[line];
            npc.UpdateRecognition(result, fromServer: true);

            //refresh current line result with the server result
            var words = npc.words;
            //don't show the UI if no words/results is available
            if (words.Count == 0 || lastWordIndex >= words.Count)
                return;
            //if user is still writing at the current line, update the server recognition
            if (line == showingResultOfLine) {
                curLineWordsStackPanel.Children.Clear();
                foreach (var b in words[lastWordIndex].GetCurWordCandidates())
                    curLineWordsStackPanel.Children.Add(b);
            }
        }

        #endregion

        #region stroke recognition/analyze/UIsettings

        private async Task<bool> analyzeInk(InkStroke lastStroke = null, bool serverFlag = false)
        {
            ///  Analyze ink strokes contained in inkAnalyzer and add phenotype candidates
            ///  from fetching API

            dispatcherTimer.Stop();
            if (inkAnalyzer.IsAnalyzing)
            {
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
                            //Debug.WriteLine($"---------------------recognizing line = {line.Id}");
                            recognizeAndSetUpUIForLine(line, serverRecog:serverFlag);
                            return true;
                        }
                    }
                    // recognize all lines
                    else
                    {
                        int lineNum = getLineNumByRect(line.BoundingRect);
                        if (phrases.ContainsKey(lineNum))
                        {
                            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(phrases[lineNum].GetString());
                            //OperationLogger.getOpLogger().Log(OperationType.Recognition, nl.Text, annoResult);
                            if (annoResult != null && annoResult.Count != 0)
                            {
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

        private async void recognizeAndSetUpUIForLine(InkAnalysisLine line, bool indetails = false, 
                                                        bool serverRecog = false, bool timerFlag = false)

        {
            if (line == null)
                return;

            int lineNum = getLineNumByRect(line.BoundingRect);

            if (!indetails) {

                // switch to another line, clear result of current line
                if (lineNum != showingResultOfLine)
                {
                    Debug.WriteLine($"Switching to a different line, line num= {lineNum}");
                    lastWordCount = 1;
                    lastWordIndex = 0;
                    curLineCandidatePheno.Clear();
                    curLineWordsStackPanel.Children.Clear();
                    HWRManager.getSharedHWRManager().clearCache();
                    phenoCtrlSlide.Y = 0;
                    showingResultOfLine = lineNum;
                    curLineObject = line;
                }
                //writing on an existing line
                if (phrases.ContainsKey(lineNum))
                {
                    NotePhraseControl phrase = phrases[lineNum];
                    lastWordCount = phrase.words.Count == 0 ? 1 : phrase.words.Count;
                    List<HWRRecognizedText> results = await RecognizeLine(lineNum, wholeline: true);
                    phrase.UpdateRecognition(results,false);
                    Canvas.SetLeft(phrase, lastStrokeBound.X);
                    phrase.SetPhrasePosition(lastStrokeBound.X, phrase.lineIndex * LINE_HEIGHT);
                }
                //writing on a new line 
                else
                {
                    //new line
                    NotePhraseControl phrase = new NotePhraseControl(lineNum);
                    recognizedCanvas.Children.Add(phrase);
                    double left = curStroke == null ? 0 : curStroke.BoundingRect.X;
                    Canvas.SetLeft(phrase, left);
                    Canvas.SetTop(phrase, lineNum * LINE_HEIGHT);
                    phrase.SetPhrasePosition(left, lineNum * LINE_HEIGHT);
                    phrases[lineNum] = phrase;
                    Debug.WriteLine($"Created a new line = {lineNum}");
                }
                //OperationLogger.getOpLogger().Log(OperationType.StrokeRecognition, line.RecognizedText);
            }

            // HWR result UI
            var words = phrases[lineNum].words;
            //don't show the UI if no words/results is available
            if (words.Count == 0 || lastWordIndex >= words.Count)
                return;

            curLineWordsStackPanel.Children.Clear();
            foreach (var b in words[lastWordIndex].GetCurWordCandidates())
                curLineWordsStackPanel.Children.Add(b);

            loading.Visibility = Visibility.Collapsed;
            curLineWordsStackPanel.Visibility = Visibility.Visible;
            curLineParentStack.Visibility = Visibility.Visible;
            curLineResultPanel.Visibility = Visibility.Visible;
            Canvas.SetLeft(curLineResultPanel, lastWordPoint.X);
            Canvas.SetTop(curLineResultPanel, lastWordPoint.Y - LINE_HEIGHT);

            // annotation and UI
            annotateCurrentLineAndUpdateUI(line);
            //logging recognized text to logger
            if (timerFlag)
            {
                curLineWordsStackPanel.Children.Clear();
                curLineParentStack.Visibility = Visibility.Collapsed;
                if (curLineCandidatePheno.Count == 0)
                    curWordPhenoControlGrid.Visibility = Visibility.Collapsed;
            }

            if (indetails)
            {
                curLineWordsStackPanel.Children.Clear();
                string str = phrases[lineNum].GetString();
                TextBlock tb = new TextBlock();
                tb.Text = str;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.FontSize = 16;
                curLineWordsStackPanel.Children.Add(tb);
                PopupCommandBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefPanel.Visibility = Visibility.Visible;
                Canvas.SetLeft(PopupCommandBar, boundingRect.X);
                Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height);
                PopupCommandBar.Width = Math.Max(boundingRect.Width, PopupCommandBar.MinWidth);
                Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
                Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);

                Canvas.SetLeft(curLineResultPanel, boundingRect.X);
                Canvas.SetTop(curLineResultPanel, boundingRect.Y - LINE_HEIGHT);

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

        /// <summary>
        /// select and recognize a line by its id
        /// </summary>
        private async Task<List<HWRRecognizedText>> RecognizeLine(int lineid, bool wholeline = false)
        {
            // only one thread is allowed to use select and recognize
            await selectAndRecognizeSemaphoreSlim.WaitAsync();
            try
            {
                // clear selection
                foreach (var stroke in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                {
                    stroke.Selected = false;
                }
                //first setting lower/uppbound in case argument lineid refers to UI line (called from erase.tick)
                double lowerbound = lineid * LINE_HEIGHT;
                double upperbound = (lineid + 1) * LINE_HEIGHT;
                // select storkes of this line
                var allwords = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);

                ////for microsoft HWR line.id
                //exclusively for erased lines
                var thisline = new List<IInkAnalysisNode>();
                int line_index = 0;
                if (wholeline)
                {
                    thisline.AddRange(allwords.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 3) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 3) <= upperbound).OrderByDescending(x => x.BoundingRect.X).ToList());
                    var temp = thisline.OrderBy(x => x.BoundingRect.X).ToList();
                    lastStrokeBound = temp.Count == 0? lastStrokeBound: temp[0].BoundingRect;
                }
                else {
                    thisline.Add( allwords.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 3) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 3) <= upperbound).FirstOrDefault());
                    lastStrokeBound = thisline.Count == 0? lastStrokeBound: thisline[0].BoundingRect;
                }
                //Debug.WriteLine($"first word is at x={lastStrokeBound.X}");
                //Debug.WriteLine($"this line node count = {thisline.Count}");

                //return empty recognized result if there's no strokes
                if (thisline.Count == 0)
                    return new List<HWRRecognizedText>();

                line_index = getLineNumByRect(thisline[0].BoundingRect);

                lowerbound = line_index * LINE_HEIGHT;
                upperbound = (line_index + 1) * LINE_HEIGHT;
                //sometimes microsoft thinks two phrases on the same line 
                foreach (var x in thisline)
                {
                    foreach (var sid in x.GetStrokeIds())
                        inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                }

                //recognize selection
                List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer,InkRecognitionTarget.Selected, line_index);

                //find the most recent word of the line and records its coordinates
                var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
                words = words.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 3) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 3) <= upperbound).ToList();
                if (words.Count > 0)
                {
                    var lastword = words.OrderByDescending(x => x.BoundingRect.X).Where(x => x.GetStrokeIds().Contains(curStroke.Id)).First();
                    //only updates the lastwordindex if HWR returns valid recognition, o.w. program will throw exception
                    if (recognitionResults != null)
                        lastWordIndex = words.OrderBy(x => x.BoundingRect.X).ToList().IndexOf(lastword);

                    if (lastword != null)
                        lastWordPoint = new Point(lastword.BoundingRect.X, getLineNumByRect(lastword.BoundingRect) * LINE_HEIGHT);
                }
                //if no words are recognized, by default sets X-pos to starting of line
                else
                {
                    lastWordPoint = new Point(thisline[0].BoundingRect.X, line_index * LINE_HEIGHT);
                    lastWordIndex = 0;
                }



                return recognitionResults;
            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error($"Failed to recognize line ({lineid}): {ex} + {ex.Message}");
            }
            finally
            {
                selectAndRecognizeSemaphoreSlim.Release();
            }
            return null;
        }

        public void ClearAllParsedText() {
            recognizedCanvas.Children.Clear();
            phrases.Clear();
        }

        //private void setUpCurrentLineResultUI(int line_index)
        //{

        //}

        private void ShowAlterOnHover(Point pos)
        {
            foreach (var s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
            {
                SetDefaultStrokeStyle(s);
            }
            Rect pointerRec = new Rect(pos.X, pos.Y, 1, 1);
            int lineNum = getLineNumByRect(pointerRec);
            var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
            double lowerbound = lineNum * LINE_HEIGHT;
            double upperbound = (lineNum + 1) * LINE_HEIGHT;

            words = words.Where(x => x.BoundingRect.Y + x.BoundingRect.Height / 3 >= lowerbound && x.BoundingRect.Y + x.BoundingRect.Height / 3 <= upperbound).OrderBy(x => x.BoundingRect.X).ToList();

            bool hovering = false;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];

                Rect wordRect = new Rect(w.BoundingRect.X, lineNum * LINE_HEIGHT, w.BoundingRect.Width, LINE_HEIGHT);
                if (wordRect.Contains(pos))
                {
                    WordBlockControl wbc = phrases.Where(x => x.Key == lineNum).FirstOrDefault().Value.words.Where(x => x.word_index == i).FirstOrDefault();
                    if (wbc != null)
                    {
                        foreach (var sid in w.GetStrokeIds()) {
                            var s = inkCan.InkPresenter.StrokeContainer.GetStrokeById(sid);
                            SetSelectedStrokeStyle(s);
                        }
                        hovering = true;
                        curLineWordsStackPanel.Children.Clear();
                        foreach (var b in wbc.GetCurWordCandidates()) {
                            curLineWordsStackPanel.Children.Add(b);
                        }
                        //foreach (string alternative in wbc.candidates)
                        //{
                        //    Button alter = new Button();
                        //    alter.VerticalAlignment = VerticalAlignment.Center;
                        //    alter.FontSize = 16;
                        //    alter.Content = alternative;
                        //    alter.Click += (object sender, RoutedEventArgs e) =>
                        //    {
                        //        int ind = wbc.candidates.IndexOf((string)((Button)sender).Content);
                        //        wbc.selected_index = ind;
                        //        wbc.current = wbc.candidates[ind];
                        //        wbc.WordBlock.Text = wbc.current;
                        //        wbc.corrected = true;
                        //        HideCurLineStackPanel();
                        //        annotateCurrentLineAndUpdateUI(line_index: lineNum);
                        //        MainPage.Current.NotifyUser($"Changed to {wbc.current}", NotifyType.StatusMessage, 1);
                        //        ClearSelectionAsync();
                        //        UpdateLayout();
                        //    };
                        //    curLineWordsStackPanel.Children.Add(alter);
                        //}
                        TextBox tb = new TextBox();
                        tb.Width = 40;
                        tb.Height = curLineWordsStackPanel.ActualHeight * 0.7;
                        tb.KeyDown += (object sender, KeyRoutedEventArgs e) => {
                            if (e.Key.Equals(VirtualKey.Enter))
                            {
                                wbc.ChangeAlterFromStroke(tb.Text);
                                HideCurLineStackPanel();
                            }
                        };
                        tb.LostFocus += (object sender, RoutedEventArgs e) => {
                            wbc.ChangeAlterFromStroke(tb.Text);
                            HideCurLineStackPanel();
                        };
                        curLineWordsStackPanel.Children.Add(tb);
                        loading.Visibility = Visibility.Collapsed;
                        curWordPhenoControlGrid.Visibility = Visibility.Collapsed;
                        curLineWordsStackPanel.Visibility = Visibility.Visible;
                        curLineParentStack.Visibility = Visibility.Visible;
                        curLineResultPanel.Visibility = Visibility.Visible;
                        Canvas.SetLeft(curLineResultPanel, w.BoundingRect.X);
                        Canvas.SetTop(curLineResultPanel, (lineNum - 1) * LINE_HEIGHT);
                        return;
                    }

                }

            }
            if (!hovering)
                HideCurLineStackPanel();


        }

        public void ShowAbbreviationAlter(WordBlockControl wbc, List<string> alter)
        {
            alternativeListView.ItemsSource = alter;
            alter_flyout.ShowAt(curLineResultPanel);
        }

        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e)
        {

            //var citem = (string)e.ClickedItem;
            //int ind = alternativeListView.Items.IndexOf(citem);
            
            //phrases[showingResultOfLine].words[ind].current = citem;
            //phrases[showingResultOfLine].words[ind].selected_index = phrases[showingResultOfLine].words[ind].candidates.IndexOf(citem);
            //foreach (var w in phrases[showingResultOfLine].words)
            //{
            //    TextBlock tb = new TextBlock();
            //    tb.Text = w.current;
            //    tb.Tapped += (object s, TappedRoutedEventArgs args) => {
            //        alternativeListView.ItemsSource = w.candidates;
            //        alter_flyout.ShowAt(tb);

            //    };
            //    curLineWordsStackPanel.Children.Add(tb);
            //}

            //NoteLine curLine = idToNoteLine[(uint)showingResultOfLine];
            //Dictionary<string, List<string>> dict = HWRManager.getSharedHWRManager().getDictionary();

            //var citem = (string)e.ClickedItem;
            //int ind = alternativeListView.Items.IndexOf(citem);
            //int previous = curLine.HwrResult[showAlterOfWord].selectedIndex;
            //string old_form = curLine.HwrResult[showAlterOfWord].selectedCandidate;
            //string term = "";
            //if (showAlterOfWord - 1 >= 0)
            //    term = curLine.HwrResult[showAlterOfWord - 1].selectedCandidate;

            //When changing the alternative of an abbreviation, just change result UI and re-annotate
            //note that all words in an extended form of an abbereviation will contains at least one space
            //this is a faster way of identifying an abbreviation, but feel free to change if there's better way of
            //doing so.
            //if (dict.ContainsKey(term.ToLower()) && dict[term.ToLower()].Contains(old_form))
            //{
            //    Debug.WriteLine("\nchanging alternative of an abbreviation.\n");
            //    curLine.updateHwrResult(showAlterOfWord, ind, old_form);
            //    string new_form = curLine.HwrResult[showAlterOfWord].selectedCandidate;
            //    string parsed = OperationLogger.getOpLogger().ParseCandidateList(curLine.HwrResult[showAlterOfWord].candidateList);
            //    //OperationLogger.getOpLogger().Log(OperationType.Abbreviation,curLine.Text ,term ,old_form ,new_form ,ind.ToString(),parsed);
            //}
            //else
            //{
            //    Debug.WriteLine("\nchanging a normal word in dispaly.\n");
            //    //HWRManager.getSharedHWRManager().setRequestType(false);
            //    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
            //        curLine.updateHwrResult(showAlterOfWord, ind);
            //    });
            //    string new_form = curLine.HwrResult[showAlterOfWord].selectedCandidate;
            //    //OperationLogger.getOpLogger().Log(OperationType.Alternative, old_form, new_form, ind.ToString());
            //    curLine.HwrResult = await HWRManager.getSharedHWRManager().ReRecognizeAsync(curLine.HwrResult);

            //}

            //// HWR result UI
            //setUpCurrentLineResultUI(curLineObject);
            //// re-annotation and UI set-up after all HWR has been updated
            //phenoCtrlSlide.Y = 0;
            //curLineCandidatePheno.Clear();
            //annotateCurrentLineAndUpdateUI(curLineObject);
        }

        public async void annotateCurrentLineAndUpdateUI(InkAnalysisLine line=null, int line_index = -1)
        {
            int lineNum = line == null? line_index:getLineNumByRect(line.BoundingRect);
            // after get annotation, recognized text has also changed
            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(phrases[lineNum].GetString());
            //OperationLogger.getOpLogger().Log(OperationType.Recognition, idToNoteLine.GetValueOrDefault(line.Id).Text, annoResult);

            if (annoResult != null && annoResult.Count > 0)
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
                //idToNoteLine.GetValueOrDefault(line.Id).phenotypes = annoResult.Values.ToList();

                curWordPhenoControlGrid.Visibility = Visibility.Visible;

                if (curLineCandidatePheno.Count == 0 || phenoCtrlSlide.Y == 0)
                {
                    //Debug.WriteLine($"current Y offset is at {phenoCtrlSlide.Y}, visibility is {curWordPhenoControlGrid.Visibility}");
                    phenoCtrlSlide.Y = 0;
                    ((DoubleAnimation)curWordPhenoAnimation.Children[0]).By = -45;
                    curWordPhenoAnimation.Begin();
                }


                foreach (var pheno in annoResult.Values.ToList())
                {
                    var temp = curLineCandidatePheno.Where(x => x == pheno).FirstOrDefault();
                    pheno.state = PhenotypeManager.getSharedPhenotypeManager().getStateByHpid(pheno.hpId);
                    if (temp == null)
                        curLineCandidatePheno.Add(pheno);
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
                curWordPhenoControlGrid.Visibility = Visibility.Collapsed;
                //curWordPhenoControlGrid.Margin = new Thickness(0, 0, 0, 0);
                // phenoCtrlSlide.Y = 0;
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

            curWordPhenoControlGrid.Visibility = Visibility.Visible;

        }

        #endregion


        // ================================= INK RECOGNITION ==============================================                      
        /// <summary>
        /// PS:10/9/2018 this method is currently not used as the timer tick is commented out
        /// </summary>
        private void recognizeLine(int line)
        {
            if (line < 0)
                return;
            SelectAndAnnotateByLineNum(line);
        }

        /// <summary>
        /// PS:10/9/2018 this method is currently not used as the timer tick is commented out
        /// </summary>
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
                        List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(
                                                                        inkCanvas.InkPresenter.StrokeContainer,InkRecognitionTarget.Selected);

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
        /// Handwrting recognition on all selected strokes and returns string results.
        /// </summary>
        private async Task<string> recognizeSelection()
        {
            //Debug.WriteLine("\n recognizeSelection triggered\n");
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                List<HWRRecognizedText> recognitionResults = new List<HWRRecognizedText>();
                recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected);
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
            ClearSelectionAsync();
            return String.Empty;
        }

        /// <summary>
        /// Recognize selected strokes within the selected bounding rectangle and searches for possible phenotypes.
        /// </summary>
        public async void RecognizeSelection(string text = "")
        {
            selectionCanvas.Children.Clear();

            if (boundingRect.Width <= 0 || boundingRect.Height <= 0)
                return;

            //recogPhenoFlyout.ShowAt(rectangle);
            string str = text.Length == 0 ? await recognizeSelection() : text;

            if (!str.Equals(String.Empty) && !str.Equals(""))
            {
                PopupCommandBar.Visibility = Visibility.Visible;
                recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
                Canvas.SetLeft(PopupCommandBar, boundingRect.X);
                Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height);
                PopupCommandBar.Width = Math.Max(boundingRect.Width, PopupCommandBar.MinWidth);
                //Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
                //Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);
                selectionRectangle.Width = boundingRect.Width;
                selectionRectangle.Height = boundingRect.Height;
                //selectionRectangleTranform = new TranslateTransform();
                //selectionRectangle.RenderTransform = this.selectionRectangleTranform;



                if (ehrPage != null)
                {
                    Canvas.SetLeft(selectionRectangle, boundingRect.Left + 10);
                    Canvas.SetTop(selectionRectangle, boundingRect.Bottom);
                    //Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height + ehrPage.LINE_HEIGHT);
                    //ehrPage.popupCanvas.Children.Add(PopupCommandBar);
                    if (!ehrPage.popupCanvas.Children.Contains(selectionRectangle))
                        ehrPage.popupCanvas.Children.Add(selectionRectangle);
                    if (!ehrPage.popupCanvas.Children.Contains(recognizedPhenoBriefPanel))
                        ehrPage.popupCanvas.Children.Add(recognizedPhenoBriefPanel);
                    selectionRectangle.Visibility = Visibility.Collapsed;

                }
                else {
                    Canvas.SetLeft(selectionRectangle, boundingRect.Left);
                    Canvas.SetTop(selectionRectangle, boundingRect.Top);

                    selectionCanvas.Children.Add(PopupCommandBar);
                    selectionCanvas.Children.Add(recognizedPhenoBriefPanel);
                    selectionCanvas.Children.Add(selectionRectangle);
                    //TestC.Children.Add(selectionRectangle);

                }
                var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
                recognizedResultTextBlock.Text = str;
                searchPhenotypes(str);
                recogPhenoFlyout.ShowAt(selectionRectangle);

            }
        }

        public void ClearBriefPhenotypeEHR() {
            recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
            selectionRectangle.Visibility = Visibility.Collapsed;
        }

        public void HideCurLineStackPanel()
        {
            curLineWordsStackPanel.Children.Clear();
            curLineParentStack.Visibility = Visibility.Collapsed;
            curLineResultPanel.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// After timer ticks, trigger server side HWR for abbreviation detection
        /// note this method will be sending with request type = new.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TriggerRecogServer(object sender, object e)
        {
            //Debug.WriteLine("\n TRIGGER TICKED, WILL ANALYZE THROUGH SERVER...\n");

            recognizeTimer.Stop();
            if (inkAnalyzer.IsAnalyzing)
            {
                //Debug.WriteLine("ink still analyzing-trigger");
                // inkAnalyzer is being used 
                // try again after some time by dispatcherTimer 
                recognizeTimer.Start();
                return;
            }
            // analyze 
            var result = await inkAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //int line = linesToAnnotate.Dequeue();
                IReadOnlyList<IInkAnalysisNode> lineNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);

                foreach (InkAnalysisLine line in lineNodes)
                {
                    // current line
                    if (curStroke != null && line.GetStrokeIds().Contains(curStroke.Id))
                    {
                        //Debug.WriteLine("\nfound line.");
                        // set up for current line
                        HWRManager.getSharedHWRManager().setRequestType(true);
                        recognizeAndSetUpUIForLine(line, false, serverRecog: true);
                    }

                }
            }
        }

        private async Task<int> RecognizeInkOperation()
        {
            /// Recognize a set of strokes as whether a shape or just drawing and handles each case
            /// accordingly.

            var result = await inkOperationAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //first need to clear all previous selections to filter out strokes that don't want to be deleted
                foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                //Gets all strokes from inkoperationanalyzer
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                {
                    //user has drawn a square/rectangle for adding an add-in
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                    {
                        recognizeTimer.Stop();
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
                        //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                    }
                }

                // delete all strokes that are too large, like drawings
                foreach (var sid in inkOperationAnalyzer.AnalysisRoot.GetStrokeIds())
                {
                    inkOperationAnalyzer.RemoveDataForStroke(sid);
                    inkCan.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                }
                inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                return 1;
            }
            //inkoperation has no update, does nothing
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

        /// <summary>
        /// Called upon page creation/page switch, will re-analyze everything on the current page and
        /// change phenotype candidates accordingly.
        /// </summary>
        public async void initialAnalyze()
        {
            rootPage.NotifyUser("Analyzing current page ...", NotifyType.StatusMessage, 2);
            inkAnalyzer = new InkAnalyzer();
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            bool result = false;
            while (!result)
                result = await analyzeInk(serverFlag: true);//will be using server side HWR upon page load
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
