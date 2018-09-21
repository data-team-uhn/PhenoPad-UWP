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
    /**
     * 
     * This is the control for note pages
     * It contains all the strokes, add-in images
     * We also perform handwriting recognitiona and natural language processing
     * 
    **/
    public sealed partial class NotePageControl : UserControl
    {
        #region class properties
        public string pageId;
        public string notebookId;

        /***** configurable settings *****/
        // Distance between two neighboring note lines
        public  float LINE_HEIGHT = 50;
        // Max hight for writing, those with hight exceeding this values will be deleted.
        public  float MAX_WRITING = (float)2.5 * 50;
        // Style of "unprocessed stroke" or right dragging stroke.

        // can not do this, modify opacity of UNPROCESSED_COLOR will modify the static resource for the whole application
        // private SolidColorBrush UNPROCESSED_COLOR = Application.Current.Resources["Button_Background"] as SolidColorBrush;
        private SolidColorBrush UNPROCESSED_COLOR = Application.Current.Resources["WORD_DARK"] as SolidColorBrush;
        private float UNPROCESSED_OPACITY = 0.2f;
        private int UNPROCESSED_THICKNESS = 20;
        private int UNPROCESSED_RESOLUTION = 5;
        private Color DEFAULT_STROKE_COLOR = Colors.Black;
        private Color SELECTED_STROKE_COLOR = (Color) Application.Current.Resources["WORD_DARK_COLOR"];
        public float PAGE_HEIGHT = 1980;
        public float PAGE_WIDTH = 1400;

        private DoubleCollection UNPROCESSED_DASH = new DoubleCollection() { 5, 2 };
        private Rect boundingRect;
        //TranslateTransform selectionRectangleTranform;
        private Polyline lasso;
        Symbol LassoSelect = (Symbol)0xEF20;
        Symbol TouchWriting = (Symbol)0xED5F;
        private bool isBoundRect;
        InkAnalyzer inkAnalyzer;
        InkAnalyzer inkOperationAnalyzer;
        InkAnalysisParagraph paragraphSelected;
        List<InkAnalysisInkWord> wordListSelected;
        object selectDeleteLock = new object();

        //Semaphore used for auto-savings
        SemaphoreSlim autosaveSemaphore = new SemaphoreSlim(1);

        DispatcherTimer dispatcherTimer;
        DispatcherTimer operationDispathcerTimer; //try to recognize last stroke as operation
        DispatcherTimer unprocessedDispatcherTimer;
        DispatcherTimer textNoteDispatcherTimer;
        DispatcherTimer autosaveDispatcherTimer; //For stroke auto saves
        DispatcherTimer lineAnalysisDispatcherTimer;

        Queue<int> linesToUpdate;
        object lineUpdateLock = new object();
        Dictionary<int, string> stringsOfLines;
        Dictionary<int, List<Phenotype>> phenotypesOfLines;
        public ObservableCollection<Phenotype> curLineCandidatePheno = new ObservableCollection<Phenotype>();

        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();
        public ObservableCollection<Phenotype> recognizedPhenotypes = new ObservableCollection<Phenotype>();
        public ObservableCollection<HWRRecognizedText> recognizedText = new ObservableCollection<HWRRecognizedText>();

        private MainPage rootPage;
        //private string[] textLines;
        CoreInkIndependentInputSource core;

        private bool leftLasso = false;

        Queue<uint> linesToAnnotate = new Queue<uint>();
        int lastStrokeLine = -1;
        List<string> curLineWords = new List<string>();

        private static ILogger logger =MetroLogger.getSharedLogger();

        public InkCanvas inkCan
        {
            get
            {
                return inkCanvas;
            }
        }
        private IReadOnlyList<InkStroke> leftLossoStroke;
        public List<AddInImageControl> imagesWithAnnotations
        {
            get
            {
                List<AddInImageControl> controls = new List<AddInImageControl>();
                foreach( var cd in this.userControlCanvas.Children)
                {
                    controls.Add((AddInImageControl)cd);
                }
                return controls;
            }
        }

        private Dictionary<int, List<TextBox>> recognizedTextBlocks = new Dictionary<int, List<TextBox>>();
        private Dictionary<uint, NoteLine> idToNoteLine = new Dictionary<uint, NoteLine>();
        private uint showingResultOfLine;
        private InkAnalysisLine curLineObject;

        private int showAlterOfWord = -1;

        private Dictionary<string, Phenotype> cachedAnnotation = new Dictionary<string, Phenotype>();
        private HashSet<int> annotatedLines = new HashSet<int>();
        int hoveringLine = -1;

        int iWordForCandidateSelection = 0;
        private SemaphoreSlim deleteSemaphoreSlim;
        private SemaphoreSlim selectAndRecognizeSemaphoreSlim;

        private Dictionary<int, Rectangle> lineToRect = new Dictionary<int, Rectangle>();

        private InkDrawingAttributes drawingAttributesBackUp;
        Dictionary<string, List<Phenotype>> oldAnnotations = new Dictionary<string, List<Phenotype>>();
        Dictionary<TextBox, List<string>> textBlockToAlternatives = new Dictionary<TextBox, List<string>>();
        /*************************END OF CLASS PROPERTIES*************************************/
        #endregion



        /// <summary>
        /// Initializes a new note page controller instance given notebook id and notepage id.
        /// </summary>
        public NotePageControl(string notebookid,string pageid)
        {  
            rootPage = MainPage.Current;
            

            this.InitializeComponent();

            this.Visibility = Visibility.Collapsed;

            this.DrawBackgroundLines();

            this.notebookId = notebookid;
            this.pageId = pageid;
            

            UNPROCESSED_COLOR = new SolidColorBrush(UNPROCESSED_COLOR.Color);
            UNPROCESSED_COLOR.Opacity = UNPROCESSED_OPACITY;

            // Initialize the InkCanvas
            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;

            // Handlers to clear the selection when inking or erasing is detected
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollectedAsync;
    
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
           
            inkCanvas.Tapped += InkCanvas_Tapped;
            inkCanvas.DoubleTapped += InkCanvas_DoubleTapped;
            //CoreInkIndependentInputSource core = CoreInkIndependentInputSource.Create(inkCanvas.InkPresenter);
            //core.PointerMoving += InkCanvas_PointerMoving;
            

            inkAnalyzer = new InkAnalyzer();
            inkOperationAnalyzer = new InkAnalyzer();
            paragraphSelected = null;
            wordListSelected = new List<InkAnalysisInkWord>();

            dispatcherTimer = new DispatcherTimer();
            operationDispathcerTimer = new DispatcherTimer();
            textNoteDispatcherTimer = new DispatcherTimer();
            autosaveDispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += InkAnalysisDispatcherTimer_Tick;  // Ink Analysis time tick
            autosaveDispatcherTimer.Tick += on_stroke_changed; // When timer ticks after user input, auto saves stroke
            operationDispathcerTimer.Tick += OperationDispatcherTimer_Tick;
            textNoteDispatcherTimer.Tick += TextNoteDispatcherTimer_Tick;

            unprocessedDispatcherTimer = new DispatcherTimer();
            unprocessedDispatcherTimer.Tick += UnprocessedDispathcerTimer_Tick;
            

            // We perform analysis when there has been a change to the
            // ink presenter and the user has been idle for 1 second.
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            textNoteDispatcherTimer.Interval = TimeSpan.FromSeconds(0.1);
            operationDispathcerTimer.Interval = TimeSpan.FromMilliseconds(500);
            unprocessedDispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(1); //setting stroke auto save interval to be 1 sec

            linesToUpdate = new Queue<int>();
            lineAnalysisDispatcherTimer = new DispatcherTimer();
            lineAnalysisDispatcherTimer.Tick += LineAnalysisDispatcherTimer_Tick;
            lineAnalysisDispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            //lineAnalysisDispatcherTimer.Start();

            // hovering event
            // core = CoreInkIndependentInputSource.Create(inkCanvas.InkPresenter);
            // core.PointerHovering += Core_PointerHovering;
            // core.PointerExiting += Core_PointerExiting;
            // core.PointerEntering += Core_PointerHovering;
            //Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerEntered += App_PointerMoved;

            stringsOfLines = new Dictionary<int, string>();
            phenotypesOfLines = new Dictionary<int, List<Phenotype>>();
            deleteSemaphoreSlim = new SemaphoreSlim(1);
            selectAndRecognizeSemaphoreSlim = new SemaphoreSlim(1);
            
            recognizedTextCanvas.Visibility = Visibility.Collapsed;

            this.SizeChanged += NotePageControl_SizeChanged;

            // textNoteEditBox.SetValue(Paragraph.LineHeightProperty, 20);
            //textEditGrid.Visibility = Visibility.Collapsed;
            
            var format = textNoteEditBox.Document.GetDefaultParagraphFormat();
            textNoteEditBox.FontSize = 22;
            format.SetLineSpacing(LineSpacingRule.Exactly, 33.7f);
            textNoteEditBox.Document.SetDefaultParagraphFormat(format);

            

            // disable moving strokes for now
            //selectionRectangle.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            //selectionRectangle.ManipulationStarted += SelectionRectangle_ManipulationStarted;
            //selectionRectangle.ManipulationDelta += SelectionRectangle_ManipulationDelta;
            //selectionRectangle.ManipulationCompleted += SelectionRectangle_ManipulationCompleted;      

        }


        #region UI Display
        // ============================== UI DISPLAYS HANDLER ==============================================//
        // draw background lines for notes
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
        private void DropShadowOf(StackPanel uie)
        {
            // Drop shadow of current line result panel
            var contentVisual = ElementCompositionPreview.GetElementVisual(uie);
            var _compositor = contentVisual.Compositor;

            var sprite = _compositor.CreateSpriteVisual();
            sprite.Size = new Vector2((float)uie.ActualWidth, (float)uie.ActualHeight);
            sprite.CenterPoint = contentVisual.CenterPoint;

            var shadow = _compositor.CreateDropShadow();
            sprite.Shadow = shadow;
            shadow.BlurRadius = 15;
            shadow.Offset = new Vector3(0, 0, 0);
            shadow.Color = Colors.DarkGray;
            ElementCompositionPreview.SetElementChildVisual(uie, sprite);
        }

        // page control size change event
        private void NotePageControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawBackgroundLines();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines

            DrawBackgroundLines();
            scrollViewer.ChangeView(null, 100, null, true);
            sideScrollView.ChangeView(null, 100, null, true);
        }
       
        // Left button lasso control
        
        public void enableLeftButtonLasso() {
            drawingAttributesBackUp = inkCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            var temp = inkCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            temp.Size = new Size(0, 0);
            temp.Color = (Color) Application.Current.Resources["Button_Background_Color"];
            inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(temp);
            leftLasso = true;

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_PointerPressed;
            inkCanvas.InkPresenter.StrokeInput.StrokeContinued += StrokeInput_PointerMoved;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_PointerReleased;

        }
        public void disableLeftButtonLasso()
        {
            ClearLeftLassoStroke();
            if(drawingAttributesBackUp!=null)
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributesBackUp);
            leftLasso = false;

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted -= StrokeInput_PointerPressed;
            inkCanvas.InkPresenter.StrokeInput.StrokeContinued -= StrokeInput_PointerMoved;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded -= StrokeInput_PointerReleased;

        }

        public void enableUnprocessedInput()
        {
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
        }
        private async void DrawCanvas_PointerExitingAsync(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //rp.BorderThickness = new Thickness(1);
            });
        }

        private async void DrawCanvas_PointerEnteringAsync(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //rp.BorderThickness = new Thickness(4);
            });
        }

        private void DrawCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            //rp.BorderThickness = new Thickness(1);
        }

        private void DrawCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            //rp.BorderThickness = new Thickness(4);
        }

        private void DrawCanvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var can = (InkCanvas)sender;
            var panel = (RelativePanel)can.Parent;
            Canvas.SetLeft(panel, Canvas.GetLeft(panel) + e.Delta.Translation.X);
            Canvas.SetTop(panel, Canvas.GetTop(panel) + e.Delta.Translation.Y);
        }


        #endregion


        #region Typing mode
        //============================= TYPING MODE ===================================/
        public void setTextNoteEditBox()
        {

        }

        // FIXME
        public void showTextEditGrid()
        {
            textEditGrid.Visibility = Visibility.Visible;
            //textEditGrid.SetValue(Canvas.ZIndexProperty, 2);
            //inkCanvas.SetValue(Canvas.ZIndexProperty, 1);
            textNoteEditBox.IsReadOnly = false;
            textNoteEditBox.IsTapEnabled = false;
        }

        public void hideTextEditGrid()
        {
            textEditGrid.Visibility = Visibility.Collapsed;
            //textEditGrid.SetValue(Canvas.ZIndexProperty, 1);
            //inkCanvas.SetValue(Canvas.ZIndexProperty, 2);
            textNoteEditBox.IsReadOnly = true;
            textNoteEditBox.IsTapEnabled = false;
        }

        #endregion

        #region View mode
        //============================= REVIEW MODE ===================================/
        public void showRecognizedTextCanvas()
        {
            recognizedTextCanvas.Visibility = Visibility.Visible;
            inkCanvas.Visibility = Visibility.Collapsed;
            backgroundCanvas.Background = new SolidColorBrush(Colors.WhiteSmoke);
        }

        public void hideRecognizedTextCanvas()
        {
            recognizedTextCanvas.Visibility = Visibility.Collapsed;
            inkCanvas.Visibility = Visibility.Visible;
            backgroundCanvas.Background = new SolidColorBrush(Colors.White);
        }

        private async void Core_PointerExiting(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.showRecognizedTextCanvas();
            });
            /**
            var x = args.CurrentPoint.Position.X;
            var y = args.CurrentPoint.Position.Y;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var ttv = HoverPhenoPanel.TransformToVisual(Window.Current.Content);
                Point point = ttv.TransformPoint(new Point(0, 0));
                point.X = point.X - HoverPhenoPanel.ActualWidth;
                var xFlag = x >= point.X-5 && x <= point.X + HoverPhenoPanel.ActualWidth+5;
                
                var yFlag = y >= point.Y-5 && y <= point.Y + HoverPhenoPanel.ActualHeight+5;
               
                if (!(xFlag && yFlag))
                    HoverPhenoPanel.Visibility = Visibility.Collapsed;
            });
            **/
        }     
        private async void Core_PointerHovering(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.hideRecognizedTextCanvas();
            });

            /**
            int curLine = (int)args.CurrentPoint.Position.Y / (int)LINE_HEIGHT;
            if (curLine != hoveringLine)
            {
                hoveringLine = curLine;
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        recognizeLine(hoveringLine);
                    });
            }
            **/
        }
        #endregion

        #region Hand writting mode 
        // ==================================== Handwriting mode ===================================================/

        private async void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            ClearSelectionAsync();

            curLineResultPanel.Visibility = Visibility.Collapsed;

            dispatcherTimer.Stop();
            autosaveDispatcherTimer.Start();

            //operationDispathcerTimer.Stop();
            foreach (var stroke in args.Strokes)
            {
                inkAnalyzer.RemoveDataForStroke(stroke.Id);
            }
            //operationDispathcerTimer.Start();
            await analyzeInk();
        }
        //stroke input handling: a stroke input has started
        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            if (!leftLasso)
            {
                //ClearSelection();

                // dispatcherTimer.Stop();
                //operationDispathcerTimer.Stop();
                inkOperationAnalyzer.ClearDataForAllStrokes();
                autosaveDispatcherTimer.Stop();
                /***
                core.PointerHovering -= Core_PointerHovering;
                core.PointerExiting -= Core_PointerExiting;
                core.PointerEntering -= Core_PointerHovering;
                ***/
            }
        }
        //stroke input handling: a stroke input has stopped
        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            autosaveDispatcherTimer.Start();
        }
     
        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {
                // dispatcherTimer.Stop();
                operationDispathcerTimer.Stop();

                
                foreach (var s in args.Strokes)
                {
                    if (s.BoundingRect.Height > MAX_WRITING)
                    {
                        inkOperationAnalyzer.AddDataForStroke(s);
                        try
                        {
                            await RecognizeInkOperation();
                        }
                        catch (Exception) { }
                    }
                    else
                    {
                        inkAnalyzer.AddDataForStroke(s);
                        inkAnalyzer.SetStrokeDataKind(s.Id, InkAnalysisStrokeKind.Writing);
                        await analyzeInk(s);
                    }
                }
                // recognize by line
                /**
                if (lastStroke != null)
                {
                    int lineIndex = getLineNumByRect(lastStroke.BoundingRect);
                    if (lineIndex != lastStrokeLine || linesToAnnotate.Count == 0)
                    {
                        linesToAnnotate.Enqueue(lineIndex);
                        lastStrokeLine = lineIndex;
                    }
                }
                **/

                // start to analyze ink anaylsis on collected strokes.
                // dispatcherTimer.Start();

                //inkAnalyzer.AddDataForStrokes(args.Strokes);
                
            }
            else
            {
                leftLossoStroke = args.Strokes;
                foreach (var s in args.Strokes)
                {
                    //inkCanvas.InkPresenter.StrokeContainer.
                }
            }

        }

        // stroke input handling: mouse pointer pressed
        private void StrokeInput_PointerPressed(InkStrokeInput sender, PointerEventArgs args)
        {
            UnprocessedInput_PointerPressed(null, args);
            //autosaveDispatcherTimer.Stop();
        }
        // stroke input handling: mouse pointer moved
        private void StrokeInput_PointerMoved(InkStrokeInput sender, PointerEventArgs args)
        {
            UnprocessedInput_PointerMoved(null, args);
        }
        // stroke input handling: mouse pointer released
        private void StrokeInput_PointerReleased(InkStrokeInput sender, PointerEventArgs args)
        {
            UnprocessedInput_PointerReleased(null, args);
            //autosaveDispatcherTimer.Start();
        }

        // select strokes by "marking" handling: pointer pressed
        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            ClearSelectionAsync();
            lasso = new Polyline()
            {
                Stroke = UNPROCESSED_COLOR,
                StrokeThickness = UNPROCESSED_THICKNESS,
                //StrokeDashArray = UNPROCESSED_DASH,
            };

            lasso.Points.Add(args.CurrentPoint.RawPosition);
            selectionCanvas.Children.Add(lasso);
            isBoundRect = true;
            unprocessedDispatcherTimer.Start();
        }
        // select strokes by "marking" handling: pointer moved
        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (isBoundRect)
            {
                lasso.Points.Add(args.CurrentPoint.RawPosition);

            }
        }
        // select strokes by "marking" handling: pointer released
        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            unprocessedDispatcherTimer.Stop();
            lasso.Points.Add(args.CurrentPoint.RawPosition);
            //lasso.Points.Add(lasso.Points.ElementAt(0));
            isBoundRect = false;
            RecognizeSelection();
            /**
            if (lasso.Points.Count() < 20)
            {
                TapAPosition(lasso.Points.ElementAt(0));
            }
            else
            {
                boundingRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(lasso.Points);
                if (boundingRect.Equals(new Rect(0, 0, 0, 0)))
                {
                    boundingRect = Rect.Empty;
                    selectionCanvas.Children.Clear();
                    SelectByUnderLine(new Rect(
                        new Point(lasso.Points.Min(p => p.X), lasso.Points.Min(p => p.Y)),
                        new Point(lasso.Points.Max(p => p.X), lasso.Points.Max(p => p.Y))
                        ));
                }
                else
                {
                    foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    {
                        if(s.Selected == true)
                            SetSelectedStrokeStyle(s);
                    }
                }
                isBoundRect = false;
                DrawBoundingRect();
            }
            **/
        }
        public void SelectByUnderLine(Rect strokeRect)
        {
            var xFrom = strokeRect.Left;
            var xTo = strokeRect.Right;
            //var y = (strokeRect.Top + strokeRect.Bottom) / 2
            var y = strokeRect.Top;
            /****
            var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
            foreach (var word in words)
            {
                if (y > word.BoundingRect.Top && y <= word.BoundingRect.Bottom + LINE_HEIGHT * 0.6)
                {
                        if (xFrom < word.BoundingRect.Right && xTo > word.BoundingRect.Left)
                        {
                            boundingRect.Union(word.BoundingRect);
                            wordListSelected.Add((InkAnalysisInkWord)word); // record the words selected, for clear selection purpose
                            IReadOnlyList<uint> strokeIds = word.GetStrokeIds();
                            foreach (var sid in strokeIds)
                            {
                                var s = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid);
                                s.Selected = true;
                                SetSelectedStrokeStyle(s);
                            }
                        }
                }
            }
            ****/
            Polyline slasso = new Polyline();
            double dis = (strokeRect.Y + strokeRect.Height / 2.0) % LINE_HEIGHT;
            int lineNum = (int)((strokeRect.Y + strokeRect.Height / 2.0) / LINE_HEIGHT);
            if (dis > LINE_HEIGHT * 0.75)
                lineNum++;
            var yFrom = (lineNum - 1) * LINE_HEIGHT - 0.2 * LINE_HEIGHT;
            var yTo = lineNum * LINE_HEIGHT + 1.0 * LINE_HEIGHT;
            slasso.Points.Add(new Point(xFrom, yFrom));
            slasso.Points.Add(new Point(xFrom, yTo));
            slasso.Points.Add(new Point(xTo, yTo));
            slasso.Points.Add(new Point(xTo, yFrom));
            slasso.Points.Add(new Point(xFrom, yFrom));
            Rect selectRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(slasso.Points);
            if (!selectRect.Equals(Rect.Empty))
            {
                foreach (var ss in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    if (ss.Selected)
                    {
                        if (ss.BoundingRect.Top > lineNum * LINE_HEIGHT || ss.BoundingRect.Bottom < (lineNum - 1) * LINE_HEIGHT)
                        {
                            ss.Selected = false;
                        }
                        else
                        {
                            boundingRect.Union(ss.BoundingRect);
                            SetSelectedStrokeStyle(ss);
                        }
                    }
            }

            /**
            var paragraphs = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Paragraph);
            foreach (var para in paragraphs)
            {
                if (y > para.BoundingRect.Top && y <= para.BoundingRect.Bottom + LINE_HEIGHT*0.4)
                {
                    foreach (var line in para.Children)
                    {
                        if (y > line.BoundingRect.Top && y <= line.BoundingRect.Bottom + LINE_HEIGHT*0.4)
                        {
                            foreach (var word in line.Children)
                            {
                                if (xFrom < word.BoundingRect.Right && xTo > word.BoundingRect.Left)
                                {
                                    boundingRect.Union(word.BoundingRect);
                                    wordListSelected.Add((InkAnalysisInkWord) word); // record the words selected, for clear selection purpose
                                    IReadOnlyList<uint> strokeIds = word.GetStrokeIds();
                                    foreach (var sid in strokeIds)
                                    {
                                        var s = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid);
                                        s.Selected = true;
                                        SetSelectedStrokeStyle(s);
                                    }
                                }
                            }
                        }
                    }
                }
            }**/
        }

        private void SetSelectedStrokeStyle(InkStroke stroke)
        {
            var drawingAttributes = stroke.DrawingAttributes;
            drawingAttributes.Color = SELECTED_STROKE_COLOR;
            stroke.DrawingAttributes = drawingAttributes;
        }

        private void SetDefaultStrokeStyle(InkStroke stroke)
        {
            var drawingAttributes = stroke.DrawingAttributes;
            drawingAttributes.Color = DEFAULT_STROKE_COLOR;
            stroke.DrawingAttributes = drawingAttributes;
        }

        #endregion


        private void SelectionRectangle_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Enabled;
            scrollViewer.VerticalScrollMode = ScrollMode.Enabled;

            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
                if (stroke.Selected)
                {
                    inkAnalyzer.ReplaceDataForStroke(stroke);
                }
                    
            dispatcherTimer.Start();
        }

        private void SelectionRectangle_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            dispatcherTimer.Stop();
        }
        
        private void SelectionRectangle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // Move the rectangle.
            /***
            selectionRectangleTranform.X += e.Delta.Translation.X;
            selectionRectangleTranform.Y += e.Delta.Translation.Y;
            
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
                if (stroke.Selected)
                {
                    
                        stroke.PointTransform = Matrix3x2.CreateTranslation(
                        (float)selectionRectangleTranform.X,
                        (float)selectionRectangleTranform.Y
                        );
                    
                }
              ***/
        }

        public void PhenotypeSelectionFlyoutClosed(object sender, object o)
        {
            ClearSelectionAsync();
            candidateListView.Visibility = Visibility.Collapsed;
            recognizedPhenoListView.Visibility = Visibility.Collapsed;
            recognizedResultTextBlock.SelectionChanged += recognizedResultTextBlock_SelectionChanged;
            candidateListView.ItemsSource = new List<string>();

        }
        
        private void ClearSelectionAsync()
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
            {
                stroke.Selected = false;
                SetDefaultStrokeStyle(stroke);
            }
            
            ClearDrawnBoundingRect();
            
        }

        private void ClearDrawnBoundingRect()
        {

            if (leftLasso)
            {
                ClearLeftLassoStroke();
            }
            if (selectionCanvas.Children.Count > 0)
            {
                selectionCanvas.Children.Clear();
                breifPhenoProgressBar.Visibility = Visibility.Visible;
                if(phenoProgressRing != null)
                    phenoProgressRing.Visibility = Visibility.Visible;
                recognizedPhenoBriefListView.Visibility = Visibility.Collapsed;
                boundingRect = Rect.Empty;
            }
            curLineResultPanel.Visibility = Visibility.Collapsed;
        }

        private void ClearLeftLassoStroke()
        {
            if (leftLossoStroke != null)
            {
                foreach (var ss in leftLossoStroke)
                {
                    ss.Selected = true;
                }
                inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                leftLossoStroke = null;
            }
        }

        #region add-in controls 
        // ============================== ADD-INS Images/Annotations ==============================================//

        public async Task<List<AddInControl>> GetAllAddInControls()
        {
            List<AddInControl> cons = new List<AddInControl>();
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                foreach (var c in this.userControlCanvas.Children)
                {
                    if (c.GetType() == typeof(AddInControl))
                    {
                        cons.Add(c as AddInControl);
                    }
                }
            }
            );
            return cons;
        }

        public async Task<List<ImageAndAnnotation>> GetAllAddInObjects()
        {
            List<ImageAndAnnotation> olist = new List<ImageAndAnnotation>();
            List<AddInControl> addinlist = await GetAllAddInControls();
            foreach(var con in addinlist)
            {
                ImageAndAnnotation temp = new ImageAndAnnotation(con.name, notebookId, pageId, con.canvasLeft, con.canvasTop,
                                                                      con.transX, con.transY, con.transScale, con.ActualWidth, con.ActualHeight);
                olist.Add(temp);
            }

            return olist;
        }

        public async void addImageAndAnnotationControlFromBitmapImage(string imageString)
        {
            try
            {
                var byteArray = Convert.FromBase64String(imageString);
                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    var ras = stream.AsRandomAccessStream();
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, ras);
                    var provider = await decoder.GetPixelDataAsync();
                    byte[] buffer = provider.DetachPixelData();
                    WriteableBitmap bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                    await bitmap.PixelBuffer.AsStream().WriteAsync(buffer, 0, buffer.Length);
                    addImageAndAnnotationControl(FileManager.getSharedFileManager().CreateUniqueName(), 50, 50, false, bitmap);
                }
            }
            catch (Exception e)
            {
                logger.Error("Failed to add image control from BitmapImage: " + e.Message);
                Debug.WriteLine(e.Message);
            }
           
        }

        public void addImageAndAnnotationControl(string name, double left, double top, bool loadFromDisk, WriteableBitmap wb = null, 
                                                    double transX = 0, double transY = 0, double transScale = 0, double width = -1, double height = -1)
        {
            AddInControl canvasAddIn = new AddInControl(name, notebookId, pageId, width, height);
            //canvasAddIn.Width = 400; //stroke.BoundingRect.Width;
            //canvasAddIn.Height = 400;  //stroke.BoundingRect.Height;
            canvasAddIn.canvasLeft = left;
            canvasAddIn.canvasTop = top;
            Canvas.SetLeft(canvasAddIn, left);
            Canvas.SetTop(canvasAddIn, top);
            userControlCanvas.Children.Add(canvasAddIn);

            if (loadFromDisk)
                canvasAddIn.InitializeFromDisk(false, transX, transY, transScale);

            if (wb != null)
                canvasAddIn.InitializeFromImage(wb);





           
        }

        public void AddImageControl(string imagename, SoftwareBitmapSource source)
        {
            AddInImageControl imageControl = new AddInImageControl(notebookId, pageId, imagename);
            imageControl.Height = 300;
            imageControl.Width = 400;
            Canvas.SetLeft(imageControl, 0);
            Canvas.SetTop(imageControl, 500);
            imageControl.height = imageControl.Height;
            imageControl.width = imageControl.Width;
            imageControl.canvasLeft = Canvas.GetLeft(imageControl);
            imageControl.canvasTop = Canvas.GetTop(imageControl);

            imageControl.getImageControl().Source = source;
            imageControl.CanDrag = true;
            imageControl.ManipulationMode = ManipulationModes.All;
            imageControl.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
            {
                if (args.Delta.Expansion == 0)
                {
                    Canvas.SetLeft(imageControl, Canvas.GetLeft(imageControl) + args.Delta.Translation.X);
                    Canvas.SetTop(imageControl, Canvas.GetTop(imageControl) + args.Delta.Translation.Y);
                    imageControl.canvasLeft = Canvas.GetLeft(imageControl);
                    imageControl.canvasTop = Canvas.GetTop(imageControl);
                }
                else
                {
                    imageControl.Width += args.Delta.Translation.X * 2;
                    imageControl.Height += args.Delta.Translation.Y * 2;
                    imageControl.height = imageControl.Height;
                    imageControl.width = imageControl.Width;
                }
            };
            userControlCanvas.Children.Add(imageControl);
            /**
            CameraCaptureUI captureUI = new CameraCaptureUI();
            captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            captureUI.PhotoSettings.CroppedSizeInPixels = new Size(300, 300);
            captureUI.PhotoSettings.AllowCropping = false;
            StorageFile photo = await captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo);

            if (photo == null)
            {
                // User cancelled photo capture
                return;
            }
            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            // Fall back to the local app storage if the Pictures Library is not available
            StorageFolder destinationFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;
            await photo.CopyAsync(destinationFolder, "ProfilePhoto.jpg", NameCollisionOption.ReplaceExisting);

            IRandomAccessStream stream = await photo.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

            SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
            await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
            //Image imageControl = new Image();
            //imageControl.Height = 300;
            //imageControl.Width = 300;
            //Canvas.SetLeft(imageControl, selectionCanvas.Width/2 - imageControl.Width/2);
            //Canvas.SetTop(imageControl, selectionCanvas.Height / 2 - imageControl.Height / 2);
            imageControl.Source = bitmapSource;
            //selectionCanvas.Children.Add(imageControl);
            await photo.DeleteAsync();
    **/
        }
        #endregion



        #region Timer Event Handlers
        //==============================TIMER EVENT HANDLERS ========================================//

        /// <summary>
        /// Recognized from type notes line by line
        /// </summary>
        private async void TextNoteDispatcherTimer_Tick(object sender, object e)
        {
            textNoteDispatcherTimer.Stop();
            //int cursorPosi = textNoteEditBox.Document.Selection.StartPosition;
            string text = String.Empty;
            textNoteEditBox.Document.GetText(TextGetOptions.None, out text);
            string[] lines = text.Split("\r\n".ToArray());
            /**int ind = 0;
            int i = 0;
            for (; i < lines.Count(); ++i)
            {
                ind += lines[i].Length;
                if (ind >= cursorPosi)
                    break;
            }**/
            foreach (string str in lines)
            {
                if (str != "")
                {
                    // we already annotate this string: str
                    // but it may come from another line
                    if (oldAnnotations.Keys.Contains(str))
                    {
                        /**
                        List<Phenotype> ps = oldAnnotations[str];
                        foreach (Phenotype p in ps)
                        {
                            p.sourceType = SourceType.Notes;
                            PhenoMana.addPhenotypeCandidate(p, SourceType.Notes);
                        }
                        **/
                        continue;
                    }

                    string temp = oldAnnotations.Keys.Where(x => str.IndexOf(x) == 0).FirstOrDefault();

                    if (temp != null)
                    {
                        oldAnnotations.Remove(temp);
                    }

                    var results = await PhenoMana.annotateByNCRAsync(str);
                    if (results == null)
                        return;

                    List<Phenotype> phenos = new List<Phenotype>();
                    foreach (var key in results.Keys)
                    {
                        Phenotype p = results[key];
                        p.sourceType = SourceType.Notes;
                        PhenoMana.addPhenotypeCandidate(p, SourceType.Notes);
                        phenos.Add(p);
                    }
                    if (!oldAnnotations.Keys.Contains(str))
                        oldAnnotations.Add(str, phenos);

                }
            }
        }

        private void UnprocessedDispathcerTimer_Tick(object sender, object e)
        {
            unprocessedDispatcherTimer.Stop();
            if (lasso.Points.Count() > 20)
            {
                //Rect bRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(lasso.Points);
                
                //if (bRect.Equals(new Rect(0, 0, 0, 0)))
                {
                    for (int i = UNPROCESSED_RESOLUTION; i < lasso.Points.Count; i+= UNPROCESSED_RESOLUTION)
                    {
                        //Rect bRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithLine(
                        SelectWithLineWithThickness(
                            lasso.Points[i- UNPROCESSED_RESOLUTION],
                            lasso.Points[i],
                            UNPROCESSED_THICKNESS / 2
                            );
                    }
                    //selectionCanvas.Children.Clear();
                    /**SelectByUnderLine(new Rect(
                        new Point(lasso.Points.Min(p => p.X), lasso.Points.Min(p => p.Y)),
                        new Point(lasso.Points.Max(p => p.X), lasso.Points.Max(p => p.Y))
                        ));**/
                }
                
                //else
                {
                    foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    {
                        if (s.Selected == true)
                            SetSelectedStrokeStyle(s);
                    }
                }
                
            }
            unprocessedDispatcherTimer.Start();
        }

        private async void OperationDispatcherTimer_Tick(object sender, object e)
        {
            operationDispathcerTimer.Stop();
            if (!inkOperationAnalyzer.IsAnalyzing)
            {
                var result = await inkOperationAnalyzer.AnalyzeAsync();

                if (result.Status == InkAnalysisStatus.Updated)
                {
                    var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                    foreach(InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                    {
                        if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                        {
                                foreach (var dstroke in drawNode.GetStrokeIds())
                                {
                                    var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                                    AddInControl canvasAddIn = new AddInControl(FileService.FileManager.getSharedFileManager().CreateUniqueName(), notebookId, pageId);
                                    canvasAddIn.Width = stroke.BoundingRect.Width;
                                    canvasAddIn.Height = stroke.BoundingRect.Height;
                                    Canvas.SetLeft(canvasAddIn, stroke.BoundingRect.X);
                                    Canvas.SetTop(canvasAddIn, stroke.BoundingRect.Y);
                                    canvasAddIn.CanDrag = true;
                                    canvasAddIn.ManipulationMode = ManipulationModes.All;
                                    canvasAddIn.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
                                    {
                                        if (args.Delta.Expansion == 0)
                                        {
                                            Canvas.SetLeft(canvasAddIn, Canvas.GetLeft(canvasAddIn) + args.Delta.Translation.X);
                                            Canvas.SetTop(canvasAddIn, Canvas.GetTop(canvasAddIn) + args.Delta.Translation.Y);
                                        }
                                        else
                                        {
                                            canvasAddIn.Width += args.Delta.Translation.X * 2;
                                            canvasAddIn.Height += args.Delta.Translation.Y * 2;
                                        }
                                        /**
                                        if (Math.Abs(args.Delta.Translation.X) > Math.Abs(args.Delta.Translation.Y))
                                        {
                                            //canvasAddIn.Width += args.Delta.Expansion;
                                            Debug.WriteLine(args.Delta.Scale);
                                            canvasAddIn.Width *= args.Delta.Scale;
                                        }
                                        else
                                        {
                                            //canvasAddIn.Height += args.Delta.Expansion;
                                            canvasAddIn.Height *= args.Delta.Scale;
                                        }
                                        **/
                                    };


                                    userControlCanvas.Children.Add(canvasAddIn);
                                    stroke.Selected = true;
                                }
                                inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
                                inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                            

                        }
                        if (drawNode.DrawingKind == InkAnalysisDrawingKind.Drawing)
                        { // straight line for annotation
                            //var lineRect = node.BoundingRect;

                        }
                        if (drawNode.DrawingKind == InkAnalysisDrawingKind.Ellipse || drawNode.DrawingKind == InkAnalysisDrawingKind.Circle)
                        {
                            //Debug.WriteLine("Circle!");
                        }
                    }
                    foreach (var sid in inkOperationAnalyzer.AnalysisRoot.GetStrokeIds())
                    {
                        inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                        inkOperationAnalyzer.RemoveDataForStroke(sid);
                    }
                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                }
                else
                {
                    operationDispathcerTimer.Start();
                }
            }
        }

        /// <summary>
        /// Called after strokes are collected to recoginze words and shapes
        /// </summary>
        private async void InkAnalysisDispatcherTimer_Tick(object sender, object e)
        {
            //await deleteSemaphoreSlim.WaitAsync();
            //try
            //{

            dispatcherTimer.Stop();
            await analyzeInk();
            //}
            // finally
            // {
            //   deleteSemaphoreSlim.Release();
            //}
        }

        /// <summary>
        /// Analyze the currently hovering line
        /// </summary>
        private void LineAnalysisDispatcherTimer_Tick(object sender, object e)
        {
            var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
            var x = pointerPosition.X - Window.Current.Bounds.X;
            var y = pointerPosition.Y - Window.Current.Bounds.Y;
            int curLine = (int)y / (int)LINE_HEIGHT;
            if (curLine != hoveringLine)
            {
                hoveringLine = curLine;
                //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                //{
                recognizeLine(hoveringLine);
                //});
            }
        }

        #endregion

        #region Save Events
        //============================== SAVING EVENTS ================================================//

        /// <summary>
        /// Saves all strokes and add-ins of current notepage to local files, return false if failed.
        /// </summary>
        public async Task<bool> SaveToDisk()
        {
            try
            {
                bool result1 = false;
                StorageFile file = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.Strokes);
                if (file == null)
                {
                    logger.Error($"SaveToDisk():Failed to get note file.");
                    return false;
                }
                // save handwritings
                //result1 = await FileManager.getSharedFileManager().saveStrokes(file, this.inkCan);
                result1 = await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, pageId, this);


                // save add in controls
                var flag = false;
                var result2 = true;
                List<AddInControl> addinlist = await GetAllAddInControls();
                foreach (var addin in addinlist)
                {
                    var strokesFile = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotation, addin.name);
                    flag = await FileManager.getSharedFileManager().saveStrokes(strokesFile, addin.inkCan);
                    if (!flag)
                    {
                        logger.Error($"note-{notebookId} at page {pageId}, {addin.name} failed to save.");
                        result2 = false;
                    }
                }

                // add in meta data
                var result3 = false;
                List<ImageAndAnnotation> imageList = await GetAllAddInObjects();
                string metapath = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                result3 = await FileManager.getSharedFileManager().SaveObjectSerilization(metapath, imageList, typeof(List<ImageAndAnnotation>));

                return result1 && result2 && result3;
            }
            catch (Exception e)
            {
                logger.Error($"Failed to save page {pageId} of notebook {notebookId}: {e.Message}");
                Debug.WriteLine(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Saves current page view as .jpg file.
        /// </summary>
        public async void printPage()
        {
            List<AddInControl> addinlist = await GetAllAddInControls();
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => {
                // hide control UI of all addincontrols
                foreach (var addin in addinlist)
                {
                    addin.hideControlUI();
                }
            });


            var bitmap = new RenderTargetBitmap();

            StorageFile file = await KnownFolders.PicturesLibrary.CreateFileAsync("note page.jpg",

            CreationCollisionOption.GenerateUniqueName);

            await bitmap.RenderAsync(outputGrid);

            var buffer = await bitmap.GetPixelsAsync();

            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))

            {

                var encod = await BitmapEncoder.CreateAsync(

                    BitmapEncoder.JpegEncoderId, stream);

                encod.SetPixelData(BitmapPixelFormat.Bgra8,

                    BitmapAlphaMode.Ignore,

                    (uint)bitmap.PixelWidth,

                    (uint)bitmap.PixelHeight,
                    500, 500,
                    //DisplayInformation.GetForCurrentView().LogicalDpi,

                    //DisplayInformation.GetForCurrentView().LogicalDpi,

                    buffer.ToArray()

                   );

                await encod.FlushAsync();

            }

            await Windows.System.Launcher.LaunchFileAsync(file);

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                // show control UI of all addincontrols
                foreach (var addin in addinlist)
                {
                    addin.showControlUI();
                }
            });

        }

        /// <summary>
        /// Saves a specific addin control and meta data to disk
        /// </summary>
        public async Task<bool> AutoSaveAddin(String name) {
            bool result1 = true;
            bool result2 = true;
            try {
                
                if (name != null)
                {
                    //finds the specific add-in control and save it to disk  
                    List<AddInControl> addinlist = await GetAllAddInControls();
                    AddInControl addin = addinlist.Where(x => x.name == name).ToArray()[0];
                    Debug.WriteLine($"found current addin to be saved {addin.name}");

                    var strokesFile = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotation, addin.name);
                    result1 = await FileManager.getSharedFileManager().saveStrokes(strokesFile, addin.inkCan);
                    if (!result1)
                        MetroLogger.getSharedLogger().Error($"Auto-save: note-{notebookId} at page {pageId}, {addin.name} failed to save.");
                }

                // add in meta data
                List<ImageAndAnnotation> imageList = await GetAllAddInObjects();
                string metapath = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                result2 = await FileManager.getSharedFileManager().SaveObjectSerilization(metapath, imageList, typeof(List<ImageAndAnnotation>));

                if (result1 && result2)
                    logger.Info($"Auto-saving add-in completed.");

                
            }
            catch (Exception e) {
                MetroLogger.getSharedLogger().Error($"Failed to auto-save addin: {e.Message}");
            }

            return result2 && result1;

        }

        /// <summary>
        /// Auto-saves current strokes on page after input with idle more than 1 second
        /// </summary>
        public async void on_stroke_changed(object sender = null, object e = null)
        {
            await autosaveSemaphore.WaitAsync();
            try
            {
                StorageFile file = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.Strokes);
                if (file == null)
                {
                    MetroLogger.getSharedLogger().Error($"Failed to get note file.");
                }
                // save handwritings
                await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, pageId, this);
                logger.Info("Autosaved current strokes.");

            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error($"Failed to auto-save strokes: {ex.Message}");
            }
            finally
            {
                autosaveSemaphore.Release();
                autosaveDispatcherTimer.Stop();
            }
        }

        #endregion

        // ============================== INK RECOGNITION ==============================================//



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
                List<HWRRecognizedText> recognitionResults = await HWRService.HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected);
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

        private async Task<int> RecognizeInkOperation()
        {
            var result = await inkOperationAnalyzer.AnalyzeAsync();

            if (result.Status == InkAnalysisStatus.Updated)
            {
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                {
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
                    {
                        foreach (var dstroke in drawNode.GetStrokeIds())
                        {
                            var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                            addImageAndAnnotationControl(FileManager.getSharedFileManager().CreateUniqueName(),
                                stroke.BoundingRect.X, stroke.BoundingRect.Y, false,
                                width: stroke.BoundingRect.Width, height: stroke.BoundingRect.Height);
                            /**
                            canvasAddIn.PointerEntered += delegate (object sender, PointerRoutedEventArgs e)
                            {
                                canvasAddIn.showControlPanel();
                            };
                            canvasAddIn.PointerExited += delegate (object sender, PointerRoutedEventArgs e)
                            {
                                canvasAddIn.hideControlPanel();
                            };
                            
                            canvasAddIn.CanDrag = true;
                            canvasAddIn.ManipulationMode = ManipulationModes.All;
                            canvasAddIn.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
                            {
                                if (args.Delta.Expansion == 0)
                                {
                                    Canvas.SetLeft(canvasAddIn, Canvas.GetLeft(canvasAddIn) + args.Delta.Translation.X);
                                    Canvas.SetTop(canvasAddIn, Canvas.GetTop(canvasAddIn) + args.Delta.Translation.Y);
                                }
                                else
                                {
                                    //canvasAddIn.Width += args.Delta.Translation.X * 2;
                                    //canvasAddIn.Height += args.Delta.Translation.Y * 2;
                                }
                            };
                            **/
                            stroke.Selected = true;
                        }
                        inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
                        inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();


                    }
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Drawing)
                    { // straight line for annotation
                      //var lineRect = node.BoundingRect;

                    }
                    if (drawNode.DrawingKind == InkAnalysisDrawingKind.Ellipse || drawNode.DrawingKind == InkAnalysisDrawingKind.Circle)
                    {
                        //Debug.WriteLine("Circle!");
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
        /// <param name="line">current line</param>
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

        // =============================== ANALYZE INKS ==============================================//

        public async Task StartAnalysisAfterLoad()
        {
            inkAnalyzer.AddDataForStrokes(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
            await inkAnalyzer.AnalyzeAsync();
            /**
            for (int i = 0; i < (PAGE_HEIGHT / LINE_HEIGHT); ++i) {
                linesToAnnotate.Enqueue(i);
            }
            dispatcherTimer.Start();
            **/
            //core.PointerHovering += Core_PointerHovering;
            //core.PointerExiting += Core_PointerExiting;
            //core.PointerEntering += Core_PointerHovering;
        }

        public async void initialAnalyze()
        {
            inkAnalyzer.AddDataForStrokes(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
            // dispatcherTimer.Start();

            bool result = false;
            while (!result)
            {
                result = await analyzeInk();
                await Task.Delay(1000);
            }
        }

        /// <summary>
        ///  Analyze ink strokes
        /// </summary>
        /// <param name="lastStroke">if this is not null, only focus on current line</param>
        /// <returns></returns>
        private async Task<bool> analyzeInk(InkStroke lastStroke = null)
        {
            logger.Info("Trying to analyze ink strokes of current page...");
            if (inkAnalyzer.IsAnalyzing)
            {
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
                        if (!idToNoteLine.ContainsKey(line.Id))
                        {
                            //new line
                            NoteLine nl = new NoteLine(line);
                            // hwr
                            var hwrresult = await RecognizeLine(line.Id);
                            nl.HwrResult = hwrresult;
                            idToNoteLine[line.Id] = nl;
                            Dictionary<string, Phenotype> annoResult = await PhenoMana.annotateByNCRAsync(idToNoteLine.GetValueOrDefault(line.Id).Text);
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

                                foreach(var pp in annoResult.Values)
                                {
                                    pp.sourceType = SourceType.Notes;
                                    PhenoMana.addPhenotypeCandidate(pp, SourceType.Notes);
                                }
                            }
                        }
                    }

                 
                }

                // this.textLines[paraLine] = ((InkAnalysisLine)line).RecognizedText;
                //this.setTextNoteEditBox();

                // remove shown textblocks of that line 
                /**
                if (recognizedTextBlocks.Keys.Contains(line.Id))
                {
                    foreach (TextBox tb in recognizedTextBlocks[line])
                    {
                        recognizedTextCanvas.Children.Remove(tb);
                    }
                }
                recognizedTextBlocks[line] = new List<TextBox>();
                string str = "";
                foreach (IInkAnalysisNode child in lineNode.Children)
                {
                    if(child.Kind == InkAnalysisNodeKind.InkWord)
                    {
                        str += ((InkAnalysisInkWord)child).RecognizedText + " ";
                        recognizedTextBlocks[line].Add(AddBoundingRectAndLabel(
                            line, 
                            child.BoundingRect, 
                            ((InkAnalysisInkWord)child).RecognizedText,
                            new List<String>(((InkAnalysisInkWord)child).TextAlternates))
                        );
                    }
                }**/
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
                scoreMatrix[0, ind] =0;
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
                    if(scoreMatrix[i-1, j] + gap_score > scoreMatrix[i, j])
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
            while ( i >= 0 && j >=0)
            {
                switch (tracebackMatrix[i, j])
                {
                    case -1:
                        newIndex.Insert(0, i-1);
                        oldIndex.Insert(0, -1); // gap
                        i--;
                        break;
                    case 1:
                        newIndex.Insert(0, -1); // gap
                        oldIndex.Insert(0, j-1);
                        j--;
                        break;
                    case 0:
                        newIndex.Insert(0, i-1);
                        oldIndex.Insert(0, j-1);
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

                if(curLineCandidatePheno.Count != 0)
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
            else {
                curWordPhenoControlGrid.Margin = new Thickness(0, 0, 0, 0);
                int lineNum = getLineNumByRect(line.BoundingRect);
                if (lineToRect.ContainsKey(lineNum)) {
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

        /**
        private void HoverPhenoPopupText_Tapped(object sender, TappedRoutedEventArgs e)
        {
            phenotypesOfLines[hoveringLine].ElementAt(0).state = 1;
            PhenotypeManager.getSharedPhenotypeManager().addPhenotype(phenotypesOfLines[hoveringLine].ElementAt(0), SourceType.Notes);
            HoverPhenoPanel.BorderThickness = new Thickness(2);
        }**/


        // ======================== INK CANVAS INTERACTION EVENT HANDLERS ====================================//

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            inkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
        }

        private void OnCut(object sender, RoutedEventArgs e)
        {
            inkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            ClearDrawnBoundingRect();
        }

        private void OnPaste(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.InkPresenter.StrokeContainer.CanPasteFromClipboard())
            {
                inkCanvas.InkPresenter.StrokeContainer.PasteFromClipboard(new Point((scrollViewer.HorizontalOffset + 10) / scrollViewer.ZoomFactor, (scrollViewer.VerticalOffset + 10) / scrollViewer.ZoomFactor));
            }
            else
            {
                // rootPage.NotifyUser("Cannot paste from clipboard.", NotifyType.ErrorMessage);
            }
        }

        /**
       private void InkCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
       {
           var position = e.GetCurrentPoint(inkCanvas).Position;
           TapAPosition(position);
       }
       private void InkCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
       {
           var position = e.GetCurrentPoint(inkCanvas).Position;
           TapAPosition(position);
       }
       private void InkCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
       {
           
       }
       **/
        private void TapAPosition(Point position)
        {
            ClearSelectionAsync();


            var line = FindHitLine(position);
            if (line != null)
            {
                // Show the selection rect at the paragraph's bounding rect.
                //boundingRect.Union(line.BoundingRect);
                boundingRect = line.BoundingRect;
                IReadOnlyList<uint> strokeIds = line.GetStrokeIds();
                foreach (var strokeId in strokeIds)
                {
                    var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                    stroke.Selected = true;
                    SetSelectedStrokeStyle(stroke);
                }

                // flyout 
                // RecognizeSelection();

                // pop up panel
                recognizeAndSetUpUIForLine(line, true);

            }
        }

        private void InkCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var position = e.GetPosition(inkCanvas);
            TapAPosition(position);
        }

        private void InkCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            /**
            // Convert the selected paragraph or list item
            if (paragraphSelected != null)
            {
                Rect rect = paragraphSelected.BoundingRect;
                var text = ExtractTextFromParagraph(paragraphSelected);

                if ((rect.X > 0) && (rect.Y > 0) && (text != string.Empty))
                {
                    // Create text box with recognized text
                    var textBlock = new TextBlock();
                    textBlock.Text = text;
                    textBlock.MaxWidth = rect.Width;
                    textBlock.MaxHeight = rect.Height;
                    Canvas.SetLeft(textBlock, rect.X);
                    Canvas.SetTop(textBlock, rect.Y);

                    // Remove strokes from InkPresenter
                    IReadOnlyList<uint> strokeIds = paragraphSelected.GetStrokeIds();
                    foreach (var strokeId in strokeIds)
                    {
                        var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                        stroke.Selected = true;
                    }
                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();

                    // Remove strokes from InkAnalyzer
                    inkAnalyzer.RemoveDataForStrokes(strokeIds);

                    // Hide the SelectionRect
                    //SelectionRect.Visibility = Visibility.Collapsed;

                    selectionCanvas.Children.Add(textBlock);
                    paragraphSelected = null;
                }
            }
             **/
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
                if(pt.X > xFrom && pt.X < xTo && pt.Y > yFrom && pt.Y < yTo)
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


        //private void DeleteSymbolIcon_PointerReleased(object sender, PointerRoutedEventArgs e)
        //{

        //}

        private void MoreSymbolIcon_Click(object sender, RoutedEventArgs e)
        {
            recognizedPhenoBriefPanel.Visibility = Visibility.Collapsed;
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
            var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
            recogPhenoFlyout.ShowAt(rectangle);
        }

        
        private TextBox AddBoundingRectAndLabel(int line, Rect bounding, string str, List<string> alters) {
            /**
            var rectangle = new Rectangle()
            {
                Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
                Width = bounding.Width,
                Height = bounding.Height
            };

            Canvas.SetLeft(rectangle, bounding.X);
            Canvas.SetTop(rectangle, bounding.Y);
            **/
            TextBox tb = new TextBox();
            tb.Text = str;
            tb.BorderThickness = new Thickness(0);
            tb.FontSize = 22;
            tb.Width = bounding.Width;
            tb.Height = LINE_HEIGHT ;
            int ln = line + 1;
            Canvas.SetLeft(tb, bounding.X);
            Canvas.SetTop(tb, ln * LINE_HEIGHT - tb.Height + 8); 
            //selectionCanvas.Children.Add(rectangle);
            recognizedTextCanvas.Children.Add(tb);
            textBlockToAlternatives.Add(tb, alters);
            //tb.PointerPressed += RecognizedText_SelectionChanged;
            tb.AddHandler(TappedEvent, new TappedEventHandler(RecognizedText_SelectionChanged), true);
            return tb;
        }

        private void RecognizedText_SelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
            alterFlyout.ShowAt(tb);
            alternativeListView.ItemsSource = textBlockToAlternatives[tb];
        }

        private void recognizedResultTextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            
        }

        private void recognizedResultTextBlock_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            Console.WriteLine(tb.SelectionStart);
        }

        private void recognizedResultTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            int index = -1;
            int iword = 0;
            foreach (var word in recognizedText)
            {
                index += word.selectedCandidate.Length + 1;
                if (index > tb.SelectionStart)
                    break;
                iword++;
            }
            if (iword < recognizedText.Count)
            {
                iWordForCandidateSelection = iword;
                tb.SelectionStart = index - recognizedText.ElementAt(iword).selectedCandidate.Length;
                tb.SelectionLength = recognizedText.ElementAt(iword).selectedCandidate.Length;

                candidateListView.Visibility = Visibility.Visible;
                candidateListView.ItemsSource = recognizedText.ElementAt(iword).candidateList;
            }
        }

        private void candidateListViewButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            TextBlock tb = (TextBlock)btn.Content;
            recognizedText.ElementAt(iWordForCandidateSelection).selectedCandidate = tb.Text;

            string str = "";
            foreach (var rt in recognizedText)
            {
                str += rt.selectedCandidate + " ";
            }
            recognizedResultTextBlock.Text = str;

            phenoProgressRing.Visibility = Visibility.Visible;
            recognizedPhenoListView.Visibility = Visibility.Collapsed;
            searchPhenotypes(str);
        }

        private Polyline getPolylineByRect(Rect rect)
        {
            if (rect.Equals(Rect.Empty))
                return null;
            Polyline lasso = new Polyline();
            lasso.Points.Add(new Point(rect.X, rect.Y));
            lasso.Points.Add(new Point(rect.X, rect.Y + rect.Height));
            lasso.Points.Add(new Point(rect.X + rect.Width, rect.Y + rect.Height));
            lasso.Points.Add(new Point(rect.X + rect.Width, rect.Y));
            lasso.Points.Add(new Point(rect.X, rect.Y));
            return lasso;
        }


        private void PopupDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var stroke in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
            {
                if (stroke.Selected == true)
                    inkAnalyzer.RemoveDataForStroke(stroke.Id);
            }
            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            ClearDrawnBoundingRect();
        }

        private void ShowToastNotification(string title, string stringContent)
        {
            ToastNotifier ToastNotifier = ToastNotificationManager.CreateToastNotifier();
            Windows.Data.Xml.Dom.XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            Windows.Data.Xml.Dom.XmlNodeList toastNodeList = toastXml.GetElementsByTagName("text");
            toastNodeList.Item(0).AppendChild(toastXml.CreateTextNode(title));
            toastNodeList.Item(1).AppendChild(toastXml.CreateTextNode(stringContent));
            Windows.Data.Xml.Dom.IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            Windows.Data.Xml.Dom.XmlElement audio = toastXml.CreateElement("audio");
            audio.SetAttribute("src", "ms-winsoundevent:Notification.SMS");

            ToastNotification toast = new ToastNotification(toastXml);
            toast.ExpirationTime = DateTime.Now.AddSeconds(4);
            ToastNotifier.Show(toast);
        }



        private void recognizedResultTextBlock_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            recognizedResultTextBlock.SelectionChanged -= recognizedResultTextBlock_SelectionChanged;
            candidateListView.Visibility = Visibility.Collapsed;
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var str = recognizedResultTextBlock.Text;
                phenoProgressRing.Visibility = Visibility.Visible;
                recognizedPhenoListView.Visibility = Visibility.Collapsed;
                searchPhenotypes(str);
            }
        }

        /**
        private void HoverPhenoPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            HoverPhenoPanel.Visibility = Visibility.Visible;
            HoverPhenoPanel.Opacity = 1.0;
        }

        private void HoverPhenoPanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            HoverPhenoPanel.Opacity = 0.5;
        }
    **/

        private void alternativeListViewButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            TextBlock tb = (TextBlock)btn.Content;
            
        }

        private void ChangeAlternativeFlyoutClosed(object sender, object e)
        {

        }

        private void TextNoteEdit_TextChanged(object sender, RoutedEventArgs e)
        {
            textNoteDispatcherTimer.Stop();
            textNoteDispatcherTimer.Start();
        }



        #region for test only
        // FOR TESTING ONLY
        private Dictionary<uint, Rectangle> addedBoundIds = new Dictionary<uint, Rectangle>();
        private async void DrawBoundingForTest()
        {

            var result = await inkAnalyzer.AnalyzeAsync();
            // paragraph
            IReadOnlyList<IInkAnalysisNode> paraNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Paragraph);
            foreach (var para in paraNodes)
            {
                if (!addedBoundIds.ContainsKey(para.Id))
                {
                    var rectangle = new Rectangle()
                    {
                        Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection() { 5, 2 },
                        Width = para.BoundingRect.Width,
                        Height = para.BoundingRect.Height
                    };

                    Canvas.SetLeft(rectangle, para.BoundingRect.X);
                    Canvas.SetTop(rectangle, para.BoundingRect.Y);
                    selectionCanvas.Children.Add(rectangle);

                    addedBoundIds.Add(para.Id, rectangle);
                }
                else
                {
                    var rectangle = addedBoundIds.GetValueOrDefault(para.Id);
                    selectionCanvas.Children.Remove(rectangle);
                    rectangle.Width = para.BoundingRect.Width;
                    rectangle.Height = para.BoundingRect.Height;
                    Canvas.SetLeft(rectangle, para.BoundingRect.X);
                    Canvas.SetTop(rectangle, para.BoundingRect.Y);
                    selectionCanvas.Children.Add(rectangle);
                }

            }

            /***
                        // line
                        IReadOnlyList<IInkAnalysisNode> lineNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
                        foreach (var line in lineNodes)
                        {
                            if (!addedBoundIds.ContainsKey(line.Id))
                            {
                                var rectangle = new Rectangle()
                                {
                                    Stroke = new SolidColorBrush(Windows.UI.Colors.Red),
                                    StrokeThickness = 1,
                                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                                    Width = line.BoundingRect.Width,
                                    Height = line.BoundingRect.Height
                                };

                                Canvas.SetLeft(rectangle, line.BoundingRect.X);
                                Canvas.SetTop(rectangle, line.BoundingRect.Y);
                                selectionCanvas.Children.Add(rectangle);
                            }
                            else
                            {
                                var rectangle = addedBoundIds.GetValueOrDefault(line.Id);
                                selectionCanvas.Children.Remove(rectangle);
                                rectangle.Width = line.BoundingRect.Width;
                                rectangle.Height = line.BoundingRect.Height;
                                Canvas.SetLeft(rectangle, line.BoundingRect.X);
                                Canvas.SetTop(rectangle, line.BoundingRect.Y);
                                selectionCanvas.Children.Add(rectangle);
                            }
                        }

                        // word
                        IReadOnlyList<IInkAnalysisNode> wordNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
                        foreach (var word in wordNodes)
                        {
                            if (!addedBoundIds.ContainsKey(word.Id))
                            {
                                var rectangle = new Rectangle()
                                {
                                    Stroke = new SolidColorBrush(Windows.UI.Colors.Green),
                                    StrokeThickness = 1,
                                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                                    Width = word.BoundingRect.Width,
                                    Height = word.BoundingRect.Height
                                };

                                Canvas.SetLeft(rectangle, word.BoundingRect.X);
                                Canvas.SetTop(rectangle, word.BoundingRect.Y);
                                selectionCanvas.Children.Add(rectangle);
                            }
                            else
                            {
                                var rectangle = addedBoundIds.GetValueOrDefault(word.Id);
                                selectionCanvas.Children.Remove(rectangle);
                                rectangle.Width = word.BoundingRect.Width;
                                rectangle.Height = word.BoundingRect.Height;
                                Canvas.SetLeft(rectangle, word.BoundingRect.X);
                                Canvas.SetTop(rectangle, word.BoundingRect.Y);
                                selectionCanvas.Children.Add(rectangle);
                            }
                        }
                        ****/

        }
        #endregion


        private void curLineResultButtonClick(object sender, RoutedEventArgs e)
        {
            this.curLineResultPanel.Visibility = Visibility.Collapsed;
        }

        private void curLineWordsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var citem = (string) e.ClickedItem;
            int ind = curLineWordsListView.Items.IndexOf(citem);
            var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
            alternativeListView.ItemsSource = idToNoteLine[showingResultOfLine].HwrResult[ind].candidateList;
            alterFlyout.ShowAt((FrameworkElement)sender);
        }

        private void curLineResultTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBlock;
            int start = tb.SelectionStart.Offset;
            int ind = 0;
            int wordInd = 0;
            foreach (var run in tb.Inlines)
            {
                var rr = run as Run;
                if (start >= ind && start < ind + rr.Text.Length)
                    break;
                ind += rr.Text.Length;
                wordInd++;
            }
            if (wordInd < idToNoteLine[showingResultOfLine].HwrResult.Count)
            {
                var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
                alternativeListView.ItemsSource = idToNoteLine[showingResultOfLine].HwrResult[wordInd].candidateList;
                alterFlyout.ShowAt((FrameworkElement)sender);
            }
        }

        private void curLineResultTextBlock_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void curLineResultTextBlock_KeyDown(object sender, KeyRoutedEventArgs e)
        {

        }

        private void ScrollView_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender == sideScrollView)
            {
                //scrollViewer.ScrollToVerticalOffset(sideScrollView.VerticalOffset);
                scrollViewer.ChangeView(null, sideScrollView.VerticalOffset, sideScrollView.ZoomFactor, true);
            }
            else
            {
                //sideScrollView.ScrollToVerticalOffset(scrollViewer.VerticalOffset);
                sideScrollView.ChangeView(null, scrollViewer.VerticalOffset, scrollViewer.ZoomFactor, true);
            }
        }

        private void alternativeListView_ItemClick(object sender, ItemClickEventArgs e)
        {
                var citem = (string)e.ClickedItem;
                int ind = alternativeListView.Items.IndexOf(citem);
                idToNoteLine[showingResultOfLine].updateHwrResult(showAlterOfWord, ind);

                // HWR result UI
                setUpCurrentLineResultUI(curLineObject);

                curLineCandidatePheno.Clear();
                // annotation and UI
                annotateCurrentLineAndUpdateUI(curLineObject);
            }



    }


}
