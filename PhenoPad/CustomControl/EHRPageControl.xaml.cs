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
using PhenoPad.PhenotypeService;
using System.Collections.ObjectModel;

//using System.Drawing;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class EHRPageControl : UserControl
    {
        enum InsertMode {
            Handwriting,
            typing
        }
        #region Class Attributes

        private double EHR_HEIGHT = 3000;
        private double EHR_WIDTH = 2000;
        public GridLength EHR_TEXTBOX_WIDTH = new GridLength(1200);
        public float LINE_HEIGHT = 50;
        private InsertMode insertMode;
        private Polyline lasso;

        private float UNPROCESSED_OPACITY = 0.5f;
        private int UNPROCESSED_THICKNESS = 35;
        private SolidColorBrush UNPROCESSED_COLOR = new SolidColorBrush(Colors.Yellow);
        private SolidColorBrush INSERT_MARKUP = new SolidColorBrush(Color.FromArgb(0,66, 134, 244));
        public ObservableCollection<Phenotype> curLineCandidatePheno = new ObservableCollection<Phenotype>();
        public Dictionary<string, Phenotype> cachedAnnotation = new Dictionary<string, Phenotype>();

        private Color INSERTED_COLOR = (Color)Application.Current.Resources["WORD_DARK_COLOR"];//Dark Blue
        private Color HIGHLIGHT_COLOR = Color.FromArgb(0, 255, 248, 173);//Light Yellow
        private Color ANNOTATE_COLOR = Color.FromArgb(0, 255, 235, 173);//Light Yellow

        private bool isHighlighting;
        private bool isErasing;
        private string highlightState = "Highlight";
        private string deleteState = "Delete";
        public int lastAddedCommentID;
        private int showAlterOfWord = -1;
        private int current_index;//the most current pen position to text
        private (int, int) cur_selected;//most current selected phrase
        private (int, int) cur_highlight;
        private (int, int) cur_delete;
        private AddInControl cur_comment;

        //2D lists for keeping edited records
        public List<List<int>> inserts;//List<(ID, start_index, phrase_length)>
        public List<List<int>> highlights;//List<(ID, start, phrase_length)>
        public List<List<int>> deletes;//List<(ID, start, phrase_length)>
        public List<List<int>> annotated; //List<(ID, start_index, end_index)>
        public List<AddInControl> comments;

        public InkStroke curStroke;
        public List<uint> lastOperationStrokeIDs;

        InkAnalyzer inkOperationAnalyzer;
        InkAnalyzer inputInkAnalyzer;
        List<HWRRecognizedText> inputRecogResult;
        DispatcherTimer autosaveDispatcherTimer; //For auto saves

        public string notebookid;
        public string pageid;
        public NotePageControl parentControl;

        #endregion

        #region Constructor

        public EHRPageControl(StorageFile file, NotePageControl parent)
        {
            this.InitializeComponent();
            //setting the text grid line format
            {
                RemovePasteFormat();
            }

            parentControl = parent;
            insertMode = InsertMode.Handwriting;
            this.notebookid = parent.notebookId;
            this.pageid = parent.pageId;

            var drawingAttributes = inkCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            drawingAttributes.DrawAsHighlighter = true;
            drawingAttributes.Color = Colors.OrangeRed;
            drawingAttributes.Size = new Size(5, 5);
            inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            inkCanvas.UpdateLayout();

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;

            inkCanvas.Tapped += OnElementTapped;
            inkCanvas.DoubleTapped += ShowCPMenu;

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += inkCanvas_StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokesCollected += inkCanvas_InkPresenter_StrokesCollectedAsync;

            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;

            annotations.Tapped += OnElementTapped;
            annotations.InkPresenter.StrokesCollected += annotations_StrokesCollected;
            annotations.InkPresenter.StrokeInput.StrokeStarted += annotations_StokeStarted;
            annotations.InkPresenter.StrokeInput.StrokeEnded += annotations_StokeEnded;

            inputInkCanvas.InkPresenter.StrokesCollected += inputInkCanvas_StrokesCollected;
            inputInkCanvas.DoubleTapped += InsertToEHR;

            EHRTextBox.Paste += RemovePasteFormat;
            EHRTextBox.Document.ApplyDisplayUpdates();

            autosaveDispatcherTimer = new DispatcherTimer();
            autosaveDispatcherTimer.Tick += TriggerAutoSave;
            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(3); //setting stroke auto save interval to be 1 sec

            EHRTextBox.SizeChanged += CheckExtendPage;

            inserts = new List<List<int>>();// List<(start, length)>
            highlights = new List<List<int>>();// List<(start, length)>
            deletes = new List<List<int>>();// List<(start, length)>
            annotated = new List<List<int>>();// List<(ID,start,length)>

            comments = new List<AddInControl>();
            cur_selected = (-1, -1);
            cur_highlight = (-1, -1);
            cur_delete = (-1, -1);

            inkOperationAnalyzer = new InkAnalyzer();
            inputInkAnalyzer = new InkAnalyzer();
            inputRecogResult = new List<HWRRecognizedText>();
            lastOperationStrokeIDs = new List<uint>();

            this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }


        #endregion

        #region Page Set-up / UI-displays

        public async void SetUpEHRFile(StorageFile file)
        {/// Takes the EHR text file and converts content onto RichEditBox

            if (file == null)
            {//if no file found, setup an empty text file
                EHRTextBox.Document.SetText(TextSetOptions.None, "");
                await FileManager.getSharedFileManager().SaveEHRText(notebookid, pageid, this);
                await FileManager.getSharedFileManager().SaveEHRFormats(notebookid, pageid, this);
                return;
            }

            try
            {
                string text = await FileIO.ReadTextAsync(file);
                string[] paragraphs = text.Split(Environment.NewLine);
                text = "";
                //manually reformatting text by adding a space at the end of paragraphs
                foreach (string p in paragraphs)
                {
                    if (!p.EndsWith(" "))
                        text += p + " " + Environment.NewLine;
                    else
                        text += p + Environment.NewLine;
                }
                text = text.TrimEnd() + " ";
                EHRTextBox.Document.SetText(TextSetOptions.None, text);
                await FileManager.getSharedFileManager().SaveEHRText(notebookid, pageid, this);
                EHRFormats formats = await FileManager.getSharedFileManager().LoadEHRFormats(notebookid, pageid);
                if (formats != null)
                {
                    this.inserts = formats.inserts;
                    this.highlights = formats.highlights;
                    this.deletes = formats.deletes;
                    this.annotated = formats.annotates;
                    RefreshTextStyle();
                }
            }
            //for taking care of non-existing saved format record files
            catch (FileNotFoundException) { }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
            }
        }

        public void DrawBackgroundLines()
        {/// Draws background dashed lines to background canvas 
            //drawing background line for EHR text box
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
                var line2 = new Line()
                {
                    Stroke = new SolidColorBrush(Windows.UI.Colors.Gray),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                    X1 = 0,
                    X2 = backgroundCanvas.RenderSize.Width,
                    Y1 = i * LINE_HEIGHT * 1.5,
                    Y2 = i * LINE_HEIGHT * 1.5
                };
                backgroundCanvas.Children.Add(line);
                inputCanvasbg.Children.Add(line2);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {/// Triggered everytime the specific EHRPageControl is loaded
            
            // Draw background lines
            DrawBackgroundLines();
        }

        private void ShowInputPanel(Point p)
        {/// <summary>Displays input canvas for inserting into EHR</summary>

            double newX = p.X - 20;
            double newY = ((int)(p.Y / LINE_HEIGHT) + 2) * LINE_HEIGHT - 20;
            if (p.X + inputgrid.ActualWidth > EHR_TEXTBOX_WIDTH.Value)
                newX = EHR_TEXTBOX_WIDTH.Value - inputgrid.ActualWidth;
            else if (p.X > 600)
                newX -= 400;

            Canvas.SetLeft(inputgrid, newX);
            Canvas.SetTop(inputgrid, newY + LINE_HEIGHT);
            Canvas.SetLeft(inputMarkup, p.X - 5);
            Canvas.SetTop(inputMarkup, newY);

            inputgrid.Visibility = Visibility.Visible;
            inputMarkup.Visibility = Visibility.Visible;
            if (insertMode == InsertMode.typing)
                inputTypeBox.Focus(FocusState.Pointer);
        }

        private void ShowCPMenu(object sender, DoubleTappedRoutedEventArgs e)
        {//Shows the menu UI with text copy/paste options
            Canvas.SetLeft(cpMenu, e.GetPosition(backgroundCanvas).X - cpMenu.ActualWidth - 10);
            Canvas.SetTop(cpMenu, e.GetPosition(backgroundCanvas).Y - cpMenu.ActualHeight / 2);
            cpMenu.Visibility = Visibility.Visible;
        }

        private async void AddComment(object sender, RoutedEventArgs e)
        { //adds a new addin control in parent NotePageControl as a comment type

            SelectionMenu.Visibility = Visibility.Collapsed;
            var range = EHRTextBox.Document.GetRange(cur_selected.Item1, cur_selected.Item1 + 1);
            Point pos;
            range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Bottom, PointOptions.ClientCoordinates, out pos);
            double newY = (Math.Ceiling(pos.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT;
            pos.X -= 10;
            AddInControl comment = parentControl.NewEHRCommentControl(pos.X, newY + 110, cur_selected.Item1, AnnotationType.Comment);
            comment.commentslideX = EHR_TEXTBOX_WIDTH.Value - pos.X + 20;
            comment.commentslideY = -LINE_HEIGHT;
            annotated.Add(new List<int>() { cur_selected.Item1, cur_selected.Item2 });

            this.comments = comments.OrderBy(x => x.commentID).ToList();
            foreach (AddInControl c in comments)
            {
                //if the new comment collides with previously saved comments, shift this comment down
                bool collides = await Collides(comment, c);
                if (comment.commentID > c.commentID && collides) {
                    Debug.WriteLine("collides");
                    double overlap_offset = (c.canvasTop + c.commentslideY + c.Height + 10) - (comment.canvasTop + comment.commentslideY) + 10;
                    comment.commentslideY += overlap_offset;
                }
            }               
            this.comments.Add(comment);
            lastAddedCommentID = comment.commentID;
            RefreshTextStyle();
        }

        private async Task<bool> Collides(AddInControl c1, AddInControl c2)
        {// Checks whether two addin controls collides in bounding
            double height1 = await c1.GetCommentHeight();
            double height2 = await c2.GetCommentHeight();
            Rect bound1 = new Rect(c1.canvasLeft + c1.commentslideX, c1.canvasTop + c1.commentslideY, 400, height1);
            Rect bound2 = new Rect(c2.canvasLeft + c2.commentslideX, c2.canvasTop + c2.commentslideY, 400, height2);
            bool collides = !(RectHelper.Intersect(bound1, bound2) == Rect.Empty);
            return collides;
        }

        public async void RemoveComment(AddInControl comment)
        {// removes a comment object from parent NotePageControl and shifts others up accordingly
            parentControl.addinCanvasEHR.Children.Remove(comment);
            List<int> commentRecord = annotated.Where(x => x[0] == comment.commentID).FirstOrDefault();
            annotated.Remove(commentRecord);
            //shifts the Y position for all comments before the deleted comment
            comments = comments.OrderBy(c => c.commentID).ToList();
            bool shift = true;
            comments.Remove(comment);

            for (int i = 0; i < comments.Count; i ++)
            {
                if (comments[i].commentID > comment.commentID)
                {
                    double slideOffset = 0;
                    if (i == 0) {
                        slideOffset =  -(comments[i].commentslideY + LINE_HEIGHT);
                        comments[i].commentslideY = -LINE_HEIGHT;
                        Debug.WriteLine($"i=0 slideoffset = {slideOffset}");
                    }
                    else
                    {
                        double lastOffset = (comments[i].canvasTop + comments[i].commentslideY) - (comments[i - 1].canvasTop + comments[i-1].commentslideY + comments[i - 1].Height + 20);
                        Debug.WriteLine($"i > 0 lastOffset = {lastOffset}");
                        if (comments[i].commentslideY - lastOffset < -LINE_HEIGHT)
                        {
                            Debug.WriteLine($"i > 0 exceeded");
                            slideOffset = -(comments[i].commentslideY + LINE_HEIGHT);
                            comments[i].commentslideY = -LINE_HEIGHT;
                            Debug.WriteLine($"i > 0 final slide offset = {slideOffset}");
                        }
                        else
                        {
                            comments[i].commentslideY -= lastOffset;
                            slideOffset = - lastOffset;
                        }

                    }
                    comments[i].Slide(y:slideOffset);



                    ////only slides up the comment if the offset has not exceeded it's original offset (-LINE_HEIGHT)
                    //if ((comments[i].canvasTop + slideOffset >= comments[i].canvasTop - LINE_HEIGHT))
                    //{
                    //    comments[i].commentslideY = slideOffset;
                    //    comments[i].SlideVertical(slideOffset);
                    //}
                    //else {
                    //    //sets sequential shift flag to false to prevent comments overlapping eachother
                    //    shift = false;
                    //}
                }
            }
            cur_comment = null;
            RefreshTextStyle();
            HideCommentLine();
            await MainPage.Current.curPage.AutoSaveAddin(null);
            await FileManager.getSharedFileManager().DeleteAddInFile(notebookid, pageid, comment.name);
        }

        internal void ShowCommentLine(AddInControl comment)
        {//shows the comment card direction line UI
            List<int> annoRange = this.annotated.Where(x => x[0] == comment.commentID).FirstOrDefault();
            if (annoRange != null) {
                var range1 = EHRTextBox.Document.GetRange(annoRange[0], annoRange[1] - 1);
                Rect bounding;
                int hit;
                range1.GetRect(PointOptions.ClientCoordinates, out bounding, out hit);

                var line1 = new Polyline()
                {
                    Stroke = new SolidColorBrush(comment.BORDER_ACTIVE),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    Points = new PointCollection()
                    {
                        //new Point(bounding.X + 10,bounding.Y + bounding.Height),
                        new Point(bounding.X + 10,bounding.Y + bounding.Height + LINE_HEIGHT),
                        new Point(bounding.X + bounding.Width + 10, bounding.Y + bounding.Height + LINE_HEIGHT),
                        new Point(comment.canvasLeft + comment.commentslideX, 
                                  comment.canvasTop + comment.commentslideY - 110 + (comment.Height / 2))
                    }
                };
                popupCanvas.Children.Add(line1);
            }

        }

        internal void HideCommentLine()
        {//hides the comment card direction line UI
            var line = popupCanvas.Children.Where(x => x.GetType() == typeof(Polyline)).ToList();
            foreach(Polyline l in line)
                popupCanvas.Children.Remove(l);          
        }

        public async void SlideCommentsToSide()
        {//compress comment cards by sliding them to right of EHR textedit box
            bool shift = false;//for sequtienal shifting
            AddInControl lastAdded = comments.Where(c => c.commentID == lastAddedCommentID).FirstOrDefault();
            comments = comments.OrderBy(x => x.commentID).ToList();
            double overlap_offset = 0;
            foreach (AddInControl comment in comments)
            {
                if (lastAdded == null)
                    comment.SlideToRight();
                else
                {
                    bool collides = await Collides(lastAdded, comment);
                    if (comment.commentID <= lastAddedCommentID)
                        comment.SlideToRight();
                    else if (shift)
                    {
                        comment.commentslideY += overlap_offset;
                        comment.Slide(y:overlap_offset);
                    }
                    else if (comment.commentID > lastAddedCommentID && collides)
                    {
                        double commentHeight = await lastAdded.GetCommentHeight();
                        overlap_offset = (lastAdded.canvasTop + lastAdded.commentslideY + lastAdded.Height + 10) - (comment.canvasTop + comment.commentslideY) + 10;
                        Debug.WriteLine($"overlap offset = {overlap_offset}");
                        comment.commentslideY += overlap_offset;
                        comment.Slide(y:overlap_offset);
                        shift = true;
                    }

                }
            }
            autosaveDispatcherTimer.Start();
        }

        private void ToggleInsertMode(object sender, RoutedEventArgs e)
        {//switches between insert mode (writing->recognized text) and (typing mode)
            if (insertMode == InsertMode.Handwriting)
            {
                inputCanvasbg.Visibility = Visibility.Collapsed;
                inputInkCanvas.Visibility = Visibility.Collapsed;
                inputTypeBox.KeyDown +=(object obj, KeyRoutedEventArgs ke) => {
                    if (ke.Key == Windows.System.VirtualKey.Enter) {
                        InsertToEHRClick();
                    }
                };
                insertSymbol.Symbol = Symbol.Edit;
                insertMode = InsertMode.typing;
                inputTypeBox.Visibility = Visibility.Visible;
                inputTypeBox.AcceptsReturn = false;
                inputTypeBox.Focus(FocusState.Pointer);
            }
            else {
                inputCanvasbg.Visibility = Visibility.Visible;
                inputInkCanvas.Visibility = Visibility.Visible;
                inputTypeBox.Visibility = Visibility.Collapsed;
                insertMode = InsertMode.Handwriting;
                insertSymbol.Symbol = Symbol.Keyboard;

            }
        }

        private void CheckExtendPage(object sender, SizeChangedEventArgs e)
        {//checks if EHR text height has exceeded the page size and extend if needed
            Debug.WriteLine($"size changed ! new height = {e.NewSize.Height}");
            if (e.NewSize.Height + 500 > EHR_HEIGHT)
            {
                EHRRootGrid.Height = e.NewSize.Height + 500;
            }
            this.DrawBackgroundLines();
            this.UpdateLayout();
        }


        #endregion

        #region Gesture Operations

        private async void OnElementTapped(object sender = null, TappedRoutedEventArgs e = null)
        {/// Dismiss all pop-ups when tapping on canvas
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => {
                if (inputInkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
                    InsertToEHR();
                SlideCommentsToSide();
                inputgrid.Visibility = Visibility.Collapsed;
                inputMarkup.Visibility = Visibility.Collapsed;
                cpMenu.Visibility = Visibility.Collapsed;
                SelectionMenu.Visibility = Visibility.Collapsed;
                ClearOperationStrokes();
                if (popupCanvas.Children.Contains(lasso))
                {
                    popupCanvas.Children.Remove(lasso);
                }
                HideCommentLine();
                //DeleteComment.Visibility = Visibility.Collapsed;
                cur_selected = (-1, -1);
                cur_highlight = (-1, -1);
                cur_delete = (-1, -1);
                current_index = -1;
                RefreshTextStyle();
            });
        }

        private async void TriggerAutoSave(object sender = null, object e = null)
        {/// Trigger parent NotePageControl's auto-save to save all progress
            autosaveDispatcherTimer.Stop();
            if (MainPage.Current.curPage != null)
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

        private void UndoOperation(object sender, RoutedEventArgs e)
        {
            //todo
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
            foreach (List<int> a in annotated) {
                ITextRange range;
                AddInControl anno = comments.Where(x => x.commentID == a[0]).FirstOrDefault();
                if (anno != null && anno.anno_type == AnnotationType.Comment)
                {
                    range = EHRTextBox.Document.GetRange(a[0], a[1] - 1);
                    range.CharacterFormat.BackgroundColor = ANNOTATE_COLOR;
                }
                else if (anno != null && anno.anno_type == AnnotationType.RawInsert)
                {
                    range = EHRTextBox.Document.GetRange(a[0], a[1]);
                    range.CharacterFormat.BackgroundColor = INSERTED_COLOR;
                }

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

        public void PreviewEHR()
        {/// Removes all deleted phrase and sets preview version of EHR Text

            string text = getEHRText();
            foreach (List<int> r in deletes)
                text = text.Substring(0, r[0]) + new string('*', r[1]) + text.Substring(r[0] + r[1]);
            text = text.Replace("*", "");
            EHRPreview.Document.SetText(TextSetOptions.None, text);
            EHRTextBox.Visibility = Visibility.Collapsed;
            EHRPreview.Visibility = Visibility.Visible;           
        }

        public void ReturnToEdit()
        {/// Updates UI back to EHR edit mode
            EHRTextBox.Visibility = Visibility.Visible;
            EHRPreview.Visibility = Visibility.Collapsed;
        }

        private void UpdateRecordListInsert(int start = -1, int length = 0)
        {/// Updates and shifts saved record indexes and positions accordingly

            //Checking for insert range bound collision and add to inserts record
            bool add = true;
            for (int i = 0; i < inserts.Count; i++)
            {
                int list_start = inserts[i][0];
                int list_end = inserts[i][0] + inserts[i][1];

                Debug.WriteLine($"current insert record starting: {list_start}");

                //Case1:New insert index is before previously inserted words
                if (start < list_start && length > 0 && start > -1)
                {
                    Debug.WriteLine("insert is strcitly before inserted");
                    inserts[i][0] += length;
                }
                //Case2: Inserting range collides with inserted range
                else if (start >= list_start && start < list_end)
                {
                    Debug.WriteLine("---insert collides with inserted");
                    inserts[i][1] += length;
                    add = false;
                }
            }
            if (add)
                inserts.Add(new List<int>() { start, length });

            //extend highlighting range according to inserted
            for (int i = 0; i < highlights.Count; i++)
            {
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

            //extend deleting range according to inserted
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

            //entend annotated range according to inserted
            for (int i = 0; i < annotated.Count; i++)
            {
                //Case 1: insert index in middle of highlight section
                if (annotated[i][0] < start && start + length < annotated[i][1])
                {
                    annotated[i][1] += length;
                }
                //Case 2: insert index in front of highlight section
                else if (annotated[i][0] >= start && length > 0)
                {
                    AddInControl comment = comments.Where(c => c.commentID == annotated[i][0]).FirstOrDefault();
                    comment.commentID += length;
                    annotated[i][0] += length;
                    annotated[i][1] += length;
                    //shift the comment panel's left-top position according to new starting index;
                    var range = EHRTextBox.Document.GetRange(comment.commentID, comment.commentID + 1);
                    Point pos;
                    range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Bottom, PointOptions.ClientCoordinates, out pos);
                    double newY = (Math.Ceiling(pos.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT + 110;
                    double xOffset = (pos.X + 10) - comment.canvasLeft;
                    double yOffset = newY - comment.canvasTop ;
                    Debug.WriteLine($"after insert, Yoffset = {yOffset}");
                    if (yOffset != 0) {
                        comment.Slide(y: yOffset);
                        comment.commentslideY += yOffset - LINE_HEIGHT;
                        comment.canvasTop = newY;
                        Canvas.SetTop(comment, comment.canvasTop);
                    }
                    comment.Slide(x: -xOffset);
                    comment.commentslideX -= xOffset;
                    comment.canvasLeft = pos.X + 10;
                    Canvas.SetLeft(comment, comment.canvasLeft);
                }

            }

        }

        private async void InsertToEHR(object sender = null, DoubleTappedRoutedEventArgs e = null)
        {///Adds the raw insert as an annotation comment
            var strokes = inputInkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (InkStroke s in strokes) {
                s.Selected = true;
            }
            inputInkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
            var range = EHRTextBox.Document.GetRange(current_index - 1, current_index);
            Point pos;
            range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Baseline, PointOptions.ClientCoordinates, out pos);
            double newY = (Math.Ceiling(pos.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT;
            AddInControl comment = parentControl.NewEHRCommentControl(pos.X + 10, newY + 110, current_index, AnnotationType.RawInsert);
            comment.commentslideX = EHR_TEXTBOX_WIDTH.Value - pos.X + 10;
            comment.commentslideY = -LINE_HEIGHT;
            annotated.Add(new List<int>() { current_index, current_index + 1 });
            comment.inkCan.InkPresenter.StrokeContainer.PasteFromClipboard(new Point(2,2));

            this.comments = comments.OrderBy(x => x.commentID).ToList();
            foreach (AddInControl c in comments)
            {
                //if the new comment collides with previously saved comments, shift this comment down
                bool collides = await Collides(comment, c);
                if (comment.commentID > c.commentID && collides)
                {
                    Debug.WriteLine("collides");
                    double overlap_offset = (c.canvasTop + c.commentslideY + c.Height + 10) - (comment.canvasTop + comment.commentslideY) + 10;
                    comment.commentslideY += overlap_offset;
                }
            }
            this.comments.Add(comment);
            lastAddedCommentID = comment.commentID;
            inputgrid.Visibility = Visibility.Collapsed;
            inputMarkup.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            SlideCommentsToSide();
            RefreshTextStyle();
        }

        private void InsertToEHRClick(object sender = null, RoutedEventArgs e = null)
        {//Invoked by clicking "+" in insert panel, inserts all recognized text into EHR text and refresh format and records
            string text = "";
            if (insertMode == InsertMode.Handwriting)
            {
                foreach (HWRRecognizedText word in inputRecogResult)
                    text += word.selectedCandidate + " ";
            }
            else
            {
                string typeText;
                inputTypeBox.Document.GetText(TextGetOptions.None, out typeText);
                text = (typeText.TrimStart()).TrimEnd() + " ";
                if (text[0] == ' ')
                    text = text.Substring(1);//for some reason there's a white space at index 0 that won't be trimmed
                inputTypeBox.Document.SetText(TextSetOptions.None, "");
            }

            string all_text = getEHRText();

            if (current_index == all_text.Length)
            { //inserting at end of text
                all_text = all_text.Substring(0, all_text.Length).TrimEnd() + " ";
                EHRTextBox.Document.SetText(TextSetOptions.None, all_text + text);
            }
            else
            {
                string result = all_text.Insert(current_index + 1, text);
                EHRTextBox.Document.SetText(TextSetOptions.None, result);
            }

            //clears the previous input
            UpdateRecordListInsert(current_index + 1, text.Length);
            UpdateRecordMerge();
            inputgrid.Visibility = Visibility.Collapsed;
            inputMarkup.Visibility = Visibility.Collapsed;
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
            curLineWordsStackPanel.Children.Clear();
            ClearOperationStrokes();
        }

        private void ClearInput(object sender, RoutedEventArgs e)
        {//Clears all recognized input and strokes in the inputcanvas panel
            curLineWordsStackPanel.Children.Clear();
            inputInkCanvas.InkPresenter.StrokeContainer.Clear();
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
        {/// Deletes all texts within the [start,end] range by adding range to delete record

            // the scribble does not collide with any text, just ignore
            if (start < 0 || end < 0 || end < start || start == end || start == getEHRText().Length)
                return;

            deletes.Add(new List<int>() { start, end - start });
            UpdateRecordMerge();
            autosaveDispatcherTimer.Start();
        }

        private void DeleteSelectedText(object sender, RoutedEventArgs e)
        {//Deletes the currently saved selected text range
            if (deleteText.Text == "Delete") {
                if (cur_delete.Item1 == -1)
                    DeleteTextInRange(cur_selected.Item1, cur_selected.Item2);
                else
                    DeleteTextInRange(cur_delete.Item1, cur_delete.Item2);
                deleteText.Text = "Cancel Delete";
            }
            else {
                if (cur_delete.Item1 == -1)
                    RemoveFormat(cur_selected.Item1, cur_selected.Item2, ref deletes);
                else
                    RemoveFormat(cur_delete.Item1, cur_delete.Item2, ref deletes);
                deleteText.Text = "Delete";
            }
        }

        private void HighlightTextInRange(int start, int end)
        {//Highlights a specific range of text given its start and end index

            // the scribble does not collide with any text, just ignore
            if (start < 0 || end < 0 || end <= start || start >= getEHRText().Length || end > getEHRText().Length)
                return;
            Debug.WriteLine($"adding a new block of highlight with length = {end - start}");
            highlights.Add(new List<int>() { start, end - start });
            UpdateRecordMerge();
            RefreshTextStyle();
        }

        private void HighlightSelectedText(object sender, RoutedEventArgs e)
        {//Highlights the currently saved selected text 

            if (highlightState == "Highlight")
            {
                if (cur_highlight.Item1 == -1)
                    HighlightTextInRange(cur_selected.Item1, cur_selected.Item2);
                else
                    HighlightTextInRange(cur_highlight.Item1, cur_highlight.Item2);
                highlightText.Text = "Cancel Highlight";

            }
            else {
                if (cur_highlight.Item1 == -1)
                    RemoveFormat(cur_selected.Item1, cur_selected.Item2, ref highlights);
                else
                    RemoveFormat(cur_highlight.Item1, cur_highlight.Item2, ref highlights);
                highlightText.Text = "Highlight";
            }
        }

        private void RemoveFormat(int start, int end, ref List<List<int>> record)
        {//removes a formatted text range in a given format record list
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
        {//selects a range of EHR text based on a given rectangle

            //making the start and end range smaller to avoid over-sensitive range detections
            Point start = new Point(bounding.X + 10, bounding.Y - LINE_HEIGHT - 20);
            Point end = new Point(bounding.X + bounding.Width - 30, bounding.Y - LINE_HEIGHT - 20);
            var range1 = EHRTextBox.Document.GetRangeFromPoint(start, PointOptions.ClientCoordinates);
            var range2 = EHRTextBox.Document.GetRangeFromPoint(end, PointOptions.ClientCoordinates);

            string sub_text1 = getEHRText().Substring(0, range1.StartPosition);
            string sub_text2 = getEHRText().Substring(range2.StartPosition);
            //Guarantees that the range of format "some word followed by space "
            int sel_start = sub_text1.LastIndexOf(" ") + 1;
            int sel_end = sub_text2.IndexOf(" ") + range2.StartPosition + 1;

            var sel_range = EHRTextBox.Document.GetRange(sel_start, sel_end);

            sel_range.CharacterFormat.Underline = UnderlineType.ThickDash;
            sel_range.CharacterFormat.ForegroundColor = Colors.Orange;
            cur_selected = (sel_start, sel_end);
            highlightState = CheckIsHighlighted(sel_start, sel_end);
            highlightText.Text = highlightState;
            deleteState = CheckIsDeleted(sel_start, sel_end);
            deleteText.Text = deleteState;

            Canvas.SetLeft(SelectionMenu, start.X);
            Canvas.SetTop(SelectionMenu, (Math.Ceiling(end.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT);
            SelectionMenu.Visibility = Visibility.Visible;
        }

        private string CheckIsHighlighted(int start, int end)
        {// checks if given range of text is within the hightlighted record
            highlights = highlights.OrderBy(lst => lst[0]).ToList();
            foreach (List<int> range in highlights) {
                int list_start = range[0];
                int list_end = range[0] + range[1];

                if (start >= list_start && end <= list_end)
                    return "Cancel Highlight";
            }
            return "Highlight";
        }

        private string CheckIsDeleted(int start, int end)
        { // checks if given range of text is within the deleted record
            deletes = deletes.OrderBy(lst => lst[0]).ToList();
            foreach (List<int> range in deletes)
            {
                int list_start = range[0];
                int list_end = range[0] + range[1];

                if (start >= list_start && end <= list_end)
                    return "Cancel Delete";
            }
            return "Delete";
        }

        private bool CheckForFormat(int start, int end, Point p)
        {//checks if a given index range in EHR Text is already formatted

            foreach (List<int> a in annotated)
            {
                if (start >= a[0] && end <= a[1])
                {
                    AddInControl inserted = comments.Where(x => x.commentID == a[0]).FirstOrDefault();
                    if (inserted != null && inserted.anno_type == AnnotationType.Comment && inserted.canvasLeft + inserted.addinSlide.X > EHR_TEXTBOX_WIDTH.Value)
                        inserted.ReEdit();
                    return true;
                }
            }

            bool showMenu = false;
            foreach (List<int> h in highlights) {
                if (start >= h[0] && end <= h[0] + h[1])
                {
                    highlightText.Text = "Cancel Highlight";
                    cur_highlight = (h[0], h[0] + h[1]);
                    cur_selected = (h[0], h[0] + h[1]);
                    showMenu = true;
                    break;
                }                
            }
            foreach (List<int> d in deletes) {
                if (start >= d[0] && end <= d[0] + d[1])
                {
                    deleteText.Text = "Cancel Delete";
                    cur_delete = (d[0], d[0] + d[1]);
                    showMenu = true;
                    break;
                }
            }

            if (showMenu) {
                Canvas.SetLeft(SelectionMenu, p.X - 10);
                Canvas.SetTop(SelectionMenu, (Math.Ceiling(p.Y / LINE_HEIGHT) + 1) * LINE_HEIGHT);
                SelectionMenu.Visibility = Visibility.Visible;
            }
            return showMenu;
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

        private async void PasteTextToEHR(object sender, RoutedEventArgs e)
        {//paste text as append from clipboard to end of current ehr text
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                string[] paragraphs = text.Split(Environment.NewLine);
                text = "";
                foreach (string p in paragraphs) {
                    if (p.EndsWith(" "))
                        text += p + Environment.NewLine;
                    else
                        text += p + " " + Environment.NewLine;
                }
                string ehr_text = getEHRText();
                if (! ehr_text.EndsWith(" "))
                    EHRTextBox.Document.SetText(TextSetOptions.None, ehr_text+" "+ Environment.NewLine + text);
                else
                    EHRTextBox.Document.SetText(TextSetOptions.None, ehr_text + Environment.NewLine + text);
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
            //DeleteComment.Visibility = Visibility.Collapsed;
            inputgrid.Visibility = Visibility.Collapsed;
            SelectionMenu.Visibility = Visibility.Collapsed;
            SlideCommentsToSide();
            RefreshTextStyle();
        }

        private void inkCanvas_InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            foreach (var s in args.Strokes)
            {
                //Process strokes that is valid within textbox bound
                Rect bounding = s.BoundingRect;
                if (s.BoundingRect.Y < EHRTextBox.ActualHeight) {

                    StrokeType gesture = GestureManager.GetSharedGestureManager().GetGestureFromStroke(s);

                    if (gesture == StrokeType.Zigzag)
                    {//Zigzag for deleting
                        Point start = new Point(bounding.X + 10, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
                        Point end = new Point(bounding.X + bounding.Width - 20, bounding.Y + (bounding.Height / 2.0) - LINE_HEIGHT);
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

                    else if (gesture == StrokeType.Dot)
                    {//Dot for inserting
                        var pos = new Point(bounding.X + bounding.Width / 2 - 10, bounding.Y - LINE_HEIGHT);
                        var range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                        string text = getEHRText();
                        if (range.StartPosition == text.Length)
                        {
                            range = EHRTextBox.Document.GetRange(range.StartPosition, range.StartPosition);
                            range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Baseline,
                                                    PointOptions.ClientCoordinates, out pos);
                            current_index = text.Length;
                            ShowInputPanel(pos);
                        }
                        else {
                            Debug.WriteLine($"detected fir letter: -{text.Substring(range.StartPosition, 1)}-");
                            bool isFormatted = CheckForFormat(range.StartPosition, range.StartPosition + 1, pos);
                            if (!isFormatted)
                            {
                                current_index = GetNearestSpaceIndex(range.StartPosition);
                                AddInControl inserted = comments.Where(x => x.commentID == current_index).FirstOrDefault();
                                if (inserted == null)
                                {// available for new insert, show input grid
                                    range = EHRTextBox.Document.GetRange(current_index - 1, current_index);
                                    range.GetPoint(HorizontalCharacterAlignment.Left, VerticalCharacterAlignment.Baseline,
                                                    PointOptions.ClientCoordinates, out pos);
                                    ShowInputPanel(pos);
                                }
                                else
                                {
                                    if (inserted.anno_type == AnnotationType.RawInsert && inserted.canvasLeft + inserted.addinSlide.X > EHR_TEXTBOX_WIDTH.Value)
                                        inserted.ReEdit();
                                }
                            }
                        }

                    }
                    else if (gesture == StrokeType.HorizontalLine)
                        SelectTextInBound(bounding);
                    else if (gesture == StrokeType.VerticalLine)
                    {
                        Point pos = new Point(bounding.X + bounding.Width / 2 - 10, bounding.Y + bounding.Height / 2 - LINE_HEIGHT);
                        var range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                        // the scribble does not collide with any text, just ignore
                        string text;
                        EHRTextBox.Document.GetText(TextGetOptions.None, out text);
                        Debug.WriteLine($"detected fir letter: -{text.Substring(range.StartPosition, 1)}-");
                        current_index = range.StartPosition;
                        //if (text.Substring(range.StartPosition, 1) == " ")
                        //    InsertNewLineToEHR();
                    }
                }
            }
            //clears all strokes because they are only gesture strokes
            inkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private void annotations_StokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Stop();
            OnElementTapped();
        }

        private void annotations_StokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
        }

        private void annotations_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            foreach (InkStroke s in args.Strokes)
            {
                if (s.BoundingRect.Height > 3 * LINE_HEIGHT) {
                    StrokeType gesture = GestureManager.GetSharedGestureManager().GetGestureFromStroke(s);
                    if (gesture == StrokeType.Rectangle) {
                        lastOperationStrokeIDs.Add(s.Id);
                        var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(s.Id);
                        //adds a new add-in control 
                        string name = FileManager.getSharedFileManager().CreateUniqueName();
                        //adding extra 100 pixel to bounding Y to cope the padding
                        MainPage.Current.curPage.NewAddinControl(name, false, left: s.BoundingRect.X + EHR_TEXTBOX_WIDTH.Value, top: s.BoundingRect.Y + 100, widthOrigin: s.BoundingRect.Width, heightOrigin: s.BoundingRect.Height, ehr: this);
                        ClearOperationStrokes();
                    }
                }
            }
        }

        private async void inputInkCanvas_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            Rect bound = inputInkCanvas.InkPresenter.StrokeContainer.BoundingRect;
            if (bound.Height > inputInkCanvas.Height * 0.8)
                inputCanvasRow.Height = new GridLength(inputCanvasRow.Height.Value + (double)LINE_HEIGHT);

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
           
        private async Task<bool> analyzeInk(InkStroke lastStroke = null, bool serverFlag = false)
        {/// <summary> Analyze ink strokes contained in inkAnalyzer and add phenotype candidates from fetching API</summary>

            if (inputInkAnalyzer.IsAnalyzing)
            {
                return false;
            }
            // analyze 
            var result = await inputInkAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                //int line = linesToAnnotate.Dequeue();
                var lineNodes = inputInkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
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

            foreach (var stroke in inputInkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                stroke.Selected = true;

            //recognize selection
            List<HWRRecognizedText> newResult = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inputInkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected, serverRecog);
            inputRecogResult = newResult == null ? inputRecogResult : newResult;

            // HWR result UI
            Dictionary<string, List<string>> dict = HWRManager.getSharedHWRManager().getDictionary();
            curLineWordsStackPanel.Children.Clear();

            for (int index = 0; index < inputRecogResult.Count; index++)
            {
                string word = inputRecogResult[index].selectedCandidate;
                TextBlock tb = new TextBlock();
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.FontSize = 24;
                //for detecting abbreviations
                if (index != 0 && dict.ContainsKey(inputRecogResult[index - 1].selectedCandidate.ToLower()) &&
                    dict[inputRecogResult[index - 1].selectedCandidate.ToLower()].Contains(word))
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
            curLineWordsStackPanel.Visibility = Visibility.Visible;
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
