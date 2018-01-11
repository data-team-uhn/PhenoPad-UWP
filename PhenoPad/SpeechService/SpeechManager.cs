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
        private AudioDeviceInputNode deviceInputNode;
        private AudioFrameOutputNode frameOutputNode;

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

            private AudioGraph graph;
            private AudioDeviceOutputNode deviceOutputNode;
            private AudioFrameInputNode frameInputNode;
            public double theta = 0;

        private object writeLock;
        private bool writeMore;

        public  async void StartAudio()
            {
                await CreateAudioGraph();
                frameInputNode.Start();
            }

            public void EndAudio()
            {
                frameInputNode.Stop();
                if (graph != null)
                {
                    graph.Dispose();
                }
            }
        
            unsafe private AudioFrame GenerateAudioData(uint samples)
            {
                // Buffer size is (number of samples) * (size of each sample)
                // We choose to generate single channel (mono) audio. For multi-channel, multiply by number of channels
                uint bufferSize = samples * sizeof(Int16);
                AudioFrame frame = new Windows.Media.AudioFrame(bufferSize);

            /**
                using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacityInBytes;
                    Int16* dataInFloat;

                    // Get the buffer from the AudioFrame
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                    byte[] bfr = new byte[capacityInBytes];
                    Marshal.Copy((IntPtr)dataInBytes, bfr, 0, (int)capacityInBytes);
       
                    // Cast to float since the data we are generating is float
                    dataInFloat = (Int16*)dataInBytes;

                    float freq = 1000; // choosing to generate frequency of 1kHz
                    float amplitude = 0.3f;
                    int sampleRate = (int)graph.EncodingProperties.SampleRate;
                    double sampleIncrement = (freq * (Math.PI * 2)) / sampleRate;

                    // Generate a 1kHz sine wave and populate the values in the memory buffer
                    for (int i = 0; i < samples; i++)
                    {
                        double sinValue = amplitude * Math.Sin(theta);
                        dataInFloat[i] = (float)sinValue;
                        theta += sampleIncrement;
                    }
                }
    **/
                return frame;
            }

            private async Task CreateAudioGraph()
            {
                writeLock = new object();
                writeMore = true;

            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
                CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

                if (result.Status != AudioGraphCreationStatus.Success)
                {
                    // Cannot create graph
                    //rootPage.NotifyUser(String.Format("AudioGraph Creation Error because {0}", result.Status.ToString()), NotifyType.ErrorMessage);
                    return;
                }

                graph = result.Graph;

                // Create a device output node
                CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    // Cannot create device output node
                    //rootPage.NotifyUser(String.Format("Audio Device Output unavailable because {0}", deviceOutputNodeResult.Status.ToString()), NotifyType.ErrorMessage);
                    //speakerContainer.Background = new SolidColorBrush(Colors.Red);
                }

                deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
                //rootPage.NotifyUser("Device Output Node successfully created", NotifyType.StatusMessage);
                //speakerContainer.Background = new SolidColorBrush(Colors.Green);

                // Create the FrameInputNode at the same format as the graph, except explicitly set mono.
                AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
                nodeEncodingProperties.ChannelCount = 1;
                nodeEncodingProperties.SampleRate = 16000;
                //nodeEncodingProperties.BitsPerSample = 16;
            nodeEncodingProperties.Subtype = "PCM";
                frameInputNode = graph.CreateFrameInputNode(nodeEncodingProperties);
                frameInputNode.AddOutgoingConnection(deviceOutputNode);
                //frameContainer.Background = new SolidColorBrush(Colors.Green);

                // Initialize the Frame Input Node in the stopped state
                frameInputNode.Stop();

            // Hook up an event handler so we can start generating samples when needed
            // This event is triggered when the node is required to provide data
            frameInputNode.QuantumStarted += node_QuantumStarted;
            
            // Start the graph since we will only start/stop the frame input node
            graph.Start();
            }

            private void node_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
            {
                // GenerateAudioData can provide PCM audio data by directly synthesizing it or reading from a file.
                // Need to know how many samples are required. In this case, the node is running at the same rate as the rest of the graph
                // For minimum latency, only provide the required amount of samples. Extra samples will introduce additional latency.
                uint numSamplesNeeded = (uint)args.RequiredSamples;

                if (numSamplesNeeded != 0)
                {
                    AudioFrame audioData = GenerateAudioData(numSamplesNeeded);
                    frameInputNode.AddFrame(audioData);
                }
            }


        }
    


    
}
