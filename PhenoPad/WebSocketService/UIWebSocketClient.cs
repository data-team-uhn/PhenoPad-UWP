using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;

using System.Diagnostics;

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

        // WARNING! server address changes every time
        private static string serverAddress = "phenopad.ccm.sickkids.ca";
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
        }

        /// <summary>
        /// Tries to connect to speech engine 
        /// </summary>
        public async Task<bool> ConnectToServer()
        {
            client = new MessageWebSocket();
            client.Closed += clientClosedHandler;
            client.MessageReceived += clientMessageReceivedHandler;

            try
            {
                Uri url = new Uri(getUI_URI());
                LogService.MetroLogger.getSharedLogger().Info($"Connecting to: {url.ToString()}...");
                Task connectTask = client.ConnectAsync(url).AsTask();

                await connectTask;
                this.dataWriter = new DataWriter(client.OutputStream);
                return true;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"UIWebSocket:{ex.Message}");
                client.Dispose();
                client = null;
                return false;
            }
        }

        public void disconnect()
        {
            if (this.client != null)
            {
                this.client.Dispose();
            }
            this.client = null;
        }

        private void clientClosedHandler(Windows.Networking.Sockets.IWebSocket sender, 
                                                Windows.Networking.Sockets.WebSocketClosedEventArgs args)
        {
            LogService.MetroLogger.getSharedLogger().Info("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
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
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("Send data error.");
                MainPage.Current.NotifyUser("Error sending data to speech engine", NotifyType.ErrorMessage, 2);

                await Task.Delay(2);

                return false;
            }
        }


        private async void clientMessageReceivedHandler(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
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
                                }
                                if (json.server_status.waiting_for_server.Contains("Diarization") == false)
                                {
                                }
                                if (json.server_status.ready)
                                {
                                }
                            }
                        });
                    }
                    else if (parsed.GetType() == typeof(ManagerIDRootObject))
                    {
                        ManagerIDRootObject json = (ManagerIDRootObject)(parsed);
                        if (json.manager_id != null)
                        {
                            manager_id = json.manager_id;
                            await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("manager_id " + this.manager_id);
                            await BluetoothService.BluetoothService.getBluetoothService().sendBluetoothMessage("server_ip " + serverAddress);
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
                catch (Exception e)
                {
                    Debug.WriteLine("Message of length " + message.Length.ToString() + " cannot be parsed as ServerStatusRootObject");
                    LogService.MetroLogger.getSharedLogger().Error("tryParseUIMessage:" + e.Message);
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
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine("Message of length " + message.Length.ToString() + " cannot be parsed as ManagerIDRootObject");
                }
            }
            return result;
        }
    }
}
