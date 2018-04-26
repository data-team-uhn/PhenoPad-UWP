using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;

using System.Diagnostics;

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
    public class UIWebSocketClient
    {
        private static UIWebSocketClient sharedUIWebSocketClient = new UIWebSocketClient();

        public static UIWebSocketClient getSharedUIWebSocketClient()
        {
            if (sharedUIWebSocketClient == null)
            {
                sharedUIWebSocketClient = new UIWebSocketClient();
                return sharedUIWebSocketClient;
            }
            else
            {
                return sharedUIWebSocketClient;
            }
        }

        // !!WARNING !! server address changes every time
        private static string serverAddress = "speechengine.ccm.sickkids.ca";
        private static string serverPort = "8888";

        public static string getUI_URI()
        {
            string uri_string = "ws://" + UIWebSocketClient.serverAddress + ":" + UIWebSocketClient.serverPort + "/ui";
            return uri_string;
        }

        private MessageWebSocket client = null;
        private DataWriter dataWriter;
        private DataReader dataReader;

        public UIWebSocketClient()
        {
            client = new MessageWebSocket();
            client.Closed += clientClosedHandler;
            client.MessageReceived += clientMessageReceivedHandler;
        }


        public async Task<bool> ConnectToServer()
        {
            try
            {
                Task connectTask = client.ConnectAsync(new Uri(getUI_URI())).AsTask();
                MainPage.Current.NotifyUser("Connecting to speech engine, please wait ...", NotifyType.StatusMessage, 2);

                await connectTask;
                this.dataWriter = new DataWriter(client.OutputStream);
                return true;
            }
            catch (Exception ex)
            {
                client.Dispose();
                client = null;
                return false;
            }
        }

        private async void clientClosedHandler(Windows.Networking.Sockets.IWebSocket sender, 
                                                Windows.Networking.Sockets.WebSocketClosedEventArgs args)
        {

            MainPage.Current.NotifyUser("Websocket connection is closed. Please try to reconnect.", NotifyType.ErrorMessage, 1);
            Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
            // Add additional code here to handle the WebSocket being closed.
        }


        // Our current UI client should only send STRING messages to server
        public async Task<bool> SendStringAsync(string message)
        {
            if (client == null)
            {
                return false;
            }
            try
            {
                dataWriter.WriteString(message);
                await dataWriter.StoreAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Send data error.");
                MainPage.Current.NotifyUser("Error sending data to speech engine", NotifyType.ErrorMessage, 2);

                await Task.Delay(2);

                return false;
            }
        }


        private void clientMessageReceivedHandler(Windows.Networking.Sockets.MessageWebSocket sender, 
                                Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader dataReader = args.GetDataReader())
                {
                    dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    string message = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                    Debug.WriteLine("Message received from MessageWebSocket: " + message);
                    this.client.Dispose();
                }
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add additional code here to handle exceptions.
                Debug.WriteLine("Receve message failed because " + ex.Message);
            }
        }
    }
}
