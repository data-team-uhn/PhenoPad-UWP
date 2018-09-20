using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using System.Diagnostics;
using Windows.Devices.SerialCommunication;
using Windows.Networking.Connectivity;
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
        public string rpi_ipaddr = null;
        //private DataReader readPacket = null;
        private CancellationTokenSource cancellationSource;
        private RfcommDeviceService blueService = null;
        private BluetoothDevice bluetoothDevice = null;

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
            bool blueConnected = await this.checkConnection();
            if (!blueConnected)
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    rootPage.NotifyUser("Trying to connect Raspberry Pi through bluetooth.", NotifyType.StatusMessage, 3);
                });
                await InitiateConnection();
            }
            else
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    rootPage.NotifyUser("Bluetooth is now connected.", NotifyType.StatusMessage, 3);
                    rootPage.BluetoothProgresssBox.Text = "Connected to Raspberry Pi";
                    rootPage.bluetoonOn = true;
                });
            }
        }
        

        /// <summary>
        /// Initiate bluetooth connection with Raspberry Pi
        /// </summary>
        /// <returns></returns>
        private async Task InitiateConnection()
        {
            var serviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort), new string[] { "System.Devices.AepService.AepId" });

            //Debug.WriteLine(DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort)));

            foreach (var serviceInfo in serviceInfoCollection)
            {
                var deviceInfo = await DeviceInformation.CreateFromIdAsync((string)serviceInfo.Properties["System.Devices.AepService.AepId"]);

                //System.Diagnostics.Debug.WriteLine($"Device name is: '{deviceInfo.Name}' and Id is: '{deviceInfo.Id}'");
            }


            // Request additional properties
            string[] requestedProperties = new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")",
                                                            requestedProperties,
                                                            DeviceInformationKind.AssociationEndpoint);

            // Hook up handlers for the watcher events before starting the watcher
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, deviceInfo) =>
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High, async() =>
                {
                    // Make sure device name isn't blank
                    if (deviceInfo.Name == "raspberrypi")
                    {
                        DeviceAccessStatus accessStatus = DeviceAccessInformation.CreateFromId(deviceInfo.Id).CurrentStatus;
                        if (accessStatus == DeviceAccessStatus.DeniedByUser)
                        {
                            rootPage.NotifyUser("This app does not have access to connect to the remote device (please grant access in Settings > Privacy > Other Devices", NotifyType.ErrorMessage, 3);
                            return;
                        }
                        try
                        {
                            bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                            if (bluetoothDevice == null)
                            {
                                rootPage.NotifyUser("Bluetooth Device returned null. Access Status = " + accessStatus.ToString(), NotifyType.ErrorMessage, 3);
                                return;
                            }


                            var rfcommServices = await bluetoothDevice.GetRfcommServicesForIdAsync(
                  RfcommServiceId.FromUuid(Constants.RfcommChatServiceUuid), BluetoothCacheMode.Uncached);
                            var attempNum = 1;
                            for (; attempNum <= 5; attempNum++)
                            {
                                if (rfcommServices.Services.Count > 0)
                                {
                                    blueService = rfcommServices.Services[0];
                                    break;
                                }
                                else
                                {
                                    rootPage.NotifyUser(
                                       "Could not discover Bluetooh server on the remote device, trying again...",
                                       NotifyType.StatusMessage, 1);
                                    await Task.Delay(1000);
                                }
                            }
                            if (attempNum == 6)
                            {
                                rootPage.NotifyUser(
                                       "Could not discover Bluetooh server on the remote device",
                                       NotifyType.ErrorMessage, 2);
                                rootPage.serverConnectButton.IsEnabled = true;
                                rootPage.BluetoothProgresssBox.Text = "Please try again soon";
                                rootPage.BluetoothProgress.IsActive = false;
                                rootPage.BluetoothComplete.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                                rootPage.bluetoonOn = false;
                                return;
                            }

                            

                            StopWatcher();

                            lock (this)
                            {
                                _socket = new StreamSocket();
                            }
                            try
                            {
                                await _socket.ConnectAsync(blueService.ConnectionHostName, blueService.ConnectionServiceName);
                               
                                //SetChatUI(attributeReader.ReadString(serviceNameLength), bluetoothDevice.Name);
                                dataWriter = new DataWriter(_socket.OutputStream);
                                //readPacket = new DataReader(_socket.InputStream);
                            }
                            //catch (Exception ex) when ((uint)ex.HResult == 0x80070490) // ERROR_ELEMENT_NOT_FOUND
                            //{

                            //}
                            catch (Exception ex) // ERROR_ELEMENT_NOT_FOUND
                            {
                                LogService.MetroLogger.getSharedLogger().Error(ex.Message);

                            }
                            // send hand shake
                            try
                            {
                                string temp = "HAND_SHAKE";

                                //dataWriter.WriteUInt32((uint)temp.Length);
                                dataWriter.WriteString(temp);
                                Debug.WriteLine("Writing message " + temp.ToString() + " via Bluetooth");
                                await dataWriter.StoreAsync();

                                this.initialized = true;

                                rootPage.NotifyUser(
                                   "Connected to Raspberry Pi",
                                   NotifyType.StatusMessage, 2);
                                rootPage.bluetoonOn = true;
                                rootPage.bluetoothInitialized(true);
                                rootPage.serverConnectButton.IsEnabled = true;
                            }
                            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
                            {
                                rootPage.NotifyUser(
                                   "The remote device has disconnected.",
                                   NotifyType.StatusMessage, 2);
                                LogService.MetroLogger.getSharedLogger().Info("The remote device has disconnected.");
                                return;
                            }

                            // keep receiving data 
                            if(cancellationSource != null)
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
                                        rootPage.NotifyUser(result, NotifyType.StatusMessage, 3);
                                        string[] temp = result.Split(' ');
                                        if (temp[0] == "ip")
                                        {
                                            rpi_ipaddr = temp[1];
                                        }
                                    }

                                }
                            }, cancellationToken);
                            
                        }
                        catch (Exception e)
                        {
                            rootPage.NotifyUser(e.Message, NotifyType.ErrorMessage, 3);
                            return;
                        }
                        
                        //ResultCollection.Add(new RfcommChatDeviceDisplay(deviceInfo));
                        
                        /*rootPage.NotifyUser(
                            String.Format("{0} devices found.", ResultCollection.Count),
                            NotifyType.StatusMessage);
                        */
                    }
                });

            });

            deviceWatcher.Updated += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, deviceInfoUpdate) =>
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    /**
                    foreach (RfcommChatDeviceDisplay rfcommInfoDisp in ResultCollection)
                    {
                        if (rfcommInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            rfcommInfoDisp.Update(deviceInfoUpdate);
                            break;
                        }
                    }**/
                });
            });

            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    /**
                    rootPage.NotifyUser(
                        String.Format("{0} devices found. Enumeration completed. Watching for updates...", ResultCollection.Count),
                        NotifyType.StatusMessage);**/
                });
            });

            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, deviceInfoUpdate) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // Find the corresponding DeviceInformation in the collection and remove it
                    /**
                    foreach (RfcommChatDeviceDisplay rfcommInfoDisp in ResultCollection)
                    {
                        if (rfcommInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            ResultCollection.Remove(rfcommInfoDisp);
                            break;
                        }
                    }

                    rootPage.NotifyUser(
                        String.Format("{0} devices found.", ResultCollection.Count),
                        NotifyType.StatusMessage);
    **/
                });
            });

            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    //ResultCollection.Clear();
                });
            });

            deviceWatcher.Start();
        }

        public string GetPiIP()
        {
            return this.rpi_ipaddr;
        }

        public async Task<bool> checkConnection()
        {
            try
            {
                string temp = "are you alive?";
                //dataWriter.WriteUInt32((uint)temp.Length);
                dataWriter.WriteString(temp);

                LogService.MetroLogger.getSharedLogger().Info("Checking whether the connection is still alive");

                await dataWriter.StoreAsync();
                return true;
            }
            catch (Exception)
            {
                // The remote device has disconnected the connection
                this.CloseConnection();
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
            }
            catch (Exception exp)
            {
                Debug.WriteLine("failed to post a read failed with error:  " + exp.Message);
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
            //try
            //{
            //    await this._socket.CancelIOAsync();
            //    this.cancellationSource.Cancel();
            //    return true;
            //}
            //catch (Exception e)
            //{

            //}
            try
            {
                this.cancellationSource.Cancel();
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
                
                return true;

            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to close bluetooth connection:{e}:{e.Message}");
                return false;
            }

        }

        public async Task sendBluetoothMessage(string message)
        {

            if (this.initialized == false)
            {
                await this.InitiateConnection();

                if (this.initialized == false)
                {
                    rootPage.NotifyUser(
                    "Unable to re-initialize Bluetooth device (should be raspberry pi)",
                    NotifyType.StatusMessage, 2);

                    // Later command should attemp to re-initialize
                    this.initialized = false;
                    return;
                }
            }


            try
            {

                dataWriter.WriteString(message);
                Debug.WriteLine("Writing message " + message + " via Bluetooth");
                await dataWriter.StoreAsync();

                //rootPage.NotifyUser(
                //            "Sending message \"" + message + "\"",
                //            NotifyType.StatusMessage, 1);
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
