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
using Newtonsoft.Json;
using Windows.UI.Core;

namespace PhenoPad.WebSocketService
{

    // JSON for parsing server status messages
    public class ServerStatus
    {
        public bool ready { get; set; }
        public List<string> waiting_for_server { get; set; }
        public List<string> waiting_for_client { get; set; }
    }

    public class ServerStatusRootObject
    {
        public ServerStatus server_status { get; set; }
    }
    //---------------

    // JSON for manager_id
    public class ManagerIDRootObject
    {
        public string manager_id { get; set; }
    }


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

        private string manager_id = null;

        public UIWebSocketClient()
        {
            Debug.WriteLine("Initializer does nothing");
        }

        public async Task<bool> ConnectToServer()
        {
            client = new MessageWebSocket();
            client.Closed += clientClosedHandler;
            client.MessageReceived += clientMessageReceivedHandler;

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

        public void disconnect()
        {
            this.client.Dispose();
            this.client = null;
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


        private async void clientMessageReceivedHandler(Windows.Networking.Sockets.MessageWebSocket sender, 
                                Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader dataReader = args.GetDataReader())
                {
                    dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    string message = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                    Debug.WriteLine("Message received from MessageWebSocket: " + message);

                    var parsed = this.tryParseUIMessage(message);

                    if (parsed.GetType() == typeof(ServerStatusRootObject))
                    {
                        await MainPage.Current.Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                        {
                            ServerStatusRootObject json = (ServerStatusRootObject)(parsed);
                            if (json.server_status != null)
                            {
                                if (json.server_status.waiting_for_server.Contains("ASR") == false)
                                {
                                    MainPage.Current.setStatus("recognition");
                                }
                                if (json.server_status.waiting_for_server.Contains("Diarization") == false)
                                {
                                    MainPage.Current.setStatus("diarization");
                                }
                                if (json.server_status.ready)
                                {
                                    MainPage.Current.setStatus("ready");
                                }
                            }
                        });
                    }
                    else if (parsed.GetType() == typeof(ManagerIDRootObject))
                    {
                        ManagerIDRootObject json = (ManagerIDRootObject)(parsed);
                        if (json.manager_id != null)
                        {
                            this.manager_id = json.manager_id;
                            BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("manager_id " + this.manager_id);
                            BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("server_ip " + UIWebSocketClient.serverAddress);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
                // Add additional code here to handle exceptions.
                Debug.WriteLine("Receve message failed because " + ex.Message);
            }
        }

        private object tryParseUIMessage(string message)
        {
            object result = null;
            if (message.Contains("server_status"))
            {
                try
                {
                    var parsedSpeech = JsonConvert.DeserializeObject<ServerStatusRootObject>(message);
                    result = parsedSpeech;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Message of length " + message.Length.ToString() + " cannot be parsed as ServerStatusRootObject");
                }
            }
            
            if (message.Contains("manager_id"))
            {
                try
                {
                    var parsedSpeech = JsonConvert.DeserializeObject<ManagerIDRootObject>(message);
                    result = parsedSpeech;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Message of length " + message.Length.ToString() + " cannot be parsed as ManagerIDRootObject");
                }
            }

            return result;
        }
    }
}
