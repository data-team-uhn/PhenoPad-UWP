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

namespace PhenoPad
{
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

    /// <summary>
    /// This page shows the code to configure the InkToolbar.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        
        
        // private MainPage rootPage = MainPage.Current;
                
        
        Symbol LassoSelect = (Symbol)0xEF20;
        Symbol TouchWriting = (Symbol)0xED5F;

        
        
        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

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
        private SimpleOrientationSensor _simpleorientation;
        public InkCanvas inkCanvas = null;
        private NotePageControl curPage = null;
        private int curPageIndex = -1;
        public static MainPage Current;

        public MainPage()
        {
            Current = this;
            this.InitializeComponent();
            notePages = new List<NotePageControl>();
            NotePageControl aPage = new NotePageControl();
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = aPage;
            var screenSize = HelperFunctions.GetCurrentDisplaySize();
            aPage.Height = screenSize.Height;
            aPage.Width = screenSize.Width;
            curPageIndex = 0;
            PageHost.Content = curPage;
            setPageIndexText();
            
            isListening = false;
            dictatedTextBuilder = new StringBuilder();

            _simpleorientation = SimpleOrientationSensor.GetDefault();

            // Assign an event handler for the sensor orientation-changed event 
            if (_simpleorientation != null)
            {
                _simpleorientation.OrientationChanged += new TypedEventHandler<SimpleOrientationSensor, SimpleOrientationSensorOrientationChangedEventArgs>(OrientationChanged);
            }

            

            //scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ZoomFactorProperty, OnPropertyChanged);
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
                curPage.DrawBackgroundLines();
            });
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines
            curPage.DrawBackgroundLines();

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                micButton.IsEnabled = true;
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                this.cmdBarTextBlock.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                micButton.IsEnabled = false;
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
           

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
        }

        
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //HelperFunctions.UpdateCanvasSize(RootGrid, outputGrid, inkCanvas);
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
                inkCanvas.InkPresenter.InputDeviceTypes |= CoreInputDeviceTypes.Touch;
            }
            else
            {
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Touch;
            }
        }
       
        

       

        private void ToolButton_Lasso(object sender, RoutedEventArgs e)
        {
            // By default, pen barrel button or right mouse button is processed for inking
            // Set the configuration to instead allow processing these input on the UI thread
            /**
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;

            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            **/
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
                OverivePopUpPage.Height = Window.Current.Bounds.Height - cmdBar.ActualHeight;
                OverviewPopUp.IsOpen = true;

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
                {
                    var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                    if (titleBar != null)
                    {
                        titleBar.BackgroundColor = (Application.Current.Resources["AppBarBackground"] as SolidColorBrush).Color;
                        titleBar.ButtonBackgroundColor = (Application.Current.Resources["AppBarBackground"] as SolidColorBrush).Color;
                    }
                }
            }
            else
            {
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
                SpeechPopUpPage.Height = Window.Current.Bounds.Height - cmdBar.ActualHeight;
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
                micButton.IsEnabled = false;
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
                    SpeechManager.getSharedSpeechManager().AddNewMessage(args.Result.Text);
                    List<Phenotype> annoResults = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(cmdBarTextBlock.Text);
                    if (annoResults != null)
                    {
                        PhenotypeManager.getSharedPhenotypeManager().addPhenotypeInSpeech(annoResults);
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

        int testflag = 0;
        private void TestWS_Click(object sender, RoutedEventArgs e) {
            //SpeechStreamSocket sss = new SpeechStreamSocket();
            //sss.connect();
            if (testflag == 0)
            {
                testflag = 1;
                SpeechManager.getSharedSpeechManager().StartAudio();
            }
            else {
                testflag = 0;
                SpeechManager.getSharedSpeechManager().EndAudio();
            }
        }
        private async void MicButton_Click(object sender, RoutedEventArgs e)
        {
            micButton.IsEnabled = false;
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
                            // Show a UI link to the privacy settings.
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
            micButton.IsEnabled = true;
        }

        private void PageOverviewButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddPageButton_Click(object sender, RoutedEventArgs e)
        {
            NotePageControl aPage = new NotePageControl();
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            curPage = aPage;
            var screenSize = HelperFunctions.GetCurrentDisplaySize();
            aPage.Height = screenSize.Height;
            aPage.Width = screenSize.Width;
            curPageIndex = notePages.Count - 1;

            (PageHost.ContentTransitions.ElementAt(0) as ContentThemeTransition).HorizontalOffset = 500;
            PageHost.Content = curPage;

            setPageIndexText();
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
            pageIndexTextBlock.Text = "Page: " + (curPageIndex+1) + "/" + notePages.Count;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
        }

        private void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            CameraCanvas.Visibility = Visibility.Visible;
            captureControl.setUp();
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var imageSource = await captureControl.TakePhotoAsync();
            curPage.AddImageControl(imageSource);
        }

        private void CameraClose_Click(object sender, RoutedEventArgs e)
        {
            CameraCanvas.Visibility = Visibility.Collapsed;
            captureControl.unSetUp();
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
                    }
                    else
                    {
                        // File couldn't be saved.
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
        
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

}
