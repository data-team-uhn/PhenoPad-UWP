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
using Windows.Devices.Input;
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
using WobbrockLib;
using PhenoPad.Gestures;
using System.Text.RegularExpressions;
using DCSoft.RTF;

//using System.Drawing;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class EHRPageControl : UserControl
    {
        #region Class Attributes
        //By default sets the EHR template size to be US Letter size in pixels
        private double EHR_HEIGHT = 2200;
        private double EHR_WIDTH = 2300;
        public float LINE_HEIGHT = 50;
        private Recognizer GESTURE_RECOGNIZER;
        public string GESTURE_PATH = @"C:\Users\helen\AppData\Local\Packages\16bc6b12-daff-4104-a251-1fa502edec02_qfxtr3e52dkcc\LocalState\Gestures";

        private Polyline lasso;

        private float UNPROCESSED_OPACITY = 0.5f;
        private int UNPROCESSED_THICKNESS = 35;
        private SolidColorBrush UNPROCESSED_COLOR = new SolidColorBrush(Colors.Yellow);
        private SolidColorBrush INSERT_MARKUP = new SolidColorBrush(Color.FromArgb(0,66, 134, 244));

        private Color INSERTED_COLOR = (Color)Application.Current.Resources["WORD_DARK_COLOR"];//Dark Blue
        private Color HIGHLIGHT_COLOR = Color.FromArgb(0, 255, 248, 173);//Light Yellow

        private bool isHighlighting;
        private bool isErasing;
        private string highlightState = "Highlight";
        private string deleteState = "Delete";

        //For keeping edited records, all List<int> format = <start_index, phrase_length>
        public List<List<int>> inserts;
        public List<List<int>> highlights;
        public List<List<int>> deletes;

        public List<AddInControl> comments;

        private Stack<(string, int, int)> gestureStack;

        public InkStroke curStroke;
        public List<uint> lastOperationStrokeIDs;

        InkAnalyzer inkOperationAnalyzer;
        InkAnalyzer inputInkAnalyzer;
        List<HWRRecognizedText> inputRecogResult;
        DispatcherTimer autosaveDispatcherTimer; //For auto saves

        private int showAlterOfWord = -1;
        private int current_index;//the most current pen position to text
        private (int, int) cur_selected;//most current selected phrase

        public string notebookid;
        public string pageid;

        #endregion

        #region Constructor

        public EHRPageControl(StorageFile file, string noteid, string pageid)
        {
            this.InitializeComponent();
            //setting the text grid line format
            {
                RemovePasteFormat();
            }

            this.notebookid = noteid;
            this.pageid = pageid;

            GESTURE_RECOGNIZER = new Recognizer();

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen;

            inkCanvas.Tapped += OnElementTapped;
            annotations.Tapped += OnElementTapped;

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += inkCanvas_StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += inkCanvas_StrokeInput_StrokeEnded;
            inkCanvas.InkPresenter.StrokesCollected += inkCanvas_InkPresenter_StrokesCollectedAsync;
            inkCanvas.InkPresenter.StrokesErased += inkCanvas_StrokeErased;

            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;

            var drawingAttributes = inkCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            drawingAttributes.DrawAsHighlighter = true;
            drawingAttributes.Color = Colors.OrangeRed;
            drawingAttributes.Size = new Size(5, 5);
            inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            inkCanvas.UpdateLayout();

            annotations.InkPresenter.StrokesCollected += annotations_StrokesCollected;


            inputInkCanvas.InkPresenter.StrokesCollected += inputInkCanvas_StrokesCollected;

            EHRTextBox.Paste += RemovePasteFormat;
            EHRTextBox.IsDoubleTapEnabled = true;
            this.DoubleTapped += ShowCPMenu;

            EHRTextBox.Document.ApplyDisplayUpdates();

            autosaveDispatcherTimer = new DispatcherTimer();
            autosaveDispatcherTimer.Tick += TriggerAutoSave;
            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(1); //setting stroke auto save interval to be 1 sec

            inserts = new List<List<int>>();
            highlights = new List<List<int>>();
            deletes = new List<List<int>>();
            comments = new List<AddInControl>();
            gestureStack = new Stack<(string, int, int)>();

            inkOperationAnalyzer = new InkAnalyzer();
            inputInkAnalyzer = new InkAnalyzer();

            inputRecogResult = new List<HWRRecognizedText>();

            lastOperationStrokeIDs = new List<uint>();

            this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }

        #endregion

        #region Page Set-up / UI-displays

        public void DrawBackgroundLines()
        {/// Draws background dashed lines to background canvas 
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {/// Triggered everytime the specific EHRPageControl is loaded
            
            // Draw background lines
            DrawBackgroundLines();
        }

        public async void SetUpEHRFile(StorageFile file)
        {/// Takes the EHR text file and converts content onto RichEditBox
            await GESTURE_RECOGNIZER.LoadGestureFromPath();

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
                EHRFormats formats = await FileManager.getSharedFileManager().LoadEHRFormats(notebookid, pageid);
                this.inserts = formats.inserts;
                this.highlights = formats.highlights;
                this.deletes = formats.deletes;
                RefreshTextStyle();

            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
            }
        }
       
        private void ShowInputPanel(Point p)
        {/// <summary>Displays input canvas for inserting into EHR</summary>

            double newX = p.X - 20;
            double newY = ((int)(p.Y / LINE_HEIGHT) + 2) * LINE_HEIGHT - 20;
            if (p.X > 600)
                newX -= 400;
            Canvas.SetLeft(inputgrid, newX);
            Canvas.SetTop(inputgrid, newY + 50);
            Canvas.SetLeft(inputMarkup, p.X - 5);
            Canvas.SetTop(inputMarkup, newY);

            inputgrid.Visibility = Visibility.Visible;
            inputMarkup.Visibility = Visibility.Visible;
        }

        private void ShowCPMenu(object sender, DoubleTappedRoutedEventArgs e)
        {
            Canvas.SetLeft(cpMenu, e.GetPosition(popupCanvas).X - cpMenu.ActualWidth - 10);
            Canvas.SetTop(cpMenu, e.GetPosition(popupCanvas).Y - cpMenu.ActualHeight/2);
            cpMenu.Visibility = Visibility.Visible;
        }

        private void ShowCommentCanvas(object sender, RoutedEventArgs e) {

            SelectionMenu.Visibility = Visibility.Collapsed;
            HighlightTextInRange(cur_selected.Item1, cur_selected.Item2);
            var range = EHRTextBox.Document.GetRange(cur_selected.Item1, cur_selected.Item1);
            Point pos;
            range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Bottom, PointOptions.ClientCoordinates, out pos);
            double newY = (Math.Ceiling(pos.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT;
            AddInControl comment = MainPage.Current.curPage.NewEHRCommentControl(pos.X + 10, newY + 110); //adding padding offsets to march lining
            this.comments.Add(comment);
        }

        public void RemoveComment(AddInControl comment) {
            comments.Remove(comment);
        }

        public void SlideCommentsToSide()
        {
            foreach (AddInControl comment in comments)
            {
                comment.SlideToRight(1200 - comment.canvasLeft + 20, -1 * LINE_HEIGHT);
            }
        }

        #endregion

        #region Gesture Operations

        private void OnElementTapped(object sender, TappedRoutedEventArgs e)
        {/// Dismiss all pop-ups when tapping on canvas
            inputgrid.Visibility = Visibility.Collapsed;
            inputMarkup.Visibility = Visibility.Collapsed;
            cpMenu.Visibility = Visibility.Collapsed;
            RefreshTextStyle();
            SelectionMenu.Visibility = Visibility.Collapsed;
            ClearOperationStrokes();
            if (popupCanvas.Children.Contains(lasso))
            {
                popupCanvas.Children.Remove(lasso);
            }
            SlideCommentsToSide();
        }

        private async void TriggerAutoSave(object sender = null, object e = null)
        {/// Trigger parent NotePageControl's auto-save to save all progress
            autosaveDispatcherTimer.Stop();
            await MainPage.Current.curPage.SaveToDisk();
        }

        private void RemovePasteFormat(object sender = null, TextControlPasteEventArgs e = null)
        {/// When pasting from other sources, auto removes the rtf format to match 

            var format = EHRTextBox.Document.GetDefaultParagraphFormat();
            EHRTextBox.FontSize = 32;
            EHRPreview.FontSize = 32;
            //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
            format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            EHRTextBox.Document.SetDefaultParagraphFormat(format);
            EHRPreview.Document.SetDefaultParagraphFormat(format);
        }

        #endregion

        #region EHR Text Operations

        private void RefreshTextStyle()
        {/// Reapplies updated text styles

            string text = getEHRText();
            EHRTextBox.Document.SetText(TextSetOptions.None, text);

            foreach (List<int> r in inserts)
            {
                var range = EHRTextBox.Document.GetRange(r[0], r[0] + r[1] - 1);
                range.CharacterFormat.ForegroundColor = INSERTED_COLOR;
            }
            foreach (List<int> r in highlights)
            {
                var range = EHRTextBox.Document.GetRange(r[0], r[0] + r[1] - 1);
                range.CharacterFormat.BackgroundColor = HIGHLIGHT_COLOR;
            }
            foreach (List<int> r in deletes)
            {
                var range = EHRTextBox.Document.GetRange(r[0], r[0] + r[1] - 1);
                range.CharacterFormat.ForegroundColor = Colors.LightGray;
                range.CharacterFormat.Strikethrough = FormatEffect.On;
            }
            EHRTextBox.UpdateLayout();
        }

        private void UpdateRecordMerge()
        {/// checks each record list interval and merge those that intersect eachother

            inserts = inserts.OrderBy(lst => lst[0]).ToList();
            highlights = highlights.OrderBy(lst => lst[0]).ToList();
            deletes = deletes.OrderBy(lst => lst[0]).ToList();

            //Debug.WriteLine($"Sarting merge in insert, original length = {inserts.Count}");
            for (int i = 0; i < inserts.Count; i++)
            {
                //Debug.WriteLine($"starting index = {inserts[i][0]}, length = {inserts[i][1]}");
                for (int j = i + 1; j < inserts.Count; j++)
                {
                    if (inserts[i][0] + inserts[i][1] >= inserts[j][0])
                    {
                        //Debug.WriteLine("merging");
                        int offset = (inserts[i][0] + inserts[i][1]) - inserts[j][0];
                        inserts[i][1] = inserts[i][1] + inserts[j][1] - offset;
                        inserts.Remove(inserts[j]);
                    }
                }
            }
            Debug.WriteLine($"Insert After merging = {inserts.Count}");

            for (int i = 0; i < highlights.Count; i++)
            {
                for (int j = i + 1; j < highlights.Count; j++)
                {
                    if (highlights[i][0] + highlights[i][1] >= highlights[j][0])
                    {
                        int offset = (highlights[i][0] + highlights[i][1]) - highlights[j][0];
                        highlights[i][1] = highlights[i][1] + highlights[j][1] - offset;

                        highlights.Remove(highlights[j]);
                    }
                }
            }
            Debug.WriteLine($"highlight After merging = {highlights.Count}");

            for (int i = 0; i < deletes.Count; i++)
            {
                Debug.WriteLine($"i : starting index = {deletes[i][0]}, length = {deletes[i][1]}");
                for (int j = i + 1; j < deletes.Count; j++)
                {
                    Debug.WriteLine($"j : starting index = {deletes[j][0]}, length = {deletes[j][1]}");
                    if (deletes[i][0] + deletes[i][1] >= deletes[j][0])
                    {
                        //Debug.WriteLine("merging");
                        int offset = (deletes[i][0] + deletes[i][1]) - deletes[j][0];
                        deletes[i][1] = deletes[i][1] + deletes[j][1] - offset;
                        deletes.Remove(deletes[j]);
                    }
                }
            }
            Debug.WriteLine($"deletes After merging = {deletes.Count}");
            RefreshTextStyle();
        }

        private void UpdateRecordListDelete(int start, int length)
        {//updates index ranges based on deleted phrases, currently not in use 12/07/2018
            //updating indexs in inserts
            for (int i = 0; i < inserts.Count; i++)
            {
                int end = start + length;
                int list_start = inserts[i][0];
                int list_end = inserts[i][0] + inserts[i][1]; //because second element of record sub_list indicates word length

                //Case 1: list element range fully surrounds [start,end]
                if (start > list_start && end < list_end)
                {
                    Debug.WriteLine("Delete (list element range fully surrounds [start,end])");
                    inserts[i][1] -= length;
                }
                //Case 2: list element range is subset of [start,end]
                else if ((start >= list_start && end <= list_end) || (list_start > start && list_end < end))
                {
                    Debug.WriteLine("Delete (list element range is subset of [start,end])");
                    if (length == inserts[i][1])
                        inserts.RemoveAt(i);
                    else
                        inserts[i][1] -= length;
                }
                //Case 3: partial intersections head
                else if (start < list_start && end < list_end && end > list_start)
                {
                    Debug.WriteLine("Delete (start < list start, end < list end)");
                    inserts[i][1] = inserts[i][1] + list_start - end;
                    inserts[i][0] = start;
                }
                //Case 4: partial intersections tail
                else if (start > list_start && end > list_end && start < list_end)
                {
                    Debug.WriteLine("Delete (start > list start, end > list end)");
                    inserts[i][1] -= list_end - start;
                }
                //Case 5: Before phrase
                else if (start < list_start && end <= list_start) {
                    inserts[i][0] -= length;
                }
            }

            //updating indexs in highlights
            for (int i = 0; i < highlights.Count; i++)
            {
                int end = start + length;
                int list_start = highlights[i][0];
                int list_end = highlights[i][0] + highlights[i][1]; //because second element of record sub_list indicates word length

                //Case 1: list element range fully surrounds [start,end]
                if (start > list_start && end < list_end)
                {
                    Debug.WriteLine("Delete (list element range fully surrounds [start,end])");
                    highlights[i][1] -= length;
                }
                //Case 2: list element range is subset of [start,end]
                else if ((start >= list_start && end <= list_end) || (list_start > start && list_end < end))
                {
                    Debug.WriteLine("Delete (list element range is subset of [start,end])");
                    if (length == highlights[i][1])
                        highlights.RemoveAt(i);
                    else
                        highlights[i][1] -= length;
                }
                //Case 3: partial intersections head
                else if (start < list_start && end < list_end && end > list_start)
                {
                    Debug.WriteLine("Delete (start < list start, end < list end)");
                    highlights[i][1] = highlights[i][1] + list_start - end;
                    highlights[i][0] = start;
                }
                //Case 4: partial intersections tail
                else if (start > list_start && end > list_end && start < list_end)
                {
                    Debug.WriteLine("Delete (start > list start, end > list end)");
                    highlights[i][1] -= list_end - start;
                }
                //Case 5: Before phrase
                else if (start < list_start && end <= list_start)
                {
                    highlights[i][0] -= length;
                }
            }



        }

        private void UpdateRecordListInsert(int start = -1, int length = 0)
        {/// Updates and shifts saved record indexes accordingly

            for (int i = 0; i < inserts.Count; i++)
            {
                Debug.WriteLine($"current insert record starting: {inserts[i][0]}");
                //Case1:New insert index is before previously inserted words
                if (inserts[i][0] > start && length > 0 && start > -1)
                {
                    Debug.WriteLine("insert is before inserted");
                    inserts[i][0] += length;
                }

                //Case2: Insert into inserted phrase
                else if (inserts[i][0] < start && start < inserts[i][0] + inserts[i][1])
                {
                    inserts[i][1] += length; //just extends this phrase by new word length + space char
                    Debug.WriteLine("insert is within inserted");
                }
                //for handling new inserts right before an inserted element
                else if (start == inserts[i][0])
                {
                    Debug.WriteLine("insert is right before inserted");
                    inserts[i][0] += length;
                }
            }
            //Add newly inserted range to record
            if (start != -1 && length != 0) {
                inserts.Add(new List<int>() { current_index + 1, length });
            }

            for (int i = 0; i < highlights.Count; i++) {
                //Case 1: insert index in middle of highlight section
                if (highlights[i][0] < start && start + length < highlights[i][0] + highlights[i][1])
                {
                    highlights[i][1] += length;
                }

                //Case 2: insert index in front of highlight section
                else if (highlights[i][0] >= start && length > 0)
                {
                    highlights[i][0] += length; //just extends this phrase by new word length + space char
                }
            }

            for (int i = 0; i < deletes.Count; i++)
            {
                //Case 1: insert index in middle of highlight section
                if (deletes[i][0] < start && start + length < deletes[i][0] + deletes[i][1])
                {
                    deletes[i][1] += length;
                }

                //Case 2: insert index in front of highlight section
                else if (deletes[i][0] >= start && length > 0)
                {
                    deletes[i][0] += length; //just extends this phrase by new word length + space char
                }
            }

            UpdateRecordMerge();

        }

        public void PreviewEHR()
        {/// Removes all deleted phrase and sets preview EHR Text

            string text = getEHRText();
            foreach (List<int> r in deletes)
                text = text.Substring(0, r[0]) + new string('*', r[1]) + text.Substring(r[0] + r[1]);
            text = text.Replace("*", "");
            EHRPreview.Document.SetText(TextSetOptions.None, text);
            EHRTextBox.Visibility = Visibility.Collapsed;
            EHRPreview.Visibility = Visibility.Visible;           
        }

        public void ReturnToEdit() {
            EHRTextBox.Visibility = Visibility.Visible;
            EHRPreview.Visibility = Visibility.Collapsed;
        }

        private void InsertToEHR(object sender, RoutedEventArgs e)
        {/// Inserts current recognized text into EHR text edit box

            string text = "";
            foreach (HWRRecognizedText word in inputRecogResult) {
                text += word.selectedCandidate + " ";
            }

            string all_text = getEHRText();

            if (current_index == all_text.Length - 1)
            { //inserting at end of text
                all_text = all_text.Substring(0, all_text.Length).TrimEnd() + " ";
                EHRTextBox.Document.SetText(TextSetOptions.None, all_text + text);
            }

            else {
                //guarantees that first_half is trimmed with ending space character
                string first_half = all_text.Substring(0, current_index + 1);
                //guarantees that second_half is trimmed beginning with no space character
                string rest = all_text.Substring(current_index + 1);
                EHRTextBox.Document.SetText(TextSetOptions.None, first_half + text + rest);
            }

            //clears the previous input
            UpdateRecordListInsert(current_index + 1, text.Length);
            RefreshTextStyle();
            gestureStack.Push(("insert", current_index + 1, text.Length));
            inputgrid.Visibility = Visibility.Collapsed;
            inputMarkup.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            ClearOperationStrokes();
        }

        private void InsertNewLineToEHR(object sender = null, RoutedEventArgs e = null)
        {/// Inserts a new line in EHR text, currently disabled 12/07/2018

            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);

            if (current_index == all_text.Length - 1)
            { //inserting at end of text
                all_text = all_text.Substring(0, all_text.Length).TrimEnd();
                EHRTextBox.Document.SetText(TextSetOptions.None, all_text + Environment.NewLine);
            }
            else
            {
                //guarantees that first_half is trimmed with ending space character
                string first_half = all_text.Substring(0, current_index + 1);
                //guarantees that second_half is trimmed beginning with no space character
                string rest = all_text.Substring(current_index + 1);

                EHRTextBox.Document.SetText(TextSetOptions.None, first_half + Environment.NewLine + rest);
            }

            //clears the previous input
            UpdateRecordListInsert(current_index + 1, 0);
            inputgrid.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            ClearOperationStrokes();
            autosaveDispatcherTimer.Start();
        }

        private void DeleteTextInRange(int start, int end)
        {/// Deletes all texts within the [start,end] range by adding range to record

            // the scribble does not collide with any text, just ignore
            if (start < 0 || end < 0 || end < start || start == end || start == getEHRText().Length)
                return;

            deletes.Add(new List<int>() { start, end - start });
            gestureStack.Push(("delete", start, end - start));
            UpdateRecordMerge();
            autosaveDispatcherTimer.Start();
        }

        private void DeleteSelectedText(object sender, RoutedEventArgs e)
        {
            if (deleteText.Text == "Delete")
                DeleteTextInRange(cur_selected.Item1, cur_selected.Item2);
            else
                RemoveFormat(cur_selected.Item1, cur_selected.Item2, ref deletes);
            SelectionMenu.Visibility = Visibility.Collapsed;
        }

        private void HighlightTextInRange(int start, int end) {

            // the scribble does not collide with any text, just ignore
            if (start < 0 || end < 0 || end <= start || start >= getEHRText().Length || end > getEHRText().Length)
                return;
            Debug.WriteLine($"adding a new block of highlight with length = {end - start}");
            highlights.Add(new List<int>() { start, end - start });
            UpdateRecordMerge();
            RefreshTextStyle();
            gestureStack.Push(("highlight", start, end - start));
        }

        private void HighlightSelectedText(object sender, RoutedEventArgs e) {

            if (highlightState == "Highlight")
                HighlightTextInRange(cur_selected.Item1, cur_selected.Item2);
            else
                RemoveFormat(cur_selected.Item1, cur_selected.Item2, ref highlights);
            SelectionMenu.Visibility = Visibility.Collapsed;
        }

        private void RemoveFormat(int start, int end, ref List<List<int>> record)
        {
            if (start < 0 || end < 0 || end <= start || start >= getEHRText().Length || end > getEHRText().Length)
                return;

            int count = record.Count;

            for (int i = 0; i < count; i++)
            {
                int list_start = record[i][0];
                int list_end = list_start + record[i][1];
                //case 1: selected part is fully in bound
                if (start >= list_start && end <= list_end)
                {
                    Debug.WriteLine("subset");
                    if (start > list_start && end < list_end)
                    {
                        Debug.WriteLine("strict subset");
                        record.Add(new List<int>() { list_start, start - list_start });
                        record.Add(new List<int>() { end, list_end - end });
                    }
                    else if (start == list_start && end < list_end)
                        record.Add(new List<int>() { end, list_end - end });
                    else if (end == list_end && start > list_start)
                        record.Add(new List<int>() { list_start, start - list_start });
                    record.RemoveAt(i);
                    count--;
                }
                //case 2: selected part intersects at start
                else if (start <= list_start && end <= list_end && end > list_start)
                {
                    Debug.WriteLine("start intersects");
                    list_start = end;
                    record[i][1] = list_end - end;
                }
                //case 3: selected part intersects at end
                else if (start >= list_start && end > list_end && start < list_end)
                {
                    Debug.WriteLine("end intersects");
                    record[i][1] = list_end - start;
                }
            }

            RefreshTextStyle();
        }

        private void SelectTextInBound(Rect bounding)
        {
            Point start = new Point(bounding.X, bounding.Y - LINE_HEIGHT - 20);
            Point end = new Point(bounding.X + bounding.Width - 20, bounding.Y - LINE_HEIGHT - 20);
            var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
            var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

            string sub_text1 = getEHRText().Substring(0, range1.StartPosition);
            string sub_text2 = getEHRText().Substring(range2.StartPosition);
            //Guarantees that the range of format "some word followed by space "
            int sel_start = sub_text1.LastIndexOf(" ") + 1;
            int sel_end = sub_text2.IndexOf(" ") + range2.StartPosition + 1;

            cur_selected = (sel_start, sel_end);
            var sel_range = EHRTextBox.Document.GetRange(sel_start, sel_end);

            sel_range.CharacterFormat.Underline = UnderlineType.ThickDash;
            sel_range.CharacterFormat.ForegroundColor = Colors.Orange;

            highlightState = CheckIsHighlighted(sel_start, sel_end);
            highlightText.Text = highlightState;

            deleteState = CheckIsDeleted(sel_start, sel_end);
            deleteText.Text = deleteState;


            Canvas.SetLeft(SelectionMenu, start.X);
            Canvas.SetTop(SelectionMenu, (Math.Ceiling(end.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT);
            SelectionMenu.Visibility = Visibility.Visible;
        }

        private string CheckIsHighlighted(int start, int end) {
            highlights = highlights.OrderBy(lst => lst[0]).ToList();
            foreach (List<int> range in highlights) {
                int list_start = range[0];
                int list_end = range[0] + range[1];

                if (start >= list_start && end <= list_end)
                    return "UnHighlight";
            }
            return "Highlight";
        }

        private string CheckIsDeleted(int start, int end) {
            deletes = deletes.OrderBy(lst => lst[0]).ToList();
            foreach (List<int> range in deletes)
            {
                int list_start = range[0];
                int list_end = range[0] + range[1];

                if (start >= list_start && end <= list_end)
                    return "UnDelete";
            }
            return "Delete";

        }

        private void RedoOperation(object sender, RoutedEventArgs e) {

        }

        private void CopyTextToClipboard(object sender, RoutedEventArgs e)
        {/// Trims the extra newlines in current EHR text and copies it to windows clipboard

            DataPackage dataPackage = new DataPackage();
            // copy 
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            string text = getEHRText();
            foreach (List<int> r in deletes)
                text = text.Substring(0, r[0]) + new string('*', r[1]) + text.Substring(r[0] + r[1]);
            text = text.Replace("*", "");
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            MainPage.Current.NotifyUser("Copied EHR Text to Clipboard", NotifyType.StatusMessage, 1);
            cpMenu.Visibility = Visibility.Collapsed;
        }

        private async void PasteTextToEHR(object sender, RoutedEventArgs e) {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                // To output the text from this example, you need a TextBlock control
                EHRTextBox.Document.SetText(TextSetOptions.None, text);
            }
            MainPage.Current.NotifyUser("Pasted EHR to note", NotifyType.StatusMessage, 1);
            cpMenu.Visibility = Visibility.Collapsed;
            inserts.Clear();
            highlights.Clear();
            deletes.Clear();
        }

        private void ClearOperationStrokes()
        {/// Deletes all processed operation strokes from annotation inkcanvas
            foreach (uint sid in lastOperationStrokeIDs)
            {
                InkStroke s = annotations.InkPresenter.StrokeContainer.GetStrokeById(sid);
                if ( s != null)
                    s.Selected = true;
            }
            annotations.InkPresenter.StrokeContainer.DeleteSelected();
            lastOperationStrokeIDs.Clear();
        }

        public string getEHRText()
        {/// Returns the current non-formatted text in EHR text box as string

            string body;
            EHRTextBox.Document.GetText(TextGetOptions.None, out body);
            body = body.TrimEnd();
            return body;
        }


        #endregion


        #region Stroke Event Handlers

        private void inkCanvas_StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            inputMarkup.Visibility = Visibility.Collapsed;
            inputgrid.Visibility = Visibility.Collapsed;
            SelectionMenu.Visibility = Visibility.Collapsed;
            RefreshTextStyle();
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

        private void inkCanvas_InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {

            foreach (var s in args.Strokes)
            {
                //Process strokes that is valid within textbox bound
                Rect bounding = s.BoundingRect;
                if (s.BoundingRect.Y < EHRTextBox.ActualHeight) {
                    lastOperationStrokeIDs.Add(s.Id);
                    string is_line = PreCheckGesture(s);
                    //Not a line gesture, pass to $1 to recognize
                    if (is_line == null)
                    {
                        List<TimePointR> pts = GetStrokePoints(s);
                        NBestList result = GESTURE_RECOGNIZER.Recognize(pts, false);
                        Debug.WriteLine($" {result.Name}, score = {result.Score}");
                        var resultges = Regex.Replace(result.Name, @"[\d-]", string.Empty);
                        if (resultges == "zigzag")
                        {
                            Point start = new Point(bounding.X + 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                            Point end = new Point(bounding.X + bounding.Width - 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                            var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
                            var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

                            string sub_text1 = getEHRText().Substring(0, range1.StartPosition);
                            string sub_text2 = getEHRText().Substring(range2.StartPosition);
                            //Guarantees that the range if of format "some word followed by space "
                            int del_start = sub_text1.LastIndexOf(" ") + 1;
                            int del_end = sub_text2.IndexOf(" ") + range2.StartPosition + 1;

                            Debug.WriteLine($"Attempt to delete text:-{getEHRText().Substring(del_start, del_end - del_start)}-");
                            DeleteTextInRange(del_start, del_end);
                        }
                        else if (resultges == "line")
                        {
                            SelectTextInBound(bounding);
                        }
                        //else if (resultges == "rectangle") {
                        //    Point start = new Point(bounding.X + 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                        //    Point end = new Point(bounding.X + bounding.Width - 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                        //    var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
                        //    var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

                        //    string sub_text1 = getEHRText().Substring(0, range1.StartPosition);
                        //    string sub_text2 = getEHRText().Substring(range2.StartPosition);
                        //    //Guarantees that the range if of format "some word followed by space "
                        //    int high_start = sub_text1.LastIndexOf(" ") + 1;
                        //    int high_end = sub_text2.IndexOf(" ") + range2.StartPosition + 1;

                        //    Debug.WriteLine($"Attempt to highlight text:-{getEHRText().Substring(high_start, high_end - high_start)}-");
                        //    HighlightTextInRange(high_start, high_end);
                        //}
                    }

                    //Recognized a line
                    else
                    {
                        switch (is_line)
                        {
                            case ("vline"):
                                Point pos = new Point(bounding.X + bounding.Width / 2 - 10, bounding.Y + bounding.Height / 2 - LINE_HEIGHT);
                                var range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                                // the scribble does not collide with any text, just ignore
                                string text;
                                EHRTextBox.Document.GetText(TextGetOptions.None, out text);
                                Debug.WriteLine($"detected fir letter: -{text.Substring(range.StartPosition, 1)}-");
                                current_index = range.StartPosition;
                                //if (text.Substring(range.StartPosition, 1) == " ")
                                //    InsertNewLineToEHR();
                                break;
                            case ("hline"):
                                SelectTextInBound(bounding);
                                break;
                            case ("zigzag"):
                                Point start = new Point(bounding.X + 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                                Point end = new Point(bounding.X + bounding.Width - 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                                var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
                                var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

                                string sub_text1 = getEHRText().Substring(0, range1.StartPosition);
                                string sub_text2 = getEHRText().Substring(range2.StartPosition);
                                //Guarantees that the range if of format "some word followed by space "
                                int del_start = sub_text1.LastIndexOf(" ") + 1;
                                int del_end = sub_text2.IndexOf(" ") + range2.StartPosition + 1;

                                Debug.WriteLine($"Attempt to delete text:-{getEHRText().Substring(del_start, del_end - del_start)}-");
                                DeleteTextInRange(del_start, del_end);
                                break;
                            case ("dot"):
                                pos = new Point(bounding.X + bounding.Width / 2 - 10, bounding.Y - LINE_HEIGHT);
                                range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                                // the scribble does not collide with any text, just ignore
                                EHRTextBox.Document.GetText(TextGetOptions.None, out text);
                                Debug.WriteLine($"detected fir letter: -{text.Substring(range.StartPosition, 1)}-");
                                current_index = GetNearestSpaceIndex(range.StartPosition);
                                range = EHRTextBox.Document.GetRange(current_index - 1, current_index);
                                range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Baseline,
                                                PointOptions.ClientCoordinates, out pos);
                                ShowInputPanel(pos);
                                break;
                            default:
                                break;
                        }
                    }


                }

            }
            inkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private void annotations_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            foreach (InkStroke s in args.Strokes)
            {
                if (s.BoundingRect.Width > LINE_HEIGHT) {                  
                    List<TimePointR> pts = GetStrokePoints(s);
                    Debug.WriteLine(pts.Count);
                    NBestList result = GESTURE_RECOGNIZER.Recognize(pts, false);
                    //foreach (var r in result.Names)
                    //    Debug.WriteLine(r);
                    //Debug.WriteLine("\n\n\n");

                    //foreach (var p in result.Scores)
                    //    Debug.WriteLine(p);

                    var resultges = Regex.Replace(result.Name, @"[\d-]", string.Empty);
                    if (resultges == "rectangle") {
                        lastOperationStrokeIDs.Add(s.Id);
                        var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(s.Id);
                        //adds a new add-in control 
                        string name = FileManager.getSharedFileManager().CreateUniqueName();
                        //adding extra 100 pixel to bounding Y to cope the padding
                        MainPage.Current.curPage.NewAddinControl(name, false, left: s.BoundingRect.X + 1500, top: s.BoundingRect.Y + 100,
                                        widthOrigin: s.BoundingRect.Width, heightOrigin: s.BoundingRect.Height, ehr: this);
                        ClearOperationStrokes();
                    }
                }
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

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {// select strokes by "marking" handling: pointer pressed

            Debug.WriteLine("eraser detected");

            //Side button is pressed on pen
            if (args.CurrentPoint.PointerDevice.PointerDeviceType == PointerDeviceType.Pen && args.CurrentPoint.IsInContact)
            {
                lasso = new Polyline()
                {
                    Stroke = UNPROCESSED_COLOR,
                    StrokeThickness = UNPROCESSED_THICKNESS,
                    //StrokeDashArray = UNPROCESSED_DASH,
                };
                lasso.Opacity = UNPROCESSED_OPACITY;
                popupCanvas.Children.Add(lasso);
                isHighlighting = true;
            }


        }
        
        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {// select strokes by "marking" handling: pointer moved
            Debug.WriteLine("eraser detected");

            if (isHighlighting)
            {
                lasso.Points.Add(args.CurrentPoint.RawPosition);
            }
            else if (isErasing) {
                Debug.WriteLine("erasing");

            }

        }
        
        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {// select strokes by "marking" handling: pointer released
            isHighlighting = false;
            isErasing = false;
            popupCanvas.Children.Remove(lasso);
            Rect bounding = GetSelectBoundingRect(lasso);
            //chooses the left top point as starting point and bottom right and ending point
            Point start = new Point(bounding.X - 10, bounding.Y - LINE_HEIGHT);
            Point end = new Point(bounding.X + bounding.Width, bounding.Y + bounding.Height - LINE_HEIGHT);

            var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
            var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

            int sel_start = range1.StartPosition;
            int sel_end = range2.StartPosition;

            Debug.WriteLine($"highlighting at: start = {sel_start}, end = {sel_end}");
            HighlightTextInRange(sel_start, sel_end);
        }

        #endregion

        #region Stroke Recognition / Analysis

        private List<TimePointR> GetStrokePoints(InkStroke s)
        {/// <summary>Converts a stroke's InkPoint to TimePointF for gesture recognition</summary>
            Debug.WriteLine("total points from getinkpoints =" + s.GetInkPoints().Count);
            List<TimePointR> points = new List<TimePointR>();
            foreach (InkPoint p in s.GetInkPoints())
                points.Add(new TimePointR((float)p.Position.X, (float)p.Position.Y, (long)p.Timestamp));
            return points;
        }

        private int GetNearestSpaceIndex(int index)
        {/// <summary> Gets the nearest space index for inserting a new word </summary>

            int left = index;
            int right = index;

            int left_dist = 99;
            int right_dist = 99;

            string text = getEHRText();
            bool finding = true;

            string lc = "";
            string rc = "";

            while (finding) {
                if (left > 0 && left < text.Length - 1) {
                    lc = text.Substring(left, 1);
                    if (lc == " ") {
                        left_dist = index - left;
                        break;
                    }
                }
                if (right < text.Length - 1) {
                    rc = text.Substring(right, 1);
                    if (rc == " ")
                    {
                        right_dist = right - index;
                        break;
                    }
                }
                left--;
                right++;
            }
            if (lc == " ")
                return left;
            else 
                return right;
        }
        
        private Rect GetSelectBoundingRect(Polyline lasso)
        {/// <summary> Gets the bounding Rect of a lasso selection</summary>

            Point top_left = new Point(EHR_WIDTH,EHR_HEIGHT);
            Point bottom_right = new Point(0, 0);
            //fornow we only focus on selecting one line max
            foreach (Point p in lasso.Points) {
                if (p.X <= top_left.X) 
                    top_left = p;
                if (p.X >= bottom_right.X) 
                    bottom_right = p;                
            }
            return new Rect(top_left,bottom_right);           
        }
       
        private int GetSpaceIndexFromEHR(int start)
        {/// <summary>Gets the position of first occuring space index given a point posiiton</summary>
            string all_text;
            EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);
            string rest = all_text.Substring(start - 1);//move index 1 forward for error torlerance
            int space_index = rest.IndexOf(' ');
            return space_index;
        }
      
        private string PreCheckGesture(InkStroke s)
        {/// <summary>Pre-checks the stroke gesture before passing in to $1</summary>

            Rect bound = s.BoundingRect;
            if (bound.Width < 10 && bound.Height < 10)
                return "dot";
            else if (bound.Height / bound.Width > 3 && bound.Width < 20)
                return "vline";
            else if (bound.Width / bound.Height > 3) {
                List<InkPoint> pts = s.GetInkPoints().ToList();
                InkPoint pre_point = pts[0];
                int spike_count = 0;
                int direction = 1;
                for (int i = 0; i < pts.Count; i+=3) {
                    if (direction == 1 && pts[i].Position.X < pre_point.Position.X)
                    {
                        spike_count++;
                        direction = -1;
                    }
                    else if (direction == -1 && pts[i].Position.X > pre_point.Position.X) {
                        spike_count++;
                        direction = 1;
                    }
                    pre_point = pts[i];
                }
                if (spike_count > 3)
                    return "zigzag";
                else
                    return "hline";
            }
            else
                return null;
        }
      
        private async Task<bool> analyzeInk(InkStroke lastStroke = null, bool serverFlag = false)
        {/// <summary> Analyze ink strokes contained in inkAnalyzer and add phenotype candidates from fetching API</summary>

            //if (lastStroke == null) { 
            //    PhenoMana.phenotypesCandidates.Clear();
            //}
            //Debug.WriteLine("analyzing...");
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
       
        private async void recognizeAndSetUpUIForLine(InkAnalysisLine line, bool indetails = false, bool serverRecog = false)
        {/// <summary>set up for current line, i.e. hwr, show recognition results and show recognized phenotypes</summary>

            if (line == null)
                return;
            curLineWordsStackPanel.Children.Clear();
            HWRManager.getSharedHWRManager().clearCache();

            // hwr
            List<HWRRecognizedText> newResult = await RecognizeLine(line.Id, serverRecog);
            inputRecogResult = newResult == null ? inputRecogResult : newResult;

            // HWR result UI
            setUpCurrentLineResultUI(line);
        }
       
        private async Task<List<HWRRecognizedText>> RecognizeLine(uint lineid, bool serverFlag)
        {/// <summary>Recognize the line strokes in the input canvas</summary>
            try
            {
                foreach (var stroke in inputInkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                {
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
        
        private void setUpCurrentLineResultUI(InkAnalysisLine line)
        {/// <summary> Adds recognized words into recognized UI line </summary>
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
       
        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e)
        {/// <summary>Triggered when user changes a word's alternative in input panel </summary>
            var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
            alterFlyout.Hide();
            var citem = (string)e.ClickedItem;
            int ind = alternativeListView.Items.IndexOf(citem);
            inputRecogResult[showAlterOfWord].selectedIndex = ind;
            inputRecogResult[showAlterOfWord].selectedCandidate = citem;
            TextBlock tb = (TextBlock)curLineWordsStackPanel.Children.ElementAt(showAlterOfWord);
            tb.Text = citem;
        }
               
        #endregion

    }
}
