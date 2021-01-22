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
        /// Gets an BluetoothService object.
        /// </summary>
        /// <remarks>
        /// Returns the shared Bluetooth service object, initialize a new one if it does not exist.
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
        /// If Bluetooth not connected, intiate new connection; otherwise, update t
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
                        // only one thread allowed to initlize at a time
                        await MainPage.Current.InitBTConnectionSemaphore.WaitAsync();
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
            // NOTE: this function does a lot of things. Might be better to separate some of the code into other functions?
            if (initialized)
            {
                return;
            }
            var serviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort), new string[] { "System.Devices.AepService.AepId" });
            
            #region RFCOMMService Loop
            // Identify the target RFCOMM device (Raspberry Pi) and establish Bluetooth connection with the device.
            foreach (var serviceInfo in serviceInfoCollection)
            {
                var deviceInfo = await DeviceInformation.CreateFromIdAsync((string)serviceInfo.Properties["System.Devices.AepService.AepId"]);
                // NOTE: this might cause problem if there are more than 1 device with the name "raspberrypi", 
                //       since the rfcomm service iterate through all devices and runs connection process for each one that meets deviceInfo.Name == "raspberrypi"
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
                            // stop if attempted 6 times and could not find available rfcommService
                            // NOTE: 6 times seems a bit excessive, is it better to reduce # of attempts?
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
            #endregion

            #region Socket Connection
            // Establish Bluetooth connection on a stream socket.
            //lock (this)
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
            

            // Send hand shake message to Raspberry Pi through socket to confirm connection.
            try
            {
                string temp = "HAND_SHAKE";
                dataWriter.WriteString(temp);
                Debug.WriteLine("Writing message " + temp.ToString() + " via Bluetooth");
                await dataWriter.StoreAsync();
                initialized = true;
                MainPage.Current.SetBTUIOnInit(initialized);
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
            {
                //remote side disconnect
                LogService.MetroLogger.getSharedLogger().Info(ex.Message);
                return;
            }
            #endregion

            #region RPI Message Loop
            // Handles message from Raspberry Pi (e.g. in the case of an audio service Error).
            // TODO: Learn more about cancellationSource
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
            #endregion

            // Request additional properties
            string[] requestedProperties = new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")", requestedProperties, DeviceInformationKind.AssociationEndpoint);
            // Hook up handlers for the watcher events before starting the watcher
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, deviceInfo) =>
            {
                if (initialized)
                {
                    return;
                }
            });
            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, object>((watcher, deviceInfo) => 
            {
                //Debug.WriteLine("device watcher stopped");
            });
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>((watcher, deviceInfo) => 
            {
                //Debug.WriteLine("device watcher removed");
            });
            deviceWatcher.Start();           
        }

        public async void HandleAudioException(string message)
        {
            /// <summary>
            /// Handles audio service errors caused by crashes on Raspberry Pi side.
            /// </summary>

            // If Raspberry Pi's audio client fails
            if (message.Equals(RESTART_AUDIO_FLAG) && MainPage.Current.speechManager.speechResultsSocket.streamSocket != null)
            {
                LogService.MetroLogger.getSharedLogger().Error("\nBluetoothService=> GOT EXCEPTION, will try to kill audio\n");
                await MainPage.Current.KillAudioService();
            }
            // If Raspberry Pi's Bluetooth server/control client fails
            else if (message.Equals(RESTART_BLUETOOTH_FLAG))
            {
                LogService.MetroLogger.getSharedLogger().Error("\nBluetoothService=> GOT DEXCEPTION, will try to restart bluetooth\n");
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
            /// <summary>
            /// Checks if Bluetooth connection is alive by sending a test message.
            /// </summary>
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
            catch (Exception e)
            {
                // The remote device has disconnected the connection
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
                CloseConnection();
                return false;
            }
        }

        
        public async Task<String> ReceiveMessageUsingStreamWebSocket()
        {
            /// <summary>
            /// Receive data from stream socket.
            /// </summary>

            string returnMessage = String.Empty;
            try
            {
                uint length = 100;     // Leave a large buffer.
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
                //sharedBluetoothService = null;
                return true;

            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to close bluetooth connection:{e}:{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add content here...
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<bool> sendBluetoothMessage(string message)
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
                    // Later command should attemp to re-initialize
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
                    NotifyType.StatusMessage, 2);
                // Later command should attemp to re-initialize
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
