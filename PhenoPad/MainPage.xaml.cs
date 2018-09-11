using System;
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
using Windows.System.Threading;
using System.IO;
using Windows.Storage;
using Windows.Media.Editing;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PhenoPad
{
    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    /// <summary>
    /// A Page used for note taking along with toolbars and different services.
    /// Note: Each Notebook instance will have its own MainPage instance when editing.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        //This partial class mainly contains event handler for interface interactions 
        //such as buttons, pointer movements and display notices.
        //Other parts of the logical controls including web socket/video/audio are moved to other partial class files.
        #region Attributes definitions

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

        private List<NotePageControl> notePages;
        private List<Button> pageIndexButtons;
        private SimpleOrientationSensor _simpleorientation;
        public InkCanvas inkCanvas = null;
        public NotePageControl curPage = null;
        public int curPageIndex = -1;
        public static MainPage Current;
        private string curPageId = "";
        private string notebookId = "";
        private Notebook notebookObject;
        public static readonly string TypeMode = "Typing Mode";
        public static readonly string WritingMode = "Handwriting Mode";
        public static readonly string ViewMode = "View Mode";
        private string currentMode = WritingMode;
        private bool ifViewMode = false;

        private SemaphoreSlim notifySemaphoreSlim = new SemaphoreSlim(1);
        #endregion

        //******************************END OF ATTRIBUTES DEFINITION***************************************
        /// <summary>
        /// Creates and initializes a new MainPage instance.
        /// </summary>
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
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Black;
            titleBar.ButtonInactiveBackgroundColor = Colors.Black;




            // We want to react whenever speech engine has new results
            // this.speechManager.EngineHasResult += SpeechManager_EngineHasResult;

            //scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ZoomFactorProperty, OnPropertyChanged);

            //showTextGrid.PointerPressed += new PointerEventHandler(showTextGrid_PointerPressed);
            modeTextBlock.PointerReleased += new PointerEventHandler(modeTextBlock_PointerReleased);
            modeTextBlock.PointerCanceled += new PointerEventHandler(modeTextBlock_PointerExited);
            modeTextBlock.PointerCaptureLost += new PointerEventHandler(modeTextBlock_PointerExited);
            modeTextBlock.PointerEntered += new PointerEventHandler(modeTextBlock_PointerEntered);
            modeTextBlock.PointerExited += new PointerEventHandler(modeTextBlock_PointerExited);

            chatView.ItemsSource = SpeechManager.getSharedSpeechManager().conversation;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            realtimeChatView.ItemsSource = SpeechManager.getSharedSpeechManager().realtimeConversation;

            PropertyChanged += MainPage_PropertyChanged;
            // save to disk every 10 seconds
            // this.saveNotesTimer(30);
        }



        /// <summary>
        /// Initializes display for the loaded page
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

            // for figures of PhenoPad paper
            // SpeechManager.getSharedSpeechManager().AddFakeSpeechResults();
            // PhenotypeManager.getSharedPhenotypeManager().AddFakePhenotypesInSpeech();


            //setting the view state of page display
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
            if (curPage != null)
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

        /// <summary>
        /// Clears all page index records in the StackPanel.
        /// </summary>
        private void clearPageIndexPanel()
        {
            if (pageIndexPanel.Children.Count() > 1)
            {
                while (pageIndexPanel.Children.Count() > 1)
                    pageIndexPanel.Children.RemoveAt(0);
            }
        }

        /// <summary>
        /// Sets the ink bar controller to the current ink canvas.
        /// </summary>
        private void setPageIndexText()
        {
            MainPageInkBar.TargetInkCanvas = inkCanvas;
        }



        //  ************Switching between editing / view mode *********************
        #region Switching between Editing / View Mode
        /// <summary>
        /// Switches back editing mode panel
        /// </summary>
        private void modeTextBlock_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode)
            {
                curPage.hideRecognizedTextCanvas();
                modeTextBlock.Text = currentMode;

            }
        }

        /// <summary>
        /// Switches to view mode panel
        /// </summary>
        private void modeTextBlock_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode)
            {
                curPage.showRecognizedTextCanvas();
                modeTextBlock.Text = ViewMode;
            }
        }

        /// <summary>
        /// Switching between edit / view mode after clicking mode text block
        /// </summary>
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
            //not yet implemented
        }
        #endregion

        // *************Page Display / navigations **************************


        /// <summary>
        /// Redrawing background lines when display orientation is changed.
        /// </summary>
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
                if (curPage != null)
                    curPage.DrawBackgroundLines();
            });
        }

        /// <summary>
        /// Initializes the Notebook when user navigated to MainPage.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Black;
            titleBar.ButtonInactiveBackgroundColor = Colors.Black;
            LogService.MetroLogger.getSharedLogger().Info($"Naviaged to MainPage");
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
                FileManager.getSharedFileManager().currentNoteboookId = nid;
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

        /// <summary>
        /// Clearing all cache and index records before leaving MainPage.
        /// </summary>
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Leaving MainPage");
            // this.Frame.BackStack.Clear();
            notePages = null;
            // clear page index panel
            clearPageIndexPanel();
            inkCanvas = null;
            curPage = null;
            PhenotypeManager.getSharedPhenotypeManager().phenotypesCandidates.Clear();
            SpeechManager.getSharedSpeechManager().cleanUp();

            //dispatcherTimer.Stop();
            // Microsoft ASR, not used for now
            if (this.speechRecognizer != null)
            {
                if (isListening)
                {
                    await this.speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    isListening = false;
                }

                //cmdBarTextBlock.Text = "";

                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }


            if (speechEngineRunning)
            {
                await this.speechManager.EndAudio(notebookId);
                speechEngineRunning = false;
            }

            //cmdBarTextBlock.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Redrawing background lines when note page size is changed.
        /// </summary>
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

        /// <summary>
        /// Makes virtual keyboard disappear
        /// </summary>
        private void LoseFocus(object sender)
        {
            var control = sender as Control;
            var isTabStop = control.IsTabStop;
            control.IsTabStop = false;
            control.IsEnabled = false;
            control.IsEnabled = true;
            control.IsTabStop = isTabStop;
        }

        /// <summary>
        /// Sets the display color of all note page buttons
        /// </summary>
        /// <param name="index"></param>
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

        /// <summary>
        /// Adds a new button to page index after creating a new page
        /// </summary>
        private void addNoteIndex(int index)
        {
            Button btn = new Button();
            btn.Click += IndexBtn_Click;
            btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
            btn.Foreground = new SolidColorBrush(Colors.Black);
            btn.Padding = new Thickness(0, 0, 0, 0);
            btn.Content = "" + (index + 1);
            btn.Width = 30;
            btn.Height = 30;
            pageIndexButtons.Add(btn);
            if (pageIndexPanel.Children.Count >= 1)
                pageIndexPanel.Children.Insert(pageIndexPanel.Children.Count - 1, btn);
            setNotePageIndex(index);

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

        // ************** Tool Toggle event handlers ********************
        #region Tool Toggles
        /// <summary>
        /// Toggling the touch writing function under handwritting mode.
        /// </summary>
        private void Toggle_Custom(object sender, RoutedEventArgs e)
        {
            // This is the recommended way to implement inking with touch on Windows.
            // Since touch is reserved for navigation (pan, zoom, rotate, etc.),
            // if you’d like your app to have inking with touch, it is recommended
            // that it is enabled via CustomToggle like in this scenario, with the
            // same icon and tooltip.
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

        /// <summary>
        /// Toggles lasso function under hand writting mode.
        /// </summary>
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

        /// <summary>
        /// Not yet implemented
        /// </summary>
        private void CurrentToolChanged(InkToolbar sender, object args)
        {
            /**
            bool enabled = sender.ActiveTool.Equals(toolButtonLasso);

            ButtonCut.IsEnabled = enabled;
            ButtonCopy.IsEnabled = enabled;
            ButtonPaste.IsEnabled = enabled;
            **/
        }
        /// <summary>
        /// Not yet implemented
        /// </summary>
        private void AudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // Same as audio button click :D
            //if (this.audioSwitch.IsOn == false)
            //{
            //AudioStreamButton_Clicked(null, null);
            //}
            //changeSpeechEngineState(!this.AudioOn);
            throw new NotImplementedException();
        }
        /// <summary>
        /// Toggles the video button
        /// </summary>
        private async void VideoToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            await videoStreamStatusUpdateAsync(this._videoOn);
        }
        /// <summary>
        /// Not yet implemented
        /// </summary>
        private void HPNameTextBlock_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        #endregion


        //***************************Button click handlers******************************
        #region Button Click Handler
        private void AppBarButton_Click(object sender, object e)
        {
            throw new NotImplementedException("AppBarButton_Click");
        }

        private void VideoButton_Click(object sender, object e)
        {
            throw new NotImplementedException("VideoButton_Click");
        }

        // Handwriting recognition

        private void NotesButton_Click(object sender, RoutedEventArgs e)
        {
            NotesButton.IsChecked = true;
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
            NotesButton.IsChecked = false;
            if (!OverviewPopUp.IsOpen)
            {
                OverivePopUpPage.Width = Window.Current.Bounds.Width;
                OverivePopUpPage.Height = Window.Current.Bounds.Height - topCmdBar.ActualHeight;
                OverivePopUpPage.Margin = new Thickness(0, topCmdBar.ActualHeight, 0, 0);
                OverviewPopUp.IsOpen = true;

            }
            else
            {
                OverviewPopUp.IsOpen = false;

            }
            if (SpeechPopUp.IsOpen)
            {
                SpeechPopUp.IsOpen = false;
                SpeechButton.IsChecked = false;
            }
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException("MenuFlyoutItem_Click");
        }
        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException("MenuFlyoutItem_Click_1");
        }

        private void SpeechButton_Click(object sender, RoutedEventArgs e)
        {
            NotesButton.IsChecked = false;
            if (!SpeechPopUp.IsOpen)
            {
                SpeechPopUpPage.Width = Window.Current.Bounds.Width;
                SpeechPopUpPage.Height = Window.Current.Bounds.Height - topCmdBar.ActualHeight;
                SpeechPopUpPage.Margin = new Thickness(0, topCmdBar.ActualHeight, 0, 0);
                SpeechPopUp.IsOpen = true;
            }
            else
            {
                SpeechPopUp.IsOpen = false;
            }

            if (OverviewPopUp.IsOpen)
            {
                OverviewPopUp.IsOpen = false;
                OverviewButton.IsChecked = false;
            }
        }

        private void AudioStreamButton_Clicked(object sender, RoutedEventArgs e)
        {
            // use external microphone
            if (ConfigService.ConfigService.getConfigService().IfUseExternalMicrophone())
            {
                if (audioButton.IsChecked == true)
                {
                    changeSpeechEngineState_BT(true);
                    audioStatusText.Text = "ON";
                }
                else
                {
                    changeSpeechEngineState_BT(false);
                    audioStatusText.Text = "OFF";
                }
            }
            // use internal microphone
            else
            {
                if (audioButton.IsChecked == true)
                {
                    changeSpeechEngineState(true);
                    audioStatusText.Text = "ON";
                }
                else
                {
                    changeSpeechEngineState(false);
                    audioStatusText.Text = "OFF";
                }
            }


        }

        private void MicButton_Click(object sender, RoutedEventArgs e)
        {
            enableMic();
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

            curPage.initialAnalyze();
        }

        /// <summary>
        /// Invoked when user clicks export note button from drop down menu
        /// </summary>
        private async void SaveNote_Click(object sender, RoutedEventArgs e)
        {
            int saved = await saveImageToDisk();
            switch (saved)
            {
                case 1:
                    NotifyUser("Your note has been saved.", NotifyType.StatusMessage, 2);
                    break;
                case 0:
                    NotifyUser("Your note couldn't be saved.", NotifyType.ErrorMessage, 2);
                    break;
                default:
                    break;

            }
        }

        /// <summary>
        /// Invoked when user clicks the "load note" button from drop down menu
        /// </summary>
        private async void LoadNote_Click(object sender, RoutedEventArgs e)
        {
            bool is_loaded = await loadStrokefromGif();
            if (is_loaded)
                NotifyUser("The note has been loaded.", NotifyType.StatusMessage, 2);
            else
                NotifyUser("Failed to load note", NotifyType.ErrorMessage, 2);
        }
        /// <summary>
        /// Invoked when user clicks "load an image".
        /// </summary>
        private async void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            bool is_loaded = await loadImagefromDisk();
            if (is_loaded)
                NotifyUser("The note has been loaded.", NotifyType.StatusMessage, 2);
            else
                NotifyUser("Failed to load note", NotifyType.ErrorMessage, 2);
        }

        private async void SaveNoteToImage_Click(object sender, RoutedEventArgs e)
        {
            int saved = await saveImageToDisk();
            switch (saved)
            {
                case 1:
                    NotifyUser("Your note has been saved.", NotifyType.StatusMessage, 2);
                    break;
                case 0:
                    NotifyUser("Your note couldn't be saved.", NotifyType.ErrorMessage, 2);
                    break;
                default:
                    break;
            }
        }



        private async void ChangeServer_Click(object sender, RoutedEventArgs e)
        {
            //string text = await InputTextDialogAsync("Change a server: ", "");
            /*
            if(text != "" && text != string.Empty)
                SpeechManager.getSharedSpeechManager().setServerAddress(text);
            */

            string serverPath = SpeechManager.getSharedSpeechManager().getServerAddress() + ":" + SpeechManager.getSharedSpeechManager().getServerPort();

            if (serverPath == "")
                serverPath = "speechengine.ccm.sickkids.ca";

            string text = await InputTextDialogAsync("Change a server. Server Address (or sickkids): ", serverPath);

            string ipResult = "";
            string portResult = "";

            if (text.ToLower().IndexOf("sickkid") != -1)
            {
                //SpeechManager.getSharedSpeechManager().setServerAddress("speechengine.ccm.sickkids.ca");
                //SpeechManager.getSharedSpeechManager().setServerPort("8888");
                if (text.ToLower().IndexOf("speechengine") != -1)
                {
                    ipResult = "speechengine.ccm.sickkids.ca";
                    portResult = "8888";
                }
                else
                {
                    ipResult = "phenopad.ccm.sickkids.ca";
                    portResult = "8888";
                }

            }
            else
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

        /// <summary>
        /// Invoked when user clicks on the type mode button
        /// </summary>
        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            curPage.hideRecognizedTextCanvas();
            ifViewMode = false;
            currentMode = TypeMode;
            modeTextBlock.Text = TypeMode;
            writeButton.IsChecked = false;
            keyboardButton.IsChecked = true;
            curPage.inkCan.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.None;
            toggleButton.IsChecked = false;
            boldButton.Visibility = Visibility.Visible;
            italicButton.Visibility = Visibility.Visible;
            underlineButton.Visibility = Visibility.Visible;
            MainPageInkBar.Visibility = Visibility.Collapsed;
            curPage.showTextEditGrid();
        }

        /// <summary>
        /// Invoked when user clicks on hand write mode button
        /// </summary>
        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            curPage.hideRecognizedTextCanvas();
            ifViewMode = false;
            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;

            keyboardButton.IsChecked = false;
            writeButton.IsChecked = true;

            curPage.inkCan.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;
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



        private void OpenCandidate_Click(object sender, RoutedEventArgs e)
        {
            if (OpenCandidatePanelButton.IsChecked == true)
            {
                CandidatePanelStackPanel.Visibility = Visibility.Visible;
                OpenCandidateIcon.Visibility = Visibility.Collapsed;
                CloseCandidateIcon.Visibility = Visibility.Visible;
                // OpenCandidatePanelButtonIcon.Glyph = "\uE8BB";
                // OpenCandidatePanelButtonIcon.Foreground = new SolidColorBrush(Colors.DarkGray);
            }
            else
            {
                CandidatePanelStackPanel.Visibility = Visibility.Collapsed;
                // OpenCandidatePanelButtonIcon.Glyph = "\uE82F";
                // OpenCandidatePanelButtonIcon.Foreground = new SolidColorBrush(Colors.Gold);
                OpenCandidateIcon.Visibility = Visibility.Visible;
                CloseCandidateIcon.Visibility = Visibility.Collapsed;
            }

        }
        public void OpenCandidate()
        {
            if (candidatePhenoListView.Items.Count() > 0)
            {
                OpenCandidatePanelButton.IsChecked = true;
                CandidatePanelStackPanel.Visibility = Visibility.Visible;
                OpenCandidateIcon.Visibility = Visibility.Collapsed;
                CloseCandidateIcon.Visibility = Visibility.Visible;
                // OpenCandidatePanelButtonIcon.Foreground = new SolidColorBrush(Colors.DarkGray);
                // OpenCandidatePanelButtonIcon.Glyph = "\uE8BB";
                candidatePhenoListView.ScrollIntoView(candidatePhenoListView.Items.ElementAt(0));
            }

        }

        private void OverViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainSplitView.IsPaneOpen == false)
            {
                MainSplitView.IsPaneOpen = true;
                QuickViewButtonSymbol.Symbol = Symbol.Clear;
                speechQuickView.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (SpeechToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = true;
                    SpeechToggleButton.IsChecked = false;
                    speechQuickView.Visibility = Visibility.Collapsed;
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
                speechQuickView.Visibility = Visibility.Visible;
            }
            else
            {
                if (OverViewToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = false;
                    SpeechToggleButton.IsChecked = true;
                    speechQuickView.Visibility = Visibility.Visible;
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

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // save note
            await this.saveNoteToDisk();
            //On_BackRequested();
            this.Frame.Navigate(typeof(PageOverview));
            UIWebSocketClient.getSharedUIWebSocketClient().disconnect();

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
            // var mediaFlyout = (Flyout)this.Resources["MultimediaPreviewFlyout"];
            // mediaFlyout.ShowAt((FrameworkElement)sender);
            if (MultimediaPreviewGrid.Visibility == Visibility.Collapsed)
            {
                PreviewMultiMedia();
            }
            MultimediaPreviewGrid.Visibility = Visibility.Visible;

        }

        private void MultimediaClose_Click(object sender, RoutedEventArgs e)
        {
            MultimediaPreviewGrid.Visibility = Visibility.Collapsed;
            videoStreamWebSocket.Close(1000, "no reason:)");
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = await saveNoteToDisk();
            NotifyUser("Successfully saved to disk.", NotifyType.StatusMessage, 2);
            /**
            if (result)
            {
                Debug.WriteLine("Successfully saved to disk.");
                NotifyUser("Successfully saved to disk.", NotifyType.StatusMessage, 2);
            }
            else
            {
                Debug.WriteLine("Failed to save to disk.");
                NotifyUser("Failed to save to disk.", NotifyType.ErrorMessage, 2);
            }
            **/
        }

        private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MyscriptBtn_Click(object sender, RoutedEventArgs e)
        {
            // myScriptEditor.Visibility = MyscriptBtn.IsChecked != null && (bool)MyscriptBtn.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                curPage.printPage();
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.Message);
            }
        }


        private void SurfaceMicRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ConfigService.ConfigService.getConfigService().UseInternalMic();
            NotifyUser("Using Surface microphone", NotifyType.StatusMessage, 2);
        }

        private void ExterMicRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ConfigService.ConfigService.getConfigService().UseExternalMic();
            NotifyUser("Using external microphone", NotifyType.StatusMessage, 2);
        }

        private async void OpenFileFolder_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(ApplicationData.Current.LocalFolder.Path));
        }

        private async void ServerConnectButton_Click(object sender, RoutedEventArgs e)
        {
            uiClinet = UIWebSocketClient.getSharedUIWebSocketClient();
            await uiClinet.ConnectToServer();


            this.bluetoothService = BluetoothService.BluetoothService.getBluetoothService();
            await this.bluetoothService.Initialize();
        }

        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {

            // add image
            curPage.addImageAndAnnotationControlFromBitmapImage(latestImageString);
            /***
            if (this.bluetoothService == null)
            {
                NotifyUser("Could not reach Bluetooth device, try to connect again",
                                   NotifyType.ErrorMessage, 2);
                //this.bluetoothInitialized(false);
                return;
            }

            await this.bluetoothService.sendBluetoothMessage("camera picture");
            ****/
        }

        private async void StreamButton_Click(object sender, RoutedEventArgs e)
        {
            if (StreamButton.IsChecked == true)
            {
                await videoStreamStatusUpdateAsync(true);
                cameraStatusText.Text = "ON";
            }
            else
            {
                await videoStreamStatusUpdateAsync(false);
                cameraStatusText.Text = "OFF";
            }
        }



        #endregion

        //***************************Other event handlers********************************
        #region other event handlers
        /// <summary>
        /// Handle property changed event, including status flag of mic and camera
        /// </summary>
        private void MainPage_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine("Property " + e.PropertyName + " changed.");
        }
        /// <summary>
        /// Handle denpendency property changed event, including status flag of mic and camera
        /// </summary>
        private void OnPropertyChanged(DependencyObject sender, DependencyProperty dp)
        {
            Debug.WriteLine(sender.GetValue(dp));
        }
        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
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
        /// <summary>
        /// Updates the notice message to StatusBlock using a semaphore.
        /// </summary>
        private async void UpdateStatusAsync(string strMessage, NotifyType type, int seconds)
        {
            await notifySemaphoreSlim.WaitAsync();
            try
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
                // StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
                if (StatusBlock.Text != String.Empty)
                {
                    //StatusBorder.Visibility = Visibility.Visible;
                    StatusBorderEnterStoryboard.Begin();
                }
                else
                {
                    //StatusBorder.Visibility = Visibility.Collapsed;
                    StatusBorderExitStoryboard.Begin();
                }

                await Task.Delay(1000 * seconds);
                //StatusBorder.Visibility = Visibility.Collapsed;
                StatusBorderExitStoryboard.Begin();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                notifySemaphoreSlim.Release();
            }

        }
        /// <summary>
        /// Returns the user's input in a dialog input box.
        /// </summary>
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

        /// <summary>
        /// Changes the name of the current Notebook based on user's input
        /// </summary>
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
        /// Handles when user finish typing new note name but didn't press enter key
        /// </summary>
        private async void noteNameTextBox_LostFocus(object sender, RoutedEventArgs args)
        {
            if (notebookObject != null && !string.IsNullOrEmpty(noteNameTextBox.Text))
            {
                notebookObject.name = noteNameTextBox.Text;
                await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
            }
        }



        /// <summary>
        /// Handles when multimedia preview is closed
        /// </summary>
        private void MultimediaPreviewFlyout_Closed(object sender, object e)
        {
            // this.StreamView = new WebView();
            //videoStreamWebSocket.Close(1000, "no reason:)");
            throw new NotImplementedException("MultimediaPreviewFlyout_Closed");
        }

        /// <summary>
        /// Handles when multimedia preview is opened
        /// </summary>
        private void MultimediaPreviewFlyout_Opened(object sender, object e)
        {
            throw new NotImplementedException("MultimediaPreviewFlyout_Opened");
        }

        #endregion


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

        /*private void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            this.bluetoothService = BluetoothService.BluetoothService.getBluetoothService();
            this.bluetoothService.Initialize();
        }*/


    }




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

    //static class
    //HelperFunctions
    //{
    //    public static void UpdateCanvasSize(FrameworkElement root, FrameworkElement output, FrameworkElement inkCanvas)
    //    {
    //        output.Width = root.ActualWidth;
    //        output.Height = root.ActualHeight / 2;
    //        inkCanvas.Width = root.ActualWidth;
    //        inkCanvas.Height = root.ActualHeight / 2;
    //    }

    //    public static Size GetCurrentDisplaySize()
    //    {
    //        var displayInformation = DisplayInformation.GetForCurrentView();
    //        TypeInfo t = typeof(DisplayInformation).GetTypeInfo();
    //        var props = t.DeclaredProperties.Where(x => x.Name.StartsWith("Screen") && x.Name.EndsWith("InRawPixels")).ToArray();
    //        var w = props.Where(x => x.Name.Contains("Width")).First().GetValue(displayInformation);
    //        var h = props.Where(x => x.Name.Contains("Height")).First().GetValue(displayInformation);
    //        var size = new Size(System.Convert.ToDouble(w), System.Convert.ToDouble(h));
    //        switch (displayInformation.CurrentOrientation)
    //        {
    //            case DisplayOrientations.Landscape:
    //            case DisplayOrientations.LandscapeFlipped:
    //                size = new Size(Math.Max(size.Width, size.Height), Math.Min(size.Width, size.Height));
    //                break;
    //            case DisplayOrientations.Portrait:
    //            case DisplayOrientations.PortraitFlipped:
    //                size = new Size(Math.Min(size.Width, size.Height), Math.Max(size.Width, size.Height));
    //                break;
    //        }
    //        return size;
    //    }

    //    /// <summary>
    //    /// Decodes Base 64 string source to bitmap image
    //    /// </summary>
    //    public static async Task<BitmapImage> Base64ToBitmapAsync(string source)
    //    {
    //        var byteArray = Convert.FromBase64String(source);
    //        BitmapImage bitmap = new BitmapImage();
    //        using (MemoryStream stream = new MemoryStream(byteArray))
    //        {
    //            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
    //        }
    //        return bitmap;
    //    }
    //}

}
