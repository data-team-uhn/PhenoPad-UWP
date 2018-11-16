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
    // This is a partial class of Mainpage that contains methods regarding to video/audio functions including specch engine.
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        #region properties

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
        } //automation properties

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
        } //automation properties

        public bool bluetoonOn;

        public string RPI_ADDRESS = "http://192.168.137.32:8000";
        public BluetoothService.BluetoothService bluetoothService = null;
        public UIWebSocketClient uiClinet = null;
        private int doctor = 0;
        bool speechEngineRunning = false;
        private DispatcherTimer audioTimer = new DispatcherTimer();
        

        public SpeechManager speechManager = SpeechManager.getSharedSpeechManager();
        /// <summary>
        /// The speech recognizer used throughout this sample.
        /// </summary>
        private SpeechRecognizer speechRecognizer;

        private PhenotypeManager PhenoMana = PhenotypeManager.getSharedPhenotypeManager();
        /// <summary>
        ///Keep track of existing text that we've accepted in ContinuousRecognitionSession_ResultGenerated(), so
        ///that we can combine it and Hypothesized results to show in-progress dictation mid-sentence.
        /// </summary>
        private StringBuilder dictatedTextBuilder;

        /// <summary>
        /// This HResult represents the scenario where a user is prompted to allow in-app speech, but 
        /// declines. This should only happen on a Phone device, where speech is enabled for the entire device,
        /// not per-app.
        /// </summary>
        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        /// <summary>
        /// Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        /// </summary>
        private bool isListening;

        #endregion
        //======================================== START OF METHODS =======================================/

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
                        //cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
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
                //cmdBarTextBlock.Text = hypothesis;
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
                    //cmdBarTextBlock.Text = args.Result.Text;
                    //SpeechManager.getSharedSpeechManager().AddNewMessage(args.Result.Text);
                    //List<Phenotype> annoResults = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync("");
                    //if (annoResults != null)
                    {
                        //PhenotypeManager.getSharedPhenotypeManager().addPhenotypeInSpeech(annoResults);

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

        /// <summary>
        /// Update text to display latest sentence
        /// TODO : Feels like there exists more legitimate wasy to do this
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SpeechManager_EngineHasResult(SpeechManager sender, SpeechEngineInterpreter args)
        {
            //this.cmdBarTextBlock.Text = args.latestSentence;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Switch speech engine state for blue tooth devices
        /// </summary>
        /// <param name="state">current state of speech engine</param>
        private async void changeSpeechEngineState_BT()
        {
            try
            {
                if (speechEngineRunning == false)
                {                 
                    await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("audio start");
                    SpeechManager.getSharedSpeechManager().ReceiveASRResults();                 
                }
                else
                {                    
                    await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("audio stop");
                    SpeechManager.getSharedSpeechManager().StopASRResults();
                }
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to start/stop bluetooth audio: " + e.Message);
            }

        }

        public void onAudioStarted() {
            speechEngineRunning = true;
            NotifyUser("Audio service started.", NotifyType.StatusMessage, 3);
            LogService.MetroLogger.getSharedLogger().Info("Audio started.");
            audioStatusText.Text = "ON";
            ReEnableAudioButton(null, null);
            audioButton.IsChecked = true;
        }

        public void onAudioEnded() {
            speechEngineRunning = false;
            NotifyUser("Audio service ended.", NotifyType.StatusMessage, 3);
            LogService.MetroLogger.getSharedLogger().Info("Audio stopped.");
            audioStatusText.Text = "OFF";
            ReEnableAudioButton(null, null);
            audioButton.IsChecked = false;
        }

        /// <summary>
        /// Switch speech engine state for plug-in devices
        /// </summary>
        /// <param name="state">current state of speech engine</param>
        private async void changeSpeechEngineState()
        {
            try
            {
                //if (speechEngineRunning == false)
                //    await SpeechManager.getSharedSpeechManager().StartAudio();
                //else
                //    await SpeechManager.getSharedSpeechManager().EndAudio(notebookId);

                #region demo
                //FOR DEMO PURPOSES, COMMENT OUT FOR REAL USAGE
                if (speechEngineRunning == false)
                    await SpeechManager.getSharedSpeechManager().StartAudioDemo();
                else
                    await SpeechManager.getSharedSpeechManager().EndAudioDemo(notebookId);
                #endregion
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to start/stop audio: " + ex.Message);
                ReEnableAudioButton(null, null);
            }

        }

        /// <summary>
        /// Sets the audio input status 
        /// </summary>
        /// <param name="item"></param>
        public void setStatus(string item)
        {
            if (item == "bluetooth")
            {
                this.BluetoothProgress.IsActive = this.bluetoonOn? false:true;
                this.BluetoothComplete.Visibility = this.bluetoonOn? Visibility.Visible:Visibility.Collapsed;
                this.bluetoothStatusText.Text = this.bluetoonOn ? "ON" : "OFF";
                if (bluetoonOn && this.bluetoothService.rpi_ipaddr != null) {
                    // show ip address of 
                    PiIPAddress.Text = BluetoothService.BluetoothService.getBluetoothService().GetPiIP();
                }
                    
            }
            /***
            else if (item == "diarization")
            {
                this.DiarizationProgress.IsActive = false;
                this.DiarizationComplete.Visibility = Visibility.Visible;
            }
            else if (item == "recognition")
            {
                this.RecognitionProgress.IsActive = false;
                this.RecognitionComplete.Visibility = Visibility.Visible;
            }
            **/
            else if (item == "ready")
            {
                this.audioButton.IsEnabled = true;
                //this.audioSwitch.IsEnabled = true;
            }
        }

        /// <summary>
        /// Initialize bluetooth stream input status
        /// </summary>
        /// <param name="val">indicates whether bluetooth is initialized</param>
        public void bluetoothInitialized(bool val)
        {
            this.StreamButton.IsEnabled = val;
            this.shutterButton.IsEnabled = val;
            this.audioButton.IsEnabled = val;
            if (val)
            {
                setStatus("bluetooth");
            }
        }

        /// <summary>
        /// Updates the bluetooth video camera stream status 
        /// </summary>
        private async Task videoStreamStatusUpdateAsync(bool desiredStatus)
        {
            if (this.bluetoothService == null)
            {
                NotifyUser("Could not reach Bluetooth device, try to connect again",
                                   NotifyType.ErrorMessage, 2);
                this.VideoOn = false;

                //this.bluetoothInitialized(false);
                this.StreamButton.IsChecked = false;
                return;
            }

            Debug.WriteLine("Sending message");

            if (desiredStatus)
            {
                await this.bluetoothService.sendBluetoothMessage("camera start");
            }
            else
            {
                await this.bluetoothService.sendBluetoothMessage("camera stop");
            }
            //this.videoSwitch.IsOn = desiredStatus;
            //this.StreamButton.IsChecked = desiredStatus;

            Debug.WriteLine("Setting status value to " + (this.bluetoothService.initialized && desiredStatus).ToString());
            this.VideoOn = this.bluetoothService.initialized && desiredStatus;
        }

        /// <summary>
        /// Handles chat view container change event and displays the message on chat view container 
        /// </summary>
        private void OnChatViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            TextMessage message = (TextMessage)args.Item;

            // Only display message on the right when speaker index = 0
            //args.ItemContainer.HorizontalAlignment = (message.Speaker == 0) ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;

            if (message.IsNotFinal)
            {
                args.ItemContainer.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
            }
            else
            {
                args.ItemContainer.HorizontalAlignment = (message.Speaker == doctor) ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;
            }

            /*if (message.Speaker != 99 && message.Speaker != -1 && message.Speaker > maxSpeaker)
            {
                Debug.WriteLine("Detected speaker " + message.Speaker.ToString());
                for (var i = maxSpeaker + 1; i <= message.Speaker; i++)
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["Background_" + i.ToString()];
                    item.Content = "Speaker " + (i + 1).ToString();
                    this.speakerBox.Items.Add(item);
                }
                maxSpeaker = (int)message.Speaker;
            }*/
        }

        private async void PreviewMultiMedia()
        {
            // initialize microphone choice
            if (ConfigService.ConfigService.getConfigService().IfUseExternalMicrophone())
                ExternalMicRadioBtn.IsChecked = true;
            else
                SurfaceMicRadioBtn.IsChecked = true;

            // steaming video
            string RPI_IP_ADDRESS = BluetoothService.BluetoothService.getBluetoothService().GetPiIP();
            RPI_ADDRESS = "http://" + RPI_IP_ADDRESS + ":8000";
            // this.StreamView.Navigate(new Uri(RPI_ADDRESS));

            this.videoStreamWebSocket = new Windows.Networking.Sockets.MessageWebSocket();
            // In this example, we send/receive a string, so we need to set the MessageType to Utf8.
            this.videoStreamWebSocket.Control.MessageType = Windows.Networking.Sockets.SocketMessageType.Utf8;
            this.videoStreamWebSocket.Closed += WebSocket_Closed;
            this.videoStreamWebSocket.MessageReceived += WebSocket_MessageReceived;

            try
            {
                videoStreamCancellationToken = videoCancellationSource.Token;
                Task connectTask = this.videoStreamWebSocket.ConnectAsync(new Uri("ws://" + RPI_IP_ADDRESS + ":8000/websocket")).AsTask();
                await connectTask.ContinueWith(_ => this.SendMessageUsingMessageWebSocketAsync("read_camera"));
                //Task.Run(() => this.WebSocket_MessageReceived());
                //Task.Run(() => this.SendMessageUsingStreamWebSocket(Encoding.UTF8.GetBytes("read_camera")));

            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add additional code here to handle exceptions.
            }
        }

        //private async void enableMic()
        //{
        //    //micButton.IsEnabled = false;
        //    if (isListening == false)
        //    {
        //        // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
        //        // This prevents an exception from occurring.
        //        if (speechRecognizer.State == SpeechRecognizerState.Idle)
        //        {
        //            try
        //            {
        //                isListening = true;
        //                await speechRecognizer.ContinuousRecognitionSession.StartAsync();
        //            }
        //            catch (Exception ex)
        //            {
        //                var messageDialog = new MessageDialog(ex.Message, "Exception");
        //                await messageDialog.ShowAsync();

        //                if ((uint)ex.HResult == HResultPrivacyStatementDeclined)
        //                {
        //                    //Show a UI link to the privacy settings.
        //                    //hlOpenPrivacySettings.Visibility = Visibility.Visible;
        //                }
        //                else
        //                {
        //                    // var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
        //                    // await messageDialog.ShowAsync();
        //                }

        //                isListening = false;

        //            }
        //        }
        //    }
        //    else
        //    {
        //        isListening = false;

        //        if (speechRecognizer.State != SpeechRecognizerState.Idle)
        //        {
        //            // Cancelling recognition prevents any currently recognized speech from
        //            // generating a ResultGenerated event. StopAsync() will allow the final session to 
        //            // complete.
        //            try
        //            {
        //                await speechRecognizer.ContinuousRecognitionSession.StopAsync();

        //                Console.WriteLine("Speech recognition stopped.");
        //                // Ensure we don't leave any hypothesis text behind
        //                //cmdBarTextBlock.Text = dictatedTextBuilder.ToString();
        //            }
        //            catch (Exception exception)
        //            {
        //                var messageDialog = new MessageDialog(exception.Message, "Exception");
        //                await messageDialog.ShowAsync();
        //            }
        //        }
        //    }
        //    //micButton.IsEnabled = true;
        //}

    }
}
