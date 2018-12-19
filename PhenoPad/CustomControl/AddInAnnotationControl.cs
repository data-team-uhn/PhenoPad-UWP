﻿using Microsoft.Toolkit.Uwp.UI.Animations;
using PhenoPad.FileService;
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
        Comment,
        RawInsert
    }
    //Partial class for EHR mode annotations only
    public sealed partial class AddInControl : UserControl
    {
        public AnnotationType anno_type;
        public double DEFAULT_COMMENT_HEIGHT = 150;
        public double DEFAULT_COMMENT_WIDTH = 650;
        public double COMMENT_HEIGHT = 60;
        public double COMMENT_WIDTH = 500;
        public Color BORDER_ACTIVE;
        public Color BORDER_INACTIVE;

        public double commentslideX;
        public double commentslideY;
        public double inkRatio;

        public int commentID;
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
                this.anno_type = type;
                this.widthOrigin = DEFAULT_COMMENT_WIDTH;
                this.heightOrigin = DEFAULT_COMMENT_HEIGHT;
                this.Width = this.widthOrigin;
                this.Height = this.heightOrigin;

                inkCan.Width = this.Width;
                inkCan.Height = this.Height;

                this.inDock = false;
                this.name = name;
                this.notebookId = ehr.notebookid;
                this.pageId = ehr.pageid;
                this._isResizing = false;
                this.hasImage = false;
                this.ehr = ehr;
                this.commentID = commentID;
                commentslideX = 0;
                commentslideY = 0;
            }

            if (type == AnnotationType.Comment)
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
            //this.PointerEntered += ShowCommentLine;
            this.PointerEntered += ShowMenu;
            //this.PointerExited += HideCommentLine;
            this.PointerExited += HideMenu;

            contentGrid.Tapped += HideMenu;
            contentGrid.Tapped += ReEdit;
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
        }

        #region EHR Comment Exclusive

        private void StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            //inkAnalyzer.AddDataForStrokes(args.Strokes);
        }

        public async void SlideToRight()
        {
            if (addinSlide.X == 0)
            {
                if (this.Height == 150)
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, CompressComment);
                Canvas.SetZIndex(this,90);
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_INACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(1);
                HideMenu();

                inkCan.InkPresenter.IsInputEnabled = false;//does not allow further edit once slides
                DoubleAnimation dx = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(0);
                dx.By = commentslideX;
                DoubleAnimation dy = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(1);
                dy.By = commentslideY;
                await EHRCommentSlidingAnimation.BeginAsync();
            }
        }

        public async void ReEdit(object sender = null, TappedRoutedEventArgs e = null)
        {
            if (addinSlide.X > 0)
            {
                //if there's a comment currently at edit mode, slide it back to avoid position shifting errors
                AddInControl lastActiveComment = ehr.comments.Where(x => x.commentID == ehr.lastAddedCommentID).FirstOrDefault();
                if (lastActiveComment != null)
                    lastActiveComment.SlideToRight();
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
                HideMenu();

                foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                {
                    s.PointTransform = Matrix3x2.CreateScale(1, 1);
                    s.Selected = true;
                }

                Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-1 * bound.X + 1, -1 * bound.Y + 1));

                this.widthOrigin = DEFAULT_COMMENT_WIDTH;
                this.heightOrigin = DEFAULT_COMMENT_HEIGHT;
                this.Width = this.widthOrigin;
                this.Height = this.heightOrigin;
                inkCan.Height = this.Height;
                inkCan.Width = this.Width;
                Canvas.SetZIndex(this, 99);
                inkCan.InkPresenter.IsInputEnabled = true;
            }

        }

        public async Task SlideVertical(double y)
        {
            DoubleAnimation dx = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(0);
            dx.By = 0;
            DoubleAnimation dy = (DoubleAnimation)EHRCommentSlidingAnimation.Children.ElementAt(1);
            dy.By = y;
            await EHRCommentSlidingAnimation.BeginAsync();
        }

        private void ShowMenu(object sender, RoutedEventArgs e)
        {
            if (addinSlide.X > 0)
            {
                Grid.SetColumn(contentGrid, 0);
                Grid.SetColumnSpan(contentGrid, 1);
                //ehr.showCommentMenu(this, addinSlide.X, addinSlide.Y);
                ehr.ShowCommentLine(this);
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_ACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(2);
                Canvas.SetZIndex(this, 99);
                DeleteComment.Visibility = Visibility.Visible;
            }
        }
        
        private void HideMenu(object sender = null, RoutedEventArgs e = null)
        {
            if (addinSlide.X > 0)
            {
                Grid.SetColumn(contentGrid, 0);
                Grid.SetColumnSpan(contentGrid, 2);
                //ehr.hideCommentMenu();
                ehr.HideCommentLine();
                OutlineGrid.BorderBrush = new SolidColorBrush(BORDER_INACTIVE);
                OutlineGrid.CornerRadius = new CornerRadius(5);
                OutlineGrid.BorderThickness = new Thickness(1);
                Canvas.SetZIndex(this, 90);
                DeleteComment.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveComment(object sender, RoutedEventArgs e) {
            if (addinSlide.X > 0) {
                ehr.RemoveComment(this);
            }

        }

        private async void CompressComment()
        { //Shrinks the strokes in inkCan and readjusts the control frame size
            if (!hasImage)
            {
                ehr.HideCommentLine();
                inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
                await inkAnalyzer.AnalyzeAsync();
                var inknodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
                foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                    s.Selected = true;
                Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                Debug.WriteLine($"Number of lines detected = {inknodes.Count}, bound height = {bound.Height}\n");

                if (inknodes.Count <= 1 && bound.Height <= (1.3) * COMMENT_HEIGHT)
                {
                    inkRatio = bound.Height / COMMENT_HEIGHT;
                    this.Height = COMMENT_HEIGHT;
                }
                else
                {
                    inkRatio = bound.Height / (2 * COMMENT_HEIGHT);
                    this.Height = 2 * COMMENT_HEIGHT;
                }
                if (inkRatio >= 0.7)
                {
                    inkRatio = 0.7;
                    foreach (InkStroke s in inkCan.InkPresenter.StrokeContainer.GetStrokes())
                        s.PointTransform = Matrix3x2.CreateScale((float)inkRatio, (float)inkRatio);
                }
                bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;
                inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-1 * bound.X + 1, -1 * bound.Y + 1));
                this.Width = COMMENT_WIDTH;
                this.heightOrigin = this.Height;
                this.widthOrigin = this.Width;
                inkAnalyzer.ClearDataForAllStrokes();
            }
        }

        public async Task<double> GetCommentHeight()
        {
            inkAnalyzer.AddDataForStrokes(inkCan.InkPresenter.StrokeContainer.GetStrokes());
            await inkAnalyzer.AnalyzeAsync();
            var inknodes = inkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line);
            Rect bound = inkCan.InkPresenter.StrokeContainer.BoundingRect;

            if (inknodes.Count <= 1 && bound.Height <= (1.3)*COMMENT_HEIGHT)
                return COMMENT_HEIGHT;
            else
                return 2 * COMMENT_HEIGHT;
        }

        #endregion







    }
}
