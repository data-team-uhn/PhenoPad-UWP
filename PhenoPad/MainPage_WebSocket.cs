using System;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using PhenoPad.PhenotypeService;
using Windows.UI.Xaml.Input;
using System.Collections.Generic;
using Windows.UI.Popups;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using System.Text;
using Windows.Globalization;
using PhenoPad.SpeechService;
using Windows.Devices.Sensors;
using Windows.UI;
using System.Diagnostics;
using PhenoPad.CustomControl;
using Windows.Graphics.Display;
using System.Reflection;
using System.Linq;
using Windows.UI.Xaml.Media.Animation;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PhenoPad.WebSocketService;
using Windows.ApplicationModel.Core;
using PhenoPad.Styles;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using PhenoPad.FileService;
using Windows.UI.Xaml.Data;
using System.ComponentModel;
using System.Threading;

using PhenoPad.BluetoothService;
using Windows.System.Threading;
using System.IO;
using Windows.Storage;
using Windows.Media.Editing;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PhenoPad
{
    // This class contains methods regarding web socket usage for MainPage.
    public sealed partial class MainPage : Page
    {
        // Web socket properties
        private Windows.Networking.Sockets.MessageWebSocket videoStreamWebSocket;
        CancellationTokenSource videoCancellationSource = new CancellationTokenSource();
        CancellationToken videoStreamCancellationToken;
        private List<Image> videoFrameImages = new List<Image>();
        private StorageFile videoFile;

        private BitmapImage latestImageFromStream = new BitmapImage();
        private string latestImageString = "";

        /// <summary>
        /// Processes the message received through web socket
        /// </summary>
        private async void WebSocket_MessageReceived(Windows.Networking.Sockets.MessageWebSocket sender, Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                {
                    using (DataReader dataReader = args.GetDataReader())
                    {
                        dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                        string message = dataReader.ReadString(dataReader.UnconsumedBufferLength);

                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                        async () =>
                        {
                            latestImageString = message;
                            latestImageFromStream = await HelperFunctions.Base64ToBitmapAsync(message);
                            StreamImageView.Source = latestImageFromStream;
                        }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
            }
        }

        /// <summary>
        /// Sends the string message to video output stream using MessageWebSocket
        /// </summary>
        private async Task SendMessageUsingMessageWebSocketAsync(string message)
        {
            using (var dataWriter = new DataWriter(this.videoStreamWebSocket.OutputStream))
            {
                dataWriter.WriteString(message);
                await dataWriter.StoreAsync();
                dataWriter.DetachStream();
            }
            Debug.WriteLine("Sending message using MessageWebSocket: " + message);
        }

        /// <summary>
        /// Sends the byte[] message to video output stream using StreamWebSocket
        /// </summary>        
        private async void SendMessageUsingStreamWebSocket(byte[] message)
        {
            try
            {
                using (var dataWriter = new DataWriter(this.videoStreamWebSocket.OutputStream))
                {
                    dataWriter.WriteBytes(message);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
                Debug.WriteLine("Sending data using StreamWebSocket: " + message.Length.ToString() + " bytes");
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
            }
        }

        /// <summary>
        /// Handles event when WebSocket is closed.
        /// </summary>
        private void WebSocket_Closed(Windows.Networking.Sockets.IWebSocket sender, Windows.Networking.Sockets.WebSocketClosedEventArgs args)
        {
            Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
        }
    }
}
