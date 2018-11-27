using PhenoPad.FileService;
using PhenoPad.HWRService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class EHRPageControl : UserControl
    {
        //By default sets the EHR template size to be US Letter size in pixels
        private double EHR_HEIGHT = 2200;
        private double EHR_WIDTH = 2300;
        private float LINE_HEIGHT = 50;
        private float MAX_WRITING;

        private bool leftLasso = false;

        public InkStroke curStroke;
        public List<uint> lastOperationStrokeIDs;

        InkAnalyzer inkOperationAnalyzer;
        InkAnalyzer inputInkAnalyzer;

        private int showAlterOfWord = -1;

        private int current_index;

        private string notebookid;
        private string pageid;

        DispatcherTimer autosaveDispatcherTimer; //For auto saves

        List<HWRRecognizedText> inputRecogResult;

        //======================================
        //           CONSTRUCTOR
        //======================================

        public EHRPageControl(StorageFile file,string noteid, string pageid)
        {
            this.InitializeComponent();
            //setting the text grid line format
            {
                RemovePasteFormat();
            }

            this.notebookid = noteid;
            this.pageid = pageid;

            MAX_WRITING = LINE_HEIGHT;

            inkCanvas.AddHandler(UIElement.TappedEvent, new TappedEventHandler(OnElementTapped), true);

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += inkCanvas_StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += inkCanvas_StrokeInput_StrokeEnded;
            inkCanvas.InkPresenter.StrokesCollected += inkCanvas_InkPresenter_StrokesCollectedAsync;
            inkCanvas.InkPresenter.StrokesErased += inkCanvas_StrokeErased;

            inputInkCanvas.InkPresenter.StrokesCollected += inputInkCanvas_StrokesCollected;

            EHRTextBox.Paste += RemovePasteFormat;
            EHRTextBox.IsDoubleTapEnabled = true;
            this.DoubleTapped += CopyTextToClipboard;

            autosaveDispatcherTimer = new DispatcherTimer();
            autosaveDispatcherTimer.Tick += TriggerAutoSave;
            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(1); //setting stroke auto save interval to be 1 sec



            inkOperationAnalyzer = new InkAnalyzer();
            inputInkAnalyzer = new InkAnalyzer();

            inputRecogResult = new List<HWRRecognizedText>();

            lastOperationStrokeIDs = new List<uint>();

            this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }



        //=======================================
        //          INITIALIZATION
        //=======================================

        /// <summary>
        /// Draws background dashed lines to background canvas 
        /// </summary>
        public void DrawBackgroundLines()
        {
            for (int i = 1; i <= backgroundCanvas.RenderSize.Height / LINE_HEIGHT; ++i)
            {
                var line = new Line()
                {
                    Stroke = new SolidColorBrush(Windows.UI.Colors.LightGray),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                    X1 = 0,
                    X2 = backgroundCanvas.RenderSize.Width,
                    Y1 = i * LINE_HEIGHT,
                    Y2 = i * LINE_HEIGHT
                };
                backgroundCanvas.Children.Add(line);
            }
        }

        /// <summary>
        /// Triggered everytime the specific EHRPageControl is loaded
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines
            DrawBackgroundLines();
        }

        /// <summary>
        /// Takes the EHR text file and converts content onto template
        /// </summary>
        public async void SetUpEHRFile(StorageFile file)
        {
            if (file == null)
            {
                EHRTextBox.Document.SetText(TextSetOptions.None, "");
                return;
            }

            try
            {
                string text = await FileIO.ReadTextAsync(file);
                text = text.TrimEnd();
                EHRTextBox.Document.SetText(TextSetOptions.None, text);

            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
            }
        }

        /// <summary>
        /// Trigger parent NotePageControl's auto-save to save all progress
        /// </summary>
        private async void TriggerAutoSave(object sender, object e)
        {
            autosaveDispatcherTimer.Stop();
            await MainPage.Current.curPage.SaveToDisk();
        }


        /// <summary>
        /// When pasting from other sources, auto removes the rtf format to match 
        /// </summary>
        private void RemovePasteFormat(object sender = null, TextControlPasteEventArgs e = null)
        {
            var format = EHRTextBox.Document.GetDefaultParagraphFormat();
            EHRTextBox.FontSize = 32;
            //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
            format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            EHRTextBox.Document.SetDefaultParagraphFormat(format);
        }

        /// <summary>
        /// Dismiss all pop-ups when tapping on canvas
        /// </summary>
        private void OnElementTapped(object sender, TappedRoutedEventArgs e)
        {
            inputgrid.Visibility = Visibility.Collapsed;
            ClearOperationStrokes();
        }

        /// <summary>
        /// Displays input canvas for inserting into EHR
        /// </summary>
        /// <param name="p"></param>
        private void ShowInputPanel(Point p)
        {
            Canvas.SetLeft(inputgrid, p.X - 10);
            Canvas.SetTop(inputgrid, p.Y + LINE_HEIGHT);
            inputgrid.Visibility = Visibility.Visible;
        }


        // ====================================
        //     EHR OPERATIONS
        // ====================================


        /// <summary>
        /// Inserts current recognized text into EHR text edit box
        /// </summary>
        private void InsertToEHR(object sender, RoutedEventArgs e) {
            string text = "{";
            foreach (HWRRecognizedText word in inputRecogResult) {
                text += word.selectedCandidate + " ";
            }
            text = text.Substring(0, text.Length - 1)+"}";
            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);

            if (current_index == all_text.Length - 1)
            { //inserting at end of text
                all_text = all_text.Substring(0, all_text.Length).TrimEnd();
                EHRTextBox.Document.SetText(TextSetOptions.None, all_text + text);
            }
            else {
                string first_half = all_text.Substring(0, current_index);
                if (current_index >= 1)
                    first_half = first_half.Substring(0, first_half.LastIndexOf(' ') + 1);
                string rest = all_text.Substring(current_index);

                if (rest.IndexOf(' ') > 0)
                    rest = rest.Substring(rest.IndexOf(' '));

                EHRTextBox.Document.SetText(TextSetOptions.None, first_half + text + rest);
            }


            //clears the previous input
            inputgrid.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            ClearOperationStrokes();
        }

        /// <summary>
        /// Inserts a new line in EHR text
        /// </summary>
        private void InsertNewLineToEHR(object sender, RoutedEventArgs e) {
            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);

            if (current_index == all_text.Length - 1)
            { //inserting at end of text
                all_text = all_text.Substring(0, all_text.Length).TrimEnd();
                EHRTextBox.Document.SetText(TextSetOptions.None, all_text + "\r\n");
            }
            else
            {
                string first_half = all_text.Substring(0, current_index);
                if (current_index >= 1)
                    first_half = first_half.Substring(0, first_half.LastIndexOf(' ') + 1);
                string rest = all_text.Substring(current_index - 1);

                if (rest.IndexOf(' ') > 0)
                    rest = rest.Substring(rest.IndexOf(' '));

                EHRTextBox.Document.SetText(TextSetOptions.None, first_half + "\r\n" + rest);
            }


            //clears the previous input
            inputgrid.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            ClearOperationStrokes();
            autosaveDispatcherTimer.Start();
        }

        /// <summary>
        /// Deletes all texts within the [start,end] range in the EHR text box.
        /// </summary>
        private void DeleteTextInRange(int start, int end)
        {
            ClearOperationStrokes();
            if (start < 0 || end < 0|| end < start || start == end)
                return;
            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);
            string first_half = all_text.Substring(0, start);
            first_half = first_half.Substring(0, first_half.LastIndexOf(' ') + 1);
            string rest = all_text.Substring(end);
            if (rest.IndexOf(' ') > 0)
                rest = rest.Substring(rest.IndexOf(' '));
            //temporary inserting a star mark indicating some contents were deleted
            EHRTextBox.Document.SetText(TextSetOptions.None, first_half + " * " + rest);
            autosaveDispatcherTimer.Start();

        }

        /// <summary>
        /// Trims the extra newlines in current EHR text and copies it to windows clipboard
        /// </summary>
        private void CopyTextToClipboard(object sender, DoubleTappedRoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage();
            // copy 
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            string text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out text);
            text = text.TrimEnd();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            MainPage.Current.NotifyUser("Copied EHR Text to Clipboard", NotifyType.StatusMessage, 1);
        }

        /// <summary>
        /// Deletes all processed operation strokes from inkCanvas
        /// </summary>
        private void ClearOperationStrokes() {
            foreach (uint sid in lastOperationStrokeIDs)
            {
                InkStroke s = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid);
                if ( s != null)
                    s.Selected = true;
            }
            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            lastOperationStrokeIDs.Clear();
        }


        /// <summary>
        /// Returns the current non-formatted text in EHR text box as string
        /// </summary>
        /// <returns></returns>
        public string getEHRText() {
            string body;
            EHRTextBox.Document.GetText(TextGetOptions.None, out body);
            body = body.TrimEnd();
            return body;
        }


        //=======================================
        //      ANALYZING INK STROKES
        //=======================================

        private void inkCanvas_StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            inputgrid.Visibility = Visibility.Collapsed;
            autosaveDispatcherTimer.Stop();

        }

        private void inkCanvas_StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
        }

        private void inkCanvas_StrokeErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            inputgrid.Visibility = Visibility.Collapsed;
            autosaveDispatcherTimer.Start();
        }

        private async void inkCanvas_InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {//processing strokes inputs
                foreach (var s in args.Strokes)
                {
                    //Process strokes that excess maximum height for recognition
                    if (s.BoundingRect.Height > MAX_WRITING)
                    {
                        Debug.WriteLine("added stroke to operation");
                        inkOperationAnalyzer.AddDataForStroke(s);
                        try
                        {
                            await RecognizeInkOperation();
                        }
                        catch (Exception e)
                        {
                            LogService.MetroLogger.getSharedLogger().Error($"{e}|{e.Message}");
                        }
                    }
                }
            }
            else
            {
                //for future: left lasso bounding check with word coordinate and then selects
            }
        }

        private async void inputInkCanvas_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            foreach (var s in args.Strokes)
            {
                //Instantly analyze ink inputs
                inputInkAnalyzer.AddDataForStroke(s);
                inputInkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
                //marking the current stroke for later server recognition
                curStroke = s;
                //here we need instant call to analyze ink for the specified line input
                await analyzeInk(s);
            }

        }

        /// <summary>
        /// Gets the position of first occuring space index given a point posiiton
        /// </summary>
        private int GetSpaceIndexFromEHR(int start)
        {
            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);
            string rest = all_text.Substring(start - 1);//move index 1 forward for error torlerance
            int space_index = rest.IndexOf(' ');
            return space_index;
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
                Debug.WriteLine($"in");

                //first need to clear all previous selections to filter out strokes that don't want to be deleted
                //ClearSelectionAsync();
                foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                //Gets all strokes from inkoperationanalyzer
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                {
                    Rect bounding = drawNode.BoundingRect;

                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                    {
                        foreach (var dstroke in drawNode.GetStrokeIds())
                        {
                            var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                            //adds a new add-in control 
                            string name = FileManager.getSharedFileManager().CreateUniqueName();
                            //adding extra 100 pixel to bounding Y to cope the padding
                            MainPage.Current.curPage.NewAddinControl(name, false, left: stroke.BoundingRect.X, top: stroke.BoundingRect.Y + 100,
                                            widthOrigin: stroke.BoundingRect.Width, heightOrigin: stroke.BoundingRect.Height, ehr: this);
                            stroke.Selected = true;
                        }
                        //dispose the strokes as don't need them anymore
                        inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
                        inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                    }

                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Drawing) {
                        Debug.WriteLine("is drawing");
                        if (bounding.Width < 10)
                        {// detected operation for inserting
                            Debug.WriteLine($"detected insert at {bounding.X},{bounding.Y}====");
                            Point pos = new Point(bounding.X, bounding.Y + (bounding.Height / 2.0));
                            var range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                            // the scribble does not collide with any text, just ignore
                            if (range.StartPosition > 1500)
                            {
                                inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
                                return 1;
                            }
                            current_index = range.StartPosition;
                            if (pos.X > 300)
                                pos.X -= 300;
                            ShowInputPanel(pos);
                        }
                    }

                    else
                    {
                        Debug.WriteLine(drawNode.DrawingKind);
                    }

                    //adds recognized operation strokes into list to be cleared later
                    foreach (uint sid in drawNode.GetStrokeIds())
                    {
                        lastOperationStrokeIDs.Add(sid);
                    }


                }
                //Used to detect scribbles for deleting
                var others = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.WritingRegion);
                foreach (var node in others) {
                    Rect bounding = node.BoundingRect;
                    Debug.WriteLine($"detected delete at {bounding.X},{bounding.Y}====");
                    Point start = new Point(bounding.X + 10, bounding.Y + (bounding.Height / 2.0));
                    Point end = new Point(bounding.X + bounding.Width - 10, bounding.Y + (bounding.Height / 2.0));
                    var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
                    var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);
                    Debug.WriteLine($"start={range1.StartPosition},end={range2.StartPosition}, text={getEHRText().Length}");
                    // the scribble does not collide with any text, just ignore
                    if (range1.StartPosition == range2.StartPosition || range1.StartPosition == getEHRText().Length) {
                        inkOperationAnalyzer.RemoveDataForStrokes(node.GetStrokeIds());
                        return 1;
                    }
                    foreach (uint sid in node.GetStrokeIds())
                    {
                        lastOperationStrokeIDs.Add(sid);
                    }

                    DeleteTextInRange(range1.StartPosition, range2.StartPosition);
                }

                // delete all strokes that are too large, like drawings
                foreach (var sid in inkOperationAnalyzer.AnalysisRoot.GetStrokeIds())
                {
                    inkOperationAnalyzer.RemoveDataForStroke(sid);
                }
                //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                return 1;
            }
            return -1;
        }


        /// <summary>
        ///  Analyze ink strokes contained in inkAnalyzer and add phenotype candidates
        ///  from fetching API
        /// </summary>
        private async Task<bool> analyzeInk(InkStroke lastStroke = null, bool serverFlag = false)
        {
            //if (lastStroke == null) { 
            //    PhenoMana.phenotypesCandidates.Clear();
            //}
            Debug.WriteLine("analyzing...");
            if (inputInkAnalyzer.IsAnalyzing)
            {
                // inkAnalyzer is being used 
                // try again after some time by dispatcherTimer 
                return false;
            }
            // analyze 
            var result = await inputInkAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //int line = linesToAnnotate.Dequeue();
                IReadOnlyList<IInkAnalysisNode> lineNodes = inputInkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);

                foreach (InkAnalysisLine line in lineNodes)
                {
                    // only focus on current line
                    if (lastStroke != null)
                    {
                        // current line
                        if (line.GetStrokeIds().Contains(lastStroke.Id))
                        {
                            // set up for current line
                            recognizeAndSetUpUIForLine(line, serverFlag);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// set up for current line, i.e. hwr, show recognition results and show recognized phenotypes
        /// </summary>
        private async void recognizeAndSetUpUIForLine(InkAnalysisLine line, bool indetails = false, bool serverRecog = false)
        {
            if (line == null)
                return;
            curLineWordsStackPanel.Children.Clear();
            HWRManager.getSharedHWRManager().clearCache();

            // hwr
            List<HWRRecognizedText> newResult = await RecognizeLine(line.Id, serverRecog);
            inputRecogResult = newResult == null ? inputRecogResult : newResult;

            // HWR result UI
            setUpCurrentLineResultUI(line);
            // annotation and UI
            //annotateCurrentLineAndUpdateUI(line);


        }

        /// <summary>
        /// Adds recognized words into recognized UI line
        /// </summary>
        private void setUpCurrentLineResultUI(InkAnalysisLine line)
        {
            Dictionary<string, List<string>> dict = HWRManager.getSharedHWRManager().getDictionary();
            List<HWRRecognizedText> newResult = inputRecogResult;
            curLineWordsStackPanel.Children.Clear();

            for (int index = 0; index < newResult.Count; index++)
            {
                string word = newResult[index].selectedCandidate;
                Debug.WriteLine(word);
                TextBlock tb = new TextBlock();
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.FontSize = 16;
                //for detecting abbreviations
                if (index != 0 && dict.ContainsKey(newResult[index - 1].selectedCandidate.ToLower()) && 
                    dict[newResult[index - 1].selectedCandidate.ToLower()].Contains(word))
                {
                    tb.Text = $"({word})";
                    tb.Foreground = new SolidColorBrush(Colors.DarkOrange);
                }
                else
                {
                    tb.Text = word;
                }

                curLineWordsStackPanel.Children.Add(tb);
                //Binding event listener to each text block
                tb.Tapped += ((object sender, TappedRoutedEventArgs e) => {
                    int wi = curLineWordsStackPanel.Children.IndexOf((TextBlock)sender);
                    var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
                    showAlterOfWord = wi;
                    alternativeListView.ItemsSource = inputRecogResult[wi].candidateList;
                    alterFlyout.ShowAt((FrameworkElement)sender);
                });
            }

            //loading.Visibility = Visibility.Collapsed;
            curLineWordsStackPanel.Visibility = Visibility.Visible;
            //curLineResultPanel.Visibility = Visibility.Visible;
            //Canvas.SetLeft(curLineResultPanel, line.BoundingRect.Left);
            //int lineNum = getLineNumByRect(line.BoundingRect);
            //Canvas.SetTop(curLineResultPanel, (lineNum - 1) * LINE_HEIGHT);
        }

        /// <summary>
        /// Triggered when user changes a word's alternative in input panel
        /// </summary>
        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e) {
            var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
            alterFlyout.Hide();
            var citem = (string)e.ClickedItem;
            int ind = alternativeListView.Items.IndexOf(citem);
            inputRecogResult[showAlterOfWord].selectedIndex = ind;
            inputRecogResult[showAlterOfWord].selectedCandidate = citem;
            TextBlock tb = (TextBlock)curLineWordsStackPanel.Children.ElementAt(showAlterOfWord);
            tb.Text = citem;
        }

        /// <summary>
        /// Recognize the line strokes in the input canvas
        /// </summary>
        private async Task<List<HWRRecognizedText>> RecognizeLine(uint lineid, bool serverFlag)
        {
            try
            {
                foreach (var stroke in inputInkCanvas.InkPresenter.StrokeContainer.GetStrokes()) {
                    stroke.Selected = true;
                }

                //recognize selection
                List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().
                    OnRecognizeAsync(inputInkCanvas.InkPresenter.StrokeContainer,
                    InkRecognitionTarget.Selected, serverFlag);
                return recognitionResults;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to recognize line ({lineid}): {ex.Message}");
            }
            return null;
        }


    }
}
