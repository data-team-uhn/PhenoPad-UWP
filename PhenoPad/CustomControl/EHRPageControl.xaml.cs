using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
        private double EHR_WIDTH = 1700;
        private float LINE_HEIGHT = 50;
        private float MAX_WRITING = 40f;

        private bool leftLasso = false;


        InkAnalyzer inkOperationAnalyzer;
        DispatcherTimer operationDispathcerTimer; //try to recognize last stroke as operation

        public EHRPageControl(StorageFile file)
        {
            this.InitializeComponent();
            //setting the text grid line format
            {
                var format = EHRTextBox.Document.GetDefaultParagraphFormat();
                EHRTextBox.FontSize = 32;
                //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
                format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
                EHRTextBox.Document.SetDefaultParagraphFormat(format);
            }

            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += inkCanvas_StrokeInput_StrokeStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += inkCanvas_StrokeInput_StrokeEnded;
            inkCanvas.InkPresenter.StrokesCollected += inkCanvas_InkPresenter_StrokesCollectedAsync;

            inkOperationAnalyzer = new InkAnalyzer();

            this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }

        //=======================================
        //      METHODS BEGIN HERE
        //=======================================

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines
            DrawBackgroundLines();
        }

        /// <summary>
        /// Takes the EHR text file and converts content onto template
        /// </summary>
        /// <param name="file"></param>
        public async void SetUpEHRFile(StorageFile file) {
            try {
                string text = await Windows.Storage.FileIO.ReadTextAsync(file);
                EHRTextBox.Document.SetText(TextSetOptions.None, text);
                Debug.WriteLine(text);
            }
            catch (Exception ex) {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
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
                Debug.WriteLine($"in");

                //first need to clear all previous selections to filter out strokes that don't want to be deleted
                //ClearSelectionAsync();
                foreach (var s in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = false;
                //Gets all strokes from inkoperationanalyzer
                var inkdrawingNodes = inkOperationAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
                foreach (var node in inkdrawingNodes)
                {

                    Rect bounding = node.BoundingRect;
                    if (bounding.Width < 10) {
                        Debug.WriteLine($"detected a line from operation at coordinate {bounding.X},{bounding.Y}====");
                        Point pos = new Point(bounding.X - 10 , bounding.Y + (bounding.Height/2.0) );
                        var range = EHRTextBox.Document.GetRangeFromPoint(pos, PointOptions.ClientCoordinates);
                        string all_text;
                        EHRTextBox.Document.GetText(TextGetOptions.None, out all_text);
                        string rest = all_text.Substring(range.StartPosition);
                        int space_index = rest.IndexOf(' ');

                        int next_space_index = rest.Substring(space_index + 1).IndexOf(' ');
                        Debug.WriteLine($"{space_index}" + "//"+ rest.Substring(space_index));
                        ShowInputPanel(pos);
                    }

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


        private void inkCanvas_StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            inputgrid.Visibility = Visibility.Collapsed;
        }

        private async void inkCanvas_StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {            
        }

        private async void inkCanvas_InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (!leftLasso)
            {//processing strokes inputs
                foreach (var s in args.Strokes)
                {
                    //Process strokes that excess maximum height for recognition
                    if ( s.BoundingRect.Height > MAX_WRITING)
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

        private void ShowInputPanel(Point p) {
            Canvas.SetLeft(inputgrid, p.X - 10);
            Canvas.SetTop(inputgrid, p.Y + LINE_HEIGHT);
            inputgrid.Visibility = Visibility.Visible;
        }







    }
}
