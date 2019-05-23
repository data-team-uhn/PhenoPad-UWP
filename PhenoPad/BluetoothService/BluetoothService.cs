using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using System.Diagnostics;
using System.Threading;

namespace PhenoPad.BluetoothService
{
    public class BluetoothService
    {
        // A pointer back to the main page is required to display status messages.
        private MainPage rootPage = MainPage.Current;
        RfcommDeviceService _service = null;
        StreamSocket _socket = null;
        private DeviceWatcher deviceWatcher = null;
        private DataWriter dataWriter = null;
        private DataReader dataReader = null;
        public string rpi_ipaddr = null;
        //private DataReader readPacket = null;
        private CancellationTokenSource cancellationSource;
        private RfcommDeviceService blueService = null;
        private BluetoothDevice bluetoothDevice = null;
        public static string RESTART_AUDIO_FLAG = "EXCEPTION";
        public static string RESTART_BLUETOOTH_FLAG = "DEXCEPTION";


        public bool initialized = false;

        public static BluetoothService sharedBluetoothService;

        public static BluetoothService getBluetoothService()
        {
            if (sharedBluetoothService == null)
            {
                sharedBluetoothService = new BluetoothService();
                return sharedBluetoothService;
            }
            else
            {
                return sharedBluetoothService;
            }
        }

        public async Task Initialize()
        {
            rootPage = MainPage.Current;
            bool blueConnected = await checkConnection();
            if (!blueConnected)
            {
                rootPage.bluetoothprogress.Visibility = Windows.UI.Xaml.Visibility.Visible;
                rootPage.bluetoothicon.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>{
                    while (!initialized)
                    {
                        Debug.WriteLine("Trying Bluetooth connection ...");
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        await InitiateConnection();
                    }
                    rootPage.NotifyUser("Bluetooth is now connected.", NotifyType.StatusMessage, 2);
                });
            }
            else
            {
                rootPage.NotifyUser("Bluetooth is connected.", NotifyType.StatusMessage, 2);
                rootPage.bluetoonOn = true;
            }
            return;
        }

        /// <summary>
        /// Initiate bluetooth connection with Raspberry Pi
        /// </summary>
        private async Task InitiateConnection()
        {
            if (initialized)
                return;

            var serviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort), new string[] { "System.Devices.AepService.AepId" });
            //Debug.WriteLine("line 71");
            foreach (var serviceInfo in serviceInfoCollection)
            {
                var deviceInfo = await DeviceInformation.CreateFromIdAsync((string)serviceInfo.Properties["System.Devices.AepService.AepId"]);
                if (deviceInfo.Name == "raspberrypi")
                {
                    DeviceAccessStatus accessStatus = DeviceAccessInformation.CreateFromId(deviceInfo.Id).CurrentStatus;
                    if (accessStatus == DeviceAccessStatus.DeniedByUser)
                    {
                        rootPage.NotifyUser("Remote device access denied", NotifyType.ErrorMessage, 3);
                        LogService.MetroLogger.getSharedLogger().Error("This app does not have access to connect to the remote device (please grant access in Settings > Privacy > Other Devices");
                        return;
                    }
                    try
                    {
                        bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                        if (bluetoothDevice == null)
                        {
                            LogService.MetroLogger.getSharedLogger().Error("Bluetooth Device returned null. Access Status = " + accessStatus.ToString());
                            return;
                        }
                        var attempNum = 1;
                        for (; attempNum <= 5; attempNum++)
                        {
                            var rfcommServices = await bluetoothDevice.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(Constants.RfcommChatServiceUuid), BluetoothCacheMode.Uncached);

                            if (rfcommServices.Services.Count > 0)
                            {
                                LogService.MetroLogger.getSharedLogger().Info("Found rfcommService.");
                                blueService = rfcommServices.Services[0];
                                break;
                            }
                            else
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1));
                            }
                        }
                        if (attempNum == 6)
                        {
                            //LogService.MetroLogger.getSharedLogger().Error("Bluetooth connection attempt exceeded 5, trying again later ...");
                            rootPage.bluetoonOn = false;
                            return;
                        }

                    }
                    catch (Exception e)
                    {
                        LogService.MetroLogger.getSharedLogger().Error("Bluetooth device: " + e + e.Message);
                        return;
                    }
                }
            }

                    //========================================
                    //StopWatcher();
            lock (this)
            {
                _socket = new StreamSocket();
            }
            try
            {
                await _socket.ConnectAsync(blueService.ConnectionHostName, blueService.ConnectionServiceName);
                dataWriter = new DataWriter(_socket.OutputStream);
            }
            catch (Exception ex) // ERROR_ELEMENT_NOT_FOUND
            {
                LogService.MetroLogger.getSharedLogger().Error($"BluetoothService at line 141:{ex},{ex.Message}");
                return;
            }
            // send hand shake
            try
            {
                string temp = "HAND_SHAKE";
                //dataWriter.WriteUInt32((uint)temp.Length);
                dataWriter.WriteString(temp);
                Debug.WriteLine("Writing message " + temp.ToString() + " via Bluetooth");
                await dataWriter.StoreAsync();
                initialized = true;
                rootPage.NotifyUser("Bluetooth Connected", NotifyType.StatusMessage, 1);
                rootPage.bluetoonOn = true;
                rootPage.bluetoothInitialized(true);
                rootPage.bluetoothicon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                rootPage.bluetoothprogress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
            {
                //remote side disconnect
                LogService.MetroLogger.getSharedLogger().Info(ex.Message);
                return;
            }

            // keep receiving data 
            if (cancellationSource != null)
                cancellationSource.Cancel();
            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;
            await Task.Run(async () =>
            {
                while (true && !cancellationToken.IsCancellationRequested)
                {
                    // don't run again for 
                    await Task.Delay(500);
                    string result = await ReceiveMessageUsingStreamWebSocket();
                    if (!string.IsNullOrEmpty(result))
                    {
                        //LogService.MetroLogger.getSharedLogger().Info(result);
                        string[] temp = result.Split(' ');
                        if (temp[0] == "ip")
                            rpi_ipaddr = temp[1];
                    }
                }
            }, cancellationToken);
                            

            //Debug.WriteLine("line 75");
            // Request additional properties
            string[] requestedProperties = new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")", requestedProperties, DeviceInformationKind.AssociationEndpoint);
            // Hook up handlers for the watcher events before starting the watcher
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, deviceInfo) =>
            {
                if (initialized)
                    return;
                //await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                //{
                //    // Make sure device name isn't blank
                //    if (deviceInfo.Name == "raspberrypi")
                //    {
                //        DeviceAccessStatus accessStatus = DeviceAccessInformation.CreateFromId(deviceInfo.Id).CurrentStatus;
                //        if (accessStatus == DeviceAccessStatus.DeniedByUser)
                //        {
                //            rootPage.NotifyUser("This app does not have access to connect to the remote device (please grant access in Settings > Privacy > Other Devices", NotifyType.ErrorMessage, 3);
                //            return;
                //        }
                //        try
                //        {
                //            bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                //            if (bluetoothDevice == null)
                //            {
                //                LogService.MetroLogger.getSharedLogger().Error("Bluetooth Device returned null. Access Status = " + accessStatus.ToString());
                //                return;
                //            }
                //            var attempNum = 1;
                //            for (; attempNum <= 5; attempNum++)
                //            {
                //                var rfcommServices = await bluetoothDevice.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(Constants.RfcommChatServiceUuid), BluetoothCacheMode.Uncached);

                //                if (rfcommServices.Services.Count > 0)
                //                {
                //                    LogService.MetroLogger.getSharedLogger().Info("Found rfcommService.");
                //                    blueService = rfcommServices.Services[0];
                //                    break;
                //                }
                //                else
                //                {
                //                    //LogService.MetroLogger.getSharedLogger().Info("Could not discover Bluetooh server on the remote device, trying again...");
                //                    await Task.Delay(TimeSpan.FromSeconds(3));
                //                }
                //            }
                //            if (attempNum == 6)
                //            {
                //                //LogService.MetroLogger.getSharedLogger().Error("Bluetooth connection attempt exceeded 5, trying again later ...");
                //                rootPage.audioButton.IsEnabled = true;
                //                rootPage.audioButton.IsChecked = false;
                //                rootPage.bluetoonOn = false;
                //                return;
                //            }


                //            //========================================
                //            StopWatcher();
                //            lock (this)
                //            {
                //                _socket = new StreamSocket();
                //            }
                //            try
                //            {
                //                await _socket.ConnectAsync(blueService.ConnectionHostName, blueService.ConnectionServiceName);
                //                //SetChatUI(attributeReader.ReadString(serviceNameLength), bluetoothDevice.Name);
                //                dataWriter = new DataWriter(_socket.OutputStream);
                //                //readPacket = new DataReader(_socket.InputStream);
                //            }
                //            catch (Exception ex) // ERROR_ELEMENT_NOT_FOUND
                //            {
                //                LogService.MetroLogger.getSharedLogger().Error("in device added" + ex.Message);
                //            }
                //            // send hand shake
                //            try
                //            {
                //                string temp = "HAND_SHAKE";
                //                //dataWriter.WriteUInt32((uint)temp.Length);
                //                dataWriter.WriteString(temp);
                //                Debug.WriteLine("\nWriting message " + temp.ToString() + " via Bluetooth\n");
                //                await dataWriter.StoreAsync();
                //                this.initialized = true;
                //                rootPage.NotifyUser("Bluetooth Connected", NotifyType.StatusMessage, 1);
                //                rootPage.bluetoonOn = true;
                //                rootPage.bluetoothInitialized(true);
                //                rootPage.bluetoothicon.Visibility = Windows.UI.Xaml.Visibility.Visible;
                //                rootPage.bluetoothprogress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                //                //rootPage.StartAudioAfterBluetooth();
                //            }
                //            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
                //            {
                //                //remote side disconnect
                //                LogService.MetroLogger.getSharedLogger().Info(ex.Message);
                //                return;
                //            }

                //            // keep receiving data 
                //            if (cancellationSource != null)
                //                cancellationSource.Cancel();
                //            cancellationSource = new CancellationTokenSource();
                //            CancellationToken cancellationToken = cancellationSource.Token;

                //            await Task.Run(async () =>
                //            {
                //                while (true && !cancellationToken.IsCancellationRequested)
                //                {
                //                    // don't run again for 
                //                    await Task.Delay(500);
                //                    string result = await ReceiveMessageUsingStreamWebSocket();
                //                    if (!string.IsNullOrEmpty(result))
                //                    {
                //                        LogService.MetroLogger.getSharedLogger().Info(result);
                //                        string[] temp = result.Split(' ');
                //                        if (temp[0] == "ip")
                //                            rpi_ipaddr = temp[1];
                //                    }

                //                }
                //            }, cancellationToken);
                //        }
                //        catch (Exception e)
                //        {
                //            LogService.MetroLogger.getSharedLogger().Error(e.Message);
                //            return;
                //        }
                //    }
                //});

            });
            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, object>((watcher, deviceInfo) => {
                //Debug.WriteLine("device watcher stopped");
            });
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>((watcher, deviceInfo) => {
                //Debug.WriteLine("device watcher removed");
            });
            deviceWatcher.Start();           
        }

        public async void HandleAudioException(string message) {
            if (message.Equals(RESTART_AUDIO_FLAG))
            {
                LogService.MetroLogger.getSharedLogger().Error("BluetoothService=> GOT EXCEPTION, will try to restart audio");
                await MainPage.Current.RestartAudioOnException();
                Debug.WriteLine("BluetoothService=> after line 344");
            }
            else if (message.Equals(RESTART_BLUETOOTH_FLAG)) {
                LogService.MetroLogger.getSharedLogger().Error("BluetoothService=> GOT DEXCEPTION, will try to restart bluetooth");
                MainPage.Current.RestartBTOnException();
            }
            return;
        }

        public string GetPiIP()
        {
            return this.rpi_ipaddr;
        }

        public async Task<bool> checkConnection()
        {
            try
            {
                //dataWriter.WriteUInt32((uint)temp.Length);
                if (dataWriter != null) {
                    LogService.MetroLogger.getSharedLogger().Info("Checking whether the connection is still alive...");
                    string temp = "are you alive?";
                    dataWriter.WriteString(temp);
                    await dataWriter.StoreAsync();
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                // The remote device has disconnected the connection
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// receive data from stream socket
        /// </summary>
        public async Task<String> ReceiveMessageUsingStreamWebSocket()
        {
            string returnMessage = String.Empty;
            try
            {
                uint length = 100;     // Leave a large buffer

                var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                var readOp = await this._socket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);

                //await readOp;   // Don't move on until we have finished reading from server

                DataReader readPacket = DataReader.FromBuffer(readBuf);
                uint buffLen = readPacket.UnconsumedBufferLength;
                returnMessage = readPacket.ReadString(buffLen);
                if (returnMessage.Length > 0)
                    Debug.WriteLine($"BLUETOOTH RETURN MESSAGE=>{returnMessage}\n");
                HandleAudioException(returnMessage);
            }
            catch (Exception exp)
            {
                LogService.MetroLogger.getSharedLogger().Info("failed to post a read failed with error:  " + exp.Message);
            }

            return returnMessage;
        }

        private void StopWatcher()
        {
            if (null != deviceWatcher)
            {
                if ((DeviceWatcherStatus.Started == deviceWatcher.Status ||
                     DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status))
                {
                    deviceWatcher.Stop();
                }
                deviceWatcher = null;
            }
        }

        public bool CloseConnection()
        {
            try
            {
                if (this.cancellationSource != null)
                {
                    this.cancellationSource.Cancel();
                    this.cancellationSource = null;
                }
                if (dataWriter != null)
                {
                    dataWriter.DetachStream();
                    dataWriter = null;
                }


                if (_service != null)
                {
                    _service.Dispose();
                    _service = null;
                }
                lock (this)
                {
                    if (_socket != null)
                    {
                        _socket.Dispose();
                        _socket = null;
                    }
                }
                //StopWatcher();
                //sharedBluetoothService = null;
                sharedBluetoothService = null;
                blueService = null;
                return true;

            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to close bluetooth connection:{e}:{e.Message}");
                return false;
            }

        }

        public async Task sendBluetoothMessage(string message)
        {
            //if bluetooth connection is not establiched,
            //initializes the connection first
            if (this.initialized == false)
            {
                await this.InitiateConnection();
                if (this.initialized == false)
                {
                    rootPage.NotifyUser("Unable to re-initialize Bluetooth device (should be raspberry pi)",
                    NotifyType.StatusMessage, 2);
                    rootPage.ReEnableAudioButton();
                    // Later command should attemp to re-initialize
                    return;
                }
            }     
            try
            {
                dataWriter.WriteString(message);
                Debug.WriteLine("Writing message " + message + " via Bluetooth");
                await dataWriter.StoreAsync();              
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
            {
                rootPage.NotifyUser(
                    "Unable to send Bluetooth command to device " + bluetoothDevice.DeviceInformation.Name,
                    NotifyType.StatusMessage, 2);
                // Later command should attemp to re-initialize
                this.initialized = false;
                return;
            }
        }

    }

        /// <summary>
        /// Class containing Attributes and UUIDs that will populate the SDP record.
        /// </summary>
        class Constants
        {
        // The Chat Server's custom service Uuid: 34B1CF4D-1069-4AD6-89B6-E161D79BE4D8
        //public static readonly Guid RfcommChatServiceUuid = Guid.Parse("34B1CF4D-1069-4AD6-89B6-E161D79BE4D8");
        public static readonly Guid RfcommChatServiceUuid = Guid.Parse("94f39d29-7d6d-437d-973b-fba39e49d4ee");

        // The Id of the Service Name SDP attribute
        public const UInt16 SdpServiceNameAttributeId = 0x100;

            // The SDP Type of the Service Name SDP attribute.
            // The first byte in the SDP Attribute encodes the SDP Attribute Type as follows :
            //    -  the Attribute Type size in the least significant 3 bits,
            //    -  the SDP Attribute Type value in the most significant 5 bits.
            public const byte SdpServiceNameAttributeType = (4 << 3) | 5;

            // The value of the Service Name SDP attribute
            public const string SdpServiceName = "Bluetooth Rfcomm Chat Service";
        }
}
