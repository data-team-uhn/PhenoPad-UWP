
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
            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();
            
            // ensure we get bluetooth service
            Assert.IsNotNull(bluetoothService);

            // shouldn't be initialized at the beginning
            Assert.AreEqual(bluetoothService.initialized, false);

            Assert.AreEqual(1, 1);
        }

        [TestMethod]
        public void CheckConnectionTest()
        {
            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();


            Assert.AreEqual(bluetoothService.initialized, false);
            bool res = bluetoothService.checkConnection().Result;
            Assert.AreEqual(res,false);
        }

        [TestMethod]
        public void SendMessageTest()
        {
            PhenoPad.BluetoothService.BluetoothService bluetoothService = PhenoPad.BluetoothService.BluetoothService.getBluetoothService();

            Assert.AreEqual(bluetoothService.initialized, false);

            bluetoothService.initialized = true;
            bool res = bluetoothService.sendBluetoothMessage("test message").Result;
            Assert.AreEqual(res, false);
        }
    }
}
