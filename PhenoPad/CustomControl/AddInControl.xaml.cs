using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    /// <summary>
    /// Control class for canvas add-ins after drawing a rectangle on note page, shows options of operations.
    /// </summary>
    public sealed partial class AddInControl : UserControl
    {
        #region attribute definition

        private double DEFAULT_WIDTH = 400;
        private double DEFAULT_HEIGHT = 400;
        public string type; // photo, drawing
        InkAnalyzer inkAnalyzer = new InkAnalyzer();
        IReadOnlyList<InkStroke> inkStrokes = null;
        InkAnalysisResult inkAnalysisResults = null;

        DispatcherTimer autosaveDispatcherTimer = new DispatcherTimer();


        //public string name { get; }
        //public string notebookId { get; }
        //public string pageId { get; }

        public double height;
        public double width;
        public double canvasLeft;
        public double canvasTop;

        private MainPage rootPage;

        private bool isInitialized = false;
        public ScaleTransform scaleTransform;
        public TranslateTransform dragTransform;
        public double scale;
        public InkCanvas inkCan
        {
            get
            {
                return inkCanvas;
            }
        }
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
        public double transScale
        {
            get
            {
                return scaleTransform.ScaleX;
            }
        }

        public String name
        {
            get { return (String)GetValue(nameProperty); }
            set
            {
                SetValue(nameProperty, value);
            }
        }

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
        public AddInControl(string name, string notebookId, string pageId, double width = -1, double height = -1)
        {
            this.InitializeComponent();
            this.Height = height < DEFAULT_HEIGHT ? DEFAULT_HEIGHT : height;
            this.Width = width < DEFAULT_WIDTH ? DEFAULT_WIDTH : width;            

            this.name = name;
            this.notebookId = notebookId;
            this.pageId = pageId;
            rootPage = MainPage.Current;

            //Timer event handler bindings
            {
                inkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
                inkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
                inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
                autosaveDispatcherTimer.Tick += AutoSaveTimer_Tick;
                //If user has not add new stroke in 3 seconds, tick auto timer
                autosaveDispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            }

            scaleTransform = new ScaleTransform();
            dragTransform = new TranslateTransform();
            TransformGroup tg = new TransformGroup();
            tg.Children.Add(scaleTransform);
            tg.Children.Add(dragTransform);
            this.RenderTransform = tg;
            Debug.WriteLine("added new image component");
            /**
            this.CanDrag = true;
            this.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.Scale;
            this.ManipulationStarted += TitleRelativePanel_ManipulationStarted;
            this.ManipulationDelta += TitleRelativePanel_ManipulationDelta;
            this.ManipulationCompleted += TitleRelativePanel_ManipulationCompleted;
            **/

            //translateTransform = new TranslateTransform();
            //this.RenderTransform.translateTransform;

            /**
            TitleRelativePanel.ManipulationDelta += delegate (object sdr, ManipulationDeltaRoutedEventArgs args)
            {
                if (args.Delta.Expansion == 0)
                {
                    Canvas.SetLeft(this, Canvas.GetLeft(this) + args.Delta.Translation.X);
                    Canvas.SetTop(this, Canvas.GetTop(this) + args.Delta.Translation.Y);
                }
                else
                {
                    //canvasAddIn.Width += args.Delta.Translation.X * 2;
                    //canvasAddIn.Height += args.Delta.Translation.Y * 2;
                }
            };
    **/
        }
        #endregion

        #region title relative panel
        private void TitleRelativePanel_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            // this.Opacity = 1;
            MovingGrid.Visibility = Visibility.Collapsed;
            Debug.WriteLine("Add-in control manipulation completed");
            
        }

        private void TitleRelativePanel_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            // this.Opacity = 0.4;
            MovingGrid.Visibility = Visibility.Visible;
            Debug.WriteLine("Add-in control manipulation started");
        }

        private async void TitleRelativePanel_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            /**
            canvasLeft = Canvas.GetLeft(this) + e.Delta.Translation.X > 0 ? Canvas.GetLeft(this) + e.Delta.Translation.X : 0;
            canvasTop = Canvas.GetTop(this) + e.Delta.Translation.Y > 0 ? Canvas.GetTop(this) + e.Delta.Translation.Y : 0;
            Canvas.SetLeft(this, canvasLeft);
            Canvas.SetTop(this, canvasTop);
            **/
            dragTransform.X += e.Delta.Translation.X;
            dragTransform.Y += e.Delta.Translation.Y;


            scale = scaleTransform.ScaleX * e.Delta.Scale;
            scale = scale > 2.0 ? 2.0 : scale;
            scale = scale < 0.5 ? 0.5 : scale;
            scaleTransform.ScaleX = scale;

            scale = scaleTransform.ScaleY * e.Delta.Scale;
            scale = scale > 2.0 ? 2.0 : scale;
            scale = scale < 0.5 ? 0.5 : scale;
            scaleTransform.ScaleY = scale;

            await rootPage.curPage.AutoSaveAddin(this.name);

        }
        #endregion

        #region button click event handlers

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Deleting addin {this.name} from notepage.");
            ((Panel)this.Parent).Children.Remove(this);
            await rootPage.curPage.AutoSaveAddin(null);
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
            this.imageControl.deleteAsHide();
            PhotoButton.Visibility = Visibility.Visible;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException("editbutton_click not implemented");
        }

        private async void InsertPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            type = "photo";
            isInitialized = true;
            //this.ControlStackPanel.Visibility = Visibility.Visible;
            // Let users choose their ink file using a file picker.
            // Initialize the picker.
            Windows.Storage.Pickers.FileOpenPicker openPicker =
                new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".tif");
            // Show the file picker.
            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            // User selects a file and picker returns a reference to the selected file.
            if (file != null)
            {
                await FileService.FileManager.getSharedFileManager().CopyPhotoToLocal(file, notebookId, pageId, name);

                // Open a file stream for reading.
                IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                    SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
                    await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
                    Image imageControl = new Image();
                    imageControl.Source = bitmapSource;
                    contentGrid.Children.Add(imageControl);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    InitiateInkCanvas();
                }
                stream.Dispose();
                await rootPage.curPage.AutoSaveAddin(this.name);
            }
            // User selects Cancel and picker returns null.
            else
            {
                // Operation cancelled.
            }
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var imageSource = await captureControl.TakePhotoAsync(notebookId,
                   pageId, name);
                if (imageSource != null)
                {
                    //MainPage.Current.curPage.AddImageControl(imagename, imageSource);
                    Image imageControl = new Image();
                    //imageControl.Source = imageSource;
                    imageControl.Source = new BitmapImage(new Uri(imageSource));
                    contentGrid.Children.Add(imageControl);
                    categoryGrid.Visibility = Visibility.Collapsed;
                    InitiateInkCanvas();
                    this.PhotoButton.Visibility = Visibility.Collapsed;
                    this.CameraCanvas.Visibility = Visibility.Collapsed;
                    captureControl.unSetUp();
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
        #endregion

        #region initializations

        /// <summary>
        /// Initializes inkcanvas attributes for the add-in panel based on  whether item will be editable or not.
        /// </summary>
        private void InitiateInkCanvas(bool onlyView = false)
        {
            isInitialized = true;
            //this.ControlStackPanel.Visibility = Visibility.Visible;
            //contentGrid.Children.Add(inkCanvas);
            inkCanvas.Visibility = Visibility.Visible;
            if (!onlyView) // added from note page, need editing
            {
                // Set supported input type to default using both moush and pen
                inkCanvas.InkPresenter.InputDeviceTypes =
                    Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                    Windows.UI.Core.CoreInputDeviceTypes.Pen;

                // Set initial ink stroke attributes and updates
                InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
                drawingAttributes.Color = Windows.UI.Colors.Black;
                drawingAttributes.IgnorePressure = false;
                drawingAttributes.FitToCurve = true;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                //setting current active canvas to 
                inkToolbar.TargetInkCanvas = inkCanvas;
                inkToolbar.Visibility = Visibility.Visible;

            }
            else // only for viewing on page overview page
            {
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
        public async void InitializeFromDisk(bool onlyView = false, double transX = 0, double transY = 0, double transScale = 0)
        {
            this.categoryGrid.Visibility = Visibility.Collapsed;
            // photo file
            var file = await FileService.FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, FileService.NoteFileType.Image, name);
            if (file != null)
            {
                // Open a file stream for reading.
                IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                    SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
                    await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
                    Image imageControl = new Image();
                    imageControl.Source = bitmapSource;
                    contentGrid.Children.Add(imageControl);
                    categoryGrid.Visibility = Visibility.Collapsed;

                }
                stream.Dispose();
            }

            InitiateInkCanvas(onlyView);

            var annofile = await FileService.FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, pageId, FileService.NoteFileType.ImageAnnotation, name);
            if (annofile != null)
                await FileService.FileManager.getSharedFileManager().loadStrokes(annofile, inkCanvas);

            /**
                string strokeUri = "ms-appdata:///local/" + FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotation, name);
                BitmapIcon anno = new BitmapIcon();
                anno.UriSource = new Uri(strokeUri);
                contentGrid.Children.Add(anno);
    **/
            if (!onlyView)
            {
                dragTransform.X = transX;
                dragTransform.Y = transY;
                scaleTransform.ScaleX = transScale;
                scaleTransform.ScaleY = transScale;
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
        public void showMovingGrid()
        {
            MovingGrid.Visibility = Visibility.Visible;
        }
        public void hideMovingGrid()
        {
            MovingGrid.Visibility = Visibility.Collapsed;
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
        #endregion

    }
}