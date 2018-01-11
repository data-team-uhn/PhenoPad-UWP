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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class NotePageControl : UserControl
    {
        private static float LINE_HEIGHT = 45;
        private Rect boundingRect;
        private Polyline lasso;
        Symbol LassoSelect = (Symbol)0xEF20;
        Symbol TouchWriting = (Symbol)0xED5F;
        private bool isBoundRect;
        InkAnalyzer inkAnalyzer;
        InkAnalyzer inkOperationAnalyzer;
        InkAnalysisParagraph paragraphSelected;
        List<InkAnalysisInkWord> wordListSelected;
        object selectDeleteLock = new object();

        DispatcherTimer dispatcherTimer;
        DispatcherTimer operationDispathcerTimer; //try to recognize last stroke as operation

        DispatcherTimer lineAnalysisDispatcherTimer;
        Queue<int> linesToUpdate;
        object lineUpdateLock = new object();
        Dictionary<int, string> stringsOfLines;
        Dictionary<int, List<Phenotype>> phenotypesOfLines;

        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();
        public ObservableCollection<Phenotype> recognizedPhenotypes = new ObservableCollection<Phenotype>();
        public ObservableCollection<HWRRecognizedText> recognizedText = new ObservableCollection<HWRRecognizedText>();

        private MainPage rootPage;

        CoreInkIndependentInputSource core;

        public InkCanvas inkCan
        {
            get
            {
                return inkCanvas;
            }
        }
        

        public NotePageControl()
        {
            rootPage = MainPage.Current;
            this.InitializeComponent();
            this.DrawBackgroundLines();

            // Initialize the InkCanvas
            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;

            // Handlers to clear the selection when inking or erasing is detected
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
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
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            operationDispathcerTimer.Tick += OperationDispatcherTimer_Tick;

            // We perform analysis when there has been a change to the
            // ink presenter and the user has been idle for 1 second.
            dispatcherTimer.Interval = TimeSpan.FromSeconds(0.3);
            operationDispathcerTimer.Interval = TimeSpan.FromMilliseconds(500);

            linesToUpdate = new Queue<int>();
            lineAnalysisDispatcherTimer = new DispatcherTimer();
            lineAnalysisDispatcherTimer.Tick += LineAnalysisDispatcherTimer_Tick;
            lineAnalysisDispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            //lineAnalysisDispatcherTimer.Start();

            core = CoreInkIndependentInputSource.Create(inkCanvas.InkPresenter);
            core.PointerHovering += Core_PointerHovering;
            core.PointerExiting += Core_PointerExiting;
            core.PointerEntering += Core_PointerHovering;
            //Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerEntered += App_PointerMoved;

            stringsOfLines = new Dictionary<int, string>();
            phenotypesOfLines = new Dictionary<int, List<Phenotype>>();
            deleteSemaphoreSlim = new SemaphoreSlim(1);
        }

        private void App_PointerMoved(CoreWindow sender, PointerEventArgs args)
        {
            Debug.WriteLine(123);
        }

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

        private async void Core_PointerExiting(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
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
            
        }

        int hoveringLine = -1;
        private async void Core_PointerHovering(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            int curLine = (int)args.CurrentPoint.Position.Y / (int)LINE_HEIGHT;
            if (curLine != hoveringLine)
            {
                hoveringLine = curLine;
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        recognizeLine(hoveringLine);
                    });
            }
        }

        private async void recognizeLine(int line)
        {
            if (line < 0)
                return;
            SelectAndAnnotateByLineNum(line);
        }
            

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines
            DrawBackgroundLines();
        }

        // background lines for notes
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

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            ClearSelection();
            /**
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
            **/
            dispatcherTimer.Stop();
            //operationDispathcerTimer.Stop();
            inkOperationAnalyzer.ClearDataForAllStrokes();
            /***
            core.PointerHovering -= Core_PointerHovering;
            core.PointerExiting -= Core_PointerExiting;
            core.PointerEntering -= Core_PointerHovering;
            ***/
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            ClearSelection();
            /**
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
            **/

            dispatcherTimer.Stop();
            //operationDispathcerTimer.Stop();
            foreach (var stroke in args.Strokes)
            {
                inkAnalyzer.RemoveDataForStroke(stroke.Id);
            }
            //operationDispathcerTimer.Start();
            dispatcherTimer.Start();
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            // dispatcherTimer.Stop();
            operationDispathcerTimer.Stop();

            InkStroke lastStroke = null;
            foreach(var s in args.Strokes)
            {
                if (s.BoundingRect.Height > 2 * LINE_HEIGHT)
                {
                    inkOperationAnalyzer.AddDataForStroke(s);
                    await RecognizeInkOperation();
                }
                else
                {
                    inkAnalyzer.AddDataForStroke(s);
                    lastStroke = s;
                }
            }
            //inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            if (lastStroke != null)
                recognizeLine((int) (lastStroke.BoundingRect.Y + lastStroke.BoundingRect.Height/2) / (int) LINE_HEIGHT);
            
            //inkAnalyzer.AddDataForStrokes(args.Strokes);
           

            dispatcherTimer.Start();
            //operationDispathcerTimer.Start();
            /****
            core.PointerHovering += Core_PointerHovering;
            core.PointerExiting += Core_PointerExiting;
            core.PointerEntering += Core_PointerHovering;
            ***/

        }

        // selection 
        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            ClearSelection();
            lasso = new Polyline()
            {
                Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
            };

            lasso.Points.Add(args.CurrentPoint.RawPosition);
            selectionCanvas.Children.Add(lasso);
            isBoundRect = true;
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (isBoundRect)
            {
                lasso.Points.Add(args.CurrentPoint.RawPosition);
            }
        }

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            lasso.Points.Add(args.CurrentPoint.RawPosition);
            //lasso.Points.Add(lasso.Points.ElementAt(0));
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
        }

        private async void DrawBoundingRect()
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
            PopupCommandBar.Visibility = Visibility.Visible;
            recognizedPhenoBriefPanel.Visibility = Visibility.Visible;
            Canvas.SetLeft(PopupCommandBar, boundingRect.X);
            Canvas.SetTop(PopupCommandBar, boundingRect.Y - PopupCommandBar.Height);
            PopupCommandBar.Width = Math.Max(boundingRect.Width, PopupCommandBar.MinWidth);
            Canvas.SetLeft(recognizedPhenoBriefPanel, Math.Max(boundingRect.X, boundingRect.X));
            Canvas.SetTop(recognizedPhenoBriefPanel, boundingRect.Y + boundingRect.Height);
            
            selectionCanvas.Children.Add(PopupCommandBar);
            selectionCanvas.Children.Add(recognizedPhenoBriefPanel);
            var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeSelectionFlyout"];
            //recogPhenoFlyout.ShowAt(rectangle);
            string str = await recognizeSelection();
            if (!str.Equals(String.Empty) && !str.Equals(""))
            {
                Debug.WriteLine("$$$$   " + str);
                recognizedResultTextBlock.Text = str;
                searchPhenotypes(str);
            }
        }

        public void PhenotypeSelectionFlyoutClosed(object sender, object o)
        {
            ClearSelection();
            candidateListView.Visibility = Visibility.Collapsed;
            recognizedPhenoListView.Visibility = Visibility.Collapsed;
            recognizedResultTextBlock.SelectionChanged += recognizedResultTextBlock_SelectionChanged;
            candidateListView.ItemsSource = new List<string>();

        }


        private void ClearSelection()
        {
            Debug.WriteLine("Clear selection");
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
            if (selectionCanvas.Children.Count > 0)
            {
                selectionCanvas.Children.Clear();
                breifPhenoProgressBar.Visibility = Visibility.Visible;
                phenoProgressRing.Visibility = Visibility.Visible;
                recognizedPhenoBriefListView.Visibility = Visibility.Collapsed;
                boundingRect = Rect.Empty;
            }
        }

        // recognize ink as operation
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
                            AddInControl canvasAddIn = new AddInControl();
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
                    //ShowToastNotification("Write smallers", "Write smaller please");
                    rootPage.NotifyUser("Write smaller", NotifyType.ErrorMessage, 2);
                    inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(sid).Selected = true;
                    inkOperationAnalyzer.RemoveDataForStroke(sid);
                }
                inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            }
            return -1;
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
                                    AddInControl canvasAddIn = new AddInControl();
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

        // ink analysis
        private async void DispatcherTimer_Tick(object sender, object e)
        {
            await deleteSemaphoreSlim.WaitAsync();
            try
            {
                dispatcherTimer.Stop();
                if (!inkAnalyzer.IsAnalyzing)
                {
                    var result = await inkAnalyzer.AnalyzeAsync();

                    var inkdrawingNodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                    List<Rect> rectOfAnnoStrokes = new List<Rect>();
                    Debug.Write("Clear&&&&   ");
                    ClearSelection();

                    foreach (InkAnalysisInkDrawing drawNode in inkdrawingNodes)
                    {
                        if (drawNode.DrawingKind == InkAnalysisDrawingKind.Drawing)
                        {
                            //inkAnalyzer.AnalysisRoot.
                            foreach (var dstroke in drawNode.GetStrokeIds())
                            {
                                var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(dstroke);
                                var paragraphs = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Paragraph);
                                //bool isWritingStroke = false;
                                //bool isDeleteingStroke = false;
                                Debug.WriteLine(stroke.BoundingRect.Width);
                                if (stroke.BoundingRect.Width / stroke.BoundingRect.Height > 2 && stroke.BoundingRect.Width > 60)
                                {

                                }
                                else
                                {
                                    inkAnalyzer.SetStrokeDataKind(dstroke, InkAnalysisStrokeKind.Writing);
                                    await inkAnalyzer.AnalyzeAsync();
                                    continue;
                                }
                                double dis = (stroke.BoundingRect.Y + stroke.BoundingRect.Height / 2.0) % LINE_HEIGHT;
                                if (dis < LINE_HEIGHT * 0.25 || dis > LINE_HEIGHT * 0.75)
                                {
                                    stroke.Selected = true;
                                    rectOfAnnoStrokes.Add(stroke.BoundingRect);
                                    inkAnalyzer.RemoveDataForStroke(dstroke);
                                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                                    continue;
                                }

                                // inside a paragraph, length less than 10, should not be a drawing stroke
                                foreach (var para in paragraphs)
                                    if (!RectHelper.Intersect(para.BoundingRect, stroke.BoundingRect).Equals(Rect.Empty))
                                    {
                                        if (stroke.GetInkPoints().Count < 10)
                                        {
                                            inkAnalyzer.SetStrokeDataKind(dstroke, InkAnalysisStrokeKind.Writing);
                                            await inkAnalyzer.AnalyzeAsync();
                                            //isWritingStroke = true;
                                        }
                                        else
                                        {
                                            /**
                                            Polyline pl = getPolylineByRect(stroke.BoundingRect);
                                            Rect selectRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(pl.Points);
                                            if (!selectRect.Equals(Rect.Empty))
                                            {
                                                isDeleteingStroke = true;
                                                foreach (var ss in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                                                    if (ss.Selected)
                                                        inkAnalyzer.RemoveDataForStroke(ss.Id);
                                            }
                                            **/

                                            stroke.Selected = true;
                                            inkAnalyzer.RemoveDataForStroke(stroke.Id);
                                            var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);
                                            foreach (var word in words)
                                            {
                                                Rect interRect = RectHelper.Intersect(word.BoundingRect, stroke.BoundingRect);
                                                if (!interRect.Equals(Rect.Empty))
                                                {
                                                    //if (interRect.Width * interRect.Height > stroke.BoundingRect.Height * stroke.BoundingRect.Width * 0.9)
                                                    {
                                                        foreach (var ss in word.GetStrokeIds())
                                                            inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(ss).Selected = true;
                                                        inkAnalyzer.RemoveDataForStrokes(word.GetStrokeIds());
                                                    
                                                    }
                                                }
                                            }
                                            inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                                        }

                                    }
                            }
                        }
                        
                   
                    }

                    //ClearSelection();
                    foreach (var strokeRect in rectOfAnnoStrokes)
                    {
                        SelectByUnderLine(strokeRect);
                    }
                        
                    DrawBoundingRect();
                    

                    /****
                    var words = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Paragraph);
                    foreach (var word in words)
                    {
                        var rectangle = new Rectangle()
                        {
                            Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                            StrokeThickness = 1,
                            StrokeDashArray = new DoubleCollection() { 5, 2 },
                            Width = word.BoundingRect.Width,
                            Height = word.BoundingRect.Height
                        };

                        Canvas.SetLeft(rectangle, word.BoundingRect.X);
                        Canvas.SetTop(rectangle, word.BoundingRect.Y);

                        selectionCanvas.Children.Add(rectangle);
                    }
                    ***/

                }
                else
                {
                    // Ink analyzer is busy. Wait a while and try again.
                    dispatcherTimer.Start();
                }
            }
            finally
            {
                deleteSemaphoreSlim.Release();
            }
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
                                bool ifSaved = false;
                                if (stringsOfLines.ContainsKey(line) && stringsOfLines[line].Equals(str) && phenotypesOfLines.ContainsKey(line))
                                {
                                    pname = phenotypesOfLines[line].ElementAt(0).name;
                                    if (PhenotypeManager.getSharedPhenotypeManager().checkIfSaved(phenotypesOfLines[line].ElementAt(0)))
                                        ifSaved = true;
                                }
                                else
                                {
                                    List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
                                    if (result != null && result.Count > 0)
                                    {
                                        phenotypesOfLines[line] =  result;
                                        stringsOfLines[line] = str;
                                        pname = result.ElementAt(0).name;
                                        if (PhenotypeManager.getSharedPhenotypeManager().checkIfSaved(result.ElementAt(0)))
                                            ifSaved = true;
                                    }
                                    else
                                        return;
                                }
                                HoverPhenoPanel.Visibility = Visibility.Visible;
                                HoverPhenoPopupText.Text = pname;
                                Canvas.SetTop(HoverPhenoPanel, ((double)line-0.4) * LINE_HEIGHT);
                                Canvas.SetLeft(HoverPhenoPanel, 10);
                                HoverPhenoPopupText.Tapped += HoverPhenoPopupText_Tapped;

                                if (ifSaved)
                                    HoverPhenoPanel.BorderThickness = new Thickness(2);
                                else
                                    HoverPhenoPanel.BorderThickness = new Thickness(0);

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

        private void HoverPhenoPopupText_Tapped(object sender, TappedRoutedEventArgs e)
        {
            phenotypesOfLines[hoveringLine].ElementAt(0).state = 1;
            PhenotypeManager.getSharedPhenotypeManager().addPhenotype(phenotypesOfLines[hoveringLine].ElementAt(0), SourceType.Notes);
            HoverPhenoPanel.BorderThickness = new Thickness(2);
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
            Polyline lasso = new Polyline();
            double dis = (strokeRect.Y + strokeRect.Height / 2.0) % LINE_HEIGHT;
            int lineNum = (int)((strokeRect.Y + strokeRect.Height / 2.0) / LINE_HEIGHT);
            if (dis > LINE_HEIGHT * 0.75)
                lineNum++;
            var yFrom = (lineNum - 1) * LINE_HEIGHT - 0.2 * LINE_HEIGHT;
            var yTo = lineNum * LINE_HEIGHT + 1.0 * LINE_HEIGHT;
            lasso.Points.Add(new Point(xFrom, yFrom));
            lasso.Points.Add(new Point(xFrom, yTo));
            lasso.Points.Add(new Point(xTo, yTo));
            lasso.Points.Add(new Point(xTo, yFrom));
            lasso.Points.Add(new Point(xFrom, yFrom));
            Rect selectRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(lasso.Points);
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
            drawingAttributes.Color = MyColors.PHENOTYPE_BLUE_COLOR;
            stroke.DrawingAttributes = drawingAttributes;
        }
        private void SetDefaultStrokeStyle(InkStroke stroke)
        {
            var drawingAttributes = stroke.DrawingAttributes;
            drawingAttributes.Color = MyColors.DEFUALT_STROKE;
            stroke.DrawingAttributes = drawingAttributes;
        }

        private void TapAPosition(Point position)
        {
            wordListSelected = new List<InkAnalysisInkWord>();
            ClearSelection();
            
            /***

            var word = FindHitWord(position);
            if (word == null)
            {
                // Did not tap on a paragraph.
                //SelectionRect.Visibility = Visibility.Collapsed;
              
                foreach (var stroke in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                {
                    stroke.Selected = false;
                    SetDefaultStrokeStyle(stroke);
                }
                wordListSelected = new List<InkAnalysisInkWord>();
                ClearDrawnBoundingRect();
            }
            else
            {
                // Show the selection rect at the paragraph's bounding rect.
               
                boundingRect.Union(word.BoundingRect);
                IReadOnlyList<uint> strokeIds = word.GetStrokeIds();
                foreach (var strokeId in strokeIds)
                {
                    var stroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                    stroke.Selected = true;
                    SetSelectedStrokeStyle(stroke);
                }
                wordListSelected.Add(word);
                DrawBoundingRect();
            }
            ***/
        }
        /**
         * InkCanvas interaction event handlers
         * 
         **/
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

        private void InkCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var position = e.GetPosition(inkCanvas);
            TapAPosition(position);
        }

        private void InkCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
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
                    SelectionRect.Visibility = Visibility.Collapsed;

                    selectionCanvas.Children.Add(textBlock);
                    paragraphSelected = null;
                }
            }
        }

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

        private async Task<string> recognizeSelection()
        {
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                List<HWRRecognizedText> recognitionResults = await HWRService.HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.Selected);

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

        private async void searchPhenotypes(string str)
        {
            List<Phenotype> result = await PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);
            if (result != null && result.Count > 0)
            {
                recognizedPhenotypes.Clear();
                //recognizedPhenotypes = new ObservableCollection<Phenotype>(result);
                result.ToList().ForEach(recognizedPhenotypes.Add);
            }


            
                breifPhenoProgressBar.Visibility = Visibility.Collapsed;
                recognizedPhenoBriefListView.Visibility = Visibility.Visible;
           
                phenoProgressRing.Visibility = Visibility.Collapsed;
                recognizedPhenoListView.Visibility = Visibility.Visible;
        }

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

        private void DeleteSymbolIcon_PointerReleased(object sender, PointerRoutedEventArgs e)
        {

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

        private void recognizedResultTextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            
        }

        private void recognizedResultTextBlock_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            Console.WriteLine(tb.SelectionStart);
        }

        int iWordForCandidateSelection = 0;
        private SemaphoreSlim deleteSemaphoreSlim;

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

        public void AddImageControl(SoftwareBitmapSource source)
        {
            AddInImageControl imageControl = new AddInImageControl();
            imageControl.Height = 300;
            imageControl.Width = 400;
            Canvas.SetLeft(imageControl, 0);
            Canvas.SetTop(imageControl, 500);
            imageControl.getImageControl().Source = source;
            imageControl.CanDrag = true;
            imageControl.ManipulationMode = ManipulationModes.All;
            imageControl.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
            {
                if (args.Delta.Expansion == 0)
                {
                    Canvas.SetLeft(imageControl, Canvas.GetLeft(imageControl) + args.Delta.Translation.X);
                    Canvas.SetTop(imageControl, Canvas.GetTop(imageControl) + args.Delta.Translation.Y);
                }
                else
                {
                    imageControl.Width += args.Delta.Translation.X * 2;
                    imageControl.Height += args.Delta.Translation.Y * 2;
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

        public async Task StartAnalysisAfterLoad()
        {
            inkAnalyzer.AddDataForStrokes(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
            await inkAnalyzer.AnalyzeAsync();
            //dispatcherTimer.Start();
            core.PointerHovering += Core_PointerHovering;
            core.PointerExiting += Core_PointerExiting;
            core.PointerEntering += Core_PointerHovering;
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

        private void HoverPhenoPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            HoverPhenoPanel.Visibility = Visibility.Visible;
            HoverPhenoPanel.Opacity = 1.0;
        }

        private void HoverPhenoPanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            HoverPhenoPanel.Opacity = 0.5;
        }
    }

    public class NoopConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
