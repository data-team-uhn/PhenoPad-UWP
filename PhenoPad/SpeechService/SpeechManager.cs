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

    class SpeechManager
    {
        public static SpeechManager sharedSpeechManager;
        public Conversation conversation = new Conversation();
        private MainPage rootPage = MainPage.Current;
        private AudioGraph graph;
        private AudioFrameOutputNode frameOutputNode;
        private AudioDeviceInputNode deviceInputNode;
        public double theta = 0;
        private SpeechStreamSocket speechStreamSocket;
        private AudioFileOutputNode fileOutputNode;

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

        public void AddNewMessage(string text)
        {
            if (text.Length > 0)
            {
                this.conversation.Add(new TextMessage
                {
                    Body = text,
                    DisplayTime = DateTime.Now.ToString(),
                    IsSent = text.Length % 2 == 0? true:false
                });
            }
        }
        public async void StartAudio()
        {
            speechStreamSocket = new SpeechStreamSocket();
            bool succeed = await speechStreamSocket.ConnectToServer();
            if (!succeed) {
                rootPage.NotifyUser("Connection failed.", NotifyType.StatusMessage, 2);
                return;
            }
             
            await CreateAudioGraph();

            deviceInputNode.Start();
            // Start a task to continuously read for incoming data
            //Task receiving = ReceiveDataAsync(speechStreamSocket.streamSocket);
            await Task.Run(async () =>
             {
                 while (true)
                 {
                     // don't run again for 
                     await Task.Delay(500);
                     // do the work in the loop
                     speechStreamSocket.ReceiveMessageUsingStreamWebSocket();
                     
                 }
             });
            //Task.Run(() => speechStreamSocket.ReceiveMessageUsingStreamWebSocket(), TaskCreationOptions.LongRunning);
        }

        public async void EndAudio()
        {
            deviceInputNode.Stop();
            graph.Stop();
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
            
        }
        

        private async Task CreateAudioGraph()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Other);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
            //settings.DesiredSamplesPerQuantum = 16000 / 4;
            //settings.EncodingProperties.ChannelCount = 1;
            
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                //rootPage.NotifyUser(String.Format("AudioGraph Creation Error because {0}", result.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            graph = result.Graph;
            //AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
            /**
            graph.EncodingProperties.ChannelCount = 1;
            graph.EncodingProperties.SampleRate = 16000;
            graph.EncodingProperties.BitsPerSample = 16;
            graph.EncodingProperties.Bitrate = 16000 * 16;
            graph.EncodingProperties.Subtype = "PCM";
            
            Debug.WriteLine(graph.EncodingProperties.ChannelCount + " " + graph.EncodingProperties.SampleRate + " " + graph.EncodingProperties.BitsPerSample + " #########");
            **/

            CreateAudioDeviceInputNodeResult deviceInputNodeResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Other);

            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device input node
                rootPage.NotifyUser(String.Format("Audio Device Input unavailable because {0}", deviceInputNodeResult.Status.ToString()), NotifyType.ErrorMessage, 2);
                return;
            }

            deviceInputNode = deviceInputNodeResult.DeviceInputNode;

            AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
            nodeEncodingProperties.ChannelCount = 1;
            nodeEncodingProperties.SampleRate = 16000;
            nodeEncodingProperties.BitsPerSample = 16;
            nodeEncodingProperties.Bitrate = 16000 * 16;
            nodeEncodingProperties.Subtype = "PCM";
            // Create a frame output node
            frameOutputNode = graph.CreateFrameOutputNode(nodeEncodingProperties);
            graph.QuantumStarted += AudioGraph_QuantumStarted;
            //graph.QuantumProcessed += AudioGraph_QuantumProcessed;


            deviceInputNode.AddOutgoingConnection(frameOutputNode);
      

            // Start the graph since we will only start/stop the frame input node
            graph.Start();
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
                speechStreamSocket.SendBytesAsync(floatmsg);

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

                    // Do something with the data.
                    // This sample merely reports that the data was received.

                    bytesReceived += read;
                    Debug.WriteLine(bytesReceived.ToString());
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

    }
    


    
}
