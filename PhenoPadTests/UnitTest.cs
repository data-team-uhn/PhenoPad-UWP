
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PhenoPadTests
{
    [TestClass]
    public class BluetoothServiceTests
    {
        //[Timeout(TestTimeout.Infinite)]
        [TestMethod]
        public void InitializeServiceTest()
        {
            //PhenoPad.MainPage pg = new PhenoPad.MainPage();

            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();
            
            // ensure we get bluetooth service
            Assert.IsNotNull(bluetoothService);

            // shouldn't be initialized at the beginning
            Assert.AreEqual(bluetoothService.initialized, false);



            //var task = bluetoothService.Initialize();
            //task.Wait();
            //var res = task.Result;
            Assert.AreEqual(1, 1);
            // initialize the service
            //System.Diagnostics.Debug.WriteLine("start");
            //var initializeTask = bluetoothService.Initialize();
            //Task.Run(InitializeTask);
            //Console.WriteLine("end");
            // check that the bluetooth service has been initialized
            //Assert.AreEqual(bluetoothService.initialized, true);

        }

        [TestMethod]
        public void CheckConnectionTest()
        {
            //PhenoPad.MainPage pg = new PhenoPad.MainPage();

            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();


            Assert.AreEqual(bluetoothService.initialized, false);
            bool res = bluetoothService.checkConnection().Result;
            Assert.AreEqual(res,false);

            //var connectRes = bluetoothService.InitiateConnection().Result;

            //bluetoothService
        }

        [TestMethod]
        public void SendMessageTest()
        {

            //PhenoPad.MainPage pg = new PhenoPad.MainPage();

            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();


            Assert.AreEqual(bluetoothService.initialized, false);

            bluetoothService.initialized = true;
            bool res = bluetoothService.sendBluetoothMessage("test message").Result;
            Assert.AreEqual(res, false);

            //var connectRes = bluetoothService.InitiateConnection().Result;

            //bluetoothService
        }
    }
}
