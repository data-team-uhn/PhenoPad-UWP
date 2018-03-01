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
    public class SpeechStreamSocket
    {
        MainPage rootPage = MainPage.Current;

        // !!WARNING !! server address changes every time
        private string serverAddress = "34.236.36.193";
        private string serverPort = "8888";

        NetworkAdapter networkAdapter;
        public StreamWebSocket streamSocket;
        private DataWriter dataWriter;
        private DataReader dataReader;
    
        public SpeechStreamSocket()
        {
            // Socket constructor does nothing :D
        }
        public SpeechStreamSocket(string sAddress)
        {
            this.serverAddress = sAddress;
        }

        public void setServerAddress(string ads) {
            this.serverAddress = ads;
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
                Task connectTask = this.streamSocket.ConnectAsync(new Uri("ws://" + SpeechStreamSocket.serverAddress + ":" + SpeechStreamSocket.serverPort + 
                                            "/client/ws/speech?content-type=audio/x-raw," +
                                            "+layout=(string)interleaved," +
                                            "+rate=(int)16000," +
                                            "+format=(string)F32LE," +
                                            "+channels=(int)1")).AsTask();

                /**
                await connectTask.ContinueWith(_ =>
                 {
                     Task.Run(() => this.ReceiveMessageUsingStreamWebSocket());
                     //Task.Run(() => this.SendBytes(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 }));
                 });
                **/

                MainPage.Current.NotifyUser("Connecting to speech engine at " + 
                    SpeechStreamSocket.serverAddress + ":" + SpeechStreamSocket.serverPort + 
                    ", please wait ...", NotifyType.StatusMessage, 3);

                await connectTask;
                dataWriter = new DataWriter(this.streamSocket.OutputStream);

                return true;
            }
            catch (Exception e)
            { 
                streamSocket.Dispose();
                streamSocket = null;
                return false;
            }
            
        }


        public async Task<bool> SendBytesAsync(byte[] message)
        {
            if(streamSocket == null)
            {
                return false;
            }
            try
            {
                //using (var dataWriter = new DataWriter(this.streamSocket.OutputStream))
                //{
                    dataWriter.WriteBytes(message);
                    await dataWriter.StoreAsync();
                //dataWriter.DetachStream();
                //}
                //Debug.WriteLine("Sending data using StreamWebSocket: " + message.Length.ToString() + " bytes");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Send data error.");
                MainPage.Current.NotifyUser("Error sending data to speech engine", NotifyType.ErrorMessage, 2);

                await Task.Delay(2);

                return false;
                //Debug.WriteLine(ex.GetBaseException().HResult);
                //Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add code here to handle exceptions.
                //await ConnectToServer();

            }
        }


        public async Task<String> ReceiveMessageUsingStreamWebSocket()
        {
            string returnMessage = String.Empty;
            try
            {   
                uint length = 32768;     // Leave a large buffer

                var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                var readOp = streamSocket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);

                await readOp;   // Don't move on until we have finished reading from server

                DataReader readPacket = DataReader.FromBuffer(readBuf);
                uint buffLen = readPacket.UnconsumedBufferLength;
                returnMessage = readPacket.ReadString(buffLen);
            }
            catch (Exception exp)
            {
                Debug.WriteLine("failed to post a read failed with error:  " + exp.Message);
            }

            //Debug.WriteLine("Data length of " + returnMessage.Length.ToString() + " received from server");

            return returnMessage;
        }

        public async void CloseConnnction()
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
                streamSocket.Dispose();
                MainPage.Current.NotifyUser("Disconnecting from the speech engine", NotifyType.StatusMessage, 2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Experienced error closing socket to speech engine");
                MainPage.Current.NotifyUser("Fail to close websocket", NotifyType.ErrorMessage, 2);

                //Debug.WriteLine(ex.GetBaseException().HResult);
                //Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add code here to handle exceptions.
                //await ConnectToServer();

            }
        }
        private async void WebSocket_ClosedAsync(Windows.Networking.Sockets.IWebSocket sender, Windows.Networking.Sockets.WebSocketClosedEventArgs args)
        {
            
            rootPage.NotifyUser("Websocket connection is closed. Please try to reconnect.", NotifyType.ErrorMessage, 1);
            Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
            // Add additional code here to handle the WebSocket being closed.
        }
    }
}
