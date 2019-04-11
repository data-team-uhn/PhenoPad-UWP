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
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Storage;
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
    public enum Direction {
        TOPLEFT,
        TOPRIGHT,
        BOTTOMLEFT,
        BOTTOMRIGHT
    }
    public enum AddinType
    {
        DRAWING,
        IMAGE,
        PHOTO,
        VIDEO,
        VIEWONLY,
        EHR // EHR also has 4 types of enum, see AddInAnnotationControl.cs
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
        public InkAnalyzer inkAnalyzer = new InkAnalyzer();
        public Visibility videoVisibility = Visibility.Collapsed;

        public DispatcherTimer autosaveDispatcherTimer = new DispatcherTimer();

        //https://stackoverflow.com/questions/48397647/uwp-is-there-anyway-to-implement-control-for-resizing-and-move-textbox-in-canva

        public double canvasLeft;
        public double canvasTop;

        public bool inDock;
        public double slideOffset = 250;

        private MainPage rootPage;

        private bool isInitialized = false;

        public ScaleTransform scaleTransform;
        public TranslateTransform dragTransform;

        public AddinType addinType;

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
        private StorageFile temp;

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

        public int CommentID
        {
            get { return (int)GetValue(commentIDProperty); }
            set
            {
                SetValue(commentIDProperty, value);
            }
        }

        public static readonly DependencyProperty commentIDProperty = DependencyProperty.Register(
         "commentID",
         typeof(int),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );

        public AddinType AddinType
        {
            get { return (AddinType)GetValue(addinTypeProperty); }
            set
            {
                SetValue(addinTypeProperty, value);
            }
        }

        public static readonly DependencyProperty addinTypeProperty = DependencyProperty.Register(
         "addinType",
         typeof(AddinType),
         typeof(TextBlock),
         new PropertyMetadata(null)
       );

        public double imgratio
        {
            get { return (double)GetValue(imgratioProperty); }
            set
            {
                SetValue(imgratioProperty, value);
            }
        }

        public static readonly DependencyProperty imgratioProperty = DependencyProperty.Register(
         "imgratio",
         typeof(double),
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
                commentID = -1;
                //by default sets addinTypt to viewonly
                addinType = AddinType.VIEWONLY;
                videoVisibility = Visibility.Collapsed;
                
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
                this.commentID = -1; 
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
            Canvas.SetZIndex(this, 90);
            commentbg.Visibility = Visibility.Collapsed;
            addinType = AddinType.VIEWONLY;
            videoVisibility = Visibility.Collapsed;


        }

        #endregion

        #region Resizing / DragTransform Event Handlers
        /// <summary>
        /// invoked when user is starting the resizing (when pointer is pressed on addin at hit area)
        /// </summary>
        private void AddInManipulate_Started(object sender, ManipulationStartedRoutedEventArgs e) {

            double xPos = e.Position.X;
            double yPos = e.Position.Y;
            originalHeight = this.Height;
            originalWidth = this.Width;

            _curWidthRatio = MainPage.Current.curPage.getPageWindowRatio();

            _topSide = yPos < 48;
            _bottomSide = yPos > this.Height - 48;
            _leftSide = xPos < 48;
            _rightSide = xPos > this.Width - 48 ;

            //the pointer is in one of the resizing detection area
            if (_topSide || _bottomSide || _leftSide || _rightSide)
            {
                this._isResizing = true;
                this._isMoving = false;
                Debug.WriteLine($"resizing: top={_topSide},right={_rightSide},bottom={_bottomSide},left={_leftSide}");
            }
            else {
                hideMovingGrid();
            }


        }
        /// <summary>
        /// invoked when user is resizing the add-in
        /// </summary>
        private void AddInManipulate_Delta(object sender, ManipulationDeltaRoutedEventArgs e) {
            Opacity = 0.5;
            double left = Canvas.GetLeft(this);
            double top = Canvas.GetTop(this);
            //used to adjust sizing magnitute depending on user's zoom magnitute
            double deltaModifier = 1.0 / _curWidthRatio;
            //preselects all strokes for transformation 
            foreach (InkStroke st in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                st.Selected = true;
            //Pre-testing if a resizing operation is valid and setting condition flags         
            Rect strokeBounding = inkCan.InkPresenter.StrokeContainer.BoundingRect;
            bool canSize_Right = Width + e.Delta.Translation.X * deltaModifier >= MIN_WIDTH &&
                Width + e.Delta.Translation.X * deltaModifier >= strokeBounding.Left + strokeBounding.Width;

            bool canSize_Left = Width - e.Delta.Translation.X * deltaModifier >= MIN_WIDTH &&
                Width - e.Delta.Translation.X * deltaModifier >= inkCan.Width - strokeBounding.Left;

            bool canSize_Bottom = Height + e.Delta.Translation.Y * deltaModifier >= MIN_HEIGHT &&
                Height + e.Delta.Translation.Y * deltaModifier >= strokeBounding.Top + strokeBounding.Height + 48;

            bool canSize_Top = Height - e.Delta.Translation.Y * deltaModifier >= MIN_HEIGHT &&
                Height - e.Delta.Translation.Y * deltaModifier >= inkCan.Height - strokeBounding.Top + 48;
            bool noStroke = inkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count() == 0;
            
            //For resizing addins with image plugins
            if (_bottomSide && _rightSide && hasImage)
            {
                //only enable photo ratio resizing on this corner
                bool resizeX = Width + e.Delta.Translation.Y * deltaModifier * imgratio >= MIN_WIDTH;
                bool resizeY = Height + e.Delta.Translation.Y * deltaModifier >= MIN_WIDTH / imgratio + 48;
                if ( resizeX && resizeY )
                {
                    //only resizing based on photo width/height ratio
                    this.Height += e.Delta.Translation.Y * deltaModifier;
                    this.Width += e.Delta.Translation.Y * deltaModifier * imgratio;
                }
            }

            //Dealing with single sided extensions for drawing mode
            if ( _topSide && !hasImage)
            {
                if (canSize_Top || noStroke) {
                    this.Height -= e.Delta.Translation.Y * deltaModifier;
                    inkCan.Height = this.Height - 48;
                    Canvas.SetTop(this, top + e.Delta.Translation.Y * deltaModifier);
                    this.canvasTop = Canvas.GetTop(this.inkCan);
                    inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(0, -e.Delta.Translation.Y * deltaModifier));
                }
            }
            if (_leftSide && !hasImage )
            {
                if (canSize_Left || noStroke) {
                    this.Width -= e.Delta.Translation.X * deltaModifier;
                    inkCan.Width -= e.Delta.Translation.X * deltaModifier;
                    Canvas.SetLeft(this, left + e.Delta.Translation.X * deltaModifier);
                    inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-e.Delta.Translation.X * deltaModifier, 0));

                }
            }
            if ( _bottomSide && !hasImage )
            {
                if (canSize_Bottom || noStroke) {
                    this.Height += e.Delta.Translation.Y * deltaModifier;
                    inkCan.Height += e.Delta.Translation.Y * deltaModifier;

                }
            }
            if ( _rightSide && !hasImage)
            {
                if (canSize_Right || noStroke) {
                    this.Width += e.Delta.Translation.X * deltaModifier;
                    inkCan.Width += e.Delta.Translation.X * deltaModifier;
                }
            }
        }
        /// <summary>
        /// invoked when a resizing action is completed by the user
        /// </summary>
        private async void AddInManipulate_Completed(object sender, ManipulationCompletedRoutedEventArgs e) {
            _isMoving = false;
            _isResizing = false;
            canvasLeft = Canvas.GetLeft(this);
            canvasTop = Canvas.GetTop(this);
            viewFactor.ScaleX = this.Width / this.widthOrigin;
            viewFactor.ScaleY = this.Height / (this.heightOrigin);
            Opacity = 1;
            await rootPage.curPage.AutoSaveAddin(this.name);
        }
        /// <summary>
        /// invoked when user is starting to move the add in (when pointer is pressed in hit area)
        /// </summary>
        private void Moving_Started(object sender, ManipulationStartedRoutedEventArgs e) {
            this._isResizing = false;
            this._isMoving = true;
            _curWidthRatio = MainPage.Current.curPage.getPageWindowRatio();
            manipulateButton.IsEnabled = false;
            Opacity = 0.5;
        }
        /// <summary>
        /// invoked when user is moving the addin
        /// </summary>
        private void Moving_Delta(object sender, ManipulationDeltaRoutedEventArgs e) {
            if (!_isResizing)
            {
                //deals with dragging speed relative to current zoom ratio of notepage
                double deltaModifier = (1.0 / _curWidthRatio);
                //Debug.WriteLine($"cur_ratio = {_curWidthRatio} modifier magnitute = {deltaModifier}");
                this.dragTransform.X += e.Delta.Translation.X * deltaModifier;
                this.dragTransform.Y += e.Delta.Translation.Y * deltaModifier;
            }
        }
        /// <summary>
        /// invoked when user has finished moving the add in 
        /// </summary>
        private async void Moving_Completed(object sender, ManipulationCompletedRoutedEventArgs e) {
            this._isResizing = false;
            this._isMoving = false;
            canvasLeft = Canvas.GetLeft(this);
            canvasTop = Canvas.GetTop(this);
            Opacity = 1;
            await rootPage.curPage.AutoSaveAddin(this.name);
            manipulateButton.IsEnabled = true;
        }

        #endregion

        #region button click event handlers

        public async void Delete_Click(object sender = null, RoutedEventArgs e = null)
        {
            ((Panel)this.Parent).Children.Remove(this);
            await rootPage.curPage.AutoSaveAddin(null);
            await rootPage.curPage.refreshAddInList();
            await FileManager.getSharedFileManager().DeleteAddInFile(notebookId, pageId, name);
        }

        public async void Minimize_Click(object sender = null, RoutedEventArgs e = null)
        {
            this.inDock = true;
            var element_Visual_Relative = MainPage.Current.curPage.TransformToVisual(MainPage.Current);
            Point point  = element_Visual_Relative.TransformPoint(new Point(0, 0));
            DoubleAnimation da = (DoubleAnimation)addinPanelHideAnimation.Children.ElementAt(0);
            da.By = point.X + MainPage.Current.curPage.ActualWidth - (this.canvasLeft + this.dragTransform.X);

            await MainPage.Current.curPage.AutoSaveAddin(this.name);
            await MainPage.Current.curPage.refreshAddInList();
            MainPage.Current.curPage.quickShowDock();
            await addinPanelHideAnimation.BeginAsync();
            this.Visibility = Visibility.Collapsed;
        }


        public async void Maximize_Addin() {
            if (this.inDock) {
                this.inDock = false;
                var element_Visual_Relative = MainPage.Current.curPage.TransformToVisual(MainPage.Current);
                Point point = element_Visual_Relative.TransformPoint(new Point(0, 0));
                DoubleAnimation da = (DoubleAnimation)addinPanelShowAnimation.Children.ElementAt(0);
                DoubleAnimation daHide = (DoubleAnimation)addinPanelHideAnimation.Children.ElementAt(0);
                //for handling first time loading from disk
                if ( ! (daHide.By > 0)) {
                    Duration temp = daHide.Duration;
                    daHide.Duration -= daHide.Duration;
                    daHide.By = point.X + MainPage.Current.curPage.ActualWidth - (this.canvasLeft + this.dragTransform.X);
                    await addinPanelHideAnimation.BeginAsync();
                    daHide.Duration = temp;
                }
                da.By = daHide.By * (-1);

                this.Visibility = Visibility.Visible;
                await MainPage.Current.curPage.AutoSaveAddin(this.name);
                await addinPanelShowAnimation.BeginAsync();
                MainPage.Current.curPage.quickShowDock();
            }
        }

        private void DrawingButton_Click(object sender, RoutedEventArgs e)
        {
            type = "drawing";
            addinType = AddinType.DRAWING;
            isInitialized = true;
            //this.ControlStackPanel.Visibility = Visibility.Visible;
            categoryGrid.Visibility = Visibility.Collapsed;

            InitiateInkCanvas();

        }
        
        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {/// <summary>invoked when user selects take a photo from category grid</summary>
            this.CameraCanvas.Visibility = Visibility.Visible;
            captureControl.setUp();
            //this.imageControl.deleteAsHide();
            PhotoButton.Visibility = Visibility.Visible;
            VideoButton.Visibility = Visibility.Visible;
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e) {
            addinType = AddinType.VIDEO;
            temp = await captureControl.StartRecordingAsync(notebookId,pageId, name);
            MainPage.Current.NotifyUser("Recording Started", NotifyType.StatusMessage, 1);
            PhotoButton.Visibility = Visibility.Collapsed;
            VideoButton.Visibility = Visibility.Collapsed;
            VideoStopButton.Visibility = Visibility.Visible;
            videoVisibility = Visibility.Visible;
            UpdateLayout();


        }

        private async void VideoStopButton_Click(object sender, RoutedEventArgs e)
        {

            captureControl.OnTimeUpStopRecording();
            MainPage.Current.NotifyUser("Recording Ended", NotifyType.StatusMessage, 1);
            mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(temp);
            var properties = await temp.Properties.GetVideoPropertiesAsync();
            imgratio = (double)properties.Width / properties.Height;
            Debug.WriteLine($"imgratio of video={imgratio}+***********");
            mediaPlayerElement.Visibility = Visibility.Visible;
            categoryGrid.Visibility = Visibility.Collapsed;
            PhotoButton.Visibility = Visibility.Collapsed;
            VideoButton.Visibility = Visibility.Collapsed;
            VideoStopButton.Visibility = Visibility.Collapsed;
            CameraCanvas.Visibility = Visibility.Collapsed;
            inkToolbar.Visibility = Visibility.Collapsed;
            captureControl.unSetUp();
            hasImage = true;
            InitiateInkCanvas();
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {/// <summary>invoked when user is in camera preview mode and clicks on the camera button</summary>
            try
            {
                addinType = AddinType.PHOTO;
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
                    Height = Width / imgratio + 48;
                    widthOrigin = Width;
                    heightOrigin = Height;
                    inkCan.Height = Height - 48;
                    inkCan.Width =  Width;

                    imageControl.Source = rawphoto;
                    contentGrid.Children.Add(imageControl);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    PhotoButton.Visibility = Visibility.Collapsed;
                    VideoButton.Visibility = Visibility.Collapsed;
                    CameraCanvas.Visibility = Visibility.Collapsed;
                    mediaPlayerElement.Visibility = Visibility.Collapsed;
                    captureControl.unSetUp();
                    hasImage = true;
                    InitiateInkCanvas();
                }
                await rootPage.curPage.AutoSaveAddin(name);

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
            addinType = AddinType.IMAGE;
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
       
       
        //public async void InitializeFromImage(WriteableBitmap wb, bool onlyView = false)
        //{/// <summary>Initialize a bitmap image to add-in canvas and calls InitiateInkCanvas</summary>
        //    this.categoryGrid.Visibility = Visibility.Collapsed;

        //    Image imageControl = new Image();
        //    imageControl.Source = wb;
        //    contentGrid.Children.Add(imageControl);
        //    categoryGrid.Visibility = Visibility.Collapsed;

        //    InitiateInkCanvas(onlyView);

        //    // save bitmapimage to disk
        //    var result = await FileManager.getSharedFileManager().SaveImageForNotepage(notebookId, pageId, name, wb);
        //    await rootPage.curPage.AutoSaveAddin(this.name);
        //}
       
        public async void InitializeFromDisk(bool onlyView)
        {/// <summary>Initializing a photo to add-in through disk and calls InitiateInkCanvas</summary>

            categoryGrid.Visibility = Visibility.Collapsed;
            try
            {
                if (addinType != AddinType.EHR || commentID == -1)
                {
                    inkCan.Height = Height - 48;
                    inkCan.Width = Width;
                }

                //try to load strokes from disk
                var strokefile = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId,                                                                                             NoteFileType.ImageAnnotation, name);
                if (strokefile != null)
                {
                    await FileManager.getSharedFileManager().loadStrokes(strokefile, inkCanvas);
                    //addinType = AddinType.DRAWING;
                }

                //try to load image file from disk
                var Imagefile = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, NoteFileType.Image, name);
                if (Imagefile != null) {
                    var properties = await Imagefile.Properties.GetImagePropertiesAsync();
                    imgratio = (double)properties.Width / properties.Height;
                    BitmapImage img = new BitmapImage(new Uri(Imagefile.Path));
                    Image photo = new Image();
                    photo.Source = img;
                    photo.Visibility = Visibility.Visible;
                    contentGrid.Children.Add(photo);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    hasImage = true;
                    addinType = AddinType.IMAGE;
                }

                //try to load video file from disk
                var video = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, NoteFileType.Video, name);
                if (video != null) {
                    var properties = await video.Properties.GetVideoPropertiesAsync();
                    imgratio = (double)properties.Width / properties.Height;
                    Debug.WriteLine($"imgratio of video={imgratio}");
                    mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(video);
                    hasImage = true;
                    addinType = AddinType.VIDEO;
                }

                InitiateInkCanvas(onlyView);
            }
            catch (FileNotFoundException) { Debug.WriteLine("file not found, shouldn't reach here :"); }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"{e}:{e.Message}");
            }
        }

        private void InitiateInkCanvas(bool onlyView = false, double scaleX = 1, double scaleY = 1)
        {/// <summary>Initializes inkcanvas attributes for the add-in panel based on  whether item will be editable or not.</summary>

            isInitialized = true;
            scrollViewer.Visibility = Visibility.Visible;
            inkCan.Visibility = Visibility.Visible;

            //disables scroll viewer for all addins that are not drawings
            if (addinType != AddinType.DRAWING || onlyView)
                hideScrollViewer();


            if (addinType == AddinType.EHR || commentID != -1)
            {
                inkCan.Height = this.Height;
                inkCan.Width = this.Width;
                TitleRelativePanel.Visibility = Visibility.Collapsed;
                if (anno_type == AnnotationType.TextComment || anno_type == AnnotationType.TextInsert)
                    inkCan.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (!onlyView)
                {// added from note page, need editing       
                 // Set supported input type to default using both moush and pen
                    inkCanvas.InkPresenter.InputDeviceTypes = Windows.UI.Core.CoreInputDeviceTypes.Pen;

                    // Set initial ink stroke attributes and updates
                    InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
                    drawingAttributes.Color = Colors.Black;
                    drawingAttributes.IgnorePressure = false;
                    drawingAttributes.FitToCurve = true;
                    inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                    //setting current active canvas to 
                    inkToolbar.TargetInkCanvas = inkCanvas;
                    inkToolbar.Visibility = addinType == AddinType.VIDEO? Visibility.Collapsed: Visibility.Visible;
                    inkCanvas.Visibility = addinType == AddinType.VIDEO? Visibility.Collapsed : Visibility.Visible;
                    mediaPlayerElement.Visibility = addinType == AddinType.VIDEO ? Visibility.Visible: Visibility.Collapsed;
                }
                else
                {// only for viewing on page overview page / addin collection dock
                    inkCanvas.InkPresenter.IsInputEnabled = false;
                    if (hasImage)
                    {//when loading an image, had to manually adjust dimension to display full size strokes
                        TranslateTransform tt = new TranslateTransform();
                        tt.Y = -24;
                        contentGrid.RenderTransform = tt;
                        Width = 400;
                        Height = (int)(Width / imgratio);
                        inkCan.Height = Height;
                        inkCan.Width = Width;
                        mediaPlayerElement.Visibility = addinType == AddinType.VIDEO ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {//adjust ink canvas size/position to display full stroke view
                        Rect bound = inkCanvas.InkPresenter.StrokeContainer.BoundingRect;
                        double ratio = bound.Width / bound.Height;
                        inkCan.Height = bound.Height + 10;
                        inkCan.Width = bound.Width + 10;
                        foreach (InkStroke st in inkCanvas.InkPresenter.StrokeContainer.GetStrokes())
                            st.Selected = true;
                        inkCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-bound.Left, -bound.Top));
                    }
                }
            }

        }


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {/// <summary> When user control is loaded, sets the default size and initialize from disk.</summary>
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
            //if (isInitialized)
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
        #endregion

    }

}