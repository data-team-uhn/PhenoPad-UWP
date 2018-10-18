using Microsoft.Toolkit.Uwp.UI.Animations;
using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
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


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    enum Direction {
        TOPLEFT,
        TOPRIGHT,
        BOTTOMLEFT,
        BOTTOMRIGHT
    }
    /// <summary>
    /// Control class for canvas add-ins after drawing a rectangle on note page, shows options of operations.
    /// </summary>
    public sealed partial class AddInControl : UserControl
    {
        #region attribute definition

        private double DEFAULT_WIDTH = 400;
        private double DEFAULT_HEIGHT = 400;
        private double MIN_WIDTH = 370;
        private double MIN_HEIGHT = 370;
        public double originalWidth;
        public double originalHeight; 
        public string type; // photo, drawing
        public InkCanvas inkCan
        {
            get
            {
                return inkCanvas;
            }
        }
        InkAnalyzer inkAnalyzer = new InkAnalyzer();

        public DispatcherTimer autosaveDispatcherTimer = new DispatcherTimer();

        //https://stackoverflow.com/questions/48397647/uwp-is-there-anyway-to-implement-control-for-resizing-and-move-textbox-in-canva

        //public string name { get; }
        //public string notebookId { get; }
        //public string pageId { get; }

        public double canvasLeft;
        public double canvasTop;
        public double imgratio;

        public bool inDock;
        public double slideOffset = 250;

        private MainPage rootPage;

        private bool isInitialized = false;

        public ScaleTransform scaleTransform;
        public TranslateTransform dragTransform;
        //public double scale;

        public bool hasImage;

        public double widthOrigin;
        public double heightOrigin;
        public ScaleTransform viewFactor;
        
        public double transX
        {
            get
            {
                return dragTransform.X;
            }
        }
        public double transY
        {
            get
            {
                return dragTransform.Y;
            }
        }
        public double transScaleX
        {
            get
            {
                return scaleTransform.ScaleX;
            }
        }
        public double transScaleY
        {
            get {
                return scaleTransform.ScaleY;
            }
        }
        public Matrix3x2 previousTrans;

        public String name
        {
            get { return (String)GetValue(nameProperty); }
            set
            {
                SetValue(nameProperty, value);
            }
        }

        private bool _isResizing;
        private bool _isMoving;
        private bool _topSide;
        private bool _bottomSide;
        private bool _leftSide;
        private bool _rightSide;
        private double _curWidthRatio;


        private Direction resizeDir;
        public static readonly DependencyProperty nameProperty = DependencyProperty.Register(
         "name",
         typeof(String),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );
        public String notebookId
        {
            get { return (String)GetValue(notebookIdProperty); }
            set
            {
                SetValue(notebookIdProperty, value);
            }
        }
        public static readonly DependencyProperty notebookIdProperty = DependencyProperty.Register(
         "notebookId",
         typeof(String),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );
        public String pageId
        {
            get { return (String)GetValue(pageIdProperty); }
            set
            {
                SetValue(pageIdProperty, value);
            }
        }
        public static readonly DependencyProperty pageIdProperty = DependencyProperty.Register(
         "pageId",
         typeof(String),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );

        public bool viewOnly
        {
            get { return (bool)GetValue(viewOnlyProperty); }
            set
            {
                SetValue(viewOnlyProperty, value);
            }
        }

        public static readonly DependencyProperty viewOnlyProperty = DependencyProperty.Register(
         "viewOnly",
         typeof(bool),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );
        #endregion

        //===================================================METHODS BELOW==================================================
        #region constructors
        /// <summary>
        /// Creates a new add-in control with no parameters.
        /// </summary>
        public AddInControl()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public AddInControl GetAddInControl() {
            return this;
        }

        /// <summary>
        /// Creates and initializes a new add-in control with given parameters
        /// </summary>
        public AddInControl(string name, 
                            string notebookId, 
                            string pageId,
                            double widthOrigin, double heightOrigin)
        {

            this.InitializeComponent();
            rootPage = MainPage.Current;

            //setting pre-saved configurations of the addin control
            //this.Height = height < DEFAULT_HEIGHT ? DEFAULT_HEIGHT : height;
            //this.Width = width < DEFAULT_WIDTH ? DEFAULT_WIDTH : width;

            //setting pre-saved configurations of the control
            {
                this.widthOrigin = (widthOrigin < DEFAULT_WIDTH) ? DEFAULT_WIDTH : widthOrigin;
                this.heightOrigin = (heightOrigin < DEFAULT_HEIGHT) ? DEFAULT_HEIGHT : heightOrigin;
                Debug.WriteLine($"ORIGIN = {this.widthOrigin},{this.heightOrigin}");
                this.Width = this.widthOrigin;
                this.Height = this.heightOrigin;

                inkCan.Width = this.Width;
                inkCan.Height = this.Height - 48;
                this.inDock = false;
                this.name = name;
                this.notebookId = notebookId;
                this.pageId = pageId;
                this._isResizing = false;
                this.hasImage = false;
            }

            //Timer event handler bindings
            {
                inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
                inkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
                inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
                autosaveDispatcherTimer.Tick += AutoSaveTimer_Tick;
                //If user has not add new stroke in 0.5 seconds, tick auto timer
                autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(0.5);
            }

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

        }
        #endregion

        #region Resizing / DragTransform Event Handlers

        private void AddInManipulate_Started(object sender, ManipulationStartedRoutedEventArgs e) {

            double xPos = e.Position.X;
            double yPos = e.Position.Y;
            originalHeight = this.Height;
            originalWidth = this.Width;

            _curWidthRatio = MainPage.Current.curPage.getPageWindowRatio();

            _topSide = yPos < 48;
            _bottomSide = yPos > this.ActualHeight - 48;
            _leftSide = xPos < 48;
            _rightSide = (xPos > this.ActualWidth - 48);

            //the pointer is in one of the resizing detection area
            if (_topSide || _bottomSide || _leftSide || _rightSide)
            {
                this._isResizing = true;
                this._isMoving = false;
                Debug.WriteLine("resizing");
            }
            else {
                hideMovingGrid();
            }


        }
        private void AddInManipulate_Delta(object sender, ManipulationDeltaRoutedEventArgs e) {
            double left = Canvas.GetLeft(this);
            double top = Canvas.GetTop(this);
            this.Opacity = 0.5;
            double deltaModifier = _curWidthRatio <= 0.6 ? 2.0 : 1.2;
            Debug.WriteLine($"cur_ratio = {_curWidthRatio} modifier magnitute = {deltaModifier}");

            //corner dealings
            if (_topSide && _leftSide)
            {
                if (!hasImage)
                {
                    this.Width -= e.Delta.Translation.X * deltaModifier;
                    this.Height -= e.Delta.Translation.Y * deltaModifier;
                    if (this.Width >= this.MIN_WIDTH && this.Height >= this.MIN_HEIGHT)
                    {
                        Canvas.SetLeft(this, left + e.Delta.Translation.X * deltaModifier);
                        Canvas.SetTop(this, top + e.Delta.Translation.Y * deltaModifier);

                        this.canvasTop = Canvas.GetTop(this.inkCan);
                        this.canvasLeft = Canvas.GetLeft(this.inkCan);
                        //when extending from the top/left, shift the current strokes accordingly.
                        foreach (InkStroke st in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                            st.Selected = true;
                        inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-e.Delta.Translation.X * deltaModifier, 
                                                                                      -e.Delta.Translation.Y * deltaModifier));
                    }
                }
            }
            else if (_topSide && _rightSide) {
                if (!hasImage)
                {
                    this.Width += e.Delta.Translation.X * deltaModifier;
                    this.Height -= e.Delta.Translation.Y * deltaModifier;
                    //resetting canvas topleft corner after resizing
                    if (this.Height >= this.MIN_HEIGHT)
                    {
                        Canvas.SetTop(this, top + e.Delta.Translation.Y * deltaModifier);
                        this.canvasTop = Canvas.GetTop(this.inkCan);
                    }
                }
            }
            else if (_bottomSide && _leftSide) {
                //photo resizing will be only available for bottomright corner detection
                if (!hasImage)
                {
                    this.Width -= e.Delta.Translation.X * deltaModifier;
                    this.Height += e.Delta.Translation.Y * deltaModifier;
                    if (this.Width >= this.MIN_WIDTH)
                    {
                        Canvas.SetLeft(this, left + e.Delta.Translation.X * deltaModifier);
                        this.canvasLeft = Canvas.GetLeft(this.inkCan);
                        //when extending from the top/left, shift the current strokes accordingly.
                        foreach (InkStroke st in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                            st.Selected = true;
                        inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-e.Delta.Translation.X, 0));
                    }
                }
            }
            else if (_bottomSide && _rightSide) {
                //only enable photo ratio resizing on this corner
                if (hasImage)
                {
                    if (this.Width >= this.MIN_WIDTH && this.Height >= (this.MIN_WIDTH / imgratio + 48))
                    {

                        //only resizing based on photo width/height ratio
                        this.Height += e.Delta.Translation.Y;
                        this.Width += e.Delta.Translation.Y * imgratio;

                    }
                }
                else
                {
                    this.Width += e.Delta.Translation.X;
                    this.Height += e.Delta.Translation.Y;
                }
            }
            else if (_topSide)
            {
                if (!hasImage)
                {
                    this.Height -= e.Delta.Translation.Y;
                    //resetting canvas topleft corner after resizing
                    if (this.Height >= this.MIN_HEIGHT)
                    {
                        Canvas.SetTop(this, top + e.Delta.Translation.Y);
                        this.canvasTop = Canvas.GetTop(this.inkCan);
                    }
                }
            }
            else if (_bottomSide)
            {
                if (!hasImage) {
                    this.Height += e.Delta.Translation.Y;
                }
            }
            else if (_leftSide)
            {
                //photo resizing will be only available for bottomright corner detection
                if (!hasImage)
                {
                    this.Width -= e.Delta.Translation.X;
                    if (this.Width >= this.MIN_WIDTH)
                    {
                        Canvas.SetLeft(this, left + e.Delta.Translation.X);
                        this.canvasLeft = Canvas.GetLeft(this.inkCan);
                        //when extending from the top/left, shift the current strokes accordingly.
                        foreach (InkStroke st in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                            st.Selected = true;
                        inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-e.Delta.Translation.X, 0));
                    }
                }
            }
            else if (_rightSide)
            {
                if (! hasImage)
                    this.Width += e.Delta.Translation.X;
            }

            else
            {
                this.dragTransform.X += e.Delta.Translation.X;
                this.dragTransform.Y += e.Delta.Translation.Y;
            }

            //setting minimal resizing sizes
            if (hasImage)
            {
                this.Width = this.Width < this.MIN_WIDTH ? this.MIN_WIDTH : this.Width;
                this.Height = this.Width <= this.MIN_WIDTH ? (this.MIN_WIDTH / imgratio + 48) : this.Height;
                //proportionally zoom in/out all inkcanvas

            }
            else
            {
                //updating the inkcanvas's width/height based on how much user has extended the frame
                if (this.Width > this.inkCan.Width)
                {
                    inkCan.Width = this.Width;
                }
                if (this.Height - 48 > this.inkCan.Height)
                {
                    inkCan.Height = this.Height - 48;
                }
                this.Width = this.Width < this.MIN_WIDTH ? this.MIN_WIDTH : this.Width;
                this.Height = this.Height < this.MIN_HEIGHT ? this.MIN_HEIGHT : this.Height;
            }
        }
        private void AddInManipulate_Completed(object sender, ManipulationCompletedRoutedEventArgs e) {
            _isMoving = false;
            _isResizing = false;
            canvasLeft = Canvas.GetLeft(this);
            canvasTop = Canvas.GetTop(this);
            viewFactor.ScaleX = this.Width / this.widthOrigin;
            viewFactor.ScaleY = this.Height / (this.heightOrigin);
            Opacity = 1;
            autosaveDispatcherTimer.Start();
        }

        private void Moving_Started(object sender, ManipulationStartedRoutedEventArgs e) {
            this._isResizing = false;
            this._isMoving = true;
            _curWidthRatio = MainPage.Current.curPage.getPageWindowRatio();
            manipulateButton.IsEnabled = false;
            Opacity = 0.5;
        }
        private void Moving_Delta(object sender, ManipulationDeltaRoutedEventArgs e) {
            if (!_isResizing)
            {
                double deltaModifier = _curWidthRatio <= 0.6 ? 2.0 : 1.3;
                Debug.WriteLine($"cur_ratio = {_curWidthRatio} modifier magnitute = {deltaModifier}");
                this.dragTransform.X += e.Delta.Translation.X * deltaModifier;
                this.dragTransform.Y += e.Delta.Translation.Y * deltaModifier;
            }
        }
        private void Moving_Completed(object sender, ManipulationCompletedRoutedEventArgs e) {
            this._isResizing = false;
            this._isMoving = false;
            canvasLeft = Canvas.GetLeft(this);
            canvasTop = Canvas.GetTop(this);
            Debug.WriteLine($"\n After Manipulation: {canvasLeft},{canvasTop}, \n" +
                $"dragX={this.dragTransform.X}");
            Opacity = 1;
            autosaveDispatcherTimer.Start();
            manipulateButton.IsEnabled = true;

        }

        #endregion


        //private void Manipulator_OnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        //{
        //    Debug.WriteLine($"entered on manipulation:{e.Position.X},{e.Position.Y}");
        //    Debug.WriteLine($"canvas:{this.canvasLeft},{this.canvasTop}");
        //    bool flag1 = e.Position.X + this.canvasLeft > this.canvasLeft && e.Position.Y + this.canvasTop > this.canvasTop;
        //    bool flag2 = e.Position.X + this.canvasLeft < this.canvasLeft + 10 && e.Position.Y + this.canvasTop < this.canvasTop+10;
        //    if (flag1 && flag2)
        //    {
        //        LogService.MetroLogger.getSharedLogger().Info("is resizing");
        //        _isResizing = true;
        //    } 
        //    else _isResizing = false;
        //}

        //private void Manipulator_OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        //{
        //    if (_isResizing)
        //    {
        //        Width -= e.Delta.Translation.X;
        //        Height -= e.Delta.Translation.Y;
        //        //TODO need to set min size bound and call auto save
        //    }
        //    else
        //    {
        //        autosaveDispatcherTimer.Start();
        //        //todo only save on manipulation complete

        //        //Canvas.SetLeft(this, Canvas.GetLeft(this) + e.Delta.Translation.X);
        //        //Canvas.SetTop(this, Canvas.GetTop(this) + e.Delta.Translation.Y);
        //    }
        //}

        #region button click event handlers

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Deleting addin {this.name} from notepage.");
            ((Panel)this.Parent).Children.Remove(this);
            await rootPage.curPage.AutoSaveAddin(null);
            await rootPage.curPage.refreshAddInList();
        }

        public async void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.inDock = true;
            DoubleAnimation da = (DoubleAnimation)addinPanelHideAnimation.Children.ElementAt(0);
            da.By = rootPage.ActualWidth - (this.canvasLeft + this.dragTransform.X);

            await rootPage.curPage.AutoSaveAddin(this.name);
            await rootPage.curPage.refreshAddInList();
                rootPage.curPage.quickShowDock();
            await addinPanelHideAnimation.BeginAsync();
            this.Visibility = Visibility.Collapsed;
        }

        public async void Maximize_Addin() {
            if (this.inDock) {
                this.inDock = false;
                this.Visibility = Visibility.Visible;
                DoubleAnimation da = (DoubleAnimation) addinPanelShowAnimation.Children.ElementAt(0);
                da.By = -1 * (rootPage.ActualWidth - (this.canvasLeft +  this.dragTransform.X));
                await rootPage.curPage.AutoSaveAddin(this.name);
                await addinPanelShowAnimation.BeginAsync();
                rootPage.curPage.quickShowDock();
            }
        }

        private void DrawingButton_Click(object sender, RoutedEventArgs e)
        {
            type = "drawing";
            isInitialized = true;
            //this.ControlStackPanel.Visibility = Visibility.Visible;
            categoryGrid.Visibility = Visibility.Collapsed;

            InitiateInkCanvas();

        }

        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            this.CameraCanvas.Visibility = Visibility.Visible;
            captureControl.setUp();
            //this.imageControl.deleteAsHide();
            PhotoButton.Visibility = Visibility.Visible;
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var imageSource = await captureControl.TakePhotoAsync(notebookId,
                   pageId, name);
                if (imageSource != null)
                {
                    var file = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, NoteFileType.Image, name);
                    //MainPage.Current.curPage.AddImageControl(imagename, imageSource);
                    Image imageControl = new Image();
                    BitmapImage rawphoto = new BitmapImage(new Uri(file.Path));

                    //setting the photo width to be current frame's width
                    var properties = await file.Properties.GetImagePropertiesAsync();
                    var filewidth = properties.Width;
                    var fileheight = properties.Height;
                    //Resizing the add-in frame according to the image ratio
                    imgratio = (double)filewidth / fileheight;
                    this.Height = this.Width / imgratio + 48;
                    this.widthOrigin = this.Width;
                    this.heightOrigin = this.Height;
                    this.inkCan.Height = this.Height - 48;
                    this.inkCan.Width = this.Width;

                    imageControl.Source = rawphoto;
                    contentGrid.Children.Add(imageControl);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    this.PhotoButton.Visibility = Visibility.Collapsed;
                    this.CameraCanvas.Visibility = Visibility.Collapsed;
                    captureControl.unSetUp();
                    this.hasImage = true;
                    InitiateInkCanvas();
                }
                await rootPage.curPage.AutoSaveAddin(this.name);

            }
            catch (Exception ex)
            {
                MainPage.Current.NotifyUser("Failed to take a photo: " + ex.Message, NotifyType.ErrorMessage, 2);
            }
        }

        private void CameraClose_Click(object sender, RoutedEventArgs e)
        {
            CameraCanvas.Visibility = Visibility.Collapsed;
            captureControl.unSetUp();
        }

        private async void InsertPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            type = "photo";
            isInitialized = true;
            //this.ControlStackPanel.Visibility = Visibility.Visible;
            // Let users choose their ink file using a file picker.
            // Initialize the picker.
            Windows.Storage.Pickers.FileOpenPicker openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".tif");
            // Show the file picker.
            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            // User selects a file and picker returns a reference to the selected file.
            if (file != null)
            {
                await FileManager.getSharedFileManager().CopyPhotoToLocal(file, notebookId, pageId, name);
                var localfile = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, NoteFileType.Image, name);

                Image imageControl = new Image();
                BitmapImage rawphoto = new BitmapImage(new Uri(localfile.Path));

                //setting the photo width to be current frame's width
                var properties = await localfile.Properties.GetImagePropertiesAsync();
                var filewidth = properties.Width;
                var fileheight = properties.Height;
                //Resizing the add-in frame according to the image ratio
                imgratio = (double)filewidth / fileheight;
                this.Height = this.Width / imgratio + 48;
                this.widthOrigin = this.Width;
                this.heightOrigin = this.Height;
                this.inkCan.Height = this.Height - 48;
                this.inkCan.Width = this.Width;

                imageControl.Source = rawphoto;
                contentGrid.Children.Add(imageControl);
                categoryGrid.Visibility = Visibility.Collapsed;
                this.hasImage = true;
                InitiateInkCanvas();

                await rootPage.curPage.AutoSaveAddin(this.name);
            }
            // User selects Cancel and picker returns null.
            else
            {
                // Operation cancelled.
            }
        }


        #endregion

        #region initializations

        /// <summary>
        /// Initializes inkcanvas attributes for the add-in panel based on  whether item will be editable or not.
        /// </summary>
        private void InitiateInkCanvas(bool onlyView = false, double scaleX = 1, double scaleY = 1)
        {
            isInitialized = true;

            if (hasImage || onlyView)
              hideScrollViewer();

            scrollViewer.Visibility = Visibility.Visible;


            //this.ControlStackPanel.Visibility = Visibility.Visible;
            //contentGrid.Children.Add(inkCanvas);

            inkCan.Visibility = Visibility.Visible;
            if (!onlyView) // added from note page, need editing
            {           
                // Set supported input type to default using both moush and pen
                inkCanvas.InkPresenter.InputDeviceTypes =
                    Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                    Windows.UI.Core.CoreInputDeviceTypes.Pen;

                // Set initial ink stroke attributes and updates
                InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
                drawingAttributes.Color = Colors.Black;
                drawingAttributes.IgnorePressure = false;
                drawingAttributes.FitToCurve = true;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                //setting current active canvas to 
                inkToolbar.TargetInkCanvas = inkCanvas;
                inkToolbar.Visibility = Visibility.Visible;
            }
            else // only for viewing on page overview page
            {
                Rect bound = inkCanvas.InkPresenter.StrokeContainer.BoundingRect;
                inkCan.Height = bound.Height + bound.Top;
                inkCan.Width = bound.Width + bound.Left;
                var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                //resizeIcon.Visibility = Visibility.Collapsed;
                double ratio = bound.Width / bound.Height;

                inkCanvas.InkPresenter.InputDeviceTypes =
                    Windows.UI.Core.CoreInputDeviceTypes.None;
            }
            //await rootPage.curPage.AutoSaveAddin(this.name);

        }

        /// <summary>
        /// //Initialize a bitmap image to add-in canvas and calls InitiateInkCanvas
        /// </summary>
        public async void InitializeFromImage(WriteableBitmap wb, bool onlyView = false)
        {
            this.categoryGrid.Visibility = Visibility.Collapsed;

            Image imageControl = new Image();
            imageControl.Source = wb;
            contentGrid.Children.Add(imageControl);
            categoryGrid.Visibility = Visibility.Collapsed;

            InitiateInkCanvas(onlyView);

            // save bitmapimage to disk
            var result = await FileManager.getSharedFileManager().SaveImageForNotepage(notebookId, pageId, name, wb);
            await rootPage.curPage.AutoSaveAddin(this.name);
        }

        /// <summary>
        /// Initializing a photo to add-in through disk and calls InitiateInkCanvas
        /// </summary>
        public async void InitializeFromDisk(bool onlyView)
        {

            this.categoryGrid.Visibility = Visibility.Collapsed;
            try
            {
                var annofile = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, 
                                                                                             NoteFileType.ImageAnnotation, name);
                if (annofile != null)
                    await FileManager.getSharedFileManager().loadStrokes(annofile, inkCanvas);

                // photo file
                var file = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, NoteFileType.Image, name);
                if (file != null)
                {
                    //inkCan.Height = this.Height / viewFactor.ScaleY - 88;
                    //inkCan.Width = this.Width / viewFactor.ScaleX;
                    var properties = await file.Properties.GetImagePropertiesAsync();
                    imgratio = (double)properties.Width / properties.Height;
                    BitmapImage img = new BitmapImage(new Uri(file.Path));
                    Image photo = new Image();
                    photo.Source = img;
                    photo.Visibility = Visibility.Visible;
                    contentGrid.Children.Add(photo);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    this.hasImage = true;
                    //inkCan.RenderTransform = viewFactor;
                }
                //stroke-only addin, resetting canvas dimension to bound saved storkes
                else
                {
                    Rect bound = inkCanvas.InkPresenter.StrokeContainer.BoundingRect;
                    inkCan.Height = bound.Height + bound.Top + 20;
                    inkCan.Width = bound.Width + bound.Left + 20;
                }

                InitiateInkCanvas(onlyView);



            }
            catch (NullReferenceException e) {
                LogService.MetroLogger.getSharedLogger().Error($"{e}");
            }


        }

        /// <summary>
        /// When user control is loaded, sets the default size and initialize from disk.
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (viewOnly)
            {
                this.Width = DEFAULT_WIDTH;
                this.Height = DEFAULT_HEIGHT;
                TitleRelativePanel.Visibility = Visibility.Collapsed;
                InitializeFromDisk(true);
            }
        }

        public void refreshZoom(double scaleX,double scaleY) {
            if (hasImage) {
                ScaleTransform scale = new ScaleTransform();
                scale.ScaleX = scaleX;
                scale.ScaleY = scaleY;
                this.viewFactor = scale;
            }
        }
        #endregion

        #region Auto save drawings
        private void StrokeInput_StrokeStarted(object sender, object e)
        {
            autosaveDispatcherTimer.Stop();
        }

        private void StrokeInput_StrokeEnded(object sender, object e)
        {
            //detected stroke change, start the timer
            autosaveDispatcherTimer.Start();
        }

        private void InkPresenter_StrokesErased(object sender, object e)
        {
            autosaveDispatcherTimer.Start();
        }

        //Invoked when strokes are processed from wet to dry, tries to call autosave on inkcanvas
        private async void AutoSaveTimer_Tick(object sender, object e)
        {
            await rootPage.curPage.AutoSaveAddin(this.name);
            //stop the timer after saving 
            autosaveDispatcherTimer.Stop();
        }
        #endregion

        #region panel show / hide
        public void showControlPanel()
        {
            if (isInitialized) ;
            //this.ControlStackPanel.Visibility = Visibility.Visible;

        }
        public void hideControlPanel()
        {
            if (isInitialized) ;
            //this.ControlStackPanel.Visibility = Visibility.Collapsed;
        }
        public void showMovingGrid(object sender, RoutedEventArgs e)
        {
            MovingGrid.Visibility = Visibility.Visible;
            //only showing the bottom right resize arrow symbol for resizing
            if (hasImage) {
                topA.Visibility = Visibility.Collapsed;
                bottomA.Visibility = Visibility.Collapsed;
                leftA.Visibility = Visibility.Collapsed;
                rightA.Visibility = Visibility.Collapsed;
                tlA.Visibility = Visibility.Collapsed;
                trA.Visibility = Visibility.Collapsed;
                blA.Visibility = Visibility.Collapsed;                 
            }
        }
        public void hideMovingGrid()
        {
            MovingGrid.Visibility = Visibility.Collapsed;
            topA.Visibility = Visibility.Visible;
            bottomA.Visibility = Visibility.Visible;
            leftA.Visibility = Visibility.Visible;
            rightA.Visibility = Visibility.Visible;
            tlA.Visibility = Visibility.Visible;
            trA.Visibility = Visibility.Visible;
            blA.Visibility = Visibility.Visible;
        }
        public void hideControlUI()
        {
            OutlineGrid.Background = new SolidColorBrush(Colors.Transparent);
            this.TitleRelativePanel.Visibility = Visibility.Collapsed;
            this.inkToolbar.Visibility = Visibility.Collapsed;

        }
        public void showControlUI()
        {
            OutlineGrid.Background = new SolidColorBrush(Colors.WhiteSmoke);
            this.TitleRelativePanel.Visibility = Visibility.Visible;
            this.inkToolbar.Visibility = Visibility.Visible;
        }
        public void hideScrollViewer() {
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            scrollViewer.ZoomMode = ZoomMode.Disabled;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        #endregion

        private void manipulateButton_Holding(object sender, HoldingRoutedEventArgs e)
        {
            this._isResizing = false;
            this._isMoving = true;
        }

        private void noImageManipulation_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Debug.WriteLine("tapped");
            if (!_isResizing) {
                hideMovingGrid();
            }
        }
    }

}