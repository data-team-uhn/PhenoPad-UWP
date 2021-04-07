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
    /// <summary>
    /// A class that contains the properties and methods related to Bluetooth commmunication with the Raspberry Pi.
    /// </summary>
    public class BluetoothService
    {
        private MainPage rootPage = MainPage.Current; // A pointer back to the main page is required to display status messages.
        RfcommDeviceService _service = null;
        StreamSocket _socket = null;
        private DeviceWatcher deviceWatcher = null;
        private DataWriter dataWriter = null;
        private DataReader dataReader = null; // NOTE: this variable is never used
        public string rpi_ipaddr = null;
        //private DataReader readPacket = null;
        private CancellationTokenSource cancellationSource;
        private RfcommDeviceService blueService = null;
        private BluetoothDevice bluetoothDevice = null;
        public static string RESTART_AUDIO_FLAG = "EXCEPTION";
        public static string RESTART_BLUETOOTH_FLAG = "DEXCEPTION";
        public static BluetoothService sharedBluetoothService;

        public bool initialized = false;

        /// <summary>
        /// Gets the shared instance of the BluetoothService class.
        /// </summary>
        /// <remarks>
        /// Initialize a new static instance if one does not exist.
        /// </remarks>
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

        /// <summary>
        /// Initiate Bluetooth connection if connection does not exist.
        /// </summary>
        /// <remarks>
        /// Check if a BLuetooth connection is alive.
        /// If Bluetooth not connected, intiate new connection; otherwise, update UI element parameters.
        /// </remarks>
        public async Task Initialize()
        {
            rootPage = MainPage.Current;
            bool blueConnected = await checkConnection();
            if (!blueConnected)
            {
                rootPage.SetBTUIOnInit(blueConnected);
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    while (!initialized)
                    {
                        await MainPage.Current.InitBTConnectionSemaphore.WaitAsync(); // only one thread allowed to initlize at a time
                        await InitiateConnection();
                        MainPage.Current.InitBTConnectionSemaphore.Release();
                    }
                });
            }
            else
            {
                MainPage.Current.SetBTUIOnInit(initialized);
            }
            return;
        }

        /// <summary>
        /// Initiate bluetooth connection with Raspberry Pi 
        /// </summary>
        private async Task InitiateConnection()
        {   
            if (initialized)
            {
                return;
            }
            var serviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort), new string[] { "System.Devices.AepService.AepId" });
            
            /*---- Identify the target RFCOMM device (Raspberry Pi) and establish Bluetooth connection with it. ----*/
            foreach (var serviceInfo in serviceInfoCollection)
            {
                var deviceInfo = await DeviceInformation.CreateFromIdAsync((string)serviceInfo.Properties["System.Devices.AepService.AepId"]);

                /** NOTE: Note that this might cause problem if there are more than 1 device with the name "raspberrypi", 
                 *        since the rfcomm service iterate through all devices and runs connection process for each one 
                 *        that meets deviceInfo.Name == "raspberrypi".
                 **/       
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
                            LogService.MetroLogger.getSharedLogger().Error($"Bluetooth could not find Raspberry Pi. Access Status = " + accessStatus.ToString());
                            return;
                        }

                        var attempNum = 1;
                        for (; attempNum <= 6; attempNum++)
                        {
                            if (attempNum == 6)
                            {
                                return;
                            }

                            var rfcommServices = await bluetoothDevice.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(Constants.RfcommChatServiceUuid), BluetoothCacheMode.Uncached);

                            if (rfcommServices.Services.Count > 0 && blueService == null && _socket==null)
                            {
                                Debug.WriteLine("Found RFCommService");
                                blueService = rfcommServices.Services[0];
                                break;
                            }
                            else
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogService.MetroLogger.getSharedLogger().Error("Bluetooth device: " + e + e.Message);
                        return;
                    }
                }
            }

            /*---- Establish Bluetooth connection on the Bluetooth stream socket ----*/
            try
            {
                _socket = _socket == null ? new StreamSocket() : _socket;
                await _socket.ConnectAsync(blueService.ConnectionHostName, blueService.ConnectionServiceName);
                dataWriter = new DataWriter(_socket.OutputStream);
            }
            catch (Exception ex) // ERROR_ELEMENT_NOT_FOUND
            {
                LogService.MetroLogger.getSharedLogger().Error($"BluetoothService at line 141:{ex.Message}");
                initialized = false;                
                return;
            }
            
            /*---- Send hand shake message to Raspberry Pi to confirm connection ----*/
            try
            {
                string temp = "HAND_SHAKE";
                dataWriter.WriteString(temp);
                Debug.WriteLine("Writing message " + temp.ToString() + " via Bluetooth");
                await dataWriter.StoreAsync();
                initialized = true;
                MainPage.Current.SetBTUIOnInit(initialized);
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745) // remote side disconnect
            {
                LogService.MetroLogger.getSharedLogger().Info(ex.Message);
                return;
            }

            /*---- Handles message from Raspberry Pi ----*/
            // (e.g. in the case of an audio service Error).
            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
            }
            cancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationSource.Token;

            await Task.Run(async () =>
            {
                while (true && !cancellationToken.IsCancellationRequested)
                {
                    // don't run again for 
                    await Task.Delay(500);
                    string result = await ReceiveMessageUsingStreamWebSocket();
                    if (result == "CONNECTION_ERROR")
                    {
                        // Do nothing.
                    }
                    else if (!string.IsNullOrEmpty(result))
                    {
                        HandleAudioException(result);
                        string[] temp = result.Split(' ');
                        if (temp[0] == "ip")
                            rpi_ipaddr = temp[1];
                    }
                }
            }, cancellationToken);

            /*---- Request additional properties ----*/
            string[] requestedProperties = new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")", requestedProperties, DeviceInformationKind.AssociationEndpoint);
            
            /*---- Set up and start the watcher ----*/
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, deviceInfo) =>
            {
                if (initialized)
                {
                    return;
                }
            });
            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, object>((watcher, deviceInfo) => 
            {

            });
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>((watcher, deviceInfo) => 
            {

            });

            deviceWatcher.Start();
        }

        /// <summary>
        /// Handles audio service errors caused by crashes on Raspberry Pi side.
        /// </summary>
        /// <param name="message">
        /// The message received from Raspberry Pi.
        /// </param>
        public async void HandleAudioException(string message)
        {
            /*---- If Raspberry Pi's audio client fails ----*/
            if (message.Equals(RESTART_AUDIO_FLAG) && MainPage.Current.speechManager.speechResultsSocket.streamSocket != null)
            {
                LogService.MetroLogger.getSharedLogger().Error("\nBluetoothService=> GOT EXCEPTION, will try to kill audio\n");
                await MainPage.Current.KillAudioService();
            }
            /*---- If Raspberry Pi's Bluetooth server/control client fails ----*/
            else if (message.Equals(RESTART_BLUETOOTH_FLAG))
            {
                LogService.MetroLogger.getSharedLogger().Error("\nBluetoothService=> GOT DEXCEPTION, will try to restart bluetooth\n");
                MainPage.Current.RestartBTOnException();
            }

            return;
        }

        /// <summary>
        /// Get the IP address of the Raspberry Pi.
        /// </summary>
        /// <returns>
        /// A string representation of the Raspberry Pi's IP address.
        /// </returns>
        public string GetPiIP()
        {
            return this.rpi_ipaddr;
        }

        /// <summary>
        /// Checks if Bluetooth connection is alive by sending a test message.
        /// </summary>
        /// <returns>
        /// (bool)true if connection is still alive, (bool)false otherwise.
        /// </returns>
        public async Task<bool> checkConnection()
        {
            try
            {
                if (dataWriter != null)
                {
                    LogService.MetroLogger.getSharedLogger().Info("Checking whether the connection is still alive...");
                    string temp = "are you alive?";
                    dataWriter.WriteString(temp);
                    await dataWriter.StoreAsync();
                    return true;
                }
                return false;
            }
            catch (Exception e) // The remote device has disconnected the connection
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// Receive data from the Bluetooth stream socket.
        /// </summary>
        /// <returns>
        /// (string)Message read from the input stream. If failed to read a message, return (string)"CONNECTION_ERROR".
        /// </returns>
        public async Task<String> ReceiveMessageUsingStreamWebSocket()
        {
            string returnMessage = String.Empty;
            try
            {
                uint length = 100; // Leave a large buffer.
                var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                var readOp = await this._socket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial); // Don't move on until we have finished reading from server.
                DataReader readPacket = DataReader.FromBuffer(readBuf);
                uint buffLen = readPacket.UnconsumedBufferLength;
                returnMessage = readPacket.ReadString(buffLen);

                if (returnMessage.Length > 0)
                {
                    Debug.WriteLine($"BLUETOOTH RETURN MESSAGE=>{returnMessage}\n");
                }
            }
            catch (Exception exp)
            {
                LogService.MetroLogger.getSharedLogger().Info("failed to post a read failed with error:  " + exp.Message);
                return "CONNECTION_ERROR";
            }

            return returnMessage;
        }

        /// <summary>
        /// Stops the Bluetooth device watcher.
        /// </summary>
        private void StopWatcher()
        {
            if (null != deviceWatcher)
            {
                bool deviceWatcherStarted = (DeviceWatcherStatus.Started == deviceWatcher.Status);
                bool deviceWatcherEnumerationCompleted = (DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status);

                if (deviceWatcherStarted || deviceWatcherEnumerationCompleted)
                {
                    deviceWatcher.Stop();
                }
                deviceWatcher = null;
            }
        }

        /// <summary>
        /// Closes Bluetooth connection to the Raspberry Pi and clears related properties.
        /// </summary>
        /// <returns>
        /// (bool)true if the connection closed successfully, (bool)false otherwise.
        /// </returns>
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
                if (blueService != null)
                {
                    blueService.Dispose();
                    blueService = null;
                }

                lock (this)
                {
                    if (_socket != null)
                    {
                        _socket.Dispose();
                        _socket = null;
                    }
                }

                StopWatcher();
                initialized = false;

                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to close bluetooth connection:{e}:{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send message/command to Raspberry Pi.
        /// </summary>
        /// <param name="message">
        /// The message to be sent.
        /// </param>
        /// <returns>
        /// (bool)true if the message is successfully sent, (bool)false otherwise
        /// </returns>
        /// <remarks>
        /// If Bluetooth connection is not established, initializes the connection first.
        /// If message failed to send, notify user and update (bool)this.initialize to 
        /// signal the connection is down.
        /// </remarks>
        public async Task<bool> sendBluetoothMessage(string message)
        {
            /*---- Initialize the connection if Bluetooth connection has not been established ----*/
            if (this.initialized == false)
            {
                await this.InitiateConnection();
                if (this.initialized == false)
                {
                    rootPage.NotifyUser("Unable to re-initialize Bluetooth device (should be raspberry pi)",
                    NotifyType.StatusMessage, 2); // Later command should attemp to re-initialize

                    return false;
                }
            }

            try
            {
                dataWriter.WriteString(message);
                Debug.WriteLine("Writing message " + message + " via Bluetooth");
                await dataWriter.StoreAsync();
                return true;
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
            {
                rootPage.NotifyUser(
                    "Unable to send Bluetooth command to device " + bluetoothDevice.DeviceInformation.Name,
                    NotifyType.StatusMessage, 2); // Later command should attemp to re-initialize

                this.initialized = false; 
                return false;
            }
        }

    }

    /// <summary>
    /// Class containing Attributes and UUIDs that will populate the SDP record.
    /// </summary>
    class Constants
    {
        public static readonly Guid RfcommChatServiceUuid = Guid.Parse("94f39d29-7d6d-437d-973b-fba39e49d4ee");

        public const UInt16 SdpServiceNameAttributeId = 0x100; // The Id of the Service Name SDP attribute

        // The SDP Type of the Service Name SDP attribute.
        // The first byte in the SDP Attribute encodes the SDP Attribute Type as follows :
        //    -  the Attribute Type size in the least significant 3 bits,
        //    -  the SDP Attribute Type value in the most significant 5 bits.
        public const byte SdpServiceNameAttributeType = (4 << 3) | 5;

        // The value of the Service Name SDP attribute
        public const string SdpServiceName = "Bluetooth Rfcomm Chat Service";
    }
}
