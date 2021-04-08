using System;
using Windows.Storage;

namespace PhenoPad
{
    class AppConfigurations
    {
        static string configName = "config.txt";

        static async void CreateConfigurationFileAsync()
        {
            StorageFile sampleFile = 
                await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(configName,
                CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(sampleFile, "");
        }


        public static void saveSetting(string name, object val)
        {
            Windows.Storage.ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.RoamingSettings;
            settings.Values[name] = val;
        }

        public static object readSetting(string name)
        {
            Windows.Storage.ApplicationDataContainer settings = Windows.Storage.ApplicationData.Current.RoamingSettings;
            return settings.Values[name];
        }
    }
}
