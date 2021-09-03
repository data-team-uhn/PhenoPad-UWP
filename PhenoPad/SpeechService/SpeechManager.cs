using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Render;
using System.Runtime.InteropServices;
using Windows.Media.MediaProperties;
using System.Threading;
using PhenoPad.WebSocketService;
using System.Diagnostics;
using Windows.Networking.Sockets;
using System.IO;
using Windows.Web;
using Windows.Storage.Pickers;
using Windows.Storage;
// To parse JSON we received from speech engine server
using Newtonsoft.Json;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.Storage.Streams;
using PhenoPad.LogService;
using PhenoPad.PhenotypeService;

namespace PhenoPad.SpeechService
{
    // We are initializing a COM interface for use within the namespace
    // This interface allows access to memory at the byte level which we need to populate audio data that is generated
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public class SpeechManager
    {
        private string serverAddress = "SpeechServerAddress"; // NOTE: Replace with your own speech server's ip address
        private string serverPort = "";                       // NOTE: Replace with port #
        public static string DEFAULT_SERVER = "DefaultSpeechServerAddress"; // NOTE: Replace
        public static string DEFAULT_PORT = "";                             // NOTE: Replace
        public static string RESTART_AUIDO_SERVER = "TIMEOUTEXIT";
        public static bool NEED_RESTART_AUDIO = false;
        public static SpeechManager sharedSpeechManager;
        private static string currentAudioName;        
        public Conversation conversation = new Conversation();
        public Conversation realtimeConversation = new Conversation();
        public SpeechEngineInterpreter speechInterpreter;
        private MainPage rootPage = MainPage.Current;
        private AudioGraph graph;
        private AudioFrameOutputNode frameOutputNode;
        private AudioDeviceInputNode deviceInputNode;
        private AudioFileInputNode fileInputNode;
        public double theta = 0;
        public SpeechStreamSocket speechStreamSocket;
        public SpeechResultsSocket speechResultsSocket;
        public SpeechRESTAPI speechAPI;
        private AudioFileOutputNode fileOutputNode;

        public event TypedEventHandler<SpeechManager, SpeechEngineInterpreter> EngineHasResult;
        public event TypedEventHandler<SpeechManager, StorageFile> RecordingCreated;
        public StorageFile savedFile;

        private bool useFile = false;
        private CancellationTokenSource cancellationSource;

        public SpeechManager()
        {
            object val = AppConfigurations.readSetting("serverIP");
            if (val != null && ((string)val).Length != 0)
            {
                this.serverAddress = (string)val;
            }

            object val2 = AppConfigurations.readSetting("serverPort");
            if (val2 != null && ((string)val).Length != 0)
            {
                this.serverPort = (string)val2;
            }
            
            this.speechInterpreter = new SpeechEngineInterpreter(this.conversation, this.realtimeConversation);
            this.speechStreamSocket = new SpeechStreamSocket(this.serverAddress, this.serverPort);
            this.speechAPI = new SpeechRESTAPI();          
        }

        private Dictionary<string, int> text_to_message = new Dictionary<string, int>();
        private List<MedicalTerm> meidcalTermsList = new List<MedicalTerm>();
        public async void LoadConversation()
        {
            var messageList = new List<TextMessage>();
            string path = @"Assets\transcripts_w_medical_ctakes\2019_01_30_0.json";
            //string path = @"Assets\transcripts_w_medical\2020_03_04_4.json";
            //string path = @"Assets\transcripts_processed\2019_12_05_1.txt";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = await InstallationFolder.GetFileAsync(path);
            string text = await FileIO.ReadTextAsync(file);

            TranscriptRaw raw_transcript = JsonConvert.DeserializeObject<TranscriptRaw>(text);

            List<SpeechTranscriptRaw> speech_lines = raw_transcript.transcripts;
            Dictionary<string, MedicalConceptRaw> raw_med_terms = raw_transcript.concepts;

            // Create Text Messages with speech lines first
            int m_ind = -1;
            foreach (var line in speech_lines)
            {
                m_ind++;
                var m = new TextMessage()
                {
                    Body = line.text,
                    Speaker = UInt16.Parse(line.speaker),
                    IsFinal = true,
                    ConversationIndex = 0,
                    phenotypesInText = new List<PhenotypeService.Phenotype>()
                };
                messageList.Add(m);
            }

            // Add PhenoTypes
            foreach (KeyValuePair<string, MedicalConceptRaw> entry in raw_med_terms)
            {
                var item_name = entry.Key;
                var item_values = entry.Value;
                var item_text = item_values.text;
                var item_type = item_values.type;
                var message_ids = item_values.line_ids;

                if (item_name.Length > 5)
                {
                    var medical_term = new MedicalTerm()
                    {
                        Id = "",
                        Name = item_name,
                        Type = item_type,
                        Text = item_text,
                        MessageIndexList = message_ids
                    };

                    PhenotypeManager.getSharedPhenotypeManager().AddMedicalTerm(medical_term);
                }
            }

           
            conversation.ClearThenAddRange(messageList);
        }
        //public async void LoadConversation()
        //{
        //    var messageList = new List<TextMessage>();
        //    string path = @"Assets\transcripts_w_medical\2020_03_04_4.json";
        //    //string path = @"Assets\transcripts_processed\2019_12_05_1.txt";
        //    StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
        //    StorageFile file = await InstallationFolder.GetFileAsync(path);
        //    string text = await FileIO.ReadTextAsync(file);
        //    List<MedicalTermRaw> raw_terms = JsonConvert.DeserializeObject<List<MedicalTermRaw>>(text);
        //    int m_ind = -1;
        //    foreach (var rt in raw_terms)
        //    {
        //        m_ind++;
        //        var m = new TextMessage()
        //        {
        //            Body = rt.text,
        //            Speaker = UInt16.Parse(rt.speaker),
        //            IsFinal = true,
        //            ConversationIndex = 0,
        //            phenotypesInText = new List<PhenotypeService.Phenotype>()
        //        };
        //        messageList.Add(m);

        //        if (rt.parse_result.Count > 0)
        //        {
        //            foreach (var item in rt.parse_result)
        //            {
        //                var item_text = item.Key;
        //                var item_dict = item.Value;
        //                var item_id = item_dict["ids"][0];
        //                var item_name = item_dict["names"][0];
        //                var item_type = item_dict["types"][0];

        //                if (item_name.Length > 5)
        //                {
        //                    var medical_term = new MedicalTerm()
        //                    {
        //                        Id = item_id,
        //                        Name = item_name,
        //                        Type = item_type,
        //                        Text = item_text,
        //                        MessageIndex = m_ind
        //                    };

        //                    PhenotypeManager.getSharedPhenotypeManager().AddMedicalTerm(medical_term);
        //                }
        //            }
        //        }

        //    }

        //    conversation.ClearThenAddRange(messageList);
        //}



        /// <summary>
        /// Returns a shared speech manager object.
        /// </summary>
        /// <returns>the shared speech manager object</returns>
        /// <remarks>
        /// If an instance of the speech manager class has not been created yet,
        /// initialize a new one.
        /// </remarks>
        public static SpeechManager getSharedSpeechManager()
        {
            if (sharedSpeechManager == null)
            {
                sharedSpeechManager = new SpeechManager();
                sharedSpeechManager.LoadConversation();
                return sharedSpeechManager;
            }
            else
            {
                return sharedSpeechManager;
            }
        }

        #region Set/Get
        public void setServerAddress(string ads)
        {
            this.serverAddress = ads;
        }
        public void setServerPort(string pt)
        {
            this.serverPort = pt;
        }
        public string getServerAddress()
        {
            return this.serverAddress;
        }
        public string getServerPort()
        {
            return this.serverPort;
        }
        public void setAudioIndex(int count)
        {
            this.speechInterpreter.conversationIndex = count;
        }
        public int getAudioCount()
        {
            return this.speechInterpreter.conversationIndex;
        }
        /// <summary>
        /// Returns the audio name of the current conversation.
        /// </summary>
        public string GetAudioName()
        {
            return currentAudioName;
        }
        public void ClearCurAudioName() {
            currentAudioName = "";
        }
        public void cleanUp()
        {
            this.conversation.Clear();
            this.realtimeConversation.Clear();
            this.speechInterpreter = new SpeechEngineInterpreter(this.conversation, this.realtimeConversation);
            currentAudioName = "";
        }
        #endregion


        #region AUDIO START / STOP FOR USING EXTERNAL MICROPHONE

        /// <summary>
        /// Manages the speech service when using external microphone.
        /// </summary>
        /// <returns>(bool)false if connection to server fails/cancelled, (bool)true otherwise</returns>
        /// <remarks>
        /// Called when starting audio in "external microphone" mode. Connects to speech server,  
        /// sets up new conversation, receives speech server results, update speech bubbles with new results, 
        /// and handles result receiving exceptions.
        /// </remarks>
        public async Task<bool> ReceiveASRResults()
        {
            // Connect to speech server
            MainPage.Current.NotifyUser("Connecting to speech result server...", NotifyType.StatusMessage, 1);
            Debug.WriteLine($"Connecting to speech result server...");
            bool succeed = false;
            try
            {
                speechResultsSocket = new SpeechResultsSocket(this.serverAddress, this.serverPort);
                succeed = await speechResultsSocket.ConnectToServer();

                while (!succeed)
                {
                    // Display a dialog box to allow for retry
                    var messageDialog = new MessageDialog($"Failed to connect to ASR {serverAddress}:{serverPort}.\n Would you like to retry connection?");
                    messageDialog.Title = "CONNECTION ERROR";
                    messageDialog.Commands.Add(new UICommand("YES") { Id = 0 });
                    messageDialog.Commands.Add(new UICommand("NO") { Id = 1 });
                    //
                    messageDialog.DefaultCommandIndex = 0;
                    messageDialog.CancelCommandIndex = 1;
                    //
                    var dialogResult = await messageDialog.ShowAsync();
                    if ((int)(dialogResult.Id) == 1)
                    {
                        MainPage.Current.NotifyUser("Connection cancelled", NotifyType.ErrorMessage, 2);
                        MainPage.Current.ReEnableAudioButton();
                        return false;
                    }
                    else if ((int)(dialogResult.Id) == 0)
                        succeed = await speechResultsSocket.ConnectToServer();
                }
            }
            catch (Exception e)
            {
                // this is to handle some url problems
                MetroLogger.getSharedLogger().Error("Failed to connect to speech result socket:" + e.Message);
            }

            // Upate UI elements
            MainPage.Current.onAudioStarted(null, null);
            SpeechPage.Current.setSpeakerButtonEnabled(true);
            SpeechPage.Current.adjustSpeakerCount(2);

            // Set up variables for a new ASR session
            cancellationSource = new CancellationTokenSource(); // need this to actually cancel reading from websocketS
            CancellationToken cancellationToken = cancellationSource.Token;
            //
            this.speechInterpreter.newConversation();
            OperationLogger.getOpLogger().Log(OperationType.ASR, "Started");
            //
            CreateNewAudioName();

            // Message Receiving/Processing Loop
            await Task.Run(async () =>
            {
                string accumulator = String.Empty; // Weird issue but seems to be some buffer issue //TODO (haochi): what issue? investigate

                while (true && !cancellationToken.IsCancellationRequested)
                {
                    // Receive ASR results
                    await Task.Delay(100);
                    string serverResult = await speechResultsSocket.SpeechResultsSocket_ReceiveMessage();
                    if (serverResult == "CONNECTION_ERROR")
                    {
                        MainPage.Current.NotifyUser("Speech Result Server error", NotifyType.ErrorMessage, 2);
                    }
                    else if (serverResult.Trim('"').Equals(RESTART_AUIDO_SERVER)) // ASR Server TIMEOUT Exception
                    {
                        MainPage.Current.NotifyUser("Restarting audio service after error", NotifyType.ErrorMessage, 2);
                        Debug.WriteLine($"FROM SERVER=>{serverResult}");
                        NEED_RESTART_AUDIO = true;
                        break;
                    }

                    // Process results(JSON)
                    serverResult = serverResult.Replace('-', '_');  // Replace hyphen with underscore in the ASR result so that we can parse objects, hyphens will cause errors
                    accumulator += serverResult;
                    //
                    bool doParsing = true;
                    while (doParsing && !cancellationToken.IsCancellationRequested) // This while loop is added to make sure we get all the packages.
                    {
                        string outAccumulator = String.Empty;
                        string json = SpeechEngineInterpreter.getFirstJSON(accumulator, out outAccumulator);
                        accumulator = outAccumulator;

                        if (json.Length != 0)
                        {
                            // Updates mainpage speech panel with new results
                            try
                            {                                
                                var parsedSpeech = JsonConvert.DeserializeObject<SpeechEngineJSON>(json);
                                parsedSpeech.original = json;

                                speechInterpreter.processJSON(parsedSpeech);

                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                                   () =>
                                   {
                                       EngineHasResult.Invoke(this, speechInterpreter);
                                   });

                                continue;
                            }
                            // Report error if failed to process JSON.
                            catch (Exception e)
                            {
                                StackTrace st = new StackTrace(e, true);
                                StackFrame frame = st.GetFrames()[0];
                                Debug.WriteLine(e.ToString());
                                Debug.WriteLine(frame.ToString());
                                Debug.WriteLine(frame.GetFileName().ToString());
                                Debug.WriteLine(frame.GetFileLineNumber().ToString());
                                Debug.WriteLine("===SERIOUS PROBLEM!====");
                            }

                            // Process separate dirization results (if received)
                            try
                            {
                                json = json.Replace('_', '-');
                                var diaResult = JsonConvert.DeserializeObject<DiarizationJSON>(json);
                                speechInterpreter.processDiaJson(diaResult);
                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                  () =>
                                  {
                                      EngineHasResult.Invoke(this, speechInterpreter);
                                  });
                            }
                            catch (Exception)
                            {
                                Debug.WriteLine("This is not diarization result.");
                            }
                        }
                        else
                        {
                            doParsing = false;
                        }
                    }
                }
            }, cancellationToken);

            // On Message Receiving Loop Stops
            if (cancellationToken.IsCancellationRequested)
            {
                MetroLogger.getSharedLogger().Info("ASR connection requested cancellation from ReceiveASRResults");
                NEED_RESTART_AUDIO = false;
            }
            else if (NEED_RESTART_AUDIO)
            {
                MetroLogger.getSharedLogger().Error("Server requested AUDIO restart ... will be restarting");
                NEED_RESTART_AUDIO = false;

                BluetoothService.BluetoothService.getBluetoothService().HandleAudioException(BluetoothService.BluetoothService.RESTART_AUDIO_FLAG);
            }
            else // probably some error happened that disconnectes the ASR, stops ASR before processing
            {
                MetroLogger.getSharedLogger().Error("ASR encountered some problem, will call StopASRRsults ...");
                // stop speech session without reloading SpeechPage content
                await StopASRResults(false);
            }
            return true;
        }

        /// <summary>
        /// Stops the ASR session when using "external microphone" mode. Saves the audio and
        /// ASR transcripts to disk.
        /// </summary>
        /// <param name="reloadSpeechPage">when "reload" is true, load the saved transcripts and update the speech bubbles on SpeechPage</param>
        /// <returns>true if successfully stopped</returns>
        public async Task<bool> StopASRResults(bool reloadSpeechPage = true)
        {
            try
            {
                // Save data to disk
                cancellationSource.Cancel();
                await SaveTranscriptions(reloadSpeechPage); //TODO: this function has a part that updates speech page after saving data, consider moving it to a separate function and move the call to the last block
                MainPage.Current.SaveNewAudioName(currentAudioName);

                //Message server
                await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("audio stop");
                await speechResultsSocket.CloseConnnction();

                // Update UI and clear variables
                SpeechPage.Current.setSpeakerButtonEnabled(false);
                OperationLogger.getOpLogger().Log(OperationType.ASR, "Ended");
                speechInterpreter = new SpeechEngineInterpreter(this.conversation, this.realtimeConversation);
                MainPage.Current.onAudioEnded();

                return true;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed stopping bluetooth ASR:"+ e.Message);
            }
            return false;
        }
        #endregion

        #region AUDIO START / STOP FOR USING INTERNAL MICROPHONE

        /// <summary>
        /// Manages the speech service when using internal microphone.
        /// </summary>
        /// <remarks>
        /// Called when starting audio in "internal microphone" mode. Connects to speech server, creates and 
        /// starts audio graph sets up new conversation, receives speech server results, update speech bubbles 
        /// with new results, and handles result receiving exceptions.
        /// </remarks>
        public async Task<bool> StartAudio()
        {
            bool attemptConnection = true;
            bool connectToServerSucceed = false;
            int count = 1;

            // Connect to speech server
            while (attemptConnection)
            {
                try
                {
                    speechStreamSocket = new SpeechStreamSocket(this.serverAddress, this.serverPort);
                    CreateNewAudioName();
                    speechAPI.setupClient(this.serverAddress);
                    connectToServerSucceed = await speechStreamSocket.ConnectToServer();
                }
                catch (Exception e)
                {
                    MetroLogger.getSharedLogger().Error(e.Message);
                }

                if (!connectToServerSucceed)
                {
                    // Display a dialog box to allow for retry
                    var messageDialog = new MessageDialog("Failed to connect to (" 
                        + this.serverAddress + ":" + this.serverPort  + ").\nWould you like to retry this connection?");
                    messageDialog.Title = "CONNECTION ERROR";
                    messageDialog.Commands.Add(new UICommand("YES") { Id = 0 });
                    messageDialog.Commands.Add(new UICommand("NO") { Id = 1 });
                    messageDialog.DefaultCommandIndex = 0;
                    messageDialog.CancelCommandIndex = 1;
                    //
                    var result = await messageDialog.ShowAsync();
                    if ((int)result.Id == 0)
                    {
                        count++;
                        MainPage.Current.NotifyUser($"Retry connection at attempt #{count.ToString()} ... " , NotifyType.StatusMessage, 2);
                        attemptConnection = true;
                    }
                    else
                    {
                        MainPage.Current.NotifyUser("Connection cancelled", NotifyType.ErrorMessage, 2);
                        attemptConnection = false;
                        MainPage.Current.ReEnableAudioButton();
                        return false;
                    }
                }
                else
                {
                    attemptConnection = false;
                }
            }

            // Make UI changes and initialize then start audio graph
            await CreateAudioGraph();
            //
            // Wait 10 seconds so that the server has time to create a worker
            // else you'll see lots of audio delays.
            await Task.Delay(5000);
            //
            SpeechPage.Current.setSpeakerButtonEnabled(true);           
            SpeechPage.Current.adjustSpeakerCount(2);
            MainPage.Current.onAudioStarted(null, null);
            OperationLogger.getOpLogger().Log(OperationType.ASR, "Started");
            //
            startGraph();
            //
            if (useFile)
            {
                fileInputNode.Start();
            }
            else
            {
                deviceInputNode.Start();
            }

            // Set up variables to prep for new ASR session
            this.byte_accumulator = new List<byte>();   // save this to file
            //
            cancellationSource = new CancellationTokenSource(); // need this to actually cancel reading from websocketS
            CancellationToken cancellationToken = cancellationSource.Token;
            //
            this.speechInterpreter.newConversation();

            // Message Receiving/Processing Loop
            await Task.Run(async () =>
             {
                 string accumulator = String.Empty;

                 while (true && !cancellationToken.IsCancellationRequested)
                 {
                     // Receive results from Server
                     await Task.Delay(500);
                     //
                     string serverResult = await speechStreamSocket.SpeechStreamSocket_ReceiveMessage();
                     //
                     if (serverResult.Trim('"').Equals(RESTART_AUIDO_SERVER))
                     {
                         Debug.WriteLine($"\nFROM SERVER=>{serverResult}\n");
                         NEED_RESTART_AUDIO = true;
                         break;
                     }

                     // Process results
                     serverResult = serverResult.Replace('-', '_');     // If we don't do this that we'll have errors when parsing objects.
                     accumulator += serverResult;
                     //
                     bool doParsing = true; // Seems like if we don't do this we won't get all the packages]
                     while (doParsing && !cancellationToken.IsCancellationRequested)
                     {
                         string outAccumulator = String.Empty;
                         string json = SpeechEngineInterpreter.getFirstJSON(accumulator, out outAccumulator);
                         accumulator = outAccumulator;

                         if (json.Length != 0)
                         {
                             // Update mainpage speech panel with new results
                             try
                             {
                                 var parsedSpeech = JsonConvert.DeserializeObject<SpeechEngineJSON>(json);
                                 parsedSpeech.original = json;

                                 speechInterpreter.processJSON(parsedSpeech);

                                 await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,() =>
                                    {
                                        EngineHasResult.Invoke(this, speechInterpreter);
                                    }
                                    );

                                 continue;
                             }
                             catch (Exception e)
                             {
                                 StackTrace st = new StackTrace(e, true);
                                 StackFrame frame = st.GetFrames()[0];
                                 Debug.WriteLine(e.ToString());
                                 Debug.WriteLine(frame.ToString());
                                 Debug.WriteLine(frame.GetFileName().ToString());
                                 Debug.WriteLine(frame.GetFileLineNumber().ToString());
                                 //Debug.WriteLine(accumulator);
                                 Debug.WriteLine("===SERIOUS PROBLEM!====");
                             }
                              
                             // to decode server message as dirization result
                             try
                             {
                                 json = json.Replace('_', '-');
                                 var diaResult = JsonConvert.DeserializeObject<DiarizationJSON>(json);
                                 speechInterpreter.processDiaJson(diaResult);
                                 await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,() => {
                                       EngineHasResult.Invoke(this, speechInterpreter);
                                   });
                             }
                             catch (Exception)
                             {
                                 Debug.WriteLine("This is not diarization result.");
                             }
                         }
                         else
                         {
                             doParsing = false; // If didn't receive a valid JSON, wait for more packages.
                         }
                     }                    
                 }
             }, cancellationToken);

            // On Message Receiving Loop Stops
            if (cancellationToken.IsCancellationRequested)
            {
                MetroLogger.getSharedLogger().Info("ASR connection requested cancellation");
                return true;
            }
            else if (NEED_RESTART_AUDIO)
            {
                MetroLogger.getSharedLogger().Error("Server requested AUDIO restart ... will be restarting");
                NEED_RESTART_AUDIO = false;
                await MainPage.Current.KillAudioService();
                return false;
            }
            else //probably some error happened that disconnectes the ASR, stops ASR before processing
            {
                MetroLogger.getSharedLogger().Error("ASR encountered some problem, will call StopASRRsults ...");
                await EndAudio(MainPage.Current.notebookId);
                return false;
            }
        }

        private SemaphoreSlim endSemaphoreSlim = new SemaphoreSlim(1);
        /// <summary>
        /// Stops the ASR session when using "internal microphone" mode. Saves the audio and
        /// ASR transcripts to disk.
        /// </summary>
        /// <returns>true if successful, false otherwise</returns>
        public async Task<bool> EndAudio(string notebookid)
        {
            bool result = true;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>{
                try
                {
                    if (graph != null && (fileInputNode != null || useFile == false))   // if recorded audio using microphone
                    {
                        // Save data to disk
                        if (notebookid != "")
                        {
                            this.writeAudioToFile(notebookid);
                            await this.SaveTranscriptions(reloadSpeechPage: true);
                        }

                        // Stop audio service
                        cancellationSource.Cancel();
                        if (useFile)
                        { 
                            fileInputNode.Stop();
                        }
                        graph.Stop();
                        graph.Dispose();
                        await speechStreamSocket.CloseConnnction();

                        // Update UI
                        SpeechPage.Current.setSpeakerButtonEnabled(false);
                    }
                    MainPage.Current.onAudioEnded();
                    OperationLogger.getOpLogger().Log(OperationType.ASR, "Ended");
                }
                catch (Exception e)
                {
                    LogService.MetroLogger.getSharedLogger().Error("Error while ending audio:" + e.Message);
                    MainPage.Current.onAudioEnded();
                    result = false;
                }
            });
            return result;

        }
        #endregion

        /// <summary>
        /// Saves ASR transcripts to disk and updates the conversation history
        /// panel on Speech Page to the saved transcripts if necessary.
        /// </summary>
        public async Task SaveTranscriptions(bool reloadSpeechPage=true)
        {
            await MainPage.Current.SaveCurrentConversationsToDisk();

            // when "reloadSpeechPage" is true, load the saved transcripts and update the 
            // speech bubbles on SpeechPage
            if (reloadSpeechPage)
            {   
                MainPage.Current.updateAudioMeta();
                MainPage.Current.updatePastConversation();
            }
        }


        public async void writeAudioToFile(string notebookid)
        {
            MainPage.Current.NotifyUser("Saving conversation audio", NotifyType.StatusMessage, 2);

            savedFile = await FileService.FileManager.getSharedFileManager().GetNoteFile(notebookid, "", FileService.NoteFileType.Audio, currentAudioName);

            Debug.WriteLine("Output file to " + savedFile.Path.ToString());

            var stream = await savedFile.OpenAsync(FileAccessMode.ReadWrite);

            using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
            {
                using (DataWriter dataWriter = new DataWriter(outputStream))
                {
                    // RIFF header.
                    // Chunk ID.
                    dataWriter.WriteBytes(Encoding.ASCII.GetBytes("RIFF"));

                    // Chunk size.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)((this.byte_accumulator.Count) + 36)));

                    // Format.
                    dataWriter.WriteBytes(Encoding.ASCII.GetBytes("WAVE"));

                    // Sub-chunk 1.
                    // Sub-chunk 1 ID.
                    dataWriter.WriteBytes(Encoding.ASCII.GetBytes("fmt "));

                    // Sub-chunk 1 size.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)16));

                    // Audio format (floating point (3) or PCM (1)). Any other format indicates compression.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt16)3));

                    // Channels.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt16)1));

                    // Sample rate.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)16000));

                    // Bytes rate.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)(16000 * 1 * (32 / 8))));

                    // Block align.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt16)(1 * (32 / 8))));

                    // Bits per sample.
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt16)32));

                    // Sub-chunk 2.
                    // Sub-chunk 2 ID.
                    dataWriter.WriteBytes(Encoding.ASCII.GetBytes("data"));

                    // Sub-chunk 2 size.
                    //dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)((32 / 8) * this.byte_accumulator.Count)));
                    dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)(this.byte_accumulator.Count)));

                    dataWriter.WriteBytes(this.byte_accumulator.ToArray());
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
                await outputStream.FlushAsync();
                RecordingCreated.Invoke(this, savedFile);
            }
            MainPage.Current.SaveNewAudioName(currentAudioName);
            
        }

        /// <returns></returns>
        private async Task CreateAudioGraph()
        {
            // create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Communications);
            // note: Do not send too few bytes, otherwise will have low recognition accuracy.
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.SystemDefault;
            
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                LogService.MetroLogger.getSharedLogger().Error($"AudioGraph Creation Error because {result.Status.ToString()}");
                return;
            }

            graph = result.Graph;

            AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;

            // Makes sure this expresses 16K Hz, F32LE format, with Byte rate of 16k x 4 bytes per second
            // Note: the audio graph does not change audio format because audio graph only supports float 
            //       32 bit according to doc.
            //       See more: https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/audio-graphs
            nodeEncodingProperties.ChannelCount = 1;
            nodeEncodingProperties.SampleRate = 16000;
            nodeEncodingProperties.BitsPerSample = 32;
            nodeEncodingProperties.Bitrate = 16000 * 32;
            nodeEncodingProperties.Subtype = "Float";
            
            // Create a frame output node
            frameOutputNode = graph.CreateFrameOutputNode(nodeEncodingProperties);
            graph.QuantumStarted += onAudioGraphQuantumStarted;

            if (useFile) 
            {
                // UWP APPS do not have direct access to file system via path
                // Must use filepicker
                FileOpenPicker filePicker = new FileOpenPicker();
                filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                filePicker.FileTypeFilter.Add(".mp3");
                filePicker.FileTypeFilter.Add(".wav");
                filePicker.FileTypeFilter.Add(".wma");
                filePicker.FileTypeFilter.Add(".m4a");
                filePicker.ViewMode = PickerViewMode.Thumbnail;

                StorageFile audioFile = null;
                while (audioFile == null)
                {
                    audioFile = await filePicker.PickSingleFileAsync();
                }
                
                CreateAudioFileInputNodeResult fileInputNodeResult = await graph.CreateFileInputNodeAsync(audioFile);

                fileInputNode = fileInputNodeResult.FileInputNode;
                fileInputNode.AddOutgoingConnection(frameOutputNode);
            }
            else
            {
                CreateAudioDeviceInputNodeResult deviceInputNodeResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Communications);
                if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    rootPage.NotifyUser($"Audio Device Input unavailable because {deviceInputNodeResult.Status.ToString()}",NotifyType.ErrorMessage, 2);
                    return;
                }

                deviceInputNode = deviceInputNodeResult.DeviceInputNode;
                deviceInputNode.AddOutgoingConnection(frameOutputNode);
            }
        }
        
        private void startGraph()
        {
            try
            {
                graph.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to start the graph: " + e.Message);
            }
        }

        private async Task SelectOutputFile()
        {
            FileSavePicker saveFilePicker = new FileSavePicker();
            saveFilePicker.FileTypeChoices.Add("Pulse Code Modulation", new List<string>() { ".wav" });
            saveFilePicker.FileTypeChoices.Add("Windows Media Audio", new List<string>() { ".wma" });
            saveFilePicker.FileTypeChoices.Add("MPEG Audio Layer-3", new List<string>() { ".mp3" });
            saveFilePicker.SuggestedFileName = "New Audio Track";
            StorageFile file = await saveFilePicker.PickSaveFileAsync();

            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                return;
            }

            rootPage.NotifyUser(String.Format("Recording to {0}", file.Name.ToString()), NotifyType.StatusMessage, 2);
            MediaEncodingProfile fileProfile = CreateMediaEncodingProfile(file);

            // Operate node at the graph format, but save file at the specified format
            CreateAudioFileOutputNodeResult fileOutputNodeResult = await graph.CreateFileOutputNodeAsync(file, fileProfile);

            if (fileOutputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                rootPage.NotifyUser(String.Format("Cannot create output file because {0}", fileOutputNodeResult.Status.ToString()), NotifyType.ErrorMessage, 2);
                return;
            }

            fileOutputNode = fileOutputNodeResult.FileOutputNode;

            // Connect the input node to both output nodes
            deviceInputNode.AddOutgoingConnection(fileOutputNode);
        }

        private MediaEncodingProfile CreateMediaEncodingProfile(StorageFile file)
        {
            switch (file.FileType.ToString().ToLowerInvariant())
            {
                case ".wma":
                    return MediaEncodingProfile.CreateWma(AudioEncodingQuality.High);
                case ".mp3":
                    return MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High);
                case ".wav":
                    return MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                default:
                    throw new ArgumentException();
            }
        }

        /// <summary>
        /// Gets audio data from Audio Frame and sends data to the speech server when
        /// when the audio graph starts to process a new quantum.
        /// </summary>
        private void onAudioGraphQuantumStarted(AudioGraph sender, object args)
        {
            AudioFrame frame = frameOutputNode.GetFrame();
            ProcessFrameOutputAsync(frame);
        }

        List<byte> bytelist = new List<byte>(1000);
        /// <summary>
        /// Copies the audio data from native memory to managed memory and send it to 
        /// the speech server.
        /// </summary>
        unsafe private async void ProcessFrameOutputAsync(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                // Get buffer from the AudioFrame
                byte* dataInBytes;
                uint capacityInBytes; // number of bytes
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes); // Gets an IMemoryBuffer as an array of bytes.

                // Copy data in native memory to managed memory
                IntPtr source = (IntPtr)dataInBytes;
                byte[] floatmsg = new byte[capacityInBytes];
                Marshal.Copy(source, floatmsg, 0, (int)capacityInBytes);

                // Send data to speech server
                sendBytes(floatmsg);
            }
        }

        private List<byte> byte_accumulator;
        /// <summary>
        /// Sends audio bytes to the speech server and stores them in an array for saving to disk later.
        /// </summary>
        private async void sendBytes(byte[] bs)
        {
            if (useFile)
            {
                await Task.Delay(100);  // To slow down streaming to server when loading test file
            }
            // Send audio to speech server
            bool result = await speechStreamSocket.SendBytesAsync(bs);
            if (!result)
            {
                LogService.MetroLogger.getSharedLogger().Error("sendBytes in SpeechManager failed");
                try
                {
                  await this.EndAudio("");  // set arg to "" so the function doesn't save data to disk
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine("Diarization engine stopped unexpectedly");
                }
            }
            // Save audio (for saving to disk later)
            else
            {
                try
                {
                    byte_accumulator.AddRange(bs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }   
            }
        }

        private async Task ReceiveDataAsync(StreamWebSocket activeSocket)
        {
            // Continuously read incoming data. For reading data we'll show how to use 
            // activeSocket.InputStream.AsStream() to get a .NET stream. Alternatively 
            // you could call readBuffer.AsBuffer() to use IBuffer with activeSocket.InputStream.ReadAsync.
            Stream readStream = activeSocket.InputStream.AsStreamForRead();
            int bytesReceived = 0;
            try
            {
                Debug.WriteLine("Background read starting.");

                byte[] readBuffer = new byte[1000];

                while (true)
                {
                    int read = await readStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                    
                    bytesReceived += read;
                    string receivedJSON = bytesReceived.ToString();
                    receivedJSON.Replace('-', '_'); // So that we can parse objects

                    var parsedSpeech = JsonConvert.DeserializeObject<SpeechEngineJSON>(receivedJSON);
                    Debug.WriteLine(parsedSpeech.ToString());
                }
            }
            catch (Exception ex)
            {
                WebErrorStatus status = WebSocketError.GetStatus(ex.GetBaseException().HResult);

                switch (status)
                {
                    case WebErrorStatus.OperationCanceled:
                        Debug.WriteLine("Background read canceled.");
                        break;

                    default:
                        Debug.WriteLine("Error: " + status);
                        Debug.WriteLine(ex.Message);
                        break;
                }
            }
        }


        /// <summary>
        /// Updates current audio name when a new speech session/conversation starts.
        /// </summary>
        /// <remarks>
        /// The audio name is the time the recording session starts (Year_Month_Day_Hour_Minute).
        /// The server will save the recorded audio with name = {ManagerID}_{NoteID}_{AudioName}.
        /// It's precise to the minutes so that if a recording session is opened and closed within the same minute,
        /// a new recording opened later in the same minute overwrites it (based on the assumption that very short
        /// recordings don't contain useful information).
        /// </remarks>
        public void CreateNewAudioName()
        {
            var time = DateTime.Now;
            currentAudioName = $"{time.Year}_{time.Month}_{time.Day}_{time.Hour}_{time.Minute}";
        }

        /// <summary>
        /// Generates the name by which the ASR server saves the current audio recording.
        /// </summary>
        /// <returns>saved audio file name on the ASR server</returns>
        public string GetAudioNameForServer()
        {
            string noteName = MainPage.Current.notebookId;
            int workerID = 666; // TODO: ID shouldn't be hardcoded in the function!

            return $"{workerID}_{noteName}_{currentAudioName}";
        }
    }  
}
