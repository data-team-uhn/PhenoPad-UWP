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
using System.Numerics;
using Windows.System;
using Windows.Storage.Streams;
using System.Text;
using System.Threading;

//using System.Drawing;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class NoteEditPageControl : UserControl
    {
        enum InsertMode {
            Handwriting,
            typing
        }
        enum InsertType {
            None,
            Annotation,
            ReEdit,
            Insert,
            InsertNewLine
        }
        #region Class Attributes

        private bool alreadyAddText = false;
        private double EHR_HEIGHT = 3000;
        private double EHR_WIDTH = 1100;
        // public GridLength EHR_TEXTBOX_WIDTH = new GridLength(1200);
        public float LINE_HEIGHT = 50;
        private InsertMode insertMode;
        private InsertType insertType;
        private Polyline lasso;
        public static double COMMENT_X_OFFSET = 20;
        private bool insertingAtEnd = false;

        private SolidColorBrush INSERT = new SolidColorBrush(Colors.DodgerBlue);
        private SolidColorBrush ANNOTATION = new SolidColorBrush(Colors.Orange);

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
        private string cur_selectedText;
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

        public NoteEditPageControl(NotePageControl parent)
        {
            this.InitializeComponent();


            parentControl = parent;
            this.notebookid = parent.notebookId;
            this.pageid = parent.pageId;

           
            


            NoteTextBox.Document.ApplyDisplayUpdates();

            autosaveDispatcherTimer = new DispatcherTimer();

            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(3); //setting stroke auto save interval to be 1 sec

    

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

            //this.insertMode = InsertMode.typing;
            //insertType = InsertType.None;
            //TypeToggleBtn.IsChecked = true;
            //HWToggleBtn.IsChecked = false;

            // this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }


        #endregion

        #region Page Set-up / UI-displays

        public async void SetUpEHRFile(StorageFile file)
        {/// Takes the EHR text file and converts content onto RichEditBox

            
        }

        public void DrawBackgroundLines()
        {/// Draws background dashed lines to background canvas 
            //drawing background line for EHR text box
            for (int i = 1; i <= EHRRootGrid.Height / LINE_HEIGHT; ++i)
            {
                var line = new Line()
                {
                    Stroke = new SolidColorBrush(Colors.LightGray),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                    X1 = 0,
                    X2 = backgroundCanvas.RenderSize.Width,
                    Y1 = i * LINE_HEIGHT,
                    Y2 = i * LINE_HEIGHT
                };
                var line2 = new Line()
                {
                    Stroke = new SolidColorBrush(Colors.Gray),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                    X1 = 0,
                    X2 = backgroundCanvas.RenderSize.Width,
                    Y1 = i * LINE_HEIGHT * 1.5,
                    Y2 = i * LINE_HEIGHT * 1.5
                };
                backgroundCanvas.Children.Add(line);
                // FIXME inputCanvasbg.Children.Add(line2);
            }
            UpdateLayout();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {/// Triggered everytime the specific EHRPageControl is loaded
            var format = NoteTextBox.Document.GetDefaultParagraphFormat();
            NoteTextBox.FontSize = 32;
            NoteTextBox.FontSize = 32;
            //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
            format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            NoteTextBox.Document.SetDefaultParagraphFormat(format);
            NoteTextBox.Document.SetDefaultParagraphFormat(format);
            // Draw background lines
            DrawBackgroundLines();

            
        }

        public void set_text(string text)
        {
            // set text
            if (!alreadyAddText)
            {
                alreadyAddText = true;
                NoteTextBox.Document.SetText(TextSetOptions.None, text);
            }
        }


        #endregion

        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e)
        {/// <summary>Triggered when user changes a word's alternative in input panel </summary>
            var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
            alterFlyout.Hide();
            var citem = (string)e.ClickedItem;
            int ind = alternativeListView.Items.IndexOf(citem);
            inputRecogResult[showAlterOfWord].selectedIndex = ind;
            inputRecogResult[showAlterOfWord].selectedCandidate = citem;
            // TextBlock tb = (TextBlock)curLineWordsStackPanel.Children.ElementAt(showAlterOfWord);
            // tb.Text = citem;
        }

        private async void NoteTextBoxDropAsync(object sender, DragEventArgs e)
        {   
            string draggedText = await e.DataView.GetTextAsync();

            string orgText = string.Empty;
            RichEditBox editBox = (RichEditBox)sender;
            editBox.Document.GetText(TextGetOptions.AdjustCrlf, out orgText);

            var posi = e.GetPosition(editBox);
            int num_line = (int) (posi.Y / LINE_HEIGHT) - 1;
            num_line = num_line < 0 ? 0 : num_line;
            var lines = new List<string>(orgText.Split('\r'));
            if (num_line >= lines.Count())
                lines.Add(draggedText);
            else
            {
                if (lines[num_line] == "")
                {
                    lines[num_line] = draggedText;
                }
                else
                {
                    lines.Insert(num_line, draggedText);
                }
            }
            string new_text = String.Join('\r', lines);
            editBox.Document.SetText(TextSetOptions.None, new_text);
            

        }

        private void NoteTextBoxDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }

        private void NoteTextBoxTextChanged(object sender, RoutedEventArgs e)
        {
            var format = NoteTextBox.Document.GetDefaultParagraphFormat();
            NoteTextBox.FontSize = 32;
            NoteTextBox.FontSize = 32;
            //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
            format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            NoteTextBox.Document.SetDefaultParagraphFormat(format);
            NoteTextBox.Document.SetDefaultParagraphFormat(format);
        }
    }
}
