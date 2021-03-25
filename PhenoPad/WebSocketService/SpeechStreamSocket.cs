﻿using System;
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
        //private string serverAddress = "phenopad.ccm.sickkids.ca";
        //private string serverPort = "8888";
        public string serverAddress = "137.135.117.253";
        public string serverPort = "8080";

        private uint ERROR_INTERNET_OPERATION_CANCELLED = 0x80072EFE;
        NetworkAdapter networkAdapter;
        public StreamWebSocket streamSocket;
        private DataWriter dataWriter;
        private DataReader dataReader;
        public bool isClosed;

        public SpeechStreamSocket()
        {
            // Socket constructor does nothing :D
        }
        public SpeechStreamSocket(string sAddress, string port)
        {
            this.serverAddress = sAddress;
            this.serverPort = port;
            this.isClosed = true;
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

        /// <summary>
        /// TODO...
        /// </summary>
        /// <returns></returns>
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
            try
            {
                Uri uri = new Uri("ws://" + this.serverAddress + ":" + this.serverPort +
                                            "/client/ws/speech?content-type=audio/x-raw," +
                                            "+layout=(string)interleaved," +
                                            "+rate=(int)16000," +
                                            "+format=(string)F32LE," +
                                            "+channels=(int)1," +
                                            "+audio_name=" + MainPage.Current.speechManager.GetAudioNameForServer());
                Debug.WriteLine(uri);
                Task connectTask = this.streamSocket.ConnectAsync(uri).AsTask();

                await connectTask;
                dataWriter = new DataWriter(this.streamSocket.OutputStream);

                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed connect to speech stream socket:" + e.Message);
                streamSocket.Dispose();
                streamSocket = null;
                isClosed = true;
                return false;
            }

        }

        /// <summary>
        /// Sends audio data to the speech server.
        /// </summary>
        /// <param name="message">the audio data to send to the server as a byte array</param>
        /// <returns></returns>
        public async Task<bool> SendBytesAsync(byte[] message)
        {
            
            if (streamSocket == null)   //don't send anything if the stream socket has nothing
                return false;
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
        /// TODO...
        /// </summary>
        /// <returns></returns>
        public async Task<String> SpeechStreamSocket_ReceiveMessage()
        {
            string returnMessage = "";
            try
            {
                if (streamSocket != null)
                {
                    uint length = 1000;     // Leave a large buffer

                    var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                    var readOp = await streamSocket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);
                    DataReader readPacket = DataReader.FromBuffer(readBuf);
                    uint buffLen = readPacket.UnconsumedBufferLength;
                    returnMessage = readPacket.ReadString(buffLen);

                }
                return returnMessage;
            }
            catch (Exception exp)
            {
                //This handles the case where we forse quit 
                if (exp.HResult == (int)ERROR_INTERNET_OPERATION_CANCELLED)
                {
                    LogService.MetroLogger.getSharedLogger().Info("ERROR_INTERNET_OPERATION_CANCELLED.");

                }
                else
                    LogService.MetroLogger.getSharedLogger().Error($"Speechstreamsocket: Issue receiving:{exp.Message}");
                return returnMessage;
            }
        }

        public async Task<bool> TrySendData()
        {
            try
            {
                Encoding ascii = Encoding.ASCII;
                dataWriter.WriteBytes(ascii.GetBytes("Hello"));
                await dataWriter.StoreAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// TODO...
        /// </summary>
        public async Task CloseConnnction()
        {
            /// <summary>
            /// Gets the current datas from stream socket and dispose for disconnection.
            /// </summary>
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
                LogService.MetroLogger.getSharedLogger().Error("Failed to close speech stream socket:" + ex.Message);
                //MainPage.Current.NotifyUser("Failed to close websocket.", NotifyType.ErrorMessage, 2);
            }
            finally
            {
                streamSocket.Dispose();
                streamSocket = null;
            }
        }
        /// <summary>
        /// Handles the event when the socket is closed by either client side or server side,
        /// and will dispose the socket if not null
        /// </summary>
        private void WebSocket_ClosedAsync(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            LogService.MetroLogger.getSharedLogger().Info(
                $"SpeechStreamSocket closed: Code = {args.Code}, Reason = {args.Reason}, will dispose current stream socket...");
            if (streamSocket != null)
            {
                streamSocket.Dispose();
                streamSocket = null;
            }
        }
    }
}

