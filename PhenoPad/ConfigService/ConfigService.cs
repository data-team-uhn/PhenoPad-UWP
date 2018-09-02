using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.ConfigService
{
    class ConfigService
    {
        /**
         * This class manages App settings
         * Using external microphone or internal microphone:
         * MicMode, 0 (surface microphopne), 1 (external microphone)
         *
         **/
        public static ConfigService sharedConfigService;
        public static ConfigService getConfigService()
        {
            if (sharedConfigService == null)
            {
                sharedConfigService = new ConfigService();
            }
            return sharedConfigService;
        }

        private Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        public ConfigService()
        {

        }

        #region microphone settings
        public void UseInternalMic()
        {
            localSettings.Values["MicMode"] = 0;
        }

        public void UseExternalMic()
        {
            localSettings.Values["MicMode"] = 1;
        }

        public bool IfUseExternalMicrophone()
        {
            var value = localSettings.Values["MicMode"];
            if (value == null)
            {
                UseInternalMic();
                return false;
            }
            else
            {
                switch ((int)value)
                {
                    case 0:
                        return false;
                    case 1:
                        return true;
                }
            }

            return false;
        }
        #endregion
    }
}
