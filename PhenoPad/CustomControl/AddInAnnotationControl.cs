using Microsoft.Toolkit.Uwp.UI.Animations;
using PhenoPad.FileService;
using PhenoPad.HWRService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace PhenoPad.CustomControl
{
    public enum AnnotationType
    {
        RawComment,
        RawInsert,
        TextComment,
        TextInsert
    }
    //Partial class for EHR mode annotations only
    public sealed partial class AddInControl : UserControl
    {
        public AnnotationType anno_type;
        public static double DEFAULT_COMMENT_HEIGHT = 180;
        public static double DEFAULT_COMMENT_WIDTH = 650;
        public double COMMENT_HEIGHT = 60;
        public double COMMENT_WIDTH = 400;
        public Color BORDER_ACTIVE;
        public Color BORDER_INACTIVE;
        public string commentText;

        public double commentslideX;
        public double commentslideY;
        public double inkRatio = 0.6;

        public int commentID = -1;
        public EHRPageControl ehr;

        /// <summary>
        /// Adding control for commenting annotation in EHR Mode only
        /// </summary>
        public AddInControl(string name,
                            EHRPageControl ehr,
                            int commentID, AnnotationType type)
        {
            this.InitializeComponent();
            rootPage = MainPage.Current;

            //setting pre-saved configurations of the control
            {
                anno_type = type;
                addinType = AddinType.EHR;
                widthOrigin = DEFAULT_COMMENT_WIDTH;
                heightOrigin = DEFAULT_COMMENT_HEIGHT;
                Width = widthOrigin;
                Height = heightOrigin;

                inkCan.Width = Width;
                inkCan.Height = Height;

                inDock = false;
                this.name = name;
                notebookId = ehr.notebookid;
                pageId = ehr.pageid;
                _isResizing = false;
                hasImage = false;
                this.ehr = ehr;
                this.commentID = commentID;
                commentslideX = 0;
                commentslideY = 0;
                addinSlide.X = 0;
                addinSlide.Y = 0;
            }

            if (type == AnnotationType.TextComment || type == AnnotationType.RawComment)
            {
                commentbg.Background = new SolidColorBrush(Colors.LightYellow);
                BORDER_ACTIVE = Colors.Orange;
                BORDER_INACTIVE = Colors.LightGoldenrodYellow;
            }
            else
            {
                commentbg.Background = new SolidColorBrush(Color.FromArgb(100, 212, 236, 247));
                BORDER_ACTIVE = Color.FromArgb(255, 42, 148, 247);//navy blue
                BORDER_INACTIVE = Color.FromArgb(255, 191, 236, 255);
            }

            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Colors.Black;
            drawingAttributes.Size = new Size(2, 2);
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;
            inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            inkCanvas.InkPresenter.StrokesCollected += inkCanvas_StrokesCollected;


            //control transform group binding
            {
                TransformGroup tg = new TransformGroup();
                viewFactor = new ScaleTransform();
                viewFactor.ScaleX = 1;
                viewFactor.ScaleY = 1;
                dragTransform = new TranslateTransform();
                tg.Children.Add(dragTransform);
                this.RenderTransform = tg;
            }

            scrollViewer.ZoomMode = ZoomMode.Disabled;
            scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            commentbg.Visibility = Visibility.Visible;
            this.PointerEntered += ShowMenu;
            this.PointerExited += HideMenu;
            this.PointerCaptureLost += HideMenu;
            contentGrid.Tapped += HideMenu;
            contentGrid.PointerPressed += ReEdit;
            Canvas.SetZIndex(this, 99);

            TitleRelativePanel.Visibility = Visibility.Collapsed;
            inkToolbar.Visibility = Visibility.Collapsed;
            TitleRelativePanel.Children.Remove(manipulateButton);
            Grid.SetRow(contentGrid, 0);
            Grid.SetRow(DeleteComment, 0);
            Grid.SetRowSpan(DeleteComment, 2);
            Grid.SetRowSpan(contentGrid, 2);
            OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_ACTIVE);
            OutlineGrid.CornerRadius = new CornerRadius(5);
            OutlineGrid.BorderThickness = new Thickness(2);
            categoryGrid.Visibility = Visibility.Collapsed;
            DrawingButton_Click(null, null);
            //Visibility = Visibility.Collapsed;

        }

        public async void SlideToRight()
        {
            //Debug.WriteLine($"indock = {inDock}");
            if (! inDock)
            {
                Visibility = Visibility.Visible;
                if (this.Height >= DEFAULT_COMMENT_HEIGHT && this.Width >= DEFAULT_COMMENT_WIDTH)
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, CompressComment);
                Canvas.SetZIndex(this,90);
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_INACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(1);
                HideMenu();

                inkCan.InkPresenter.IsInputEnabled = false;//does not allow further edit once slides
                commentTextBlock.IsEnabled = false;

                DoubleAnimation dx = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(0);
                dx.By = commentslideX;
                DoubleAnimation dy = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(1);
                dy.By = commentslideY;
                await EHRCommentSlidingAnimation.BeginAsync();
                inDock = true;
            }
        }


        public async void Slide(double x = 0, double y = 0)
        {
            DoubleAnimation dx = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(0);
            dx.By = x;
            DoubleAnimation dy = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(1);
            dy.By = y;
            await EHRCommentSlidingAnimation.BeginAsync();
        }

        private void InsertConverted(object sender, RoutedEventArgs e) {
            Debug.WriteLine(this.commentID);
            ehr.AddInsert(insertFromComment: this, index: commentID);
            
        }

        private void ShowMenu(object sender, RoutedEventArgs e)
        {
            if (addinSlide.X == commentslideX)
            {
                Grid.SetColumn(contentGrid, 0);
                Grid.SetColumnSpan(contentGrid, 2);
                ehr.ShowCommentLine(this);
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_ACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(2);
                Canvas.SetZIndex(this, 99);
                DeleteComment.Visibility = Visibility.Visible;

                bool isRawStroke = anno_type == AnnotationType.RawInsert || anno_type == AnnotationType.RawComment;
                if (isRawStroke && commentText == null) {
                    ConvertComment.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(contentGrid, 1);
                }
                else if (anno_type == AnnotationType.RawInsert && commentText.Length != 0)
                {
                    InsertComment.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(contentGrid, 1);
                }
                UpdateLayout();
            }
        }
        
        private void HideMenu(object sender = null, RoutedEventArgs e = null)
        {
            Grid.SetColumn(contentGrid, 0);
            Grid.SetColumnSpan(contentGrid, 3);
            ehr.HideCommentLine();

            if (addinSlide.X == commentslideX) {
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_INACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(1);
                Canvas.SetZIndex(this, 90);
            }

            DeleteComment.Visibility = Visibility.Collapsed;
            ConvertComment.Visibility = Visibility.Collapsed;
            InsertComment.Visibility = Visibility.Collapsed;
        }

        public async void ConvertStrokeToText(object sender, RoutedEventArgs e) {
            if (addinSlide.X == commentslideX) {

                List<HWRRecognizedText> recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.All, server: false);

                //continue analyzing until we have a result
                while (recognitionResults == null) {
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    recognitionResults = await HWRManager.getSharedHWRManager().OnRecognizeAsync(inkCanvas.InkPresenter.StrokeContainer, InkRecognitionTarget.All, server: true);
                }

                string text = "";

                foreach (var res in recognitionResults)
                {
                    text += res.selectedCandidate + " ";
                }
                commentTextBlock.Document.SetText(Windows.UI.Text.TextSetOptions.None, text.Trim());
                commentTextBlock.Visibility = Visibility.Visible;
                inkCan.Visibility = Visibility.Collapsed;
                commentText = text;
                if (anno_type == AnnotationType.RawComment)
                {
                    anno_type = AnnotationType.TextComment;
                }
            }

        }

        private void RemoveComment(object sender, RoutedEventArgs e) {
            if (addinSlide.X == commentslideX) 
                ehr.RemoveAnnotation(this);
        }

        public async void ReEdit(object sender = null, RoutedEventArgs e = null)
        {//Slides comment back and re-enables edit mode
            if (inDock)
            {

                //if there's a comment currently at edit mode, slide it back to avoid position shifting errors
                AddInControl lastActiveComment = ehr.comments.Where(x => x.commentID == ehr.lastAddedCommentID).FirstOrDefault();
                if (lastActiveComment != null)
                {
                    lastActiveComment.SlideToRight();
                }

                ehr.HideCommentLine();
                ehr.lastAddedCommentID = this.commentID;
                DoubleAnimation dx = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(0);
                dx.By = -1 * commentslideX;
                DoubleAnimation dy = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(1);
                dy.By = -1 * commentslideY;
                await EHRCommentSlidingAnimation.BeginAsync();
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_ACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(2);

                foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                {
                    //zooms back to original size
                    s.PointTransform = s.PointTransform * Matrix3x2.CreateScale((float)(1/inkRatio));
                    s.Selected = true;
                }
                Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                Debug.WriteLine($"REEDIT bound.width = {bound.Width}, bound.height = {bound.Height}, bound.x = {bound.X}, bound.y = {bound.Y}");

                //foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes()) {
                //    s.PointTransform *= Matrix3x2.CreateTranslation((float)(-1 * bound.X), (float)(-1 * bound.Y));
                //}
                //inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-1 * bound.X + 1, -1 * bound.Y + 1));
                this.widthOrigin = bound.Width < DEFAULT_COMMENT_WIDTH ? DEFAULT_COMMENT_WIDTH : bound.Width + 5;
                this.Width = this.widthOrigin;
                //this.Height = this.heightOrigin;
                this.Height = Math.Max(GetCommentHeight(), heightOrigin);
                //this.Height = Math.Max(this.heightOrigin, bound.Y + bound.Height + COMMENT_HEIGHT);
                inkCan.Height = this.Height;
                inkCan.Width = this.Width;
                Canvas.SetZIndex(this, 99);
                inkCan.InkPresenter.IsInputEnabled = true;
                inDock = false;
                if (commentTextBlock.Visibility == Visibility.Visible && commentText != null)
                {
                    Debug.WriteLine("re editing a text comment!");
                    ehr.ReEditTextAnnotation(this);
                    Visibility = Visibility.Collapsed;
                }
            }

        }

        private void CompressComment()
        { //Shrinks the strokes in inkCan and readjusts the control frame size
            if (!hasImage)
            {
                if (anno_type == AnnotationType.RawComment || anno_type == AnnotationType.RawInsert) {

                    var inknodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);

                    Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                    Debug.WriteLine($"Number of lines detected = {inknodes.Count},height ratio = {bound.Height / DEFAULT_COMMENT_HEIGHT}");
                    inkRatio = bound.Width / COMMENT_WIDTH;

                    //Detected less/equal one line of strokes and the bound is less than a line height,
                    //In this case treat it as a single line of annotation
                    if (inknodes.Count <= 1 && bound.Height <= COMMENT_HEIGHT * 1.5)
                    {
                        inkRatio = Math.Min(COMMENT_HEIGHT / (bound.Height + 10), bound.Height / (COMMENT_HEIGHT + 10));
                        Debug.WriteLine($"single, ratio = {inkRatio}");
                        this.Height = COMMENT_HEIGHT;
                    }
                    else
                    {
                        int line;
                        //use InkAnalyzer's line count if available, o.w. estimate line using bound height
                        line = inknodes.Count >= 1 ? inknodes.Count : (int)(Math.Ceiling((bound.Height) / COMMENT_HEIGHT));

                        inkRatio = Math.Min((bound.Height) / (line * COMMENT_HEIGHT + 10), (line * COMMENT_HEIGHT) / (bound.Height + 10));

                        Debug.WriteLine($"multiple, #lines = {line}, ratio = {inkRatio}");
                        //recalculate number of lines relative to compressed strokes
                        if (inknodes.Count < 1)
                            line = (int)(Math.Ceiling((bound.Height * inkRatio + 1) / COMMENT_HEIGHT));

                        Debug.WriteLine($"recalculated, #lines = {line}");
                        this.Height = (line) * COMMENT_HEIGHT;
                    }
                    //further compresses the strokes if calculated ratio is over 60%
                    if (inkRatio > 0.6)
                        inkRatio = 0.6;

                    foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                    {
                        s.PointTransform = s.PointTransform * Matrix3x2.CreateScale((float)inkRatio);
                        s.Selected = true;
                    }

                    bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                    Debug.WriteLine($"bound.width = {bound.Width}, bound.height = {bound.Height}, bound.x = {bound.X}, bound.y = {bound.Y}");
                    inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-1 * bound.X + 1, -1 * bound.Y));
                    this.Width = bound.Width < COMMENT_WIDTH ? COMMENT_WIDTH : bound.Width + 5;
                    inkAnalyzer.ClearDataForAllStrokes();
                }

                else if (anno_type == AnnotationType.TextComment || anno_type == AnnotationType.TextInsert)
                {
                    this.Height = Math.Ceiling(getTextBound().Height / COMMENT_HEIGHT) * COMMENT_HEIGHT ;
                    this.Width = DEFAULT_COMMENT_WIDTH ;
                }

                ehr.HideCommentLine();
            }
        }

        public Rect getTextBound() {
            string text;
            commentTextBlock.Document.GetText(Windows.UI.Text.TextGetOptions.None, out text);
            var range = commentTextBlock.Document.GetRange(0, text.Count());
            Rect bound;
            int hit;
            range.GetRect(Windows.UI.Text.PointOptions.ClientCoordinates, out bound, out hit);
            return bound;

        }

        public double GetCommentHeight()
        {//Gets the compressed comment height without actually compressing the comment
            if (anno_type == AnnotationType.RawComment || anno_type == AnnotationType.RawInsert)
            {
                var inknodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
                Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;

                //estimated single line
                if (inknodes.Count <= 1 && bound.Height <= COMMENT_HEIGHT * 1.5)
                    return COMMENT_HEIGHT;
                //estimated multiple line
                else
                {
                    int line;
                    line = inknodes.Count >= 1 ? inknodes.Count : (int)(Math.Ceiling((bound.Height) / COMMENT_HEIGHT));
                    inkRatio = Math.Min((bound.Height) / (line * COMMENT_HEIGHT + 10), (line * COMMENT_HEIGHT) / (bound.Height + 10));
                    //recalculate number of lines relative to compressed strokes
                    if (inknodes.Count < 1)
                        line = (int)(Math.Ceiling((bound.Height * inkRatio + 1) / COMMENT_HEIGHT));
                    return (line) * COMMENT_HEIGHT;

                }
            }
            else
            {
                return Math.Ceiling(getTextBound().Height / COMMENT_HEIGHT) * COMMENT_HEIGHT;
            }
                
        }

        public double GetSlideOffsetY() {           
            return addinSlide.Y;
        }

        public void SetXSlide(double x) {
            addinSlide.X = 0;
        }

        private async void inkCanvas_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            //inkAnalyzer = new InkAnalyzer();
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            await inkAnalyzer.AnalyzeAsync();

            //detects if user input has reached maximum height and extend if necessary
            Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
            if (bound.Top + bound.Height > 0.8 * this.Height) {
                this.Height += COMMENT_HEIGHT;
                this.heightOrigin = this.Height;
                inkCanvas.Height += COMMENT_HEIGHT;
            }
        }

    }
}
