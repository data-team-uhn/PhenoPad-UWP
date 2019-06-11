using PhenoPad.LogService;
using PhenoPad.PhenotypeService;
using PhenoPad.SpeechService;
using PhenoPad.WebSocketService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.Networking.Sockets;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Streams;
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
        public string BLUETOOTHSERVICEURI = "";
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

        [XmlArray("Audios")]
        [XmlArrayItem("name")]
        public List<string> SavedAudios;


        public string RPI_ADDRESS = "http://192.168.137.32:8000";

        public BluetoothService.BluetoothService bluetoothService = null;
        public UIWebSocketClient uiClinet = null;
        private int doctor = 0;
        public bool speechEngineRunning = false;
        public DispatcherTimer audioTimer = new DispatcherTimer();
        public SemaphoreSlim InitBTConnectionSemaphore;
        public SpeechManager speechManager = SpeechManager.getSharedSpeechManager();
        /// The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;
        private PhenotypeManager PhenoMana = PhenotypeManager.getSharedPhenotypeManager();
        ///Keep track of existing text that we've accepted in ContinuousRecognitionSession_ResultGenerated(), so
        ///that we can combine it and Hypothesized results to show in-progress dictation mid-sentence.
        private StringBuilder dictatedTextBuilder;
        /// This HResult represents the scenario where a user is prompted to allow in-app speech, but 
        /// declines. This should only happen on a Phone device, where speech is enabled for the entire device,
        /// not per-app.
        private static uint HResultPrivacyStatementDeclined = 0x80045509;
        /// Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        private bool isListening;

        private bool isReading; //flag for reading audio stream
        private DispatcherTimer readTimer;
        private StreamWebSocket streamSocket;
        private CancellationTokenSource cancelSource;
        private CancellationToken token;
        private List<byte> audioBuffer;
        public static string SERVER_ADDR = "137.135.117.253";
        public static string SERVER_PORT = "8080";



        #endregion
        //======================================== START OF METHODS =======================================/

        private async Task videoStreamStatusUpdateAsync(bool desiredStatus)
        {
            /// <summary>
            /// Updates the bluetooth video camera stream status 
            /// </summary>

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

        private async void OnChatViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            /// <summary>
            /// Handles chat view container change event and displays the message on chat view container 
            /// </summary>

            if (args.InRecycleQueue) return;
            TextMessage message = (TextMessage)args.Item;

            // Only display message on the right when speaker index = 0
            //args.ItemContainer.HorizontalAlignment = (message.Speaker == 0) ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;

            if (message.IsNotFinal)
            {
                args.ItemContainer.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                args.ItemContainer.HorizontalAlignment = (message.Speaker == doctor) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                //Need this dispatcher in-order to avoid threading errors
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    chatView.UpdateLayout();
                    chatView.ScrollIntoView(chatView.Items[chatView.Items.Count - 1]);
                });
            }
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

        #region BLUETOOTH RELATED

        public async void RestartBTOnException()
        {
            //Handles DEXCEPTION received from raspberry pi, restarts bluetooth connection only
            //uiClinet.disconnect();
            //stops speech service before closing bluetooth
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {

                if (speechEngineRunning)
                    await SpeechManager.getSharedSpeechManager().StopASRResults();

                bool result = bluetoothService.CloseConnection();

                if (result)
                {
                    SetBTUIOnInit(bluetoothService.initialized);
                    Debug.WriteLine("Bluetooth Connection disconnected");
                }
                else
                {
                    LogService.MetroLogger.getSharedLogger().Error("Bluetooth Connection failed to disconnect.");
                }

            });

            await Task.Delay(TimeSpan.FromSeconds(5));
            //var success = await SetBluetoothON();
            var success = await changeSpeechEngineState_BT();
            if (success)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    var result = await StartAudioAfterBluetooth();
                });
            }

            if (!success) {
                LogService.MetroLogger.getSharedLogger().Error("Bluetooth Connection failed to reconnect.");
            }


        }

        private async void BTConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                result = await changeSpeechEngineState_BT();
            });
        }

        public async Task<bool> changeSpeechEngineState_BT()
        {
            /// <summary>
            /// Switch speech engine state for blue tooth devices
            /// </summary>
            bool result = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                if (!bluetoonOn)
                {
                    try
                    {
                        //temporarily disables for debugging reseasons
                        //=====
                        //uiClinet = UIWebSocketClient.getSharedUIWebSocketClient();
                        //bool uiResult = await uiClinet.ConnectToServer();
                        //if (!uiResult)
                        //    LogService.MetroLogger.getSharedLogger().Error("UIClient failed to connect.");
                        //=====
                        bluetoothService = BluetoothService.BluetoothService.getBluetoothService();
                        await bluetoothService.Initialize();
                        SetBTUIOnInit(bluetoothService.initialized);
                        result = bluetoothService.initialized;
                    }
                    catch (Exception e)
                    {
                        LogService.MetroLogger.getSharedLogger().Error($"Failed to turn on BT: {e.Message}");
                        result = false;
                    }
                }
                else
                {
                    try
                    {
                        //uiClinet.disconnect();
                        //stops speech service before closing bluetooth
                        if (speechEngineRunning)
                            await SpeechManager.getSharedSpeechManager().StopASRResults();
                        Debug.WriteLine("stopped ASR RESULT \n");
                        result = bluetoothService.CloseConnection();
                        if (result)
                        {
                            SetBTUIOnInit(bluetoothService.initialized);
                            Debug.WriteLine("Bluetooth Connection disconnected");
                            result = true;
                        }

                    }
                    catch (Exception e)
                    {
                        LogService.MetroLogger.getSharedLogger().Error($"Failed to turn off BT: {e.Message}");
                    }
                    result = false;

                }

            });
            return result;
        }

        public async Task<bool> StartAudioAfterBluetooth() {
            var success = false;
            DisableAudioButton();

            if (speechEngineRunning == false)
            {
                string uri = ASRAddrInput.Text.Trim();
                speechManager.CreateNewAudioName();
                string audioName = speechManager.GetAudioNameForServer();
                Debug.WriteLine($"start BT Audio, addr = {uri}");
                success = await bluetoothService.sendBluetoothMessage($"audio start manager_id=666 server_uri={uri} audiofile_name={audioName}");
                if (!success)
                {
                    LogService.MetroLogger.getSharedLogger().Error("failed to send audio start message to raspi");
                    return false;
                }
                success &= await SpeechManager.getSharedSpeechManager().ReceiveASRResults();
            }
            return success;
         }

        public void SetBTUIOnInit(bool initialized)
        {
            /// Initialize bluetooth stream input status

            StreamButton.IsEnabled = initialized;
            shutterButton.IsEnabled = initialized;
            audioButton.IsEnabled = initialized;
            bluetoothicon.Visibility = initialized ? Visibility.Visible : Visibility.Collapsed;
            bluetoonOn = initialized;
        }

        #endregion

        #region Speech Recognizer
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            /// <summary>
            /// Initialize Speech Recognizer and compile constraints.
            /// </summary>
            /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
            /// <returns>Awaitable task.</returns>

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

        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            /// <summary>
            /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
            /// some transient issues occur.
            /// </summary>
            /// <param name="sender">The continuous recognition session</param>
            /// <param name="args">The state of the recognizer</param>

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

        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            /// <summary>
            /// While the user is speaking, update the textbox with the partial sentence of what's being said for user feedback.
            /// </summary>
            /// <param name="sender">The recognizer that has generated the hypothesis</param>
            /// <param name="args">The hypothesis formed</param>

            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = dictatedTextBuilder.ToString() + " " + hypothesis + " ...";
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //cmdBarTextBlock.Text = textboxContent;
                //cmdBarTextBlock.Text = hypothesis;
            });

        }

        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            /// <summary>
            /// Handle events fired when a result is generated. Check for high to medium confidence, and then append the
            /// string to the end of the stringbuffer, and replace the content of the textbox with the string buffer, to
            /// remove any hypothesis text that may be present.
            /// </summary>
            /// <param name="sender">The Recognition session that generated this result</param>
            /// <param name="args">Details about the recognized speech</param>

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

        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            /// <summary>
            /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
            /// </summary>
            /// <param name="sender">The recognizer that is currently running.</param>
            /// <param name="args">The current state of the recognizer.</param>

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                //this.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
                Console.WriteLine(args.State.ToString());
            });
        }

        private void SpeechManager_EngineHasResult(SpeechManager sender, SpeechEngineInterpreter args)
        {
            /// <summary>
            /// Update text to display latest sentence
            /// TODO : Feels like there exists more legitimate wasy to do this
            /// </summary>

            //this.cmdBarTextBlock.Text = args.latestSentence;
            throw new NotImplementedException();
        }

        #endregion

        private async void AudioStreamButton_Clicked(object sender = null, RoutedEventArgs e = null)
        {
            /// <summary>
            /// Invoked when user presses the Microphone button on sidebar, requests speech engine connection
            /// as well as connection to remote server for speech recognition
            /// </summary>

            //Temporarily disables audio button for avoiding frequent requests
            DisableAudioButton();

            // using external microphone
            if (ConfigService.ConfigService.getConfigService().IfUseExternalMicrophone())
            {
                if (!bluetoonOn)
                {
                    NotifyUser("Bluetooth is not connected, cannot use External Microphone.", NotifyType.ErrorMessage, 2);
                    ReEnableAudioButton();
                }
                else
                {
                    //ASR is already on, turning off ASR
                    if (speechEngineRunning)
                    {
                        var success = await SpeechManager.getSharedSpeechManager().StopASRResults();
                    }
                    //Bluetooth is connected, just start ASR
                    else if (!speechEngineRunning)
                    {
                        var success = await StartAudioAfterBluetooth();
                    }

                }
            }
            // using internal microphone
            else
            {
                var success = await changeSpeechEngineState();
                ReEnableAudioButton();
            }
        }

        public async Task<bool> changeSpeechEngineState()
        {
            /// Switch speech engine state for plug-in devices
            try
            {
                var success = true;
                if (speechEngineRunning == false)
                {
                    NotifyUser("Starting Audio using internal microphone ...", NotifyType.StatusMessage, 7);
                    success = await SpeechManager.getSharedSpeechManager().StartAudio();
                }
                else
                {
                    NotifyUser("Ending Audio ...", NotifyType.StatusMessage, 2);
                    success = await SpeechManager.getSharedSpeechManager().EndAudio(notebookId);
                }
                //#region demo
                ////FOR DEMO PURPOSES, COMMENT OUT FOR REAL USAGE
                //if (speechEngineRunning == false)
                //    await SpeechManager.getSharedSpeechManager().StartAudioDemo();
                //else
                //    await SpeechManager.getSharedSpeechManager().EndAudioDemo(notebookId);
                //#endregion
                return success;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to start/stop audio: " + ex.Message);
                return false;
            }

        }


        public void onAudioStarted(object sender, object e) {
            audioTimer.Stop();
            speechEngineRunning = true;
            NotifyUser("Audio service started.", NotifyType.StatusMessage, 1);
            LogService.MetroLogger.getSharedLogger().Info("Audio started.");
            ReEnableAudioButton();
        }

        public void onAudioEnded() {
            speechEngineRunning = false;
            NotifyUser("Audio service ended.", NotifyType.StatusMessage, 1);
            LogService.MetroLogger.getSharedLogger().Info("Audio stopped.");
            ReEnableAudioButton();
        }

        public void DisableAudioButton() {
            audioButton.IsEnabled = false;
            audioStatusText.Text = "...";
        }

        public async void ReEnableAudioButton(object sender = null, object e = null)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                await Task.Delay(TimeSpan.FromSeconds(7));
                audioButton.IsEnabled = true;
                if (speechEngineRunning)
                {
                    audioStatusText.Text = "ON";
                    audioButton.IsChecked = true;
                }
                else
                {
                    audioStatusText.Text = "OFF";
                    audioButton.IsChecked = false;
                }
            });
        }

        public async Task KillAudioService() {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () => {
                if (speechEngineRunning)
                {//close all audio services before navigating
                    Debug.WriteLine("Killing audio service");
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        AudioStreamButton_Clicked();
                    });
                    await Task.Delay(TimeSpan.FromSeconds(7));
                }
            });
            return;
        }

        public async void SaveNewAudioName(string name) {
            this.SavedAudios.Add(name);
            await FileService.FileManager.getSharedFileManager().SaveAudioNamesToXML(notebookId, SavedAudios);
            speechManager.ClearCurAudioName();
        }

        public async void PlayMedia(int index,double start, double end)
        {
            StreamWebSocket streamSocket = new StreamWebSocket();
            try
            {
                string audioName = $"666_{notebookId}_{SavedAudios[index]} {start} {end}";
                Uri serverUri = new Uri("ws://" + SERVER_ADDR + ":" + SERVER_PORT + "/client/ws/file_request" +
                                           "?content-type=audio%2Fx-raw%2C+layout%3D%28string%29interleaved%2C+rate%3D%28int%2916000%2C+format%3D%28string%29S16LE%2C+channels%3D%28int%291&manager_id=666");

                Task connectTask = streamSocket.ConnectAsync(serverUri).AsTask();
                await connectTask;
                if (connectTask.Exception != null)
                    MetroLogger.getSharedLogger().Error("connectTask.Exception:" + connectTask.Exception.Message);
                Debug.WriteLine("connected, will begin receiving data");
                //StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                //StorageFile storageFile = await storageFolder.CreateFileAsync(
                //  "audio.wav", CreationCollisionOption.GenerateUniqueName);
                //=============================
                uint length = 1000000;     // Leave a large buffer
                audioBuffer = new List<Byte>();
                isReading = true;
                cancelSource = new CancellationTokenSource();
                token = cancelSource.Token;
                while (isReading)
                {
                    //readTimer.Start();
                    IBuffer op = await streamSocket.InputStream.ReadAsync(new Windows.Storage.Streams.Buffer(length), length, InputStreamOptions.Partial).AsTask(token);
                    if (op.Length > 0)
                        audioBuffer.AddRange(op.ToArray());
                    Debug.WriteLine("------------------" + audioBuffer.Count + "----------------");
                    //readTimer.Stop();
                }
            }
            catch (TaskCanceledException)
            {
                //Plays the audio received from server
                //readTimer.Stop();
                Debug.WriteLine("done receiving +++++++++++++++++++++++++");
                MemoryStream mem = new MemoryStream(audioBuffer.ToArray());
                MediaPlayer player = new MediaPlayer();
                player.SetStreamSource(mem.AsRandomAccessStream());
                player.Play();
                Debug.WriteLine("done");
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("file result:" + ex + ex.Message);
                streamSocket.Dispose();
                streamSocket = null;
            }


        }
        private void EndAudioStream(object sender, object e)
        {
            isReading = false;
            cancelSource.Cancel();
            Debug.WriteLine("Timer tick, will stop reading");
        }



    }
}
