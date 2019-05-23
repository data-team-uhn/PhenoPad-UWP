using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Windows.UI.Input.Inking.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Notifications;
using Windows.ApplicationModel.Core;
using System.Threading;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using PhenoPad.FileService;
using System.Numerics;
using Windows.UI.Xaml.Hosting;
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
        public double windowHeight;
        /***** configurable settings *****/
        // Distance between two neighboring note lines
        public static float LINE_HEIGHT = 50;
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
        public float PAGE_HEIGHT = 2400;
        public float PAGE_WIDTH = 1400;

        private DoubleCollection UNPROCESSED_DASH = new DoubleCollection() { 5, 2 };
        public Rect boundingRect;
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
        private Dictionary<uint, Color> tempFormat = new Dictionary<uint, Color>();
        private MainPage rootPage;
        public EHRPageControl ehrPage;
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
        public InkStroke curStroke;
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

        private Dictionary<uint, NoteLine> idToNoteLine = new Dictionary<uint, NoteLine>();
        private int showingResultOfLine;
        private InkAnalysisLine curLineObject;

        private int showAlterOfWord = -1;

        public Dictionary<string, Phenotype> cachedAnnotation = new Dictionary<string, Phenotype>();
        private HashSet<int> annotatedLines = new HashSet<int>();
        int hoveringLine = -1;

        int iWordForCandidateSelection = 0;
        private SemaphoreSlim deleteSemaphoreSlim;
        private SemaphoreSlim selectAndRecognizeSemaphoreSlim;

        private Dictionary<int, Rectangle> lineToRect = new Dictionary<int, Rectangle>();

        private InkDrawingAttributes drawingAttributesBackUp;
        Dictionary<string, List<Phenotype>> oldAnnotations = new Dictionary<string, List<Phenotype>>();
        Dictionary<TextBox, List<string>> textBlockToAlternatives = new Dictionary<TextBox, List<string>>();

        //for note-page switching
        public bool pointer_pressing = false;
        public double y = -1;


        //====================================================================================
        //                              END OF CLASS PROPERTIES
        //====================================================================================
        #endregion


        /// <summary>
        /// Initializes a new note page controller instance given notebook id and notepage id.
        /// </summary>
        public NotePageControl(string notebookid,string pageid)
        {
            InitializeComponent();

            rootPage = MainPage.Current;
            windowHeight = rootPage.Height;

            Visibility = Visibility.Collapsed;
            DrawBackgroundLines();
            notebookId = notebookid;
            pageId = pageid;
            ehrPage = null;


            UNPROCESSED_COLOR = new SolidColorBrush(UNPROCESSED_COLOR.Color);
            UNPROCESSED_COLOR.Opacity = UNPROCESSED_OPACITY;

            // Initialize the InkCanvas
            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen;

            // Handlers to clear the selection when inking or erasing is detected
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollectedAsync;
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;

            inkCanvas.PointerMoved += inkCanvas_PointerMoved;
           
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
            RawStrokeTimer = new DispatcherTimer();
            recognizeTimer = new DispatcherTimer();
            EraseTimer = new DispatcherTimer();

            dispatcherTimer.Tick += InkAnalysisDispatcherTimer_Tick;  // Ink Analysis time tick
            autosaveDispatcherTimer.Tick += on_stroke_changed; // When timer ticks after user input, auto saves stroke
            operationDispathcerTimer.Tick += OperationDispatcherTimer_Tick;
            textNoteDispatcherTimer.Tick += TextNoteDispatcherTimer_Tick;
            recognizeTimer.Tick += TriggerRecogServer;
            RawStrokeTimer.Tick += RawStrokeTimer_Tick;
            EraseTimer.Tick += EraseTimer_Tick;

            unprocessedDispatcherTimer = new DispatcherTimer();
            unprocessedDispatcherTimer.Tick += UnprocessedDispathcerTimer_Tick;
            
            // We perform analysis when there has been a change to the
            // ink presenter and the user has been idle for 1 second.
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            textNoteDispatcherTimer.Interval = TimeSpan.FromSeconds(0.1);
            operationDispathcerTimer.Interval = TimeSpan.FromMilliseconds(500);
            unprocessedDispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            recognizeTimer.Interval = TimeSpan.FromSeconds(0.25);// recognize through server side every 3 seconds
            autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(1); //setting stroke auto save interval to be 1 sec
            RawStrokeTimer.Interval = TimeSpan.FromSeconds(5);
            EraseTimer.Interval = TimeSpan.FromSeconds(1);

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
            RawStrokes = new List<InkStroke>();
            //currentIndex = 0;
            strokeAnalyzer = new InkAnalyzer();
            
            recognizedTextCanvas.Visibility = Visibility.Collapsed;

            this.SizeChanged += NotePageControl_SizeChanged;

            // textNoteEditBox.SetValue(Paragraph.LineHeightProperty, 20);
            //textEditGrid.Visibility = Visibility.Collapsed;
            
            var format = textNoteEditBox.Document.GetDefaultParagraphFormat();
            textNoteEditBox.FontSize = 32;
            format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            textNoteEditBox.Document.SetDefaultParagraphFormat(format);
            // disable moving strokes for now
            //selectionRectangle.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            //selectionRectangle.ManipulationStarted += SelectionRectangle_ManipulationStarted;
            //selectionRectangle.ManipulationDelta += SelectionRectangle_ManipulationDelta;
            //selectionRectangle.ManipulationCompleted += SelectionRectangle_ManipulationCompleted;   

            //lastStrokePoint = new Point(0,0);
            lastWordPoint = new Point(0, 0);
            lastWordIndex = 0;
            phrases = new Dictionary<int, NotePhraseControl>();
            lastWordCount = 0;

            ////for note-page switching event handlers
            //scrollViewer.PointerPressed += ScrollViewer_PointerPressed;
            //scrollViewer.PointerMoved += ScrollViewer_PointerMoved;
            //scrollViewer.PointerReleased += ScrollViewer_PointerReleased;
            ////outputGrid.ManipulationDelta += aaa;
            //EHRScrollViewer.PointerPressed += EHRScrollViewer_PointerPressed;
            //EHRScrollViewer.PointerMoved += EHRScrollViewer_PointerMoved;
            //EHRScrollViewer.PointerReleased += EHRScrollViewer_PointerReleased;

        }

        private void aaa(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            Debug.WriteLine("manipulation deltaaaa");
            scrollViewer.ChangeView(0, e.Position.Y, scrollViewer.ZoomFactor, true);
        }




        #region UI Display
        // ============================== UI DISPLAYS HANDLER ==============================================//

        public async Task SwitchToEHR(StorageFile file) {
            this.ehrPage = new EHRPageControl(file, this);
            //EHRScrollViewer.Content = ehrPage;
            EHROutputGrid.Children.Add(ehrPage);
            scrollViewer.Visibility = Visibility.Collapsed;

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => {
                EHRScrollViewer.ChangeView(0, 50, 0.645f, false);//changeview by itself does not work for current windows version
                EHRScrollViewer.ZoomToFactor(0.6f); 
            });
            EHRScrollViewer.Visibility = Visibility.Visible;
        }

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
            if (this.ehrPage == null)
            {
                ClearSelectionAsync();
                //Debug.WriteLine("page loaded");
                scrollViewer.ChangeView(0, 50, 0.645f, true);
                scrollViewer.Visibility = Visibility.Collapsed;
                scrollViewer.Visibility = Visibility.Visible;
            }
            else {
                scrollViewer.Visibility = Visibility.Collapsed;
                EHRScrollViewer.ChangeView(0, 50, 0.645f, true);
                EHRScrollViewer.Visibility = Visibility.Visible;
            }
            // Draw background lines
            DrawBackgroundLines();
        }

        public void ScrollToTop() {
            scrollViewer.ChangeView(0, -200, 0.645f, false);
            EHRScrollViewer.ChangeView(0, -200, 0.645f, false);

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
        public void setTextNoteEditBox(string text)
        {
            textNoteEditBox.Document.SetText(TextSetOptions.None, text);
            if (text != "")
                textNoteEditBox.Visibility = Visibility.Visible;

        }

        // FIXME
        public void showTextEditGrid()
        {
            textEditGrid.Visibility = Visibility.Visible;
            //textEditGrid.SetValue(Canvas.ZIndexProperty, 2);
            //inkCanvas.SetValue(Canvas.ZIndexProperty, 1);
            textNoteEditBox.IsReadOnly = false;
            textNoteEditBox.IsTapEnabled = false;
            textNoteEditBox.IsEnabled = true;
        }

        public void hideTextEditGrid()
        {
            //textEditGrid.Visibility = Visibility.Collapsed;
            //textEditGrid.SetValue(Canvas.ZIndexProperty, 1);
            //inkCanvas.SetValue(Canvas.ZIndexProperty, 2);
            textNoteEditBox.IsReadOnly = true;
            textNoteEditBox.IsTapEnabled = false;
            textNoteEditBox.IsEnabled = false;
        }

        #endregion

        #region View mode
        //============================= REVIEW MODE ========================================================/
        public void showRecognizedTextCanvas()
        {
            if (ehrPage == null)
            {
                //recognizedTextCanvas.Visibility = Visibility.Visible;
                recognizedCanvas.Visibility = Visibility.Visible;
                //textNoteEditBox.Document.SetText(TextSetOptions.None, ParseNoteText());
                //textNoteEditBox.Visibility = Visibility.Visible;
                inkCanvas.Visibility = Visibility.Collapsed;
                backgroundCanvas.Background = new SolidColorBrush(Colors.WhiteSmoke);
                curLineResultPanel.Visibility = Visibility.Collapsed;
            }
            else 
                ehrPage.PreviewEHR();
        }

        public void hideRecognizedTextCanvas()
        {
            if (ehrPage == null)
            {//shows the stroke editing
                //recognizedTextCanvas.Visibility = Visibility.Collapsed;
                recognizedCanvas.Visibility = Visibility.Collapsed;
                //textNoteEditBox.Visibility = Visibility.Collapsed;
                inkCanvas.Visibility = Visibility.Visible;
                backgroundCanvas.Background = new SolidColorBrush(Colors.White);
            }
            else
                ehrPage.ReturnToEdit();
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


        private void StrokeInput_PointerPressed(InkStrokeInput sender, PointerEventArgs args)
        {
            // stroke input handling: mouse pointer pressed

            UnprocessedInput_PointerPressed(null, args);
            //autosaveDispatcherTimer.Stop();
        }
        private void StrokeInput_PointerMoved(InkStrokeInput sender, PointerEventArgs args)
        {
            // stroke input handling: mouse pointer moved

            UnprocessedInput_PointerMoved(null, args);
        }
        private void StrokeInput_PointerReleased(InkStrokeInput sender, PointerEventArgs args)
        {
            // stroke input handling: mouse pointer released

            UnprocessedInput_PointerReleased(null, args);
            //autosaveDispatcherTimer.Start();
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // select strokes by "marking" handling: pointer pressed

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
        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // select strokes by "marking" handling: pointer moved

            if (isBoundRect)
            {
                lasso.Points.Add(args.CurrentPoint.RawPosition);
            }
            Debug.WriteLine(args.CurrentPoint.Position);
        }

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // select strokes by "marking" handling: pointer released

            unprocessedDispatcherTimer.Stop();
            lasso.Points.Add(args.CurrentPoint.RawPosition);
            //lasso.Points.Add(lasso.Points.ElementAt(0));
            isBoundRect = false;
            RecognizeSelection();
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
            //saves current stroke to dict for cache
            if (!tempFormat.ContainsKey(stroke.Id))
                tempFormat.Add(stroke.Id, drawingAttributes.Color);
            drawingAttributes.Color = SELECTED_STROKE_COLOR;
            stroke.DrawingAttributes = drawingAttributes;
        }

        private void SetDefaultStrokeStyle(InkStroke stroke)
        {
            //only sets the color of the stroke back if it has been set to selected color
            if (tempFormat.ContainsKey(stroke.Id))
            {
                var temp = stroke.DrawingAttributes;
                temp.Color = tempFormat[stroke.Id];
                stroke.DrawingAttributes = temp;
                tempFormat.Remove(stroke.Id);
            }
        }

        #endregion

        public double getPageWindowRatio() {
            /// <summary>
            /// Returns the current note page's zoomed width in relation to control window frame
            /// </summary>

            if (ehrPage != null) 
                return (ehrPage.ActualWidth * EHRScrollViewer.ZoomFactor) / ehrPage.ActualWidth;
            else
                return (inkCanvas.ActualWidth * scrollViewer.ZoomFactor)  / outputGrid.Width;
        }

        private void SelectionRectangle_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Enabled;
            scrollViewer.VerticalScrollMode = ScrollMode.Enabled;

            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
                if (stroke.Selected)
                    inkAnalyzer.ReplaceDataForStroke(stroke);
                    
            //dispatcherTimer.Start();
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
        
        public void ClearSelectionAsync()
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
            {
                stroke.Selected = false;
                SetDefaultStrokeStyle(stroke);
            }         
            ClearDrawnBoundingRect();
            UpdateLayout();
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
                Panel source = null;
                if (this.ehrPage != null)
                    source = addinCanvasEHR;
                else
                    source = userControlCanvas;

                foreach (var c in source.Children)
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
            foreach(var addin in addinlist)
            {
                ImageAndAnnotation temp = new ImageAndAnnotation(addin.addinType, addin.name, notebookId, pageId,
                                                                 addin.canvasLeft, addin.canvasTop,
                                                                 addin.transX, addin.transY, addin.viewFactor, 
                                                                 addin.widthOrigin, addin.heightOrigin,
                                                                 addin.Width, addin.Height, addin.inDock,
                                                                 addin.commentID, addin.commentslideX, addin.commentslideY,
                                                                 addin.inkRatio, addin.anno_type, addin.commentText);
                olist.Add(temp);
            }

            return olist;
        }

        public async Task<List<RecognizedPhrases>> GetAllRecognizedPhrases() {
            List<RecognizedPhrases> phrases = new List<RecognizedPhrases>();

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                foreach (var c in recognizedCanvas.Children)
                {
                    if (c.GetType() == typeof(NotePhraseControl))
                    {
                        NotePhraseControl npc = (NotePhraseControl)c;
                        //temoporarily setting phrase index 0, will update later when implementing stroke-text place matching
                        for (int i = 0; i < npc.words.Count; i++) {
                            WordBlockControl wb = npc.words[i];
                            RecognizedPhrases ph = new RecognizedPhrases(npc.lineIndex, npc.canvasLeft, i, wb.current, wb.candidates,wb.corrected);
                            phrases.Add(ph);
                        }
                    }
                }
            }
            );

            return phrases;
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
                    string name = FileManager.getSharedFileManager().CreateUniqueName();
                    NewAddinControl(name, false, left:50, top:50,widthOrigin:400,heightOrigin:400,wb:bitmap);
                }
            }
            catch (Exception e)
            {
                logger.Error("Failed to add image control from BitmapImage: " + e.Message);
            }
           
        }

        /// <summary>
        /// Loads pre-saved addin controls from disk using deserialized ImageAndAnnotation object.
        /// </summary>
        public void loadAddInControl(ImageAndAnnotation ia)
        {
            AddInControl canvasAddIn;
            if (ehrPage == null)
                canvasAddIn = new AddInControl(ia.name, notebookId, pageId, ia.widthOrigin, ia.heightOrigin);
            else {
                //if (ia.commentID != -1)
                if(ia.addinType == AddinType.EHR || ia.commentID != -1)
                {//is either an insert/annotation for EHR page
                    canvasAddIn = new AddInControl(ia.name, ehrPage, ia.commentID, ia.anno_type);// need to update this commengID when impleting saving
                }
                else
                    canvasAddIn = new AddInControl(ia.name, notebookId, pageId, ia.widthOrigin, ia.heightOrigin);
            }

            //Manually setting pre-saved configuration of the add-in control
            canvasAddIn.addinType = ia.addinType;
            canvasAddIn.Height = ia.height;
            canvasAddIn.Width = ia.width;
            canvasAddIn.widthOrigin = ia.widthOrigin;
            canvasAddIn.heightOrigin = ia.heightOrigin;
            canvasAddIn.inkCan.Height = ia.heightOrigin - 48;
            canvasAddIn.inkCan.Width = ia.widthOrigin;
            canvasAddIn.canvasLeft = ia.canvasLeft;
            canvasAddIn.canvasTop = ia.canvasTop;
            canvasAddIn.dragTransform.X = ia.transX;
            canvasAddIn.dragTransform.Y = ia.transY;
            canvasAddIn.commentID = ia.commentID;
            canvasAddIn.commentslideX = ia.slideX;
            canvasAddIn.commentslideY = ia.slideY;
            canvasAddIn.inkRatio = ia.inkRatio;
            Canvas.SetTop(canvasAddIn, ia.canvasTop);
            canvasAddIn.inDock = ia.inDock;
            if (ia.inDock)               
                canvasAddIn.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(canvasAddIn, ia.canvasLeft);
            canvasAddIn.viewFactor.ScaleX = ia.zoomFactorX;
            canvasAddIn.viewFactor.ScaleY = ia.zoomFactorY;

            if (ehrPage == null)
            {
                userControlCanvas.Children.Add(canvasAddIn);
                canvasAddIn.Visibility = ia.inDock ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                addinCanvasEHR.Children.Add(canvasAddIn);
                Debug.WriteLine(canvasAddIn.anno_type);
                if (canvasAddIn.addinType == AddinType.EHR || canvasAddIn.commentID != -1)
                {
                    if (canvasAddIn.anno_type == AnnotationType.TextComment || canvasAddIn.anno_type == AnnotationType.TextInsert)
                    {
                        canvasAddIn.inkCan.Visibility = Visibility.Collapsed;
                        canvasAddIn.commentTextBlock.Visibility = Visibility.Visible;
                        canvasAddIn.commentText = ia.commentText;
                        canvasAddIn.commentTextBlock.Document.SetText(TextSetOptions.None, ia.commentText);
                    }
                    canvasAddIn.inDock = false;
                    canvasAddIn.Visibility = Visibility.Visible;
                    ehrPage.comments.Add(canvasAddIn);
                    canvasAddIn.SlideToRight();
                }
            }
            canvasAddIn.InitializeFromDisk(onlyView:false);

            //If this addin was hidden during the last edit, auto hides it from initialization
        }

        internal void loadRecognizedPhrases(List<RecognizedPhrases> recogPhrases)
        {
            recogPhrases = recogPhrases.OrderBy(x => x.line_index).ThenBy(z => z.word_index).ToList();
            int last_line = recogPhrases[0].line_index;
            List<WordBlockControl> words = new List<WordBlockControl>();

            foreach (RecognizedPhrases ph in recogPhrases) {
                if (ph.line_index > last_line) {
                    NotePhraseControl npc = new NotePhraseControl(last_line, words);
                    phrases[last_line] = npc;
                    npc.SetPhrasePosition(ph.left, npc.lineIndex * LINE_HEIGHT);
                    recognizedCanvas.Children.Add(npc);
                    Canvas.SetLeft(npc, ph.left);
                    Canvas.SetTop(npc, npc.lineIndex * LINE_HEIGHT);
                    npc.UpdateLayout();
                    words = new List<WordBlockControl>();
                    last_line = ph.line_index;
                }
                List<string> candidates = new List<string>();
                candidates.Add(ph.current);
                candidates.Add(ph.candidate2);
                candidates.Add(ph.candidate3);
                candidates.Add(ph.candidate4);
                candidates.Add(ph.candidate5);
                WordBlockControl wb = new WordBlockControl(ph.line_index, ph.left, ph.word_index, ph.current, candidates);
                wb.corrected = ph.is_corrected;
                words.Add(wb);
            }
            //this handles the case when there's only one line of note on the page
            if (words.Count > 0) {
                NotePhraseControl npc = new NotePhraseControl(last_line, words);
                phrases[last_line] = npc;
                double left = recogPhrases.Count == 1 ? recogPhrases[0].left : recogPhrases[recogPhrases.Count-1].left;
                npc.SetPhrasePosition(left, npc.lineIndex * LINE_HEIGHT);
                recognizedCanvas.Children.Add(npc);
                Canvas.SetLeft(npc, left);
                Canvas.SetTop(npc, npc.lineIndex * LINE_HEIGHT);
                npc.UpdateLayout();
            }
            //Debug.WriteLine("done loading phrases");
        }

        /// <summary>
        /// Adds a new addin control to notepage based on given configuration arguments.
        /// </summary>
        public void NewAddinControl(string name, bool loadFromDisk,
                                                    double left, double top,                                                   
                                                    double widthOrigin, double heightOrigin,
                                                    double width = -1, double height = -1,
                                                    WriteableBitmap wb = null,
                                                    EHRPageControl ehr = null
                                                    )
        {
            AddInControl canvasAddIn = new AddInControl(name, notebookId, pageId, widthOrigin, heightOrigin);
            //Manually setting configuration for new add-in control
            canvasAddIn.canvasLeft = left;
            canvasAddIn.canvasTop = top;
            Canvas.SetLeft(canvasAddIn, left);
            Canvas.SetTop(canvasAddIn, top);
            canvasAddIn.slideOffset = rootPage.ActualWidth - left;

            if (ehr != null)
                addinCanvasEHR.Children.Add(canvasAddIn);
            else
                userControlCanvas.Children.Add(canvasAddIn);
            curLineResultPanel.Visibility = Visibility.Collapsed ;

            //loading a photo from disk with editing option
            if (loadFromDisk)
                canvasAddIn.InitializeFromDisk(false);
            OperationLogger.getOpLogger().Log(OperationType.ADDIN, name, getLineNumByRect(new Rect(left,top,5,5)).ToString(), pageId);
        }

        public AddInControl NewEHRCommentControl(double left, double top, int commentID, AnnotationType type)
        {
            string name = FileManager.getSharedFileManager().CreateUniqueName();
            AddInControl comment = new AddInControl(name, this.ehrPage, commentID, type);
            comment.canvasLeft = left;
            comment.canvasTop = top;
            Canvas.SetLeft(comment, left);
            Canvas.SetTop(comment, top);
            comment.slideOffset = rootPage.ActualWidth - left;
            addinCanvasEHR.Children.Add(comment);
            autosaveDispatcherTimer.Start();
            return comment;
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

        private void OperationDispatcherTimer_Tick(object sender, object e)
        {
            operationDispathcerTimer.Stop();
            Debug.WriteLine("operationdispatcher tick was called?????????????????????????");
            return;
            //if (!inkOperationAnalyzer.IsAnalyzing)
            //{
            //    var result = await inkOperationAnalyzer.AnalyzeAsync();

            //    if (result.Status == InkAnalysisStatus.Updated)
            //    {
            //        var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
            //        foreach(InkAnalysisInkDrawing drawNode in inkdrawingNodes)
            //        {
            //            if (drawNode.DrawingKind == InkAnalysisDrawingKind.Rectangle || drawNode.DrawingKind == InkAnalysisDrawingKind.Square)
            //            {
            //                    foreach (var dstroke in drawNode.GetStrokeIds())
            //                    {
            //                        var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
            //                        AddInControl canvasAddIn = new AddInControl(FileService.FileManager.getSharedFileManager().CreateUniqueName(), notebookId, pageId);
            //                        canvasAddIn.Width = stroke.BoundingRect.Width;
            //                        canvasAddIn.Height = stroke.BoundingRect.Height;
            //                        Canvas.SetLeft(canvasAddIn, stroke.BoundingRect.X);
            //                        Canvas.SetTop(canvasAddIn, stroke.BoundingRect.Y);
            //                        canvasAddIn.CanDrag = true;
            //                        canvasAddIn.ManipulationMode = ManipulationModes.All;
            //                        canvasAddIn.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
            //                        {
            //                            if (args.Delta.Expansion == 0)
            //                            {
            //                                Canvas.SetLeft(canvasAddIn, Canvas.GetLeft(canvasAddIn) + args.Delta.Translation.X);
            //                                Canvas.SetTop(canvasAddIn, Canvas.GetTop(canvasAddIn) + args.Delta.Translation.Y);
            //                            }
            //                            else
            //                            {
            //                                canvasAddIn.Width += args.Delta.Translation.X * 2;
            //                                canvasAddIn.Height += args.Delta.Translation.Y * 2;
            //                            }
            //                            /**
            //                            if (Math.Abs(args.Delta.Translation.X) > Math.Abs(args.Delta.Translation.Y))
            //                            {
            //                                //canvasAddIn.Width += args.Delta.Expansion;
            //                                Debug.WriteLine(args.Delta.Scale);
            //                                canvasAddIn.Width *= args.Delta.Scale;
            //                            }
            //                            else
            //                            {
            //                                //canvasAddIn.Height += args.Delta.Expansion;
            //                                canvasAddIn.Height *= args.Delta.Scale;
            //                            }
            //                            **/
            //                        };


            //                        userControlCanvas.Children.Add(canvasAddIn);
            //                        stroke.Selected = true;
            //                    }
            //                    inkOperationAnalyzer.RemoveDataForStrokes(drawNode.GetStrokeIds());
            //                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                            

            //            }
            //            if (drawNode.DrawingKind == InkAnalysisDrawingKind.Drawing)
            //            { // straight line for annotation
            //                //var lineRect = node.BoundingRect;

            //            }
            //            if (drawNode.DrawingKind == InkAnalysisDrawingKind.Ellipse || drawNode.DrawingKind == InkAnalysisDrawingKind.Circle)
            //            {
            //                //Debug.WriteLine("Circle!");
            //            }
            //        }
            //        foreach (var sid in inkOperationAnalyzer.AnalysisRoot.GetStrokeIds())
            //        {
            //            inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
            //            inkOperationAnalyzer.RemoveDataForStroke(sid);
            //        }
            //        inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            //    }
            //    else
            //    {
            //        operationDispathcerTimer.Start();
            //    }
            //}
        }

        /// <summary>
        /// Called after strokes are collected to recoginze words and shapes
        /// </summary>
        private async void InkAnalysisDispatcherTimer_Tick(object sender, object e)
        {
            dispatcherTimer.Stop();
            Debug.WriteLine("ink analysis tick, will analyze ink ...");
            await analyzeInk();
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
                Debug.WriteLine("saving note to disk ...");
                bool result1 = false;
                StorageFile file = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.Strokes);
                if (file == null)
                {
                    logger.Error($"SaveToDisk():Failed to get note file.");
                    return false;
                }
                // save handwritings
                result1 = await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, pageId, this);

                if (ehrPage != null)
                {
                    await FileManager.getSharedFileManager().SaveEHRText(notebookId, pageId, this.ehrPage);
                    await FileManager.getSharedFileManager().SaveEHRFormats(notebookId, pageId, this.ehrPage);
                }


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
                List<RecognizedPhrases> phraseList = await GetAllRecognizedPhrases();

                string metapath = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                result3 = await FileManager.getSharedFileManager().SaveObjectSerilization(metapath, imageList, typeof(List<ImageAndAnnotation>));

                string path = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.RecognizedPhraseMeta);
                var result4 = await FileManager.getSharedFileManager().SaveObjectSerilization(path, phraseList, typeof(List<RecognizedPhrases>));

                //saving typed text, if there's any
                string notetext;
                textNoteEditBox.Document.GetText(TextGetOptions.None, out notetext);
                await FileManager.getSharedFileManager().SaveNoteText(notebookId, pageId, notetext);


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

                    var strokesFile = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotation, addin.name);
                    result1 = await FileManager.getSharedFileManager().saveStrokes(strokesFile, addin.inkCan);
                    if (!result1)
                        MetroLogger.getSharedLogger().Error($"Auto-save: note-{notebookId} at page {pageId}, {addin.name} failed to save.");
                }

                // add in meta data
                List<ImageAndAnnotation> imageList = await GetAllAddInObjects();
                string metapath = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                result2 = await FileManager.getSharedFileManager().SaveObjectSerilization(metapath, imageList, typeof(List<ImageAndAnnotation>));
                
            }
            catch (Exception e) {
                MetroLogger.getSharedLogger().Error($"Failed to auto-save addin: {e + e.Message}");
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
                    MetroLogger.getSharedLogger().Error($"Failed to get note file for note {notebookId} at page {pageId},file is null.");
                }
                // save handwritings
                await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, pageId, this);
                List<RecognizedPhrases> phraseList = await GetAllRecognizedPhrases();
                string path = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.RecognizedPhraseMeta);
                var result4 = await FileManager.getSharedFileManager().SaveObjectSerilization(path, phraseList, typeof(List<RecognizedPhrases>));
                Debug.WriteLine("Autosaved");
            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error($"Failed to auto-save strokes/phrases: {ex + ex.Message}");
            }
            finally
            {
                autosaveSemaphore.Release();
                autosaveDispatcherTimer.Stop();
            }
        }

        #endregion

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

        private void inkCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {

        }

        private void InkCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ClearSelectionAsync();
            Point pos = e.GetPosition(inkCanvas);
            ShowAlterOnHover(pos);
        }

        private void InkCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var position = e.GetPosition(inkCanvas);
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
                RecognizeSelection();

            }


        }

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
            tb.Height = LINE_HEIGHT;
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
            alternativeListView.ItemsSource = idToNoteLine[(uint)showingResultOfLine].HwrResult[ind].candidateList;
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
            if (wordInd < idToNoteLine[(uint)showingResultOfLine].HwrResult.Count)
            {
                var alterFlyout = (Flyout)this.Resources["ChangeAlternativeFlyout"];
                alternativeListView.ItemsSource = idToNoteLine[(uint)showingResultOfLine].HwrResult[wordInd].candidateList;
                alterFlyout.ShowAt((FrameworkElement)sender);
            }
        }

        private void curLineResultTextBlock_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void curLineResultTextBlock_KeyDown(object sender, KeyRoutedEventArgs e)
        {

        }


        #region Note Page Switching

        private void ScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (pointer_pressing)
            {
                pointer_pressing = false;
            }

        }

        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (pointer_pressing && y != -1)
            {
                double offset = Math.Abs(e.GetCurrentPoint(scrollViewer).Position.Y - y);
                //Debug.WriteLine($"offset = {offset}");

                bool at_end = (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight >= scrollViewer.ExtentHeight - 1) &&
                    (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight <= scrollViewer.ExtentHeight + 1);
                bool at_start = scrollViewer.VerticalOffset == 0;

                //only triggers the handlers if the swipe offset is within valid range
                //setting an upper bound to avoid accidents
                if (offset >= 6 && offset <= 50)
                {
                    string state = "";
                    if (at_end)
                        state = "next";
                    else if (at_start)
                        state = "previous";
                    MainPage.Current.AddNewNotePage(state);
                    pointer_pressing = false;
                    y = -1;
                }
            }
        }

        private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
            {
                pointer_pressing = true;
                y = e.GetCurrentPoint(scrollViewer).Position.Y;
            }
        }

        private void ScrollView_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            //triggered when user zooms in/out of the scroll view panel

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

        private void EHRScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (pointer_pressing)
            {
                pointer_pressing = false;
            }

        }

        private void EHRScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (pointer_pressing && y != -1)
            {
                double offset = Math.Abs(e.GetCurrentPoint(EHRScrollViewer).Position.Y - y);
                Debug.WriteLine($"offset = {offset}");

                bool at_end = (EHRScrollViewer.VerticalOffset + EHRScrollViewer.ViewportHeight >= EHRScrollViewer.ExtentHeight - 1) &&
                    (EHRScrollViewer.VerticalOffset + EHRScrollViewer.ViewportHeight <= EHRScrollViewer.ExtentHeight + 1);
                bool at_start = EHRScrollViewer.VerticalOffset == 0;

                //only triggers the handlers if the swipe offset is within valid range
                //setting an upper bound to avoid accidents
                if (offset >= 6 && offset <= 50)
                {

                    string state = "";
                    if (at_end)
                        state = "next";
                    else if (at_start)
                        state = "previous";
                    MainPage.Current.AddNewNotePage(state);
                    pointer_pressing = false;
                    y = -1;
                }
            }
        }

        private void EHRScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
            {
                pointer_pressing = true;
                y = e.GetCurrentPoint(EHRScrollViewer).Position.Y;
            }
        }

        #endregion


    }


}
