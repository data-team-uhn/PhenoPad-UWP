﻿using System;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using PhenoPad.PhenotypeService;
using Windows.UI.Xaml.Input;
using System.Collections.Generic;
using Windows.UI.Popups;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using System.Text;
using Windows.Globalization;
using PhenoPad.SpeechService;
using Windows.Devices.Sensors;
using Windows.UI;
using System.Diagnostics;
using PhenoPad.CustomControl;
using Windows.Graphics.Display;
using System.Reflection;
using System.Linq;
using Windows.UI.Xaml.Media.Animation;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PhenoPad.WebSocketService;
using Windows.ApplicationModel.Core;
using PhenoPad.Styles;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using PhenoPad.FileService;
using Windows.UI.Xaml.Data;
using System.ComponentModel;
using System.Threading;

using PhenoPad.BluetoothService;

namespace PhenoPad
{
    // MyScript 
    public class FlyoutCommand : System.Windows.Input.ICommand
    {
        public delegate void InvokedHandler(FlyoutCommand command);

        public string Id { get; set; }
        private InvokedHandler _handler = null;

        public FlyoutCommand(string id, InvokedHandler handler)
        {
            Id = id;
            _handler = handler;
        }

        public bool CanExecute(object parameter)
        {
            return _handler != null;
        }

        public void Execute(object parameter)
        {
            _handler(this);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class CalligraphicPen : InkToolbarCustomPen
    {
        public CalligraphicPen()
        {
        }

        protected override InkDrawingAttributes CreateInkDrawingAttributesCore(Brush brush, double strokeWidth)
        {

            InkDrawingAttributes inkDrawingAttributes = new InkDrawingAttributes();
            inkDrawingAttributes.PenTip = PenTipShape.Circle;
            inkDrawingAttributes.IgnorePressure = false;
            SolidColorBrush solidColorBrush = (SolidColorBrush)brush;

            if (solidColorBrush != null)
            {
                inkDrawingAttributes.Color = solidColorBrush.Color;
            }

            inkDrawingAttributes.Size = new Size(strokeWidth, 2.0f * strokeWidth);
            //inkDrawingAttributes.Size = new Size(strokeWidth, strokeWidth);
            inkDrawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.CreateRotation((float)(Math.PI * 45 / 180));

            return inkDrawingAttributes;
        }
        
    }

    // private MainPage rootPage = MainPage.Current;

    /// <summary>
    /// This page shows the code to configure the InkToolbar.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {

        private bool _videoOn = false;
        public bool VideoOn
        {
            get
            {
                return this._videoOn;
            }

            set
            {
                if (value != this._videoOn)
                {
                    this._videoOn = value;
                    NotifyPropertyChanged("VideoOn");
                }
            }
        }

        private bool _audioOn;
        public bool AudioOn
        {
            get
            {
                return this._audioOn;
            }

            set
            {
                if (value != this._audioOn)
                {
                    this._audioOn = value;
                    NotifyPropertyChanged("AudioOn");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }


        Symbol LassoSelect = (Symbol)0xEF20;
        Symbol TouchWriting = (Symbol)0xED5F;
        
        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();

        // Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        private bool isListening;

        // Keep track of existing text that we've accepted in ContinuousRecognitionSession_ResultGenerated(), so
        // that we can combine it and Hypothesized results to show in-progress dictation mid-sentence.
        private StringBuilder dictatedTextBuilder;

        /// <summary>
        /// This HResult represents the scenario where a user is prompted to allow in-app speech, but 
        /// declines. This should only happen on a Phone device, where speech is enabled for the entire device,
        /// not per-app.
        /// </summary>
        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        private List<NotePageControl> notePages;
        private List<Button> pageIndexButtons;
        private SimpleOrientationSensor _simpleorientation;
        public InkCanvas inkCanvas = null;
        private NotePageControl curPage = null;
        private int curPageIndex = -1;
        public static MainPage Current;
        private string curPageId = "";
        private string notebookId = "";
        private Notebook notebookObject;
        public static readonly string TypeMode = "Typing Mode";
        public static readonly string WritingMode = "Handwriting Mode";
        public static readonly string ViewMode = "View Mode";
        private string currentMode = WritingMode;

        public string RPI_ADDRESS { get; } = "http://192.168.137.112:8000";
        public BluetoothService.BluetoothService bluetoothService = null;

        public SpeechManager speechManager = SpeechManager.getSharedSpeechManager();

        private bool loadFromDisk = false;

        public MainPage()
        {
            Current = this;
            this.InitializeComponent();
            

            isListening = false;
            dictatedTextBuilder = new StringBuilder();

            
           
            _simpleorientation = SimpleOrientationSensor.GetDefault();
            // Assign an event handler for the sensor orientation-changed event 
            if (_simpleorientation != null)
            {
                _simpleorientation.OrientationChanged += new TypedEventHandler<SimpleOrientationSensor, SimpleOrientationSensorOrientationChangedEventArgs>(OrientationChanged);
            }
            
            
            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            
            
            // We want to react whenever speech engine has new results
            this.speechManager.EngineHasResult += SpeechManager_EngineHasResult;

            //scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ZoomFactorProperty, OnPropertyChanged);

            //showTextGrid.PointerPressed += new PointerEventHandler(showTextGrid_PointerPressed);
            modeTextBlock.PointerReleased += new PointerEventHandler(modeTextBlock_PointerReleased);
            modeTextBlock.PointerCanceled += new PointerEventHandler(modeTextBlock_PointerExited);
            modeTextBlock.PointerCaptureLost += new PointerEventHandler(modeTextBlock_PointerExited);
            modeTextBlock.PointerEntered += new PointerEventHandler(modeTextBlock_PointerEntered);
            modeTextBlock.PointerExited += new PointerEventHandler(modeTextBlock_PointerExited);

            ControlView.Visibility = Visibility.Collapsed;

            BluetoothButton_Click(null, null);
        }

        private async void InitializeNotebook()
        {
            PhenotypeManager.clearCache();
            // create file structure
            notebookId = FileManager.getSharedFileManager().createNotebookId();
            bool result = await FileManager.getSharedFileManager().CreateNotebook(notebookId);

            if (!result)
                NotifyUser("Failed to create file structure, notes may not be saved.", NotifyType.ErrorMessage, 2);
            else
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

            if(notebookObject != null)
                noteNameTextBox.Text = notebookObject.name;

            notePages = new List<NotePageControl>();
            pageIndexButtons = new List<Button>();
            NotePageControl aPage = new NotePageControl();
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = aPage;
            // var screenSize = HelperFunctions.GetCurrentDisplaySize();
            //aPage.Height = screenSize.Height;
            //aPage.Width = screenSize.Width;
            curPageIndex = 0;
            PageHost.Content = curPage;
            addNoteIndex(curPageIndex);
            setNotePageIndex(curPageIndex);

            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;

            // create file sturcture for this page
            await FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());
        }

        private async void InitializeNotebookFromDisk()
        {
            PhenotypeManager.clearCache();
            List<string> pageIds = await FileService.FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);
            notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

            if (notebookObject != null)
                noteNameTextBox.Text = notebookObject.name;

            List<Phenotype> phenos = await FileManager.getSharedFileManager().GetSavedPhenotypeObjectsFromXML(notebookId);
            if (phenos != null && phenos.Count > 0)
            {
                PhenotypeManager.getSharedPhenotypeManager().addPhenotypesFromFile(phenos);
            }

            
            if (pageIds == null || pageIds.Count == 0)
            {
                NotifyUser("Did not find anything in this notebook, will create a new one.", NotifyType.ErrorMessage, 2);
                this.InitializeNotebook();
            }

            notePages = new List<NotePageControl>();
            pageIndexButtons = new List<Button>();

            for (int i = 0; i < pageIds.Count; ++i)
            {
                NotePageControl aPage = new NotePageControl();
                notePages.Add(aPage);
                aPage.pageId = pageIds[i];
                aPage.notebookId = notebookId;
                await FileManager.getSharedFileManager().LoadNotePageStroke(notebookId, pageIds[i], aPage);
                addNoteIndex(i);

                List<ImageAndAnnotation> imageAndAnno = await FileManager.getSharedFileManager().GetImgageAndAnnotationObjectFromXML(notebookId, pageIds[i]);
                if(imageAndAnno != null)
                    foreach (var ia in imageAndAnno)
                    {
                        aPage.addImageAndAnnotationControl(ia.name, ia.canvasLeft, ia.canvasTop, true);
                    }
            }

            inkCanvas = notePages[0].inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = notePages[0];
            curPageIndex = 0;
            PageHost.Content = curPage;
            setNotePageIndex(curPageIndex);

            
        }

        /**
         * Save everything to disk, include: 
         * handwritten strokes, typing words, photos and annotations, drawing, collected phenotypes
         * 
         */
        private async Task<bool> saveNoteToDisk()
        {
            bool isSuccessful = true;
            bool result;

            for (int i = 0; i < notePages.Count; ++i)
            {
                // handwritten strokes
                result = await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, i.ToString(), notePages[i]);

                // save photos and annotations to disk
                result = await FileManager.getSharedFileManager().SaveNotePageDrawingAndPhotos(notebookId, i.ToString(), notePages[i]);
            }

            // collected phenotypes
            result = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);
            if (result)
                Debug.WriteLine("Successfully save collected phenotypes.");
            else
            {
                Debug.WriteLine("Failed to save collected phenotypes.");
                isSuccessful = false;
            }
             
            return isSuccessful;
        }

        /**
        * Load everything from disk, include: 
        * handwritten strokes, typing words, photos and annotations, drawing, collected phenotypes
        * 
        */
        private async Task<bool> loadNoteFromDisk()
        {
            bool isSuccessful = true;
            bool result;

            for (int i = 0; i < notePages.Count; ++i)
            {
                // handwritten strokes
                result = await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, i.ToString(), notePages[i]);
            }

            // collected phenotypes
            result = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);
            if (result)
                Debug.WriteLine("Successfully save collected phenotypes.");
            else
            {
                Debug.WriteLine("Failed to save collected phenotypes.");
                isSuccessful = false;
            }

            return isSuccessful;
        }



        private void modeTextBlock_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode)
            {
                curPage.hideRecognizedTextCanvas();
                modeTextBlock.Text = currentMode;

            }
        }

        private void modeTextBlock_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode)
            {
                curPage.showRecognizedTextCanvas();
                modeTextBlock.Text = ViewMode;
            }
        }

        private bool ifViewMode = false;
        private void modeTextBlock_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode)
            {
                curPage.showRecognizedTextCanvas();
                ifViewMode = true;
                modeTextBlock.Text = ViewMode;
            }
            else
            {
                curPage.hideRecognizedTextCanvas();
                ifViewMode = false;
                modeTextBlock.Text = currentMode;
            }
        }

        private void showTextGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            
        }

        /**
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                fakeTileBar.Visibility = Visibility.Visible;
            }
            else
            {
                fakeTileBar.Visibility = Visibility.Collapsed;
            }
        }
        **/

        // Update text to display latest sentence
        // TODO : Feels like there exists more legitimate wasy to do this
        private void SpeechManager_EngineHasResult(SpeechManager sender, SpeechEngineInterpreter args)
        {
            //this.cmdBarTextBlock.Text = args.latestSentence;
        }
        
        private void OnPropertyChanged(DependencyObject sender, DependencyProperty dp)
        {
            Debug.WriteLine(sender.GetValue(dp));
        }

        private async void OrientationChanged(object sender, SimpleOrientationSensorOrientationChangedEventArgs e)
        {
              await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                SimpleOrientation orientation = e.Orientation;
            
                switch (orientation)
                {
                    case SimpleOrientation.NotRotated:
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        VisualStateManager.GoToState(this, "LandscapeState", false);
                        break;
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        VisualStateManager.GoToState(this, "PortraitState", false);
                        break;
                }
            /**
                var displayInformation = DisplayInformation.GetForCurrentView();
                switch (displayInformation.CurrentOrientation)
                {
                    case DisplayOrientations.Landscape:
                    case DisplayOrientations.LandscapeFlipped:
                        VisualStateManager.GoToState(this, "LandscapeState", false);
                        break;
                    case DisplayOrientations.Portrait:
                    case DisplayOrientations.PortraitFlipped:
                        VisualStateManager.GoToState(this, "PortraitState", false);
                        break;
                }
            **/
            if(curPage != null)
                curPage.DrawBackgroundLines();
           });
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var displayInformation = DisplayInformation.GetForCurrentView();
            switch (displayInformation.CurrentOrientation)
            {
                case DisplayOrientations.Landscape:
                    VisualStateManager.GoToState(this, "LandscapeState", false);
                    break;
                case DisplayOrientations.LandscapeFlipped:
                    VisualStateManager.GoToState(this, "LandscapeState", false);
                    break;
                case DisplayOrientations.Portrait:
                    VisualStateManager.GoToState(this, "PortraitState", false);
                    break;
                case DisplayOrientations.PortraitFlipped:
                    VisualStateManager.GoToState(this, "PortraitState", false);
                    break;
                default:
                    VisualStateManager.GoToState(this, "LandscapeState", false);
                    break;
            }

            // Draw background lines
            if(curPage != null)
                curPage.DrawBackgroundLines();

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
           // bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
           // if (permissionGained)
          // {
                //micButton.IsEnabled = true;
                //await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            //}
           // else
            //{
               // this.cmdBarTextBlock.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                //micButton.IsEnabled = false;
            //}

            
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.Frame.BackStack.Clear();
            PhenotypeManager.getSharedPhenotypeManager().phenotypesCandidates.Clear();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //BackButton.IsEnabled = this.Frame.CanGoBack;

            var nid = e.Parameter as string;
            if (nid == "__new__")
            {
                this.loadFromDisk = false;
            }
            else
            {
                this.loadFromDisk = true;
                this.notebookId = nid;
            }
            if (loadFromDisk) // Load notes from file
            {
                this.InitializeNotebookFromDisk();
            }
            else // Create new notebook
            {
                this.InitializeNotebook();
            }
        }
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //dispatcherTimer.Stop();
            if (this.speechRecognizer != null)
            {
                if (isListening)
                {
                    await this.speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    isListening = false;
                }

                cmdBarTextBlock.Text = "";

                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            
            if (speechEngineRunning)
            {
                this.speechManager.EndAudio();
                speechEngineRunning = false;
            }

            //cmdBarTextBlock.Visibility = Visibility.Collapsed;
        }

        
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //HelperFunctions.UpdateCanvasSize(RootGrid, outputGrid, inkCanvas);
            var displayInformation = DisplayInformation.GetForCurrentView();
            switch (displayInformation.CurrentOrientation)
            {
                case DisplayOrientations.Landscape:
                case DisplayOrientations.LandscapeFlipped:
                    VisualStateManager.GoToState(this, "LandscapeState", false);
                    break;
                case DisplayOrientations.Portrait:
                case DisplayOrientations.PortraitFlipped:
                    VisualStateManager.GoToState(this, "PortraitState", false);
                    break;
            }
            curPage.DrawBackgroundLines();
        }

        // This is the recommended way to implement inking with touch on Windows.
        // Since touch is reserved for navigation (pan, zoom, rotate, etc.),
        // if you’d like your app to have inking with touch, it is recommended
        // that it is enabled via CustomToggle like in this scenario, with the
        // same icon and tooltip.
        private void Toggle_Custom(object sender, RoutedEventArgs e)
        {
            if (toggleButton.IsChecked == true)
            {
                curPage.inkCan.InkPresenter.InputDeviceTypes |= CoreInputDeviceTypes.Touch;
                //toggleButton.Background = MyColors.TITLE_BAR_WHITE_COLOR_BRUSH;
            }
            else
            {
                curPage.inkCan.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Touch;
                //toggleButton.Background = MyColors.Button_Background;
            }
        }
       
        

       

        private void ToolButton_Lasso(object sender, RoutedEventArgs e)
        {
            // By default, pen barrel button or right mouse button is processed for inking
            // Set the configuration to instead allow processing these input on the UI thread
            if (BallpointPenButton.IsChecked == true)
            {
                if (toolButtonLasso.IsChecked == true)
                {
                    curPage.enableLeftButtonLasso();
                    BallpointPenButton.IsEnabled = false;
                    EraserButton.IsEnabled = false;
                }
                else
                {
                    curPage.disableLeftButtonLasso();
                    BallpointPenButton.IsEnabled = true;
                    EraserButton.IsEnabled = true;
                }
            }
           
            
        }

       

       


        private void CurrentToolChanged(InkToolbar sender, object args)
        {
            /**
            bool enabled = sender.ActiveTool.Equals(toolButtonLasso);

            ButtonCut.IsEnabled = enabled;
            ButtonCopy.IsEnabled = enabled;
            ButtonPaste.IsEnabled = enabled;
            **/ 
        }


       

        private void AppBarButton_Click(object sender, object e)
        {

           
        }
        
        private void VideoButton_Click(object sender, object e)
        {
           
        }
        private void HPNameTextBlock_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            
        }

        static class
        HelperFunctions
        {
            public static void UpdateCanvasSize(FrameworkElement root, FrameworkElement output, FrameworkElement inkCanvas)
            {
                output.Width = root.ActualWidth;
                output.Height = root.ActualHeight / 2;
                inkCanvas.Width = root.ActualWidth;
                inkCanvas.Height = root.ActualHeight / 2;
            }

            public static Size GetCurrentDisplaySize()
            {
                var displayInformation = DisplayInformation.GetForCurrentView();
                TypeInfo t = typeof(DisplayInformation).GetTypeInfo();
                var props = t.DeclaredProperties.Where(x => x.Name.StartsWith("Screen") && x.Name.EndsWith("InRawPixels")).ToArray();
                var w = props.Where(x => x.Name.Contains("Width")).First().GetValue(displayInformation);
                var h = props.Where(x => x.Name.Contains("Height")).First().GetValue(displayInformation);
                var size = new Size(System.Convert.ToDouble(w), System.Convert.ToDouble(h));
                switch (displayInformation.CurrentOrientation)
                {
                    case DisplayOrientations.Landscape:
                    case DisplayOrientations.LandscapeFlipped:
                        size = new Size(Math.Max(size.Width, size.Height), Math.Min(size.Width, size.Height));
                        break;
                    case DisplayOrientations.Portrait:
                    case DisplayOrientations.PortraitFlipped:
                        size = new Size(Math.Min(size.Width, size.Height), Math.Max(size.Width, size.Height));
                        break;
                }
                return size;
            }
        }

        // Handwriting recognition

        private void NotesButton_Click(object sender, RoutedEventArgs e)
        {
            if (OverviewPopUp.IsOpen)
            {
                OverviewButton.IsChecked = false;
                OverviewPopUp.IsOpen = false;

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
                {
                    var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                    if (titleBar != null)
                    {
                        titleBar.BackgroundColor = Colors.White;
                        titleBar.ButtonBackgroundColor = Colors.White;
                    }
                }
            }
            if (SpeechPopUp.IsOpen)
            {
                SpeechButton.IsChecked = false;
                SpeechPopUp.IsOpen = false;


            }
        }

        private void OverviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (!OverviewPopUp.IsOpen)
            {
                OverivePopUpPage.Width = Window.Current.Bounds.Width;
                OverivePopUpPage.Height = Window.Current.Bounds.Height - topCmdBar.ActualHeight - 40;
                OverivePopUpPage.Margin = new Thickness(0, topCmdBar.ActualHeight, 0, 40);
                OverviewPopUp.IsOpen = true;

            }
            else
            {
                OverviewPopUp.IsOpen = false;
                
            }
            
            /**
            Frame rootFrame = Window.Current.Content as Frame;
            rootFrame.Navigate(typeof(OverviewPage));

            
            **/
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SpeechButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SpeechPopUp.IsOpen)
            {
                SpeechPopUpPage.Width = Window.Current.Bounds.Width;
                SpeechPopUpPage.Height = Window.Current.Bounds.Height - topCmdBar.ActualHeight- 40;
                SpeechPopUpPage.Margin = new Thickness(0, topCmdBar.ActualHeight, 0, 40);
                SpeechPopUp.IsOpen = true;
            }
            else
            {
                SpeechPopUp.IsOpen = false;  
            }
        }

        

        ////////Speech recognition

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            this.speechRecognizer = new SpeechRecognizer(recognizerLanguage);

            // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
            // of an audio indicator to help the user understand whether they're being heard.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            // Apply the dictation topic constraint to optimize for dictated freeform speech.
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizer.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                //rootPage.NotifyUser("Grammar Compilation Failed: " + result.Status.ToString(), NotifyType.ErrorMessage);
                Console.WriteLine("Grammar Compilation Failed: " + result.Status.ToString());
                //micButton.IsEnabled = false;
            }

            // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
            // some recognized phrases occur, or the garbage rule is hit. HypothesisGenerated fires during recognition, and
            // allows us to provide incremental feedback based on what the user's currently saying.
            speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                // If TimeoutExceeded occurs, the user has been silent for too long. We can use this to 
                // cancel recognition if the user in dictation mode and walks away from their device, etc.
                // In a global-command type scenario, this timeout won't apply automatically.
                // With dictation (no grammar in place) modes, the default timeout is 20 seconds.
                if (args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //rootPage.NotifyUser("Automatic Time Out of Dictation", NotifyType.StatusMessage);
                        Console.WriteLine("Automatic Time Out of Dictation");
                        cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
                        isListening = false;
                    });
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //rootPage.NotifyUser("Continuous Recognition Completed: " + args.Status.ToString(), NotifyType.StatusMessage);
                        Console.WriteLine("Continuous Recognition Completed: " + args.Status.ToString());
                        isListening = false;
                    });
                }
            }
                
            
        }

        /// <summary>
        /// While the user is speaking, update the textbox with the partial sentence of what's being said for user feedback.
        /// </summary>
        /// <param name="sender">The recognizer that has generated the hypothesis</param>
        /// <param name="args">The hypothesis formed</param>
        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = dictatedTextBuilder.ToString() + " " + hypothesis + " ...";
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //cmdBarTextBlock.Text = textboxContent;
                cmdBarTextBlock.Text = hypothesis;
            });
                       
        }

        /// <summary>
        /// Handle events fired when a result is generated. Check for high to medium confidence, and then append the
        /// string to the end of the stringbuffer, and replace the content of the textbox with the string buffer, to
        /// remove any hypothesis text that may be present.
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // We may choose to discard content that has low confidence, as that could indicate that we're picking up
            // noise via the microphone, or someone could be talking out of earshot.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                dictatedTextBuilder.Append(args.Result.Text + " ");

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {

                    //cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
                    cmdBarTextBlock.Text = args.Result.Text;
                    //SpeechManager.getSharedSpeechManager().AddNewMessage(args.Result.Text);
                    List<Phenotype> annoResults = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(cmdBarTextBlock.Text);
                    if (annoResults != null)
                    {
                        PhenotypeManager.getSharedPhenotypeManager().addPhenotypeInSpeech(annoResults);

                        /**
                        AnnoPhenoStackPanel.Children.Clear();
                        foreach (Phenotype ap in annoResults)
                        {
                            Button tb = new Button();
                            tb.Content= ap.name;
                            tb.Margin = new Thickness(5, 5, 0, 0);
                            
                            if (PhenotypeManager.getSharedPhenotypeManager().checkIfSaved(ap))
                                tb.BorderBrush = new SolidColorBrush(Colors.Black);
                            tb.Click += delegate (object s, RoutedEventArgs e)
                            {
                                ap.state = 1;
                                PhenotypeManager.getSharedPhenotypeManager().addPhenotype(ap, SourceType.Speech);
                                tb.BorderBrush = new SolidColorBrush(Colors.Black);
                            };
                            AnnoPhenoStackPanel.Children.Add(tb);
                        }
                         **/
                    }
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                // Here, just remove any hypothesis text by resetting it to the last known good.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
                    string discardedText = args.Result.Text;
                    if (!string.IsNullOrEmpty(discardedText))
                    {
                        discardedText = discardedText.Length <= 25 ? discardedText : (discardedText.Substring(0, 25) + "...");

                        Console.WriteLine("Discarded due to low/rejected Confidence: " + discardedText);
                    }
                });
            }
        }

        private void Tb_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                //this.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
                Console.WriteLine(args.State.ToString());
            });
        }

        bool speechEngineRunning = false;
        private async void AudioStreamButton_Clicked(object sender, RoutedEventArgs e) {

            //SpeechStreamSocket sss = new SpeechStreamSocket();
            //sss.connect();
            Task speechManagerTask;
            if (speechEngineRunning == false)
            {
                speechManagerTask = SpeechManager.getSharedSpeechManager().StartAudio();
                //this.cmdBarTextBlock.Visibility = Visibility.Visible;
                speechEngineRunning = !speechEngineRunning;

                await speechManagerTask;
            }
            else {
                speechManagerTask = SpeechManager.getSharedSpeechManager().EndAudio();
                //this.cmdBarTextBlock.Visibility = Visibility.Collapsed;
                speechEngineRunning = !speechEngineRunning;
                //cmdBarTextBlock.Text = "";
            }

            // Note that we have a giant loop in speech manager so that after it is done
            // there won't be any audio processing going on
            speechEngineRunning = false;
            //testButton.IsChecked = false;
        }

        private async void MicButton_Click(object sender, RoutedEventArgs e)
        {
            //micButton.IsEnabled = false;
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    

                    try
                    {
                        isListening = true;
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult == HResultPrivacyStatementDeclined)
                        {
                            //Show a UI link to the privacy settings.
                            //hlOpenPrivacySettings.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                            await messageDialog.ShowAsync();
                        }

                        isListening = false;

                    }
                }
            }
            else
            {
                isListening = false;
               
                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    // Cancelling recognition prevents any currently recognized speech from
                    // generating a ResultGenerated event. StopAsync() will allow the final session to 
                    // complete.
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StopAsync();

                        Console.WriteLine("Speech recognition stopped.");
                        // Ensure we don't leave any hypothesis text behind
                        //cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            //micButton.IsEnabled = true;
        }

        private void PageOverviewButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void AddPageButton_Click(object sender, RoutedEventArgs e)
        {
            NotePageControl aPage = new NotePageControl();
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            curPage = aPage;
            //var screenSize = HelperFunctions.GetCurrentDisplaySize();
            //aPage.Height = screenSize.Height;
            //aPage.Width = screenSize.Width;
            curPageIndex = notePages.Count - 1;

          
            PageHost.Content = curPage;

            setPageIndexText();
            addNoteIndex(curPageIndex);

            await FileService.FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());

        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (curPageIndex < notePages.Count - 1)
            {
                curPageIndex++;
                var aPage = notePages.ElementAt(curPageIndex);
                inkCanvas = aPage.inkCan;
                curPage = aPage;
                //PageHostContentTrans.HorizontalOffset = 100;
                //(PageHost.ContentTransitions.ElementAt(0) as ContentThemeTransition).HorizontalOffset = 500;
                PageHost.Content = curPage;

                setPageIndexText();
            }
        }

        private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (curPageIndex > 0)
            {
                curPageIndex--;
                var aPage = notePages.ElementAt(curPageIndex);
                inkCanvas = aPage.inkCan;
                curPage = aPage;
                //PageHostContentTrans.HorizontalOffset = -100;
                //(PageHost.ContentTransitions.ElementAt(0) as ContentThemeTransition).HorizontalOffset = -500;
                PageHost.Content = curPage;

                setPageIndexText();
            }
        }

        private void setPageIndexText()
        {
            MainPageInkBar.TargetInkCanvas = inkCanvas;
        }
        

        /*private void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            CameraCanvas.Visibility = Visibility.Visible;
            captureControl.setUp();
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            string imagename = FileManager.getSharedFileManager().CreateUniqueName();
            var imageSource = await captureControl.TakePhotoAsync(notebookId, curPageIndex.ToString(), imagename + ".jpg");
            if(imageSource != null)
            {
                curPage.AddImageControl(imagename, imageSource);

            }
        }

        private void CameraClose_Click(object sender, RoutedEventArgs e)
        {
            CameraCanvas.Visibility = Visibility.Collapsed;
            captureControl.unSetUp();
        }*/

        private void setNotePageIndex(int index)
        {
            foreach (var btn in pageIndexButtons)
            {
                btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
                btn.Foreground = new SolidColorBrush(Colors.Gray);
             }
            pageIndexButtons.ElementAt(index).Background = Application.Current.Resources["Button_Background"] as SolidColorBrush;
            pageIndexButtons.ElementAt(index).Foreground = new SolidColorBrush(Colors.Black);
        }

        private void addNoteIndex(int index)
        {
            Button btn = new Button();
            btn.Click += IndexBtn_Click;
            btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
            btn.Foreground = new SolidColorBrush(Colors.Black);
            btn.Padding = new Thickness(0, 0, 0, 0);
            btn.Content = "" + (index+1);
            btn.Width = 30;
            btn.Height = 30;
            pageIndexButtons.Add(btn);
            if (pageIndexPanel.Children.Count >= 1)
                pageIndexPanel.Children.Insert(pageIndexPanel.Children.Count-1, btn);
            setNotePageIndex(index);

        }

        private void IndexBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            foreach (var btn in pageIndexButtons)
            {
                btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
                btn.Foreground = Application.Current.Resources["Button_Background"] as SolidColorBrush;
            }
            button.Background = Application.Current.Resources["Button_Background"] as SolidColorBrush;
            button.Foreground = new SolidColorBrush(Colors.WhiteSmoke);
            
            curPageIndex = Int32.Parse(button.Content.ToString()) - 1;
            var aPage = notePages.ElementAt(curPageIndex);
            inkCanvas = aPage.inkCan;
            curPage = aPage;
            //PageHostContentTrans.HorizontalOffset = 100;
            //(PageHost.ContentTransitions.ElementAt(0) as ContentThemeTransition).HorizontalOffset = 500;
            PageHost.Content = curPage;

            setPageIndexText();
            setNotePageIndex(curPageIndex);
        }

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type, int seconds)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatusAsync(strMessage, type, seconds);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatusAsync(strMessage, type, seconds));
            }
        }

        private async void UpdateStatusAsync(string strMessage, NotifyType type, int seconds)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Turquoise);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Tomato);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
            }

            await Task.Delay(1000 * seconds);
            StatusBorder.Visibility = Visibility.Collapsed;
        }

        
        private async void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            // Get all strokes on the InkCanvas.
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Strokes present on ink canvas.
            if (currentStrokes.Count > 0)
            {
                // Let users choose their ink file using a file picker.
                // Initialize the picker.
                Windows.Storage.Pickers.FileSavePicker savePicker =
                    new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation =
                    Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add(
                    "GIF with embedded ISF",
                    new List<string>() { ".gif" });
                savePicker.DefaultFileExtension = ".gif";
                savePicker.SuggestedFileName = "InkSample";

                // Show the file picker.
                Windows.Storage.StorageFile file =
                    await savePicker.PickSaveFileAsync();
                // When chosen, picker returns a reference to the selected file.
                if (file != null)
                {
                    // Prevent updates to the file until updates are 
                    // finalized with call to CompleteUpdatesAsync.
                    Windows.Storage.CachedFileManager.DeferUpdates(file);
                    // Open a file stream for writing.
                    IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    // Write the ink strokes to the output stream.
                    using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                    {
                        await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                        await outputStream.FlushAsync();
                    }
                    stream.Dispose();

                    // Finalize write so other apps can update file.
                    Windows.Storage.Provider.FileUpdateStatus status =
                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        // File saved.
                        NotifyUser("Your note has been saved.", NotifyType.StatusMessage, 2);
                    }
                    else
                    {
                        // File couldn't be saved.
                        NotifyUser("Your note couldn't be saved.", NotifyType.ErrorMessage, 2);
                    }
                }
                // User selects Cancel and picker returns null.
                else
                {
                    // Operation cancelled.
                }
            }
        }
        
        private async void LoadNote_Click(object sender, RoutedEventArgs e)
        {
            // Let users choose their ink file using a file picker.
            // Initialize the picker.
            Windows.Storage.Pickers.FileOpenPicker openPicker =
                new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            // Show the file picker.
            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            // User selects a file and picker returns a reference to the selected file.
            if (file != null)
            {
                // Open a file stream for reading.
                IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    await inkCanvas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
                    NotifyUser("The note has been loaded.", NotifyType.StatusMessage, 2);
                    await curPage.StartAnalysisAfterLoad();

                }
                stream.Dispose();
            }
            // User selects Cancel and picker returns null.
            else
            {
                // Operation cancelled.
            }
        }

        private async void LoadImage_Click(object sender, RoutedEventArgs e)
        {
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
                    curPage.AddImageControl("FIXME",bitmapSource);
                }
                stream.Dispose();
            }
            // User selects Cancel and picker returns null.
            else
            {
                // Operation cancelled.
            }
        }

        private async void SaveNoteToImage_Click(object sender, RoutedEventArgs e)
        {
            // Get all strokes on the InkCanvas.
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Strokes present on ink canvas.
            if (currentStrokes.Count > 0)
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)curPage.PAGE_WIDTH, (int)curPage.PAGE_HEIGHT, 96);
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.White);
                    ds.DrawInk(currentStrokes);
                }
                // Let users choose their ink file using a file picker.
                // Initialize the picker.
                Windows.Storage.Pickers.FileSavePicker savePicker =
                    new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation =
                    Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add(
                    "Images",
                    new List<string>() { ".gif", ".jpg", ".tif", ".png" });
                savePicker.DefaultFileExtension = ".jpg";
                savePicker.SuggestedFileName = "InkImage";

                // Show the file picker.
                Windows.Storage.StorageFile file =
                    await savePicker.PickSaveFileAsync();
                // When chosen, picker returns a reference to the selected file.
                if (file != null)
                {
                    // Prevent updates to the file until updates are 
                    // finalized with call to CompleteUpdatesAsync.
                    Windows.Storage.CachedFileManager.DeferUpdates(file);
                    // Open a file stream for writing.
                    IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    // Write the ink strokes to the output stream.
                    using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                    {
                        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg, 1f);
                    }
                    stream.Dispose();

                    // Finalize write so other apps can update file.
                    Windows.Storage.Provider.FileUpdateStatus status =
                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        // File saved.
                        NotifyUser("Your note has been saved.", NotifyType.StatusMessage, 2);
                    }
                    else
                    {
                        // File couldn't be saved.
                        NotifyUser("Your note couldn't be saved.", NotifyType.ErrorMessage, 2);
                    }
                }
                // User selects Cancel and picker returns null.
                else
                {
                    // Operation cancelled.
                }
            }
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private async Task<string> InputTextDialogAsync(string title, string content)
        {
            TextBox inputTextBox = new TextBox();
            inputTextBox.AcceptsReturn = false;
            inputTextBox.Height = 32;
            ContentDialog dialog = new ContentDialog();
            dialog.Content = inputTextBox;
            dialog.Title = title;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = "Ok";
            dialog.SecondaryButtonText = "Cancel";
            inputTextBox.Text = content;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                return inputTextBox.Text;
            else
                return "";
        }
        private async void ChangeServer_Click(object sender, RoutedEventArgs e)
        {
            //string text = await InputTextDialogAsync("Change a server: ", "");
            /*
            if(text != "" && text != string.Empty)
                SpeechManager.getSharedSpeechManager().setServerAddress(text);
            */
            
            string serverPath = SpeechManager.getSharedSpeechManager().getServerAddress() + ":" + SpeechManager.getSharedSpeechManager().getServerPort();

            string text = await InputTextDialogAsync("Change a server. Server Address (or sickkids): ", serverPath);

            string ipResult = "";
            string portResult = "";

            if (text.ToLower().IndexOf("sickkid") != -1)
            {
                //SpeechManager.getSharedSpeechManager().setServerAddress("speechengine.ccm.sickkids.ca");
                //SpeechManager.getSharedSpeechManager().setServerPort("8888");

                ipResult = "speechengine.ccm.sickkids.ca";
                portResult = "8888";
            } else
            {
                if (text != "" && text != string.Empty)
                {
                    int colonIndex = text.IndexOf(':');

                    // Only entered server address
                    if (colonIndex == -1)
                    {
                        //SpeechManager.getSharedSpeechManager().setServerAddress(text.Trim());

                        ipResult = text.Trim();
                        portResult = "8888";
                    }
                    // address and port both here
                    else
                    {
                        //SpeechManager.getSharedSpeechManager().setServerAddress(text.Substring(0, colonIndex).Trim());
                        //SpeechManager.getSharedSpeechManager().setServerPort(text.Substring(colonIndex + 1).Trim());

                        ipResult = text.Substring(0, colonIndex).Trim();
                        portResult = text.Substring(colonIndex + 1).Trim();
                    }
                }
            }

            SpeechManager.getSharedSpeechManager().setServerAddress(ipResult);
            SpeechManager.getSharedSpeechManager().setServerPort(portResult);

            AppConfigurations.saveSetting("serverIP", ipResult);
            AppConfigurations.saveSetting("serverPort", portResult);
        }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            curPage.hideRecognizedTextCanvas();
            ifViewMode = false;
            currentMode = TypeMode;
            modeTextBlock.Text = TypeMode;
            writeButton.IsChecked = false;
            keyboardButton.IsChecked = true;
            boldButton.Visibility = Visibility.Visible;
            italicButton.Visibility = Visibility.Visible;
            underlineButton.Visibility = Visibility.Visible;
            MainPageInkBar.Visibility = Visibility.Collapsed;
            curPage.showTextEditGrid();
        }

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            curPage.hideRecognizedTextCanvas();
            ifViewMode = false;
            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;

            keyboardButton.IsChecked = false;
            writeButton.IsChecked = true;
            boldButton.Visibility = Visibility.Collapsed;
            italicButton.Visibility = Visibility.Collapsed;
            underlineButton.Visibility = Visibility.Collapsed;
            MainPageInkBar.Visibility = Visibility.Visible;
            curPage.hideTextEditGrid();
        }
        
        private void MyScriptButton_Click(object sender, RoutedEventArgs e)
        {
            /**
            if (myScriptEditor.Visibility == Visibility.Collapsed)
            {
                myScriptEditor.Visibility = Visibility.Visible;
                myScriptEditor.NewFile();
            }
            else
            {
                myScriptEditor.Visibility = Visibility.Collapsed;
            }
    **/
            
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();
                this.NotifyUser("Exiting full screen mode", NotifyType.StatusMessage, 2);
                this.FullscreenBtn.Icon = new SymbolIcon(Symbol.FullScreen);
                // The SizeChanged event will be raised when the exit from full screen mode is complete.
            }
            else
            {
                if (view.TryEnterFullScreenMode())
                {
                    this.NotifyUser("Entering full screen mode", NotifyType.StatusMessage, 2);
                    this.FullscreenBtn.Icon = new SymbolIcon(Symbol.BackToWindow);
                    // The SizeChanged event will be raised when the entry to full screen mode is complete.
                }
                else
                {
                    this.NotifyUser("Failed to enter full screen mode", NotifyType.ErrorMessage, 2);
                }
            }
        }
        public void OpenCandidate()
        {
            OpenCandidatePanelButton.IsChecked = true;
            CandidatePanelStackPanel.Visibility = Visibility.Visible;
            OpenCandidatePanelButtonIcon.Glyph = "\uE8BB";

            candidatePhenoListView.ScrollIntoView(candidatePhenoListView.Items.ElementAt(0));
        }

        private void OpenCandidate_Click(object sender, RoutedEventArgs e)
        {
            if (OpenCandidatePanelButton.IsChecked == true)
            {
                CandidatePanelStackPanel.Visibility = Visibility.Visible;
                OpenCandidatePanelButtonIcon.Glyph = "\uE8BB";
            }
            else {
                CandidatePanelStackPanel.Visibility = Visibility.Collapsed;
                 OpenCandidatePanelButtonIcon.Glyph = "\uE82F";
            }
            
        }

        private void OverViewToggleButton_Click(object sender, RoutedEventArgs e)
        {   if (MainSplitView.IsPaneOpen == false)
            {
                MainSplitView.IsPaneOpen = true;
                QuickViewButtonSymbol.Symbol = Symbol.Clear;
            }
            else
            {
                if (SpeechToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = true;
                    SpeechToggleButton.IsChecked = false;
                }
                else
                {
                    MainSplitView.IsPaneOpen = false;
                    QuickViewButtonSymbol.Symbol = Symbol.GlobalNavigationButton;
                }

            }
        }

        private void SpeechToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainSplitView.IsPaneOpen == false)
            {
                MainSplitView.IsPaneOpen = true;
                QuickViewButtonSymbol.Symbol = Symbol.Clear;
            }
            else
            {
                if (OverViewToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = false;
                    SpeechToggleButton.IsChecked = true;
                }
                else
                {
                    MainSplitView.IsPaneOpen = false;
                    QuickViewButtonSymbol.Symbol = Symbol.GlobalNavigationButton;
                }

            }
        }

        private void QuickViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainSplitView.IsPaneOpen == true)
            {
                MainSplitView.IsPaneOpen = false;
                OverViewToggleButton.IsChecked = false;
                SpeechToggleButton.IsChecked = false;
                QuickViewButtonSymbol.Symbol = Symbol.GlobalNavigationButton;
            }
            else
            {
                MainSplitView.IsPaneOpen = true;
                QuickViewButtonSymbol.Symbol = Symbol.Clear;
                OverViewToggleButton.IsChecked = true;
                SpeechToggleButton.IsChecked = false;
            }
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            //On_BackRequested();
            this.Frame.Navigate(typeof(PageOverview));
        }
        // Handles system-level BackRequested events and page-level back button Click events
        private bool On_BackRequested()
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
                return true;
            }
            return false;
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewButton.IsChecked == true)
            {
                ControlView.Visibility = Visibility.Visible;
            }
            else
            {
                ControlView.Visibility = Visibility.Collapsed;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = await saveNoteToDisk();
            if (result)
            {
                Debug.WriteLine("Successfully saved to disk.");
            }
            else
            {
                Debug.WriteLine("Failed to save to disk.");
                NotifyUser("Failed to save to disk.", NotifyType.ErrorMessage, 2);
            }
        }

        private async void noteNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (notebookObject != null && !string.IsNullOrEmpty(noteNameTextBox.Text))
                {
                    notebookObject.name = noteNameTextBox.Text;
                    await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
                    LoseFocus(sender);
                }
            }
        }

        /// <summary>
        /// Makes virtual keyboard disappear
        /// </summary>
        /// <param name="sender"></param>
        private void LoseFocus(object sender)
        {
            var control = sender as Control;
            var isTabStop = control.IsTabStop;
            control.IsTabStop = false;
            control.IsEnabled = false;
            control.IsEnabled = true;
            control.IsTabStop = isTabStop;
        }

        private void AudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // Same as audio button click :D
            AudioStreamButton_Clicked(null, null);
        }

        private void VideoToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            videoStreamStatusUpdateAsync(this._videoOn);
        }

        private void StreamButton_Click(object sender, RoutedEventArgs e)
        {
            videoStreamStatusUpdateAsync(!this._videoOn);
        }

        private async Task videoStreamStatusUpdateAsync(bool desiredStatus)
        {
            if (this.bluetoothService == null)
            {
                NotifyUser("Could not reach Bluetooth device, try to connect again",
                                   NotifyType.ErrorMessage, 2);
                this.VideoOn = false;

                this.bluetoothInitialized(false);
                this.StreamButton.IsChecked = false;
                this.videoSwitch.IsOn = false;
                return;
            }

            Debug.WriteLine("Sending message");
            
            if (!desiredStatus)
            {
                await this.bluetoothService.sendBluetoothMessage("start");
            }
            else
            {
                await this.bluetoothService.sendBluetoothMessage("stop");
            }
            //this.videoSwitch.IsOn = desiredStatus;
            //this.StreamButton.IsChecked = desiredStatus;

            Debug.WriteLine("Setting status value to " + (this.bluetoothService.initialized && desiredStatus).ToString());
            //this.VideoOn = this.bluetoothService.initialized && desiredStatus;
        }

        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.bluetoothService == null)
            {
                NotifyUser("Could not reach Bluetooth device, try to connect again",
                                   NotifyType.ErrorMessage, 2);
                this.bluetoothInitialized(false);
                return;
            }

            await this.bluetoothService.sendBluetoothMessage("picture");
        }

        public void bluetoothInitialized(bool val)
        {
            this.StreamButton.IsEnabled = val;
            this.videoSwitch.IsEnabled = val;
            this.shutterButton.IsEnabled = val;
            this.cameraButton.IsEnabled = val;
            this.cameraButton.IsEnabled = val;
        }

        private void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            this.bluetoothService = BluetoothService.BluetoothService.getBluetoothService();
            this.bluetoothService.Initialize();
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

}
