using System;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using PhenoPad.PhenotypeService;
using Windows.UI.Xaml.Input;
using System.Collections.Generic;
using Windows.UI.Popups;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Navigation;
using System.Text;
using PhenoPad.SpeechService;
using Windows.Devices.Sensors;
using Windows.UI;
using System.Diagnostics;
using PhenoPad.CustomControl;
using Windows.Graphics.Display;
using System.Linq;
using PhenoPad.WebSocketService;
using Windows.ApplicationModel.Core;
using PhenoPad.FileService;
using System.ComponentModel;
using System.Threading;
using Windows.Storage;
using Microsoft.Toolkit.Uwp.UI.Animations;
using PhenoPad.LogService;
using Microsoft.Toolkit.Uwp.UI.Controls;

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
        // unique identifier of the current note file
        public string notebookId = "";
        private Notebook notebookObject;
        public static readonly string TypeMode = "Typing Mode";
        public static readonly string WritingMode = "Handwriting Mode";
        public static readonly string ViewMode = "Previewing Mode";
        private string currentMode = WritingMode;
        private bool ifViewMode = false;
        public CancellationTokenSource cancelService;

        //private int num = 0;
        public bool abbreviation_enabled;
        public List<Phenotype> showingPhenoSpeech;
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
            //titleBar.ButtonBackgroundColor = Colors.Black;
            //titleBar.ButtonInactiveBackgroundColor = Colors.Black;


            // We want to react whenever speech engine has new results
            // this.speechManager.EngineHasResult += SpeechManager_EngineHasResult;

            //scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ZoomFactorProperty, OnPropertyChanged);

            //showTextGrid.PointerPressed += new PointerEventHandler(showTextGrid_PointerPressed);
            modeTextBlock.PointerReleased += new PointerEventHandler(modeTextBlock_PointerReleased);
            modeTextBlock.PointerCanceled += new PointerEventHandler(modeTextBlock_PointerExited);
            modeTextBlock.PointerEntered += new PointerEventHandler(modeTextBlock_PointerEntered);
            modeTextBlock.PointerExited += new PointerEventHandler(modeTextBlock_PointerExited);
            //adding event handler to when erase all is clicked
            MainPageInkBar.EraseAllClicked += InkToolbar_EraseAllClicked;

            // Speech Panel initialization 
            chatView.ItemsSource = SpeechManager.getSharedSpeechManager().conversation;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            realtimeChatView.ItemsSource = SpeechManager.getSharedSpeechManager().realtimeConversation;
            speechEngineRunning = false;
            PropertyChanged += MainPage_PropertyChanged;


            HWRAddrInput.Text = HWRService.HWRManager.getSharedHWRManager().getIPAddr();
            string serverPath = SpeechManager.getSharedSpeechManager().getServerAddress() + ":" +
                                SpeechManager.getSharedSpeechManager().getServerPort();
            ASRAddrInput.Text = serverPath;
            PhenoSuggestionAddr.Text = PhenotypeManager.SUGGESTION_ADDR;
            DiffDiagnosisAddr.Text = PhenotypeManager.DIFFERENTIAL_ADDR;
            PhenoDetailAddr.Text = PhenotypeManager.PHENOTYPEINFO_ADDR;

            AbbreviationON_Checked(null, null);
            InitBTConnectionSemaphore = new SemaphoreSlim(1);
            InitializeBTConnection();

            this.SavedAudios = new List<string>();
            showingPhenoSpeech = new List<Phenotype>();
            conversations = new List<TextMessage>(); // Stores all TextMessages in the notebook,
                                                     // items in this list are displayed in SpeechPage's
                                                     // speech bubbles.

            playbackSem = new SemaphoreSlim(1);
            audioTimer = new DispatcherTimer();

            // waits 3 seconds before re-enabling microphone button
            audioTimer.Interval = TimeSpan.FromSeconds(3);
            audioTimer.Tick += onAudioStarted;

            isReading = false;
            readTimer = new DispatcherTimer();
            readTimer.Interval = TimeSpan.FromSeconds(1.5); //TODO: Question: if readTimer interval is set to 1.5s, and EndAudioStream is called at timer tick, how does audiostream have time to read data? 
            readTimer.Tick += EndAudioStream;

            cancelService = new CancellationTokenSource();
            this.Tapped += HideUIs;

            // When user clicks X while in mainpage, auto-saves all current process and exits the program.
            Windows.UI.Core.Preview.SystemNavigationManagerPreview.GetForCurrentView().CloseRequested +=
            async (sender, args) =>
            {
                args.Handled = true;
                bool result = await confirmOnExit_Clicked();
                if (result)
                {
                    Debug.WriteLine("Successful, will exit app ...");
                    Application.Current.Exit();
                }
                args.Handled = false;
            };
        }

        //******************************END OF CONSTRUCTORS************************************************

        private void HideUIs(object sender, TappedRoutedEventArgs e)
        {
            PhenotypePopup.Visibility = Visibility.Collapsed;
        }

        private async void InitializeBTConnection() {
            var success = await changeSpeechEngineState_BT();
        }

        /// <summary>
        /// Prompts the user for exiting confirmation and saves the most recently edited notebook
        /// if user attempts to exit while editing, exits apps after
        /// </summary>
        private async Task<bool> confirmOnExit_Clicked() {
            //no need to ask user if already at note overview page
            if (Frame.CurrentSourcePageType == typeof(PageOverview))
                Application.Current.Exit();

            var messageDialog = new MessageDialog("Save and exit?");
            messageDialog.Title = "PhenoPad";
            messageDialog.Commands.Add(new UICommand("Save") { Id = 0 });
            messageDialog.Commands.Add(new UICommand("Don't Save") { Id = 1 });
            messageDialog.Commands.Add(new UICommand("Cancel") { Id = 2 });
            // Set the command that will be invoked by default
            messageDialog.DefaultCommandIndex = 2;
            // Set the command to be invoked when escape is pressed
            messageDialog.CancelCommandIndex = 2;
            // Show the message dialog
            var result = await messageDialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                bool saved = false;
                //only saves the notes if in editing stage
                if (notebookId != null)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                    {
                        MetroLogger.getSharedLogger().Info("Saving ...");
                        saved = await saveNoteToDisk();
                        if (speechEngineRunning)
                            await KillAudioService();
                    });
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                if (saved)
                    return true;
            }
            else if ((int)result.Id == 1)
            {
                MetroLogger.getSharedLogger().Info("Exiting app without saving ...");
                return true;
            }
            MetroLogger.getSharedLogger().Info("Canceled Exiting app");
            return false;
        }

        /// <summary>
        /// Initializes display for the loaded page
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
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
        }


        //  ************Switching between editing / view mode *********************
        #region Switching between Editing / View Mode
        /// <summary>
        /// Switches back editing mode panel
        /// </summary>
        private void modeTextBlock_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!ifViewMode && curPage != null)
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
            if (!ifViewMode && curPage != null)
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

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            /// Initializes the Notebook when user navigated to MainPage.

            LoadingPopup.IsOpen = true;

            var nid = e.Parameter as string;
            StorageFile file = e.Parameter as StorageFile;

            if (nid == "__new__") {
                Debug.WriteLine("create new");
                this.loadFromDisk = false;
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, this.InitializeNotebook);
            }
            else if (e.Parameter == null || file != null)
            {//is a file for importing EHR
                Debug.WriteLine("create EHR");
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => { this.InitializeEHRNote(file); });
            }
            else
            {//is a valid note to load
                Debug.WriteLine("loading");
                this.loadFromDisk = true;
                this.notebookId = nid;
                FileManager.getSharedFileManager().currentNoteboookId = nid;
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, this.InitializeNotebookFromDisk);
            }
            await Task.Delay(TimeSpan.FromSeconds(3));
            LoadingPopup.IsOpen = false;
            return;                
        }

        //NOTE: no reference found to this function? 
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            /// Clearing all cache and index records before leaving MainPage.

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, async ()=> {

                if (speechEngineRunning)
                {//close all audio services before navigating
                    if (bluetoonOn)
                    {
                        // because we are no longer in mainpage, does not need to reload past conversation
                        await SpeechManager.getSharedSpeechManager().StopASRResults(false);
                    }
                    else
                    {
                        //NOTE: it's probably bad style to directly call an event handler function without the triggering event
                        //NOTE: if not reloading past conversation in when using RPI, then reload should be false in this case, too
                        //TODO: rewrite this
                        AudioStreamButton_Clicked();
                    }

                }
                if (curPage != null) {
                    curPage.Visibility = Visibility.Collapsed;
                    await saveNoteToDisk();
                }
                SpeechPage.Current.PlaybackTimer_Tick(null,null);
                SpeechManager.getSharedSpeechManager().cleanUp();
                CloseCandidate();
                notePages = null;
                notebookId = null;
                // clear page index panel
                //clearPageIndexPanel();
                inkCanvas = null;
                curPage = null;
                curPageIndex = -1;
                showAddIn(new List<ImageAndAnnotation>());
                PhenotypeManager.getSharedPhenotypeManager().clearCache();
            });
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

        
        private void LoseFocus(object sender)
        {
            /// <summary>
            /// Makes virtual keyboard disappear
            /// </summary>
            var control = sender as Control;
            var isTabStop = control.IsTabStop;
            control.IsTabStop = false;
            control.IsEnabled = false;
            control.IsEnabled = true;
            control.IsTabStop = isTabStop;
        }

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
            //NotesButton.IsChecked = true;
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
            //NotesButton.IsChecked = false;
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

        /// <summary>
        /// Handler function called when "Chat Record" button is clicked. Opens the ChatRecord/Speech Page.
        /// </summary>
        private void SpeechButton_Click(object sender, RoutedEventArgs e)
        {
            //NotesButton.IsChecked = false;
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


        //=======================================SWITCHING NOTE PAGES========================================
        public void AddNewNotePage(string state = "") {
            if (state == "next")
            {
                if (curPageIndex == notePages.Count - 1)
                    AddPageButton_Click();
                else
                    NextPageButton_Click();
            }
            else if (state == "previous")
            {
                PreviousPageButton_Click();
            }
        }

        private async void AddPageButton_Click(object sender = null, RoutedEventArgs e = null)
        {
            PageHostContentTrans.Edge = Windows.UI.Xaml.Controls.Primitives.EdgeTransitionLocation.Bottom;
            curPage.Visibility = Visibility.Collapsed;
            //defining a new string name for the page and creates a new page controller to bind
            string newPageName = (notePages.Count).ToString();
            NotePageControl aPage = new NotePageControl(this.notebookId, newPageName);
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            curPage = aPage;
            curPageIndex = notePages.Count - 1;

            CloseCandidate();
            PageHost.Content = curPage;
            showAddIn(new List<ImageAndAnnotation>());
            setPageIndexText(curPageIndex);

            //addNoteIndex(curPageIndex);
            await FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());
            //auto-saves whenever a new page is created, this operation doesn't need a timer since 
            //we assume the user will not spam adding pages...
            await this.saveNoteToDisk();
            curPage.Visibility = Visibility.Visible;
            curPage.ScrollToTop();
        }

        private async void NextPageButton_Click(object sender = null, RoutedEventArgs e = null)
        {
            PageHostContentTrans.Edge = Windows.UI.Xaml.Controls.Primitives.EdgeTransitionLocation.Bottom;

            if (curPageIndex < notePages.Count - 1)
            {
                curPage.Visibility = Visibility.Collapsed;
                curPageIndex++;
                var aPage = notePages.ElementAt(curPageIndex);
                inkCanvas = aPage.inkCan;
                curPage = aPage;
                PageHost.Content = curPage;
                setPageIndexText(curPageIndex);
                int count = PhenoMana.ShowPhenoCandAtPage(curPageIndex);
                if (count <= 0)
                    CloseCandidate();
                else
                    OpenCandidate();
                curPage.Visibility = Visibility.Visible;
                var addins = await curPage.GetAllAddInObjects();
                showAddIn(addins);
                curPage.ScrollToTop();
                return;
            }
            NotifyUser("This is the last page", NotifyType.StatusMessage, 1);
        }

        private async void PreviousPageButton_Click(object sender=null, RoutedEventArgs e=null)
        {
            PageHostContentTrans.Edge = Windows.UI.Xaml.Controls.Primitives.EdgeTransitionLocation.Top;

            if (curPageIndex > 0)
            {
                curPage.Visibility = Visibility.Collapsed;
                curPageIndex--;
                var aPage = notePages.ElementAt(curPageIndex);
                inkCanvas = aPage.inkCan;
                curPage = aPage;
                PageHost.Content = curPage;
                setPageIndexText(curPageIndex);
                int count = PhenoMana.ShowPhenoCandAtPage(curPageIndex);
                if (count <= 0)
                    CloseCandidate();
                else
                    OpenCandidate();
                curPage.Visibility = Visibility.Visible;
                var addins = await curPage.GetAllAddInObjects();
                showAddIn(addins);
                curPage.ScrollToTop();
                return;
            }
        }

        private void clearPageIndexPanel()
        {
            /// <summary>
            /// Clears all page index records in the StackPanel.
            /// </summary>

            if (pageIndexPanel.Children.Count() > 1)
            {
                while (pageIndexPanel.Children.Count() > 1)
                    pageIndexPanel.Children.RemoveAt(0);
            }
        }

        private void setPageIndexText(int index)
        {
            /// <summary>
            /// Sets the ink bar controller to the current ink canvas.
            /// </summary>
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPageIndexBlock.Content = $"{index + 1}";
        }

        public async void ShowAllPagePanel(object sender, RoutedEventArgs args) {

            NoteGridView.ItemsSource = new List<NotePage>();
            var curNotebook = MainPage.Current.notebookObject;
            List<NotePage> pages = await FileManager.getSharedFileManager().GetAllNotePageObjects(curNotebook.id);

            if (pages != null)
            {
                NoteGridView.ItemsSource = pages;
            }
            else
                Debug.WriteLine("oops pages are null");

            AllPagesPanel.ShowAt((Button)sender);
        }

        public async void AllPageItem_Click(object sender, ItemClickEventArgs args) {

            int index;
            Int32.TryParse((((NotePage)args.ClickedItem).id),out index);

            if (index > curPageIndex)
                PageHostContentTrans.Edge = Windows.UI.Xaml.Controls.Primitives.EdgeTransitionLocation.Bottom;
            else
                PageHostContentTrans.Edge = Windows.UI.Xaml.Controls.Primitives.EdgeTransitionLocation.Top;

            if (index >= 0 && index < notePages.Count && index != curPageIndex)
            {
                curPage.Visibility = Visibility.Collapsed;
                curPageIndex = index;
                var aPage = notePages.ElementAt(curPageIndex);
                inkCanvas = aPage.inkCan;
                curPage = aPage;
                PageHost.Content = curPage;
                setPageIndexText(curPageIndex);
                int count = PhenoMana.ShowPhenoCandAtPage(curPageIndex);
                if (count <= 0)
                    CloseCandidate();
                else
                    OpenCandidate();
                curPage.Visibility = Visibility.Visible;
                var addins = await curPage.GetAllAddInObjects();
                showAddIn(addins);
                //curPage.ScrollToTop();
                return;
            }
        }

        /// <summary>
        /// Sets the display color of all note page buttons
        /// </summary>
        //private void setNotePageIndex(int index)
        //{
        //    curPageIndexBlock.Text = $"{index + 1}";
        //    foreach (var btn in pageIndexButtons)
        //    {
        //        btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
        //        btn.Foreground = new SolidColorBrush(Colors.Gray);
        //    }
        //    pageIndexButtons.ElementAt(index).Background = Application.Current.Resources["Button_Background"] as SolidColorBrush;
        //    pageIndexButtons.ElementAt(index).Foreground = new SolidColorBrush(Colors.Black);
        //}

        /// <summary>
        /// Adds a new button to page index after creating a new page
        /// </summary>
        //private void addNoteIndex(int index)
        //{
        //    Button btn = new Button();
        //    btn.Click += IndexBtn_Click;
        //    btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
        //    btn.Foreground = new SolidColorBrush(Colors.Black);
        //    btn.Padding = new Thickness(0, 0, 0, 0);
        //    btn.Content = "" + (index + 1);
        //    btn.Width = 30;
        //    btn.Height = 30;
        //    pageIndexButtons.Add(btn);
        //    if (pageIndexPanel.Children.Count >= 1)
        //        pageIndexPanel.Children.Insert(pageIndexPanel.Children.Count - 1, btn);
        //    setNotePageIndex(index);

        //}


        //private async void IndexBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    /// <summary>
        //    /// Called when user clicks on a notepage index button
        //    /// </summary>

        //    var button = (Button)sender;
        //    foreach (var btn in pageIndexButtons)
        //    {
        //        btn.Background = new SolidColorBrush(Colors.WhiteSmoke);
        //        btn.Foreground = Application.Current.Resources["Button_Background"] as SolidColorBrush;
        //    }
        //    button.Background = Application.Current.Resources["Button_Background"] as SolidColorBrush;
        //    button.Foreground = new SolidColorBrush(Colors.WhiteSmoke);

        //    curPageIndex = Int32.Parse(button.Content.ToString()) - 1;
        //    var aPage = notePages.ElementAt(curPageIndex);
        //    inkCanvas = aPage.inkCan;
        //    curPage = aPage;
        //    PageHost.Content = curPage;
        //    setPageIndexText(curPageIndex);
        //    //setNotePageIndex(curPageIndex);
        //    //shows add-in icons into side bar
        //    var addins = await curPage.GetAllAddInObjects();
        //    showAddIn(addins);

        //    int count = PhenoMana.ShowPhenoCandAtPage(curPageIndex);
        //    if (count <= 0)
        //        CloseCandidate();
        //    else
        //        OpenCandidate();


        //    //if (curPage.ehrPage == null)
        //    //else
        //    //    aPage.ehrPage.AnalyzePhenotype();

        //    aPage.Visibility = Visibility.Visible;
        //}

        //=======================================NOTE SAVING INTERFACES=====================================

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

        
        private async void LoadNote_Click(object sender, RoutedEventArgs e)
        {
            /// <summary>
            /// Invoked when user clicks the "load note" button from drop down menu
            /// </summary>
            bool is_loaded = await loadStrokefromGif();
            if (is_loaded)
                NotifyUser("The note has been loaded.", NotifyType.StatusMessage, 2);
            else
                NotifyUser("Failed to load note", NotifyType.ErrorMessage, 2);
        }

        
        private async void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            /// <summary>
            /// Invoked when user clicks "load an image".
            /// </summary>
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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            AppSetting.Visibility = Visibility.Visible;
        }

        private async void ClearRecogBtn_Click(object sender, RoutedEventArgs e) {
            LoadingPopup.IsOpen = true;
            LoadingPopup.Visibility = Visibility.Visible;
            await Current.curPage.ClearAndRecognizePage();
            await Task.Delay(TimeSpan.FromSeconds(2));

        }

        private void ChangeServerHWR_Click(object sender, RoutedEventArgs e) {
            string newAddr = HWRAddrInput.Text;
            HWRService.HWRManager.getSharedHWRManager().setIPAddr(new Uri(newAddr));
            NotifyUser("HWR Server address has been changed",NotifyType.StatusMessage,1);
        }

        private void ChangeServerASR_Click(object sender, RoutedEventArgs e) {
            string text = ASRAddrInput.Text;
            string ipResult = "";
            string portResult = "";

            int colonIndex = text.IndexOf(':');
            if (text != string.Empty && colonIndex != -1)
            {               
                ipResult = text.Substring(0, colonIndex).Trim();
                portResult = text.Substring(colonIndex + 1).Trim();
            }
            else {
                NotifyUser("Invalid ASR address, will use default setting", NotifyType.ErrorMessage, 2);
                ipResult = SpeechManager.DEFAULT_SERVER;
                portResult = SpeechManager.DEFAULT_PORT;
                ASRAddrInput.Text = SpeechManager.DEFAULT_SERVER + ":" + SpeechManager.DEFAULT_PORT;
            }
            SpeechManager.getSharedSpeechManager().setServerAddress(ipResult);
            SpeechManager.getSharedSpeechManager().setServerPort(portResult);
            AppConfigurations.saveSetting("serverIP", ipResult);
            AppConfigurations.saveSetting("serverPort", portResult);
            NotifyUser("ASR Server address has been changed", NotifyType.StatusMessage, 1);
        }

        private void PhenoSuggestionAddr_Click(object sender, RoutedEventArgs e)
        {
            PhenotypeManager.SUGGESTION_ADDR = PhenoSuggestionAddr.Text;
            NotifyUser("Phenotype suggestion server address has been changed.",NotifyType.StatusMessage,1);
        }

        private void DiffDiagnosisAddr_Click(object sender, RoutedEventArgs e)
        {
            PhenotypeManager.DIFFERENTIAL_ADDR = DiffDiagnosisAddr.Text;
            NotifyUser("Differential diagnosis server address has been changed.", NotifyType.StatusMessage, 1);
        }

        private void PhenoDetailAddr_Click(object sender, RoutedEventArgs e)
        {
            PhenotypeManager.PHENOTYPEINFO_ADDR = PhenoDetailAddr.Text;
            NotifyUser("Phenotype detail server address has been changed.", NotifyType.StatusMessage, 1);
        }

        private void SettingsClose_Click(object sender, RoutedEventArgs e) {
            AppSetting.Visibility = Visibility.Collapsed;
        }

        
        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            /// <summary>
            /// Invoked when user clicks on the type mode button
            /// </summary>
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

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            /// <summary>
            /// Invoked when user clicks on hand write mode button
            /// </summary>
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = await saveNoteToDisk();
            if (result)
            {
                NotifyUser("Successfully saved to disk.", NotifyType.StatusMessage, 2);
            }
            else
            {
                NotifyUser("Failed to save to disk.", NotifyType.ErrorMessage, 2);
            }
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e) {

            if (ExpandButton.IsChecked == true)
            {
                CandidateGrid.Width = this.Width - 120;
                UpdateLayout();

                Grid.SetRowSpan(CandidatePanelStackPanel, 1);
                ScrollViewer.SetHorizontalScrollMode(candidatePhenoListView, ScrollMode.Enabled);
                ScrollViewer.SetVerticalScrollMode(candidatePhenoListView, ScrollMode.Disabled);
                //WrapPanel wp = new WrapPanel();
                //wp.Orientation = Orientation.Horizontal;
                //wp.FlowDirection = FlowDirection.LeftToRight;

                //candidatePhenoListView.ItemsPanelRoot.SetValue(Width, 9999);
            }
            else {
                CandidateGrid.Width = this.Width - 120;
                UpdateLayout();

                Grid.SetRowSpan(CandidatePanelStackPanel, 2);
                ScrollViewer.SetHorizontalScrollMode(candidatePhenoListView, ScrollMode.Disabled);
                ScrollViewer.SetVerticalScrollMode(candidatePhenoListView, ScrollMode.Enabled);
            }
        }

        private void OpenCandidate_Click(object sender, RoutedEventArgs e)
        {
            CandidateGrid.Width = this.Width - 120;
            UpdateLayout();

            if (OpenCandidatePanelButton.IsChecked == true)
            {
                CandidatePanelStackPanel.Visibility = Visibility.Visible;
                OpenCandidateIcon.Visibility = Visibility.Collapsed;
                CloseCandidateIcon.Visibility = Visibility.Visible;
                ExpandButton.Visibility = Visibility.Visible;
                // OpenCandidatePanelButtonIcon.Glyph = "\uE8BB";
                // OpenCandidatePanelButtonIcon.Foreground = new SolidColorBrush(Colors.DarkGray);
            }
            else
            {
                CandidatePanelStackPanel.Visibility = Visibility.Collapsed;
                // OpenCandidatePanelButtonIcon.Glyph = "\uE82F";
                // OpenCandidatePanelButtonIcon.Foreground = new SolidColorBrush(Colors.Gold);
                OpenCandidateIcon.Visibility = Visibility.Visible;
                ExpandButton.Visibility = Visibility.Collapsed;
                CloseCandidateIcon.Visibility = Visibility.Collapsed;
            }

        }

        public bool CandidateIsOpened() {
            return CandidatePanelStackPanel.Visibility == Visibility.Visible;
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
                ExpandButton.Visibility = Visibility.Visible;
            }
        }

        public void CloseCandidate() {
            CandidatePanelStackPanel.Visibility = Visibility.Collapsed;
            OpenCandidateIcon.Visibility = Visibility.Visible;
            OpenCandidatePanelButton.Visibility = Visibility.Visible;
            OpenCandidatePanelButton.IsChecked = false;
            CloseCandidateIcon.Visibility = Visibility.Collapsed;
            if (ExpandButton.IsChecked == true)
                ExpandButton_Click(null, null);
            ExpandButton.Visibility = Visibility.Collapsed;

        }

        private void OverViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainSplitView.IsPaneOpen == false)
            {
                MainSplitView.IsPaneOpen = true;
                QuickViewButtonSymbol.Symbol = Symbol.Clear;
                speechQuickView.Visibility = Visibility.Collapsed;
                //pastchatView.Visibility = Visibility.Collapsed;
                //pastSpeechView.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (SpeechToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = true;
                    SpeechToggleButton.IsChecked = false;
                    speechQuickView.Visibility = Visibility.Collapsed;
                    //pastchatView.Visibility = Visibility.Collapsed;
                    //pastSpeechView.Visibility = Visibility.Collapsed;
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
                //pastSpeechView.Visibility = Visibility.Visible;
                speechQuickView.Visibility = Visibility.Visible;
                //pastchatView.Visibility = Visibility.Visible;
            }
            else
            {
                if (OverViewToggleButton.IsChecked == true)
                {
                    OverViewToggleButton.IsChecked = false;
                    SpeechToggleButton.IsChecked = true;
                    //pastSpeechView.Visibility = Visibility.Visible;
                    speechQuickView.Visibility = Visibility.Visible;
                    //pastchatView.Visibility = Visibility.Visible;
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
            bool confirmed = await ConfirmNoteClose_OnBackButton();

            if (confirmed)
            {
                ResetSpeechPopUp_OnBackButton();

                cancelService.Cancel();
                cancelService = new CancellationTokenSource();

                LoadingPopup.IsOpen = true;
                // save note
                //await this.saveNoteToDisk();
                UIWebSocketClient.getSharedUIWebSocketClient().disconnect();
                await Task.Delay(TimeSpan.FromSeconds(1));
                LoadingPopup.IsOpen = false;

                //On_BackRequested();
                this.Frame.Navigate(typeof(PageOverview));
            }
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
            if (MultimediaPreviewGrid.Visibility == Visibility.Collapsed)
            {
                PreviewMultiMedia();
            }
            MultimediaPreviewGrid.Visibility = MultimediaPreviewGrid.Visibility == Visibility.Visible? Visibility.Collapsed : Visibility.Visible;
        }

        private void MultimediaClose_Click(object sender, RoutedEventArgs e)
        {
            MultimediaPreviewGrid.Visibility = Visibility.Collapsed;
            if (videoStreamWebSocket != null)
                videoStreamWebSocket.Close(1000, "no reason:)");
        }

        private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
        {

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

       
        private void SurfaceMicRadioButton_Checked(object sender = null, RoutedEventArgs e = null)
        {
            ConfigService.ConfigService.getConfigService().UseInternalMic();
            this.audioButton.IsEnabled = true;
            //this.serverConnectButton.IsEnabled = false;
            this.StreamButton.IsEnabled = false;
            SurfaceMicRadioBtn.IsChecked = true;
            //this.StreamButton.IsEnabled = true;
           // NotifyUser("Using Surface microphone", NotifyType.StatusMessage, 2);
        }

        private void ExterMicRadioButton_Checked(object sender = null, RoutedEventArgs e = null)
        {
            ConfigService.ConfigService.getConfigService().UseExternalMic();
            this.StreamButton.IsEnabled = false;
            this.shutterButton.IsEnabled = false;
            this.audioButton.IsEnabled = true;
            ExternalMicRadioBtn.IsChecked = true;
            //NotifyUser("Using external microphone", NotifyType.StatusMessage, 2);
        }

        private void AbbreviationON_Checked(object sender, RoutedEventArgs e) {
            this.abbreviation_enabled = true;
            AbbrONBtn.IsChecked = true;
        }
        private void AbbreviationOFF_Checked(object sender, RoutedEventArgs e) {
            this.abbreviation_enabled = false;
            AbbrOFFBtn.IsChecked = true;
        }

        private async void OpenFileFolder_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(ApplicationData.Current.LocalFolder.Path));
        }

        //Invoked when click on bluetoon button;
        //private async void ServerConnectButton_Click(object sender = null, RoutedEventArgs e= null)
        //{
        //    if (!bluetoonOn)
        //    {
        //        BluetoothProgresssBox.Text = "Connecting to Raspberry Pi";
        //        serverConnectButton.IsEnabled = false;
        //        BluetoothProgress.IsActive = true;
        //        BluetoothComplete.Visibility = Visibility.Collapsed;
        //        uiClinet = UIWebSocketClient.getSharedUIWebSocketClient();
        //        bool uiResult = await uiClinet.ConnectToServer();
        //        if (!uiResult)
        //        {
        //            LogService.MetroLogger.getSharedLogger().Error("UIClient failed to connect.");
        //        }
        //        this.bluetoothService = BluetoothService.BluetoothService.getBluetoothService();
        //        await this.bluetoothService.Initialize();
                
        //    }
        //    else {
        //        uiClinet.disconnect();
        //        bool result = this.bluetoothService.CloseConnection();               
        //        if (result)
        //        {
        //            this.bluetoothService = null;
        //            this.bluetoonOn = false;
        //            bluetoothInitialized(false);
        //            setStatus("bluetooth");
        //            BluetoothProgresssBox.Text = "Disconnected Raspberry Pi";
        //            BluetoothComplete.Visibility = Visibility.Visible;
        //            BluetoothProgress.IsActive = false;
                    
        //            NotifyUser("Bluetooth Connection disconnected.", NotifyType.StatusMessage, 2);
        //        }
        //        else {
        //            NotifyUser("Bluetooth Connection failed to disconnect.", NotifyType.ErrorMessage, 2);
        //        }                
        //    }

        //}

        private void CameraButton_Click(object sender, RoutedEventArgs e)
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

        
        private void InkToolbar_EraseAllClicked(InkToolbar sender, object args)
        {
            /// <summary>
            /// Event Handler for when user click erase all ink button
            /// </summary>
            /// 
            //calling auto-saving handler to save erased result
            //NotifyUser("");
            this.curPage.on_stroke_changed();
            curPage.ClearAllParsedText();
            PhenotypeManager.getSharedPhenotypeManager().phenotypesCandidates.Clear();
            //more clearing caches
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
        public void NotifyUser(string strMessage, NotifyType type, double seconds)
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
        private async void UpdateStatusAsync(string strMessage, NotifyType type, double seconds)
        {
            await notifySemaphoreSlim.WaitAsync();
            try
            {
                switch (type)
                {
                    case NotifyType.StatusMessage:
                        StatusBorder.Background = new SolidColorBrush(Colors.Turquoise);
                        break;
                    case NotifyType.ErrorMessage:
                        StatusBorder.Background = new SolidColorBrush(Colors.Tomato);
                        break;
                }

                StatusBlock.Text = strMessage;

                // Collapse the StatusBlock if it has no text to conserve real estate.
                // StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
                if (StatusBlock.Text != String.Empty)
                {

                    StatusBorder.Visibility = Visibility.Visible;
                    await StatusBorderEnterStoryboard.BeginAsync();
                }
                else
                {
                    await StatusBorderExitStoryboard.BeginAsync();
                    StatusBorder.Visibility = Visibility.Collapsed;
                }

                await Task.Delay(TimeSpan.FromSeconds(seconds));
                await StatusBorderExitStoryboard.BeginAsync();
                StatusBorder.Visibility = Visibility.Collapsed;

            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error(e+e.Message);
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

        
        private async void noteNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            /// <summary>
            /// Changes the name of the current Notebook based on user's input
            /// </summary>
            try
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    if (notebookObject != null && !string.IsNullOrEmpty(noteNameTextBox.Text))
                    {
                        LogService.MetroLogger.getSharedLogger().Info("Saving new notebook name ... ");
                        notebookObject.name = noteNameTextBox.Text;
                        bool result = await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
                        if (result)
                            LogService.MetroLogger.getSharedLogger().Info("Done saving new notebook name.");
                        LoseFocus(sender);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save new notebook name:{ex.Message}");
            }
        }

        /// <summary>
        /// Handles when user finish typing new note name but didn't press enter key
        /// </summary>
        private async void noteNameTextBox_LostFocus(object sender, RoutedEventArgs args)
        {
            try {
                if (notebookObject != null && !string.IsNullOrEmpty(noteNameTextBox.Text))
                {
                    LogService.MetroLogger.getSharedLogger().Info("Saving new notebook name ... ");
                    notebookObject.name = noteNameTextBox.Text;
                    bool result = await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
                    if (result)
                        LogService.MetroLogger.getSharedLogger().Info("Done saving new notebook name.");
                }
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save new notebook name:{ex.Message}");
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

        #region ADDIN PANEL HANDLERS

        public void showAddIn(List<ImageAndAnnotation> images)
        {
            /// <summary>
            /// Refreshes the listitem source of current page add-ins in the addin preview dock 
            /// </summary>

            try
            {
                badgeGrid.Visibility = Visibility.Collapsed;

                if (images.Count > 0)
                {
                    addinlist.Visibility = Visibility.Visible;
                    List<ImageAndAnnotation> addins = images.Where(x => x.commentID == -1).ToList();
                    addinlist.ItemsSource = addins;
                    if (addins.Count > 0)
                    {
                        NumIcon.Text = $"{addins.Count}";
                        badgeGrid.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    addinlist.ItemsSource = new List<ImageAndAnnotation>();
                    //addinlist.Visibility = Visibility.Collapsed;
                    NumIcon.Text = "";
                }
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed to refresh addin icon list:{e}{e.Message}");
            }
        }

        public async Task quickShowDock()
        {/// <summary>Quick plays addin dock sliding animation</summary>
            if (slide.X == 250)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await addinShowAnimation.BeginAsync();

                });
                await Task.Delay(TimeSpan.FromSeconds(0.3));

                await addinHideAnimation.BeginAsync();
            }
            else if (slide.X == 0)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                    await addinHideAnimation.BeginAsync();
                });

            }
            return;
        }

        public async void AddinsButton_Click(object sender, RoutedEventArgs e)
        {
            /// <summary>
            /// Refreshes and show the list of addins within a notepage
            /// </summary>

            if (slide.X == 250)
                await addinShowAnimation.BeginAsync();
            else
                await addinHideAnimation.BeginAsync();
        }

        /// <summary>
        /// Toggles the in dock status of an addin and show/hides it from main canvas
        /// </summary>
        public async void addInIcon_Click(object sender, RoutedEventArgs e)
        {
            //gets the clicked addin name and search for the specific addin
            //in user canvas, then hides its panel
            Viewbox icon = (Viewbox)((Button)sender).Content;
            AddInControl icon_addin = (AddInControl)icon.Child;
            string name = icon_addin.name;
            List<AddInControl> addinlist = await curPage.GetAllAddInControls();
            AddInControl addin = addinlist.Where(x => x.name == name).ToList()[0];
            addin.Maximize_Addin();
        }

        /// <summary>
        /// Refetch updated meta XML data for addins and uses showAddIn() to refresh preview dock.
        /// </summary>
        public async Task refreshAddInList()
        {
            try
            {
                List<ImageAndAnnotation> imageAndAnno = await FileManager.getSharedFileManager().
                                              GetImgageAndAnnotationObjectFromXML(notebookId, curPage.pageId);
                showAddIn(imageAndAnno);
            }
            catch (Exception)
            {
            }
        }
        #endregion

        #region FOR TESTING/DEMO ONLY
        /// <summary>
        /// Temporarily disables/enables abbreviation detection for HWR
        /// </summary>
        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {

            //curPage.changeLineHeight();
        }
        #endregion

        //***************************Helper functions********************************
        
        //TODO: might have better places for these functions
        /// <summary>
        /// Closes Speech Page and un-checks button
        /// </summary>
        private void ResetSpeechPopUp_OnBackButton()
        {
            if (SpeechPopUp.IsOpen)
            {
                SpeechPopUp.IsOpen = false;
                SpeechButton.IsChecked = false;
            }
        }

        /// <summary>
        /// Pops up a dialog box to confirm exiting note when the back button is clicked.
        /// </summary>
        /// <returns>(bool)true if the user confirms, (bool)false otherwise</returns>
        private async Task<bool> ConfirmNoteClose_OnBackButton()
        {
            const string message = "Are you sure that you would like to close note?";
            const string title = "Closing Note";
            var messageDialog = new MessageDialog(message);
            messageDialog.Title = title;
            messageDialog.Commands.Add(new UICommand { Label = "Yes", Id = 0 });
            messageDialog.Commands.Add(new UICommand { Label = "No", Id = 1 });

            messageDialog.DefaultCommandIndex = 1;
            messageDialog.CancelCommandIndex = 1;

            var result = await messageDialog.ShowAsync();

            if ( (int)result.Id == 0 )
            {
                return true;
            }
            if ( (int)result.Id == 1 )
            {
                return false;
            }
            return false;
        }


    }
    //================================= END OF MAINAPGE ==========================================/

    
    public class CalligraphicPen : InkToolbarCustomPen
    {
        /// <summary>
        /// Configurates pen tool including size, shape, color, etc.
        /// </summary>
        /// <summary>
        /// Creates a new ClligraphicPen instance.
        /// </summary>
        public CalligraphicPen()
        {
        }

        
        protected override InkDrawingAttributes CreateInkDrawingAttributesCore(Brush brush, double strokeWidth)
        {
            /// <summary>
            /// Create and returns new ink attributes and sets defult shape,color and size.
            /// </summary>
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


}
