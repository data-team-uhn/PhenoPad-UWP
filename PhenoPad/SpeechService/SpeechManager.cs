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
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
//using Google.Cloud.Speech.V1;
using System.Threading;
using PhenoPad.WebSocketService;
using System.Diagnostics;
using Windows.Networking.Sockets;
using System.IO;
using Windows.Web;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Media.Transcoding;

// To parse JSON we received from speech engine server
using Newtonsoft.Json;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.Storage.Streams;
//using Newtonsoft.Json.Linq;   // Seems like we only need JSON parsing

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
        //private string serverAddress = "54.226.217.30";
        private string serverAddress = "phenopad.ccm.sickkids.ca";
        private string serverPort = "8888";

        public static SpeechManager sharedSpeechManager;
        
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

        // file debug seems to be broken?
        private bool useFile = false;
        private CancellationTokenSource cancellationSource;

        public SpeechManager()
        {
            object val = AppConfigurations.readSetting("serverIP");
            Debug.WriteLine(val);
            if (val != null && ((string)val).Length != 0)
            {
                this.serverAddress = (string)val;
            }

            object val2 = AppConfigurations.readSetting("serverPort");
            Debug.WriteLine(val2);
            if (val2 != null && ((string)val).Length != 0)
            {
                this.serverPort = (string)val2;
            }

            this.speechInterpreter = new SpeechEngineInterpreter(this.conversation, this.realtimeConversation);
            this.speechStreamSocket = new SpeechStreamSocket(this.serverAddress, this.serverPort);
            this.speechAPI = new SpeechRESTAPI();
        }

        public void cleanUp()
        {
            this.conversation.Clear();
            this.realtimeConversation.Clear();
            this.speechInterpreter = new SpeechEngineInterpreter(this.conversation, this.realtimeConversation);
        }

        public static SpeechManager getSharedSpeechManager()
        {
            if (sharedSpeechManager == null)
            {
                sharedSpeechManager = new SpeechManager();
                return sharedSpeechManager;
            }
            else
            {
                return sharedSpeechManager;
            }
        }
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
        public void AddNewMessage(string text, double start, double duration, uint speaker)
        {
            if (text.Length > 0)
            {
                this.conversation.Add(new TextMessage
                {
                    Body = text,
                    //DisplayTime = DateTime.Now.ToString(),
                    IsFinal =  true,
                    Speaker = speaker,
                    Interval = new TimeInterval(start, start + duration)
                    
                });
            }
        }

        /// <summary>
        /// connect to client/speech/results
        /// only receive results without sending audio signals
        /// </summary>
        public async void ReceiveASRResults()
        {
            speechResultsSocket = new SpeechResultsSocket(this.serverAddress, this.serverPort);
            //speechAPI.setupClient(this.serverAddress);
            //speechAPI.setupClient(this.serverAddress);
            bool succeed = await speechResultsSocket.ConnectToServer();

            if (!succeed)
            {
                MainPage.Current.NotifyUser("Connection to speech results server failed.", NotifyType.ErrorMessage, 3);
                return;
            }

            MainPage.Current.NotifyUser("Connection established", NotifyType.StatusMessage, 2);
            SpeechPage.Current.setSpeakerButtonEnabled(true);
            SpeechPage.Current.adjustSpeakerCount(2);

            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;                 // need this to actually cancel reading from websocketS

            this.speechInterpreter.newConversation();

            await Task.Run(async () =>
            {
                // Weird issue but seems to be some buffer issue
                string accumulator = String.Empty;

                // Stop running if cancellation requested
                while (true && !cancellationToken.IsCancellationRequested)
                {
                    // don't run again for 
                    await Task.Delay(500);
                    // do the work in the loop
                    string serverResult = await speechResultsSocket.ReceiveMessageUsingStreamWebSocket();

                    // Debug.WriteLine("Got server message");

                    serverResult = serverResult.Replace('-', '_');     // So that we can parse objects

                    accumulator += serverResult;


                    // Seems like if we don't do this we won't get all the packages
                    bool doParsing = true;
                    while (doParsing && !cancellationToken.IsCancellationRequested)
                    {
                        string outAccumulator = String.Empty;
                        string json = SpeechEngineInterpreter.getFirstJSON(accumulator, out outAccumulator);
                        accumulator = outAccumulator;

                        // Only process if we have valid JSON
                        if (json.Length != 0)
                        {
                            try
                            {
                                // need - not _ here... =. =
                               
                                Debug.WriteLine("Result from speech: " + json);
                                var parsedSpeech = JsonConvert.DeserializeObject<SpeechEngineJSON>(json);
                                parsedSpeech.original = json;

                                //{'diarization': [{'start': 7.328, 'speaker': 0, 'end': 9.168000000000001, 'angle': 152.97781134625265}], 'diarization_incremental': True} 


                                //Debug.WriteLine(json);
                                //Debug.WriteLine(parsedSpeech.ToString());

                                speechInterpreter.processJSON(parsedSpeech);

                                // TODO Find a more legitimate way to fire an UI change?

                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                   () =>
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

                            //  try to decode as dirization result
                            try
                            {
                                json = json.Replace('_', '-');
                                var diaResult = JsonConvert.DeserializeObject<DiarizationJSON>(json);
                                speechInterpreter.processDiaJson(diaResult);
                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                  () =>
                                  {
                                      EngineHasResult.Invoke(this, speechInterpreter);
                                  }
                                  );
                            }
                            catch (Exception)
                            {
                                Debug.WriteLine("This is not diarization result.");
                            }
                           
                        }
                        else
                        {
                            // didn't get a valid JSON, wait for more packages
                            doParsing = false;
                        }
                    }

                }
            }, cancellationToken);
            //Task.Run(() => speechStreamSocket.ReceiveMessageUsingStreamWebSocket(), TaskCreationOptions.LongRunning);

            return;
        }

        public void StopASRResults()
        {
            //await endSemaphoreSlim.WaitAsync();
            try
            {
                MainPage.Current.NotifyUser("Disconnecting from speech engine", NotifyType.StatusMessage, 2);
                //deviceInputNode.Stop();
                cancellationSource.Cancel();
                speechResultsSocket.CloseConnnction();

                SpeechPage.Current.setSpeakerButtonEnabled(false);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                //endSemaphoreSlim.Release();
            }
        }



        public async Task StartAudio()
        {
            bool attemptConnection = true;
            int count = 1;

            while (attemptConnection)
            {
                Debug.WriteLine("connecting");
                speechStreamSocket = new SpeechStreamSocket(this.serverAddress, this.serverPort);
                //speechAPI.setupClient(this.serverAddress);
                speechAPI.setupClient(this.serverAddress);
                bool succeed = await speechStreamSocket.ConnectToServer();

                if (!succeed)
                {
                    MainPage.Current.NotifyUser("Connection to speech engine failed.", NotifyType.ErrorMessage, 5);

                    // Display a dialog box to allow for retry
                    // https://code.msdn.microsoft.com/windowsapps/How-to-show-message-dialog-35468701
                    var messageDialog = new MessageDialog("We failed to connect to speech analysis engine (" 
                        + this.serverAddress + ":" + this.serverPort  + ") just now.");

                    messageDialog.Commands.Add(new UICommand("Try Again") { Id = 0 });
                    messageDialog.Commands.Add(new UICommand("Cancel") { Id = 1 });
                    // Set the command that will be invoked by default
                    messageDialog.DefaultCommandIndex = 0;
                    // Set the command to be invoked when escape is pressed
                    messageDialog.CancelCommandIndex = 1;

                    // Show the message dialog
                    var result = await messageDialog.ShowAsync();

                    if ((int)result.Id == 0)
                    {
                        // Technically we don't have to do anything here
                        count++;
                        MainPage.Current.NotifyUser("Connect to speech engine attempt " + count.ToString(), NotifyType.StatusMessage, 2);
                        attemptConnection = true;
                    }
                    else
                    {
                        attemptConnection = false;
                        return;
                    }
                }
                else
                {
                    attemptConnection = false;
                }
            }


           await CreateAudioGraph();
           // await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("audio start");



            // Wait 10 seconds so that the server has time to create a worker
            // else you'll see lots of audio delays
            await Task.Delay(5000);
            MainPage.Current.NotifyUser("Connection established", NotifyType.StatusMessage, 2);
            SpeechPage.Current.setSpeakerButtonEnabled(true);
            SpeechPage.Current.adjustSpeakerCount(2);


            startGraph();
            if (useFile)
            {
                fileInputNode.Start();
            }
            else
            {
                deviceInputNode.Start();
                // await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("odas start");
            }


            // save this to file
            this.byte_accumulator = new List<byte>();

            // Start a task to continuously read for incoming data
            //Task receiving = ReceiveDataAsync(speechStreamSocket.streamSocket);

            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;                 // need this to actually cancel reading from websocketS

            this.speechInterpreter.newConversation();

            await Task.Run(async () =>
             {
                 // Weird issue but seems to be some buffer issue
                 string accumulator = String.Empty;

                 // Stop running if cancellation requested
                 while (true && !cancellationToken.IsCancellationRequested)
                 {
                     // don't run again for 
                     await Task.Delay(500);
                     // do the work in the loop
                     string serverResult = await speechStreamSocket.ReceiveMessageUsingStreamWebSocket();

                    // Debug.WriteLine("Got server message");

                     serverResult = serverResult.Replace('-', '_');     // So that we can parse objects

                     accumulator += serverResult;


                     // Seems like if we don't do this we won't get all the packages
                     bool doParsing = true;
                     while (doParsing && !cancellationToken.IsCancellationRequested)
                     {
                         string outAccumulator = String.Empty;
                         string json = SpeechEngineInterpreter.getFirstJSON(accumulator, out outAccumulator);
                         accumulator = outAccumulator;

                         // Only process if we have valid JSON
                         // Only process if we have valid JSON
                         if (json.Length != 0)
                         {
                             try
                             {
                                 // need - not _ here... =. =

                                 Debug.WriteLine("Result from speech: " + json);
                                 var parsedSpeech = JsonConvert.DeserializeObject<SpeechEngineJSON>(json);
                                 parsedSpeech.original = json;

                                 //{'diarization': [{'start': 7.328, 'speaker': 0, 'end': 9.168000000000001, 'angle': 152.97781134625265}], 'diarization_incremental': True} 


                                 //Debug.WriteLine(json);
                                 //Debug.WriteLine(parsedSpeech.ToString());

                                 speechInterpreter.processJSON(parsedSpeech);

                                 // TODO Find a more legitimate way to fire an UI change?

                                 await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () =>
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

                             //  try to decode as dirization result
                             try
                             {
                                 json = json.Replace('_', '-');
                                 var diaResult = JsonConvert.DeserializeObject<DiarizationJSON>(json);
                                 speechInterpreter.processDiaJson(diaResult);
                                 await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                   () =>
                                   {
                                       EngineHasResult.Invoke(this, speechInterpreter);
                                   }
                                   );
                             }
                             catch (Exception)
                             {
                                 Debug.WriteLine("This is not diarization result.");
                             }

                         }
                         else
                         {
                             // didn't get a valid JSON, wait for more packages
                             doParsing = false;
                         }
                     }
                     
                 }
             }, cancellationToken);
            //Task.Run(() => speechStreamSocket.ReceiveMessageUsingStreamWebSocket(), TaskCreationOptions.LongRunning);

            return;
        }

        private SemaphoreSlim endSemaphoreSlim = new SemaphoreSlim(1);
        public async Task EndAudio(string notebookid)
        {
            //await endSemaphoreSlim.WaitAsync();
            try {
                if (graph != null && (fileInputNode != null || useFile == false))
                {
                    MainPage.Current.NotifyUser("Disconnecting from speech engine", NotifyType.StatusMessage, 2);
                    //deviceInputNode.Stop();
                    cancellationSource.Cancel();

                    if (useFile)
                    {
                        fileInputNode.Stop();
                    }
                    
                    graph.Stop();
                    speechStreamSocket.CloseConnnction();
                    // await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("audio end");
                    /**

                    TranscodeFailureReason finalizeResult = await fileOutputNode.FinalizeAsync();
                    if (finalizeResult != TranscodeFailureReason.None)
                    {
                        // Finalization of file failed. Check result code to see why
                        rootPage.NotifyUser(String.Format("Finalization of file failed because {0}", finalizeResult.ToString()), NotifyType.ErrorMessage, 2);
                        //fileButton.Background = new SolidColorBrush(Colors.Red);
                        return;
                    }

                    //recordStopButton.Content = "Record";
                    rootPage.NotifyUser("Recording to file completed successfully!", NotifyType.StatusMessage, 1);
                    **/
                    if (graph != null)
                    {
                        graph.Dispose();
                    }

                    if (notebookid != "")
                    {
                        this.writeToFile(notebookid);
                        this.SaveTranscriptions();
                    }
                        
                    SpeechPage.Current.setSpeakerButtonEnabled(false);
                }
            }
            finally
            {
                //endSemaphoreSlim.Release();
            }
        }

        public void setAudioIndex(int count)
        {
            this.speechInterpreter.conversationIndex = count;
        }
        public int getAudioCount()
        {
            return this.speechInterpreter.conversationIndex;
        }

        public async Task SaveTranscriptions()
        {
            Debug.WriteLine("Saving transriptions for audio_" + getAudioCount());
            await this.speechInterpreter.SaveCurrentConversationsToDisk();
        }

        public Windows.Storage.StorageFile savedFile;
        public async void writeToFile(string notebookid)
        {
            MainPage.Current.NotifyUser("Saving conversation audio", NotifyType.StatusMessage, 2);

            //string datestring = System.DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss");
            savedFile = await FileService.FileManager.getSharedFileManager().GetNoteFile(notebookid, "", FileService.NoteFileType.Audio, "audio_" + this.speechInterpreter.conversationIndex);
                // await storageFolder.CreateFileAsync("sample_" + this.speechInterpreter.conversationIndex + ".wav", Windows.Storage.CreationCollisionOption.ReplaceExisting);

            Debug.WriteLine("Output file to " + savedFile.Path.ToString());

            var stream = await savedFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);

            using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
            {
                using (DataWriter dataWriter = new DataWriter(outputStream))
                {
                    // RIFF header.
                    // Chunk ID.
                    dataWriter.WriteBytes(Encoding.ASCII.GetBytes("RIFF"));

                    // Chunk size.
                    //dataWriter.WriteBytes(BitConverter.GetBytes((UInt32)(((32 / 8) * this.byte_accumulator.Count) + 36)));
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


                    //TODO: Replace "Bytes" with the type you want to write.
                    dataWriter.WriteBytes(this.byte_accumulator.ToArray());
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
                await outputStream.FlushAsync();

                RecordingCreated.Invoke(this, savedFile);
            }

        }

        private async Task CreateAudioGraph()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Communications);
            // do not send too few bytes, has negative impat on recognition accuracy
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.SystemDefault;
            
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                //rootPage.NotifyUser(String.Format("AudioGraph Creation Error because {0}", result.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            graph = result.Graph;

            AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;

            // Makes sure this expresses 16K Hz, F32LE format, with Byte rate of 16k x 4 bytes per second
            // Jixuan says the audio graph does not change audio format
            nodeEncodingProperties.ChannelCount = 1;
            nodeEncodingProperties.SampleRate = 16000;
            nodeEncodingProperties.BitsPerSample = 32;
            nodeEncodingProperties.Bitrate = 16000 * 32;
            nodeEncodingProperties.Subtype = "Float";
            // Create a frame output node
            frameOutputNode = graph.CreateFrameOutputNode(nodeEncodingProperties);
            graph.QuantumStarted += AudioGraph_QuantumStarted;

            //graph.QuantumProcessed += AudioGraph_QuantumProcessed;

            //AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
            /**
            graph.EncodingProperties.ChannelCount = 1;
            graph.EncodingProperties.SampleRate = 16000;
            graph.EncodingProperties.BitsPerSample = 16;
            graph.EncodingProperties.Bitrate = 16000 * 16;
            graph.EncodingProperties.Subtype = "PCM";
            
            Debug.WriteLine(graph.EncodingProperties.ChannelCount + " " + graph.EncodingProperties.SampleRate + " " + graph.EncodingProperties.BitsPerSample + " #########");
            **/

            if (useFile)    // For debugging only
            {
                // UWP APPS do not have direct access to file system via path T_T
                // Must use filepicker
                //StorageFile audioFile = await StorageFile.GetFileFromPathAsync("C:\\Users\\jingb\\Dropbox\\CurrentCode\\Year4\\thesis\\meeting_2min.wav");

                
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
                    // Cannot create device input node
                    rootPage.NotifyUser(String.Format("Audio Device Input unavailable because {0}", deviceInputNodeResult.Status.ToString()), NotifyType.ErrorMessage, 2);
                    return;
                }

                deviceInputNode = deviceInputNodeResult.DeviceInputNode;
                deviceInputNode.AddOutgoingConnection(frameOutputNode);
            }

            // Start the graph since we will only start/stop the frame input node
            //graph.Start();
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
                // FileOutputNode creation failed
                rootPage.NotifyUser(String.Format("Cannot create output file because {0}", fileOutputNodeResult.Status.ToString()), NotifyType.ErrorMessage, 2);
                //fileButton.Background = new SolidColorBrush(Colors.Red);
                return;
            }

            fileOutputNode = fileOutputNodeResult.FileOutputNode;
           // fileButton.Background = new SolidColorBrush(Colors.YellowGreen);

            // Connect the input node to both output nodes
            deviceInputNode.AddOutgoingConnection(fileOutputNode);
            //deviceInputNode.AddOutgoingConnection(deviceOutputNode);
            //recordStopButton.IsEnabled = true;
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

        private void AudioGraph_QuantumProcessed(AudioGraph sender, object args)
        {
            AudioFrame frame = frameOutputNode.GetFrame();
            ProcessFrameOutputAsync(frame);
        }

        private void AudioGraph_QuantumStarted(AudioGraph sender, object args)
        {
            AudioFrame frame = frameOutputNode.GetFrame();
            ProcessFrameOutputAsync(frame);

        }

        List<byte> bytelist = new List<byte>(1000);
        unsafe private async void ProcessFrameOutputAsync(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;
                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                IntPtr source = (IntPtr)dataInBytes;
                byte[] floatmsg = new byte[capacityInBytes];
                Marshal.Copy(source, floatmsg, 0, (int)capacityInBytes);
                    //Debug.WriteLine("    " + capacityInBytes);
                    // without a buffer

                sendBytes(floatmsg);
                //speechStreamSocket.SendBytesAsync(floatmsg);

                // using a buffer
                /**
                bytelist.AddRange(floatmsg);
                if (bytelist.Count >= 32000)
                {
                    byte[] tosend = bytelist.ToArray();
                    Debug.WriteLine("Sending data...");
                    speechStreamSocket.SendBytesAsync(tosend);
                    //WriteWAV(tosend);
                    bytelist = new List<byte>();
                }**/
            }
        }

        private List<byte> byte_accumulator;

        private async void sendBytes(byte[] bs)
        {
            // To slow down streaming to server when loading test file
            if (useFile)
            {
                await Task.Delay(100);
            }

            bool result = await speechStreamSocket.SendBytesAsync(bs);
            if (!result)
            {
                try
                {
                    await this.EndAudio("");
                } catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine("Diarization engine stopped unexpectedly");
                }
                
            }
            else
            {
                try
                {
                    byte_accumulator.AddRange(bs);
                } catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                
            }
        }

        // Continuously read incoming data. For reading data we'll show how to use activeSocket.InputStream.AsStream()
        // to get a .NET stream. Alternatively you could call readBuffer.AsBuffer() to use IBuffer with
        // activeSocket.InputStream.ReadAsync.
        private async Task ReceiveDataAsync(StreamWebSocket activeSocket)
        {
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
                    receivedJSON.Replace('-', '_');     // So that we can parse objects
                    //Debug.WriteLine(receivedJSON);

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



        public void AddFakeSpeechResults()
        {
            double start = 0;
            uint speaker = 0;
            string[] example = {
           "Good morning, Dr. Sharma!",
           "Good morning!What’s wrong with you?",
           "I have been suffering from fever since yesterday.",
           "Do you have any other symptoms?",
           "I also feel headache and shivering.",
           "Let me take your temperature.At this time the fever is 102 degree.Don’t worry, there is nothing serious. I am giving you the medicine, and you will be all right in couple of days.",
           "Thank you, doctor.",
           "But get your blood tested for malaria, and come with the report tomorrow.",
           "OK doctor.",
           "I shall recommend at least two days rest for you.",
           "Would you prepare a medical certificate for me to submit it in my office ?",
           "Oh sure…………. This is your medical certificate.",
           "Thank you very much.Please tell me how shall I take this medicine ?"
                        };
            foreach (string sen in example)
            {
                AddNewMessage(sen, start, 0.1 * sen.Count(), speaker);
                start = 0.1 * sen.Count() + 0.5;
                speaker = 1 - speaker;
            }
            realtimeConversation.Add(new TextMessage
            {
                    Body = "This medicine is for one day only. Take this dose as soon as you reach your home and...",
                    //DisplayTime = DateTime.Now.ToString(),
                    IsFinal =  false,

            });
        }

    }
    


    
}
