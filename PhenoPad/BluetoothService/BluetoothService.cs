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

namespace PhenoPad.BluetoothService
{
    public class BluetoothService
    {
        // A pointer back to the main page is required to display status messages.
        private MainPage rootPage = MainPage.Current;

        RfcommDeviceService _service;
        StreamSocket _socket;
        private DeviceWatcher deviceWatcher = null;
        private DataWriter dataWriter = null;
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
            if (this.initialized == false)
            {
                await InitiateConnection();
            } else
            {
                Debug.WriteLine("Bluetooth has been initialized from another page");
                await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                {
                    // rootPage.bluetoothInitialized(true);
                });
            }
        }
        
        private async Task InitiateConnection()
        {
            var serviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort), new string[] { "System.Devices.AepService.AepId" });

            Debug.WriteLine(DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort)));

            foreach (var serviceInfo in serviceInfoCollection)
            {
                var deviceInfo = await DeviceInformation.CreateFromIdAsync((string)serviceInfo.Properties["System.Devices.AepService.AepId"]);

                System.Diagnostics.Debug.WriteLine($"Device name is: '{deviceInfo.Name}' and Id is: '{deviceInfo.Id}'");
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
                    Debug.WriteLine(deviceInfo.Name);

                    // Make sure device name isn't blank
                    if (deviceInfo.Name == "raspberrypi")
                    {
                        rootPage.NotifyUser(
                           "Found Raspberry Pi",
                           NotifyType.StatusMessage,
                           3);

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

                            if (rfcommServices.Services.Count > 0)
                            {
                                blueService = rfcommServices.Services[0];
                            }
                            else
                            {
                                rootPage.NotifyUser(
                                   "Could not discover Bluetooh server on the remote device",
                                   NotifyType.StatusMessage, 3);
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
                            }
                            catch (Exception ex) when ((uint)ex.HResult == 0x80070490) // ERROR_ELEMENT_NOT_FOUND
                            {
                            }
                            try
                            {
                                string temp = "HAND_SHAKE";

                                //dataWriter.WriteUInt32((uint)temp.Length);
                                dataWriter.WriteString(temp);
                                Debug.WriteLine("Writing message " + temp.ToString() + " via Bluetooth");
                                await dataWriter.StoreAsync();

                                this.initialized = true;

                                rootPage.NotifyUser(
                                   "Bluetooth connection has been established",
                                   NotifyType.StatusMessage, 2);
                                rootPage.bluetoothInitialized(true);
                            }
                            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
                            {
                                // The remote device has disconnected the connection
                                return;
                            }
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
