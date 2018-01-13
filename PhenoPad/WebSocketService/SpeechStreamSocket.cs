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
    class SpeechStreamSocket
    {
        MainPage rootPage = MainPage.Current;
        string serverAddress;
        NetworkAdapter networkAdapter;
        public StreamWebSocket streamSocket;
        private DataWriter dataWriter;
        private DataReader dataReader;

        public SpeechStreamSocket()
        {

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
                Task connectTask = this.streamSocket.ConnectAsync(new Uri("ws://35.170.60.186:8888/client/ws/speech?content-type=audio/x-raw,+layout=(string)interleaved,+rate=(int)16000,+format=(string)F32LE,+channels=(int)1")).AsTask();

                /**
                await connectTask.ContinueWith(_ =>
                 {
                     Task.Run(() => this.ReceiveMessageUsingStreamWebSocket());
                     //Task.Run(() => this.SendBytes(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 }));
                 });
    **/
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

        public async Task SendBytesAsync(byte[] message)
        {
            try
            {
                //using (var dataWriter = new DataWriter(this.streamSocket.OutputStream))
                //{
                    dataWriter.WriteBytes(message);
                    await dataWriter.StoreAsync();
                    //dataWriter.DetachStream();
                //}
                //Debug.WriteLine("Sending data using StreamWebSocket: " + message.Length.ToString() + " bytes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Send data error.");
                //Debug.WriteLine(ex.GetBaseException().HResult);
                //Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add code here to handle exceptions.
                //await ConnectToServer();

            }
        }
        public async void ReceiveMessageUsingStreamWebSocket()
        {
            try
            {
                uint length = 256;
                var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                var readOp = streamSocket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);
                readOp.Completed = (IAsyncOperationWithProgress<IBuffer, uint>
                    asyncAction, AsyncStatus asyncStatus) =>
                {
                    switch (asyncStatus)
                    {
                        case AsyncStatus.Completed:
                            try
                            {
                                // GetResults in AsyncStatus::Error is called as it throws a user friendly error string.
                                IBuffer localBuf = asyncAction.GetResults();
                                uint bytesRead = localBuf.Length;
                                DataReader readPacket = DataReader.FromBuffer(localBuf);
                                uint buffLen = readPacket.UnconsumedBufferLength;
                                if (buffLen == 0)
                                    return;
                                string message = readPacket.ReadString(buffLen);
                                Debug.WriteLine(message);
                            }
                            catch (Exception exp)
                            {
                                Debug.WriteLine("Read operation failed:  " + exp.Message);
                            }
                            break;
                        case AsyncStatus.Error:
                            
                            break;
                        case AsyncStatus.Canceled:

                            // Read is not cancelled in this sample.
                            break;
                    }
                };
            }
            catch (Exception exp)
            {
                Debug.WriteLine("failed to post a read failed with error:  " + exp.Message);
            }
            /**
            try
            {
                using (var dataReader = new DataReader(this.streamSocket.InputStream))
                {
                    dataReader.InputStreamOptions = InputStreamOptions.Partial;
                    await dataReader.LoadAsync(256);
                    byte[] message = new byte[dataReader.UnconsumedBufferLength];
                    dataReader.ReadBytes(message);
                    Debug.WriteLine("Data received from StreamWebSocket: " + message.ToString());
                }
                //this.streamSocket.Dispose();
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add code here to handle exceptions.
            }**/
        }


        private async void WebSocket_ClosedAsync(Windows.Networking.Sockets.IWebSocket sender, Windows.Networking.Sockets.WebSocketClosedEventArgs args)
        {
            rootPage.NotifyUser("Websocket connection is off, trying to reconnect...", NotifyType.ErrorMessage, 1);
            Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
            // Add additional code here to handle the WebSocket being closed.
        }
    }
}