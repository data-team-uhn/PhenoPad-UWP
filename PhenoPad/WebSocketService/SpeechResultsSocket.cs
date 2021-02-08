using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace PhenoPad.WebSocketService
{
    public class SpeechResultsSocket
    {
        MainPage rootPage = MainPage.Current;

        // !!WARNING !! server address changes every time
        private string serverAddress;
        private string serverPort;
        private uint ERROR_INTERNET_OPERATION_CANCELLED = 0x80072EF1;
        NetworkAdapter networkAdapter;
        public StreamWebSocket streamSocket;
        private DataWriter dataWriter;
        private DataReader dataReader;

        public SpeechResultsSocket()
        {
            // Socket constructor does nothing :D
        }
        public SpeechResultsSocket(string sAddress, string port)
        {
            this.serverAddress = sAddress;
            this.serverPort = port;
            this.streamSocket = null;
        }

        public void setServerAddress(string ads, string pt)
        {
            this.serverAddress = ads;
            this.serverPort = pt;
        }

        public String getServerAddress()
        {
            return this.serverAddress;
        }

        public async Task<bool> ConnectToServer()
        {
            // By default 'HostNameForConnect' is disabled and host name validation is not required. When enabling the
            // text box validating the host name is required since it was received from an untrusted source
            // (user input). The host name is validated by catching ArgumentExceptions thrown by the HostName
            // constructor for invalid input.
            //HostName hostName = new HostName("ws://localhost:8888/client/ws/speech");


            streamSocket = new StreamWebSocket();
            //.Control.OutboundBufferSizeInBytes = 5000;
            streamSocket.Closed += WebSocket_ClosedAsync;

            // If necessary, tweak the socket's control options before carrying out the connect operation.
            // Refer to the StreamSocketControl class' MSDN documentation for the full list of control options.
            //socket.Control.OutboundBufferSizeInBytes = ;

            //socket.SetRequestHeader("content-type", "audio/x-raw");
            //socket.SetRequestHeader("content-type", "audio/x-raw");
            try
            {
                Debug.WriteLine(serverAddress);
                Debug.WriteLine(serverPort + Environment.NewLine);

                Task connectTask = this.streamSocket.ConnectAsync(new Uri("ws://" + serverAddress + ":" + serverPort +
                                           "/client/ws/speech_result" +
                                           "?content-type=audio%2Fx-raw%2C+layout%3D%28string%29interleaved%2C+rate%3D%28int%2916000%2C+format%3D%28string%29S16LE%2C+channels%3D%28int%291&manager_id=666")).AsTask();
                //Task connectTask = this.streamSocket.ConnectAsync(new Uri("ws://" + serverAddress + ":" + serverPort +
                //           "/client/ws/speech_result")).AsTask();

                await connectTask;
                if (connectTask.Exception != null)
                    LogService.MetroLogger.getSharedLogger().Error("connectTask.Exception:" + connectTask.Exception.Message);
                dataWriter = new DataWriter(this.streamSocket.OutputStream);

                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error("SpeechResultsSocket: " + e.Message);
                streamSocket.Dispose();
                streamSocket = null;
                return false;
            }

        }


        public async Task<bool> SendBytesAsync(byte[] message)
        {
            if (streamSocket == null)
            {
                return false;
            }
            try
            {
                dataWriter.WriteBytes(message);
                await dataWriter.StoreAsync();
                return true;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed send data to speech engine:" + ex.Message);
                await Task.Delay(2);
                return false;
            }
        }

        /// <summary>
        /// Receives ASR server message as a string.
        /// </summary>
        public async Task<String> SpeechResultsSocket_ReceiveMessage()
        {
            string returnMessage = String.Empty;
            try
            {
                if (streamSocket != null)
                {
                    uint length = 1000;     // Leave a large buffer

                    var readBuf = new Windows.Storage.Streams.Buffer(length);
                    var readOp = await streamSocket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);
                    DataReader readPacket = DataReader.FromBuffer(readBuf);
                    uint buffLen = readPacket.UnconsumedBufferLength;
                    returnMessage = readPacket.ReadString(buffLen);
                }
                return returnMessage;

            }
            catch (Exception exp)
            {
                //This handles the case where we force quit 
                if (exp.HResult == (int)ERROR_INTERNET_OPERATION_CANCELLED)
                {
                    LogService.MetroLogger.getSharedLogger().Info("ERROR_INTERNET_OPERATION_CANCELLED.");
                    return "CONNECTION_CANCELLED";
                }
                else
                {
                    LogService.MetroLogger.getSharedLogger().Error($"Issue receiving:{exp.Message}");
                    AbortConnection();
                    return "CONNECTION_ERROR";
                }
            }
        }

        public async Task CloseConnnction()
        {
            if (streamSocket == null)
                return;
            try
            {
                //using (var dataWriter = new DataWriter(this.streamSocket.OutputStream))
                //{
                Encoding ascii = Encoding.ASCII;
                dataWriter.WriteBytes(ascii.GetBytes("EOS"));
                await dataWriter.StoreAsync();
                //dataWriter.DetachStream();
                //}
                //Debug.WriteLine("Sending data using StreamWebSocket: " + message.Length.ToString() + " bytes");
            }

            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Speech result closeconnection exception:" + ex + ex.Message);
            }

            dataWriter.Dispose();
            streamSocket.Dispose();
            streamSocket = null;
        }

        public void AbortConnection()
        {

            if (dataWriter != null)
                dataWriter.Dispose();
            dataWriter = null;
            if (streamSocket != null)
                streamSocket.Dispose();
            streamSocket = null;

        }
        
        /// <summary>
        /// Handler function called when a stream websocket is closed.
        /// </summary>
        /// <param name="sender">the websocket being closed</param>
        /// <param name="args">contains information about reasons that the websocket was closed</param>
        /// <remarks>
        /// Clears the SpeechResultSocket instance's streamSocket and if speech service is still running, stops the service.
        /// </remarks>
        private async void WebSocket_ClosedAsync(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            LogService.MetroLogger.getSharedLogger().Info(
                $"SpeechResultSocket closed: Code = {args.Code}, Reason = {args.Reason}, will dispose current stream socket...");
            if (streamSocket != null)
            {
                streamSocket.Dispose();
                streamSocket = null;
                if (MainPage.Current.speechEngineRunning)
                {
                    await MainPage.Current.KillAudioService();
                }
            }
        }
    }

}
