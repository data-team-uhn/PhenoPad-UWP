﻿using PhenoPad.PhenotypeService;
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
    // CLASS WITH FUNCTIONS DEDICATED FOR NOTE TAKING STROKE INK ANALYSIS

    public enum NodeType{
        InkWord,
        InkLine,
        Both
    }

    public sealed partial class NotePageControl : UserControl
    {
        public DispatcherTimer RawStrokeTimer;
        DispatcherTimer EraseTimer;
        List<InkStroke> RawStrokes;
        InkAnalyzer strokeAnalyzer;
        int lastWordCount;
        int lastWordIndex;
        Rect lastStrokeBound;
        List<int> linesErased = new List<int>();
        private Dictionary<int, NotePhraseControl> phrases = new Dictionary<int, NotePhraseControl>();

        #region InkCanvas interactions

        private void InkCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ClearSelectionAsync();
            Point pos = e.GetPosition(inkCanvas);
            Rect pointerRec = new Rect(pos.X, pos.Y, 1, 1);
            int lineNum = getLineNumByRect(pointerRec);

            if (!phrases.ContainsKey(lineNum))
                return;
            bool hovering = false;

            /*---- gets the hit WBC and setup UI for hit word ----*/
            var wb = phrases[lineNum].GetHitWBC(pos);
            if (wb != null)
            {
                foreach (var wbs in wb.strokes)
                {
                    var s = inkCan.InkPresenter.StrokeContainer.GetStrokeById(wbs.Id);
                    SetSelectedStrokeStyle(s);
                }
                hovering = true;

                curLineWordsStackPanel.Children.Clear();
                foreach (var b in wb.GetCurWordCandidates())
                {
                    curLineWordsStackPanel.Children.Add(b);
                }

                TextBox tb = new TextBox();
                tb.Width = 40;
                tb.Height = curLineWordsStackPanel.ActualHeight * 0.7;
                tb.KeyDown += (object sender1, KeyRoutedEventArgs e1) =>
                {
                    if (e1.Key.Equals(VirtualKey.Enter))
                    {
                        this.Focus(FocusState.Programmatic);
                    }
                };
                tb.LostFocus += (object sender1, RoutedEventArgs e1) =>
                {
                    if (tb.Text.Length > 0)
                    {
                        wb.ChangeAlterFromTextInput(tb.Text);
                    }
                    HideCurLineStackPanel();
                };
                curLineWordsStackPanel.Children.Add(tb);
                curWordPhenoControlGrid.Visibility = Visibility.Collapsed;
                curLineWordsStackPanel.Visibility = Visibility.Visible;
                curLineResultPanel.Visibility = Visibility.Visible;
                Canvas.SetLeft(curLineResultPanel, wb.BoundingRect.X);
                Canvas.SetTop(curLineResultPanel, (lineNum - 1) * LINE_HEIGHT);
                return;
            }
            if (!hovering)
                HideCurLineStackPanel();
        }

        private void InkCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var position = e.GetPosition(inkCanvas);
            ClearSelectionAsync();

            string text = "";
            recognizedText.Clear();
            Rect pointerRec = new Rect(position.X, position.Y, 1, 1);
            int lineNum = getLineNumByRect(pointerRec);
            if (!phrases.ContainsKey(lineNum))
                return;
            var inlines = phrases[lineNum].GetHitWBCPhrase(position);
            if (inlines != null)
            {
                var strokes = inlines.SelectMany(x => x.strokes).ToList();
                foreach (var stroke in strokes)
                {
                    var s = inkCan.InkPresenter.StrokeContainer.GetStrokeById(stroke.Id);
                    s.Selected = true;
                    SetSelectedStrokeStyle(s);
                }
                List<Rect> rects = strokes.Select(x => x.BoundingRect).OrderBy(x=>x.X+x.Width).ToList();
                double left = rects.FirstOrDefault().X;
                double right = rects.LastOrDefault().X + rects.LastOrDefault().Width;
                boundingRect = new Rect(left - 5, lineNum * LINE_HEIGHT, right - left + 10, LINE_HEIGHT);

                foreach (var w in inlines)
                {
                    recognizedText.Add(w.ConvertToHWRRecognizedText());
                    text += w.current + " ";
                }
                RecognizeSelection(text);
            }
        }

        #endregion

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

                    foreach (var stroke in args.Strokes)
                    {
                        inkAnalyzer.RemoveDataForStroke(stroke.Id);
                        if (stroke.BoundingRect.Height <= MAX_WRITING)
                        {
                            int line = getLineNumByRect(stroke.BoundingRect);
                            if (StrokesInLine.ContainsKey(line) && StrokesInLine[line].Contains(stroke)) {
                                StrokesInLine[line].Remove(stroke);
                            }
                            if (!linesErased.Contains(line))
                                linesErased.Add(line);
                        }
                    }

                    autosaveDispatcherTimer.Start();
                    EraseTimer.Start();
                }
            );
        }

        private async void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            if (!leftLasso && curStroke != null)
            {
                RawStrokeTimer.Stop();              
                inkOperationAnalyzer.ClearDataForAllStrokes();

                // checks if writing on new line and instantly recognizes if so
                var newLine = (int)Math.Floor(args.CurrentPoint.Position.Y / LINE_HEIGHT);
                if (newLine != showingResultOfLine)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    recognizeAndSetUpUIForLine(line: null, lineInd:showingResultOfLine);
                    });
                    curLineCandidatePheno.Clear();
                    curLineWordsStackPanel.Children.Clear();
                    curLineResultPanel.Visibility = Visibility.Collapsed;
                    UpdateLayout();
                }

            }
            autosaveDispatcherTimer.Stop();
        }

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {
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
                            MetroLogger.getSharedLogger().Error($"InkPresenter_StrokesCollectedAsync in NotePageControl:{e.Message}");
                        }
                    }

                    // Instantly analyze ink inputs
                    else
                    {
                        // marking the current stroke for later server recognition
                        int lineNum = getLineNumByRect(s.BoundingRect);
                        if (!StrokesInLine.ContainsKey(lineNum))
                            StrokesInLine.Add(lineNum, new List<InkStroke> { s });
                        else
                            StrokesInLine[lineNum].Add(s);
                        curStroke = s;
                        inkAnalyzer.AddDataForStroke(s);
                        inkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
                        recognizeAndSetUpUIForLine(line: null, lineInd: lineNum);
                        OperationLogger.getOpLogger().Log(OperationType.Stroke, s.Id.ToString(), s.StrokeStartedTime.Value.ToString(), s.StrokeDuration.ToString(), getLineNumByRect(s.BoundingRect).ToString(), pageId.ToString());
                        RawStrokeTimer.Start();
                    }

                }
            }
            else
            {
                // processing strokes selected with left mouse lasso strokes
                leftLossoStroke = args.Strokes;
                foreach (var s in args.Strokes)
                {
                    // TODO...
                }
            }
        }

        #endregion

        #region Analysis timer tick event handlers

        private async void EraseTimer_Tick(object sender = null, object e = null)
        {
            EraseTimer.Stop();
            if (inkAnalyzer.IsAnalyzing) {               
                EraseTimer.Start();
                return;
            }
            await inkAnalyzer.AnalyzeAsync();
            // if the canvas is completely empty after erasing, just clear all HWR
            if (inkCan.InkPresenter.StrokeContainer.GetStrokes().Count == 0)
                ClearAllParsedText();
            // otherwise need to re-recognize for all lines that has been erased
            else {
                foreach (int line in linesErased)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                        if (phrases.ContainsKey(line))
                        {
                            var strokes = FindAllStrokesInLine(line);
                            List<WordBlockControl> updated = new List<WordBlockControl>();
                            NotePhraseControl npc = phrases[line];
                            if (strokes.Count == 0)
                            {
                                npc.ClearPhrase();
                            }
                            else {
                                updated = await RecognizeLineWBC(line);
                                npc.UpdateRecognition(updated, fromServer: false, fromErase:true);

                            }
                        }
                    });
                }
            }
            linesErased.Clear();
            Debug.WriteLine("done re recognizing after erase");
            autosaveDispatcherTimer.Start();
        }

        /// <summary>
        /// Recognizes the last written line when timer ticks.
        /// </summary>
        private void RawStrokeTimer_Tick(object sender = null, object e = null)
        {
            RawStrokeTimer.Stop();

            int curLine = getLineNumByRect(curStroke.BoundingRect);
            recognizeAndSetUpUIForLine(line : null, lineInd: curLine);
        }

        public async void UpdateRecognitionFromServer(int lineNum, List<HWRRecognizedText> result) {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (!phrases.ContainsKey(lineNum) || result == null || result.Count ==0)
                    return;

                var dict = GetPhrasesOnLine(lineNum);

                if (dict.Count == 0) {
                    return;
                }

                List<WordBlockControl> wbs = new List<WordBlockControl>();
                for (int i = 0; i < result.Count; i++)
                {
                    HWRRecognizedText recognized = result[i];
                    int ind = i;
                    // this handles some weird error when dict count != HWR count, by default will count this word as last phrase
                    if (i >= dict.Count)
                        ind = dict.Count - 1;
                    WordBlockControl wb = new WordBlockControl(lineNum, i, recognized.selectedCandidate, recognized.candidateList,recognized.strokes);
                    // just an intuitive way to check for abbreviation
                    if (recognized.candidateList.Count > 5)
                        wb.is_abbr = true;
                    wbs.Add(wb);
                }


                NotePhraseControl npc = phrases[lineNum];
                npc.UpdateRecognition(wbs, fromServer: true);

                // refresh current line result with the server result
                var words = npc.words;
                // don't show the UI if no words/results is available
                if (words.Count == 0 || lastWordIndex >= words.Count)
                    return;
                // if user is still writing at the current line, update the server recognition
                if (lineNum == showingResultOfLine && curLineResultPanel.Visibility == Visibility.Visible)
                {
                    curLineWordsStackPanel.Children.Clear();
                    foreach (var tb in phrases[lineNum].GetCurLineHWR())
                        curLineWordsStackPanel.Children.Add(tb);
                }
                
            });
        }

        #endregion

        #region stroke recognition/analyze/UIsettings

        /// <summary>
        /// Called after strokes are collected to recoginze words and shapes
        /// </summary>
        private async void InkAnalysisDispatcherTimer_Tick(object sender, object e)
        {
            dispatcherTimer.Stop();
            Debug.WriteLine("ink analysis tick, will analyze ink ...");
            await analyzeInk(curStroke, serverFlag: MainPage.Current.abbreviation_enabled);
        }

        /// <summary>
        /// Analyze ink strokes contained in inkAnalyzer and add phenotype candidates from fetching API
        /// </summary>
        private async Task<bool> analyzeInk(InkStroke lastStroke = null, bool serverFlag = false)
        {
            dispatcherTimer.Stop();
            if (inkAnalyzer.IsAnalyzing)
            {
                Debug.WriteLine("still analyzing...");
                dispatcherTimer.Start();
                return false;
            }

            var result = await inkAnalyzer.AnalyzeAsync();
            if (result.Status == InkAnalysisStatus.Updated)
            {
                // analyze whole line of stroke based on last written stroke
                if (lastStroke != null)
                {
                    var lineNum = getLineNumByRect(lastStroke.BoundingRect);
                    recognizeAndSetUpUIForLine(line:null, lineInd:lineNum);
                    return true;
                }
                // analyze all lines within the page
                else {
                    Debug.WriteLine("current stroke is null, will analyze whole page ...");
                    for (int i = 1; i < PAGE_HEIGHT / LINE_HEIGHT; i++) {
                        int lineNum = i;
                        if (phrases.ContainsKey(lineNum)) {
                            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(phrases[lineNum].GetString());
                            if (annoResult != null && annoResult.Count > 0)
                                foreach (var pp in annoResult.Values)
                                {
                                    pp.sourceType = SourceType.Notes;
                                    phrases[lineNum].phenotypes.Add(pp);
                                    PhenoMana.addPhenotypeCandidate(pp, SourceType.Notes);
                                }
                        }
                    }
                }
                return true;
            }

            // analyze result no update, do nothing
            Debug.WriteLine("analyze result no update");
            return false;
        }

        private async void recognizeAndSetUpUIForLine(InkAnalysisLine line, int lineInd = -1,bool indetails = false, bool timerFlag = false)
        {
            if (line == null && lineInd == -1) {
                Debug.WriteLine("recognizeAndSetUpUIForLine -> line null AND lineInd -1");
                return;
            }
                

            int lineNum = line == null? lineInd : getLineNumByRect(line.BoundingRect);
            if (!indetails) {

                // switch to another line, clear result of current line
                if (lineNum != showingResultOfLine)
                {
                    lastWordCount = 1;
                    lastWordIndex = 0;
                    HideCurLineStackPanel();
                    curLineCandidatePheno.Clear();
                    phenoCtrlSlide.Y = 0;
                    showingResultOfLine = lineNum;
                    UpdateLayout();

                }
                // writing on an existing line
                if (phrases.ContainsKey(lineNum))
                {
                    NotePhraseControl phrase = phrases[lineNum];
                    lastWordCount = phrase.words.Count == 0 ? 1 : phrase.words.Count;
                    List<WordBlockControl> results = await RecognizeLineWBC(lineNum);
                    phrase.UpdateRecognition(results, fromServer:false);
                }
                // writing on a new line 
                else
                {
                    // new line
                    NotePhraseControl phrase = new NotePhraseControl(lineNum);
                    phrases[lineNum] = phrase;
                    recognizedCanvas.Children.Add(phrase);
                    Canvas.SetLeft(phrase, 0);
                    Canvas.SetTop(phrase, lineNum * LINE_HEIGHT);
                }
                //OperationLogger.getOpLogger().Log(OperationType.StrokeRecognition, line.RecognizedText);
            }

            // HWR result UI
            var words = phrases[lineNum].words;
            // don't show the UI if no words/results is available
            if (words.Count == 0 || lastWordIndex > words.Count)
                return;

            curLineWordsStackPanel.Children.Clear();
            foreach (var txtblock in phrases[lineNum].GetCurLineHWR())
                curLineWordsStackPanel.Children.Add(txtblock);
            Canvas.SetLeft(curLineResultPanel, lastStrokeBound.X);
            Canvas.SetTop(curLineResultPanel, (showingResultOfLine - 1) * LINE_HEIGHT - 15);
            ShowCurLineStackPanel();

            if (indetails)
            {
                curLineWordsStackPanel.Children.Clear();
                string str = phrases[lineNum].GetString();
                TextBlock tb = new TextBlock();
                tb.Text = str;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.FontSize = 16;
                curLineWordsStackPanel.Children.Add(tb);

                recognizedPhenoBriefPanel.Visibility = Visibility.Visible;

                Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
                Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);

                Canvas.SetLeft(curLineResultPanel, boundingRect.X);
                Canvas.SetTop(curLineResultPanel, boundingRect.Y - LINE_HEIGHT);

                selectionCanvas.Children.Add(recognizedPhenoBriefPanel);

                selectionRectangle.Width = boundingRect.Width;
                selectionRectangle.Height = boundingRect.Height;

                selectionCanvas.Children.Add(selectionRectangle);
                Canvas.SetLeft(selectionRectangle, boundingRect.Left);
                Canvas.SetTop(selectionRectangle, boundingRect.Top);

                searchPhenotypesAndSetUpBriefView(str);
            }
        }

        /// <summary>
        /// Manually check for number of words within an inkanalysis line and returns a dictionary matching <key=phrase left, value=number of words matched>
        /// </summary>
        private List<double> GetPhrasesOnLine(int lineid)
        {
            List<double> lst = new List<double>();

            var inLine = FindInkNodeInLine(lineid, NodeType.InkLine);
            var inWords = FindInkNodeInLine(lineid, NodeType.InkWord);
            var strokeInLine = FindAllStrokesInLine(lineid);

            foreach (var phrase in inLine) {
                double start = phrase.BoundingRect.X;
                double end = phrase.BoundingRect.X + phrase.BoundingRect.Width;
                var hitWords = inWords.Where(x => x.BoundingRect.X >= start && x.BoundingRect.X + x.BoundingRect.Width <= end).ToList();
                foreach (var w in hitWords)
                    lst.Add(start);
            }
            // handling mistakes and manually add first stroke bound
            if (lst.Count == 0 && strokeInLine.Count > 0) {
                lst.Add(strokeInLine.FirstOrDefault().BoundingRect.X);
            }

            return lst;
        }

        public void InitAnalyzeStrokes()
        {
            // gets all strokes in current canvas and add them to dictionary 
            foreach (var s in inkCan.InkPresenter.StrokeContainer.GetStrokes()) {
                int lineNum = getLineNumByRect(s.BoundingRect);
                if (StrokesInLine.ContainsKey(lineNum))
                {
                    StrokesInLine[lineNum].Add(s);
                }
                else {
                    StrokesInLine.Add(lineNum, new List<InkStroke>() { s });
                }
            }
            Debug.WriteLine(StrokesInLine.Keys.Count + " ==================");
        }

        private async Task<List<WordBlockControl>> RecognizeLineWBC(int lineid)
        {
            // don't bother if there aren't any strokes to recognize
            if (! StrokesInLine.ContainsKey(lineid) || StrokesInLine[lineid].Count == 0)
                return new List<WordBlockControl>();

            // only one thread is allowed to use select and recognize
            await selectAndRecognizeSemaphoreSlim.WaitAsync();
            List<WordBlockControl> result = new List<WordBlockControl>();
            try
            {
                var strokeInLine = StrokesInLine[lineid].OrderBy(x => x.BoundingRect.X).ToList();

                lastStrokeBound = strokeInLine.FirstOrDefault().BoundingRect;
                foreach (var s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                foreach (var s in strokeInLine)
                    s.Selected = true;

                // recognize selected line phrase and parse each word as a WordBlockControl
                var HWRresult = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, lineid);


                for (int i = 0; i < HWRresult.Count; i++)
                {
                    HWRRecognizedText recognized = HWRresult[i];
                    WordBlockControl wb = new WordBlockControl(lineid, i, recognized.selectedCandidate, recognized.candidateList,recognized.strokes);
                    // just an intuitive way to check for abbreviation
                    if (recognized.candidateList.Count > 5)
                        wb.is_abbr = true;
                    result.Add(wb);
                }                               
            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error($"Failed to recognize line ({lineid}):\n{ex.Message}");
            }
            finally
            {
                selectAndRecognizeSemaphoreSlim.Release();
            }
            return result;
        }

        public void ClearAllParsedText() {
            recognizedCanvas.Children.Clear();
            inkAnalyzer.ClearDataForAllStrokes();
            phrases.Clear();
        }

        private List<uint> GetUnrecognizedFromNode(IInkAnalysisNode wordNode, IInkAnalysisNode line)
        {
            // wordNode is assumed to be the first word of line
            List<uint> ids = new List<uint>();
            if (line == null)
                return ids;

            foreach (var sid in line.GetStrokeIds()) {
                var stroke = inkCan.InkPresenter.StrokeContainer.GetStrokeById(sid);
                if (stroke.BoundingRect.X + stroke.BoundingRect.Width <= wordNode.BoundingRect.X + wordNode.BoundingRect.Width &&
                    !wordNode.GetStrokeIds().Contains(sid))
                {
                    ids.Add(sid);
                }
                else if (stroke.BoundingRect.X >= wordNode.BoundingRect.X)
                    return ids;
            }
            return ids;
        }

        public void ShowAbbreviationAlter(WordBlockControl wbc, List<string> alter)
        {
            alternativeListView.ItemsSource = alter;
            alter_flyout.ShowAt(curLineResultPanel);
        }

        /// <summary>
        /// UNIMPLEMENTED
        /// </summary>
        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e)
        {
          
        }

        public async void annotateCurrentLineAndUpdateUI(int line_index = -1)
        {
            // do not annotate if there aren't available resources yet
            if (line_index == -1 || !phrases.ContainsKey(line_index))
                return;

            int lineNum = line_index;
            // after get annotation, recognized text has also changed
            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(phrases[lineNum].GetString());
            
            // Handles when annoResult has at least one element
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

                phrases[lineNum].UpdatePhenotypes(annoResult.Values.ToList());

                // don't update UI if user is already on another line
                if (showingResultOfLine != lineNum)
                    return;

                foreach (var pheno in phrases[lineNum].phenotypes)
                {
                    var temp = curLineCandidatePheno.Where(x => x == pheno).FirstOrDefault();
                    pheno.state = PhenotypeManager.getSharedPhenotypeManager().getStateByHpid(pheno.hpId);
                    if (temp == null)
                    {

                        Debug.WriteLine("temp null will add");
                        curLineCandidatePheno.Add(pheno);

                        Debug.WriteLine("temp null added");

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
                if (curLineCandidatePheno.Count > 0)
                {
                    curWordPhenoControlGrid.Visibility = Visibility.Visible;
                }

                if (phenoCtrlSlide.Y == 0)
                {
                    ((DoubleAnimation)curWordPhenoAnimation.Children[0]).By = -47;
                    curWordPhenoAnimation.Begin();
                }


                
            }

            // Handles when annoResult has no elements
            else
            {
                curWordPhenoControlGrid.Visibility = Visibility.Collapsed;
                phenoCtrlSlide.Y = 0;
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
            }
        }

        public async void UpdateAnnotationAfterErase(int lineNum)
        {
            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(phrases[lineNum].GetString());
            // Handles when annoResult has at least one element
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
                var removed = phrases[lineNum].phenotypes.Except(annoResult.Values.ToList()).Select(x => x.hpId).ToArray();
                var newAdded = annoResult.Values.ToList().Except(phrases[lineNum].phenotypes);
                Debug.WriteLine($"removed {removed.Count()} phenotypes, added {newAdded.Count()} phenotypes");
                foreach (var id in removed)
                {
                    // deletes the phenotype only if it has not been selected as Y/N
                    var p = phrases[lineNum].phenotypes.Where(x => x.hpId == id).FirstOrDefault();
                    if (p != null && p.state == -1)
                    {
                        phrases[lineNum].phenotypes.Remove(p);
                        PhenoMana.phenotypesCandidates.Remove(p);
                    }
                    if (lineNum == showingResultOfLine) {
                        curLineCandidatePheno.Remove(p);
                    }
                }
                phrases[lineNum].phenotypes.AddRange(newAdded.ToList());
                UpdateLayout();
            }

            // Handles when annoResult has no elements
            else
            {
                if (phrases[lineNum].phenotypes.Count > 0)
                {
                    foreach (var p in phrases[lineNum].phenotypes) {
                        if (p.state == -1)
                            PhenoMana.phenotypesCandidates.Remove(p);
                    }
                    phrases[lineNum].phenotypes.Clear();
                }
            }
        }

        public async Task ClearAndRecognizePage()
        {
            try {
                // clears all caches and re-initiate inkanalyzer
                recognizedCanvas.Children.Clear();
                phrases.Clear();
                inkAnalyzer = new InkAnalyzer();

                foreach (var s in inkCan.InkPresenter.StrokeContainer.GetStrokes()) {
                    inkAnalyzer.AddDataForStroke(s);
                    inkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
                }

                for (int i = 1; i < PAGE_HEIGHT / LINE_HEIGHT; i++) {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                    {
                        int lineNum = i;
                        List<WordBlockControl> result = await RecognizeLineWBC(lineNum);
                        if (result.Count > 0)
                        {
                            NotePhraseControl npc = new NotePhraseControl(lineNum, result);
                            Canvas.SetLeft(npc, 0);
                            Canvas.SetTop(npc, lineNum * LINE_HEIGHT);
                            phrases.Add(lineNum, npc);
                            recognizedCanvas.Children.Add(npc);
                        }
                    });
                }
                MainPage.Current.NotifyUser("Page cleared and re-recognized", NotifyType.StatusMessage, 1);
                MainPage.Current.LoadingPopup.IsOpen = false;
                return;
            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }

        }

        #endregion


        // ================================= INK RECOGNITION ==============================================                      

        public double GetLeftOfLine(int lineid)
        {
            var allStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in allStrokes)
                stroke.Selected = false;

            double lowerbound = lineid * LINE_HEIGHT;
            double upperbound = (lineid + 1) * LINE_HEIGHT;
            var strokeInLine = allStrokes.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 5) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 5) <= upperbound).OrderBy(x => x.BoundingRect.X).ToList();
            return strokeInLine.FirstOrDefault().BoundingRect.X;

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
        public static int getLineNumByRect(Rect rect)
        {
            return (int)((rect.Y + rect.Height / 2) / (NotePageControl.LINE_HEIGHT));
        }

        /// <summary>
        /// Handwrting recognition on all selected strokes and returns string results.
        /// </summary>
        private async Task<string> recognizeSelection()
        {
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                List<HWRRecognizedText> recognitionResults = new List<HWRRecognizedText>();
                // don't need server recognition here
                recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected,fromEHR:true);
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

            string str = text.Length == 0 ? await recognizeSelection() : text;

            if (!str.Equals(String.Empty) && !str.Equals(""))
            {
                recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
                selectionRectangle.Width = boundingRect.Width;
                selectionRectangle.Height = boundingRect.Height;

                if (ehrPage != null)
                {
                    Canvas.SetLeft(selectionRectangle, boundingRect.Left + 10);
                    Canvas.SetTop(selectionRectangle, boundingRect.Bottom);

                    if (!ehrPage.popupCanvas.Children.Contains(selectionRectangle))
                        ehrPage.popupCanvas.Children.Add(selectionRectangle);
                    if (!ehrPage.popupCanvas.Children.Contains(recognizedPhenoBriefPanel))
                        ehrPage.popupCanvas.Children.Add(recognizedPhenoBriefPanel);
                    selectionRectangle.Visibility = Visibility.Collapsed;

                }
                else
                {
                    Canvas.SetLeft(selectionRectangle, boundingRect.X);
                    Canvas.SetTop(selectionRectangle, boundingRect.Y);

                    selectionCanvas.Children.Add(recognizedPhenoBriefPanel);
                    selectionCanvas.Children.Add(selectionRectangle);
                }
                var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
                recognizedResultTextBlock.Text = str;
                searchPhenotypes(str);
                recogPhenoFlyout.ShowAt(selectionRectangle);
            }
        }

        public void ClearBriefPhenotypeEHR()
        {
            recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
            selectionRectangle.Visibility = Visibility.Collapsed;
        }

        public void HideCurLineStackPanel()
        {
            curLineWordsStackPanel.Children.Clear();
            curLineResultPanel.Visibility = Visibility.Collapsed;
        }

        public void ShowCurLineStackPanel()
        {
            if (curLineResultPanel.Visibility == Visibility.Collapsed)
                curLineResultPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Determines whether a set of strokes is a shape or just drawing and handles each case
        /// accordingly. 
        /// </summary>
        private async Task<int> RecognizeInkOperation()
        {
            var result = await inkOperationAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                // first need to clear all previous selections to filter out strokes that don't want to be deleted
                foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                // Gets all strokes from inkoperationanalyzer
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                {
                    // user has drawn a square/rectangle for adding an add-in
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                    {
                        foreach (var dstroke in drawNode.GetStrokeIds())
                        {
                            var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                            // adds a new add-in control 
                            string name = FileManager.getSharedFileManager().CreateUniqueName();
                            NewAddinControl(name, false, left: stroke.BoundingRect.X, top: stroke.BoundingRect.Y,
                                            widthOrigin: stroke.BoundingRect.Width, heightOrigin: stroke.BoundingRect.Height);
                            stroke.Selected = true;
                        }
                        // dispose the strokes as don't need them anymore
                        inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
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
            // inkoperation has no update, does nothing
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
        /// Draw a polygon on the recognitionCanvas.
        /// </summary>
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
        }

        /// <summary>
        /// extract text from a paragraph perserving line breaks
        /// </summary>
        /// <remarks>
        /// The paragraph.RecognizedText property also returns the text,
        /// but manually walking through the lines allows us to preserve
        /// line breaks.
        /// </remarks>
        private string ExtractTextFromParagraph(InkAnalysisParagraph paragraph)
        {   
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

        #region SEARCH FOR HIT STROKES/INKNODES
        private InkAnalysisLine FindHitLine(Point pt)
        {
            // Find line by hitting position
            var lines = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);

            foreach (var line in lines)
            {

                // To support ink written with angle, RotatedBoundingRect should be used in hit testing.
                var xFrom = line.BoundingRect.X;
                var xTo = xFrom + line.BoundingRect.Width;
                var yFrom = line.BoundingRect.Y;
                var yTo = yFrom + line.BoundingRect.Height;

                if (pt.X > xFrom && pt.X < xTo && pt.Y > yFrom && pt.Y < yTo)
                {
                    return ((InkAnalysisLine)line);
                }
            }
            return null;
        }

        private List<IInkAnalysisNode> FindHitWordsInLine(InkAnalysisLine line)
        {
            Rect lineRect = line.BoundingRect;
            var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
            var hitWords = words.Where(x => x.BoundingRect.Top >= lineRect.Top && x.BoundingRect.Bottom <= lineRect.Bottom && x.BoundingRect.X >= lineRect.X && x.BoundingRect.X + x.BoundingRect.Width <= lineRect.X + lineRect.Width).ToList();
            return hitWords;
        }

        /// <summary>
        /// Find word by hitting position
        /// </summary>
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

        /// <summary>
        /// Find paragraph by hitting position
        /// </summary>
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

        private List<InkStroke> FindAllStrokesInLine(int lineNum)
        {
            double lowerbound = lineNum * LINE_HEIGHT;
            double upperbound = (lineNum + 1) * LINE_HEIGHT;
            var allStrokes = inkCan.InkPresenter.StrokeContainer.GetStrokes();
            var strokeInLine = allStrokes.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 5) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 5) <= upperbound).OrderBy(x => x.BoundingRect.X).ToList();
            return strokeInLine;
        }

        private List<IInkAnalysisNode> FindInkNodeInLine(int lineNum,NodeType type)
        {
            double lowerbound = lineNum * LINE_HEIGHT;
            double upperbound = (lineNum + 1) * LINE_HEIGHT;
            List<IInkAnalysisNode> all = null;
            if (type == NodeType.InkWord)
                all = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord).ToList();
            else if (type == NodeType.InkLine)
                all = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line).ToList();
            else if (type == NodeType.Both) {
                all = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord).ToList();
                all.AddRange(inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line));
            }

            var InLine = all.Where(x => x.BoundingRect.Y + (x.BoundingRect.Height / 5) >= lowerbound && x.BoundingRect.Y + (x.BoundingRect.Height / 5) <= upperbound).OrderBy(x => x.BoundingRect.X).ToList();
            return InLine;
        }
        #endregion

        // analyzing on opening file
        /// <summary>
        /// Called upon page creation/page switch, will re-analyze everything on the current page and
        /// change phenotype candidates accordingly.
        /// </summary>
        public async void initialAnalyze()
        {
            rootPage.NotifyUser($"Analyzing page {pageId}...", NotifyType.StatusMessage, 2);
            inkAnalyzer = new InkAnalyzer();
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            bool result = false;
            while (!result)
                result = await analyzeInk(lastStroke:null,serverFlag: true);//will be using server side HWR upon page load
        }

        public async void initialAnalyzeNoPhenotype()
        {
            inkAnalyzer = new InkAnalyzer();
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            await inkAnalyzer.AnalyzeAsync();
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
            UpdateLayout();
        }

        /// <summary>
        /// Search Phenotypes by a given string input.
        /// </summary>
        private async void searchPhenotypes(string str)
        {
            List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
            if (result != null && result.Count > 0)
            {
                foreach (var pp in result)
                {
                    pp.sourceType = SourceType.Notes;
                }

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
                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Collapsed;
                phenoProgressRing.Visibility = Visibility.Collapsed;
                recognizedPhenoListView.Visibility = Visibility.Collapsed;
                rootPage.NotifyUser("No phenotypes recognized by the selected strokes.", NotifyType.ErrorMessage, 2);
            }
        }
    }
}
