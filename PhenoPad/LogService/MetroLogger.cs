using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MetroLog;
using MetroLog.Targets;

namespace PhenoPad.LogService
{
    class MetroLogger
    {
        public static ILogger logger;
        public static ILogger getSharedLogger()
        {
            if (logger == null)
            {
                LogManagerFactory.DefaultConfiguration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
                logger = LogManagerFactory.DefaultLogManager.GetLogger<MainPage>();
            }
            return logger;
        }
        
        public MetroLogger()
        {

        }
    }
}
