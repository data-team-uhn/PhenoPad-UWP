using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetroLog;
using MetroLog.Targets;
using System.Diagnostics;

namespace PhenoPad.LogService
{
    class MetroLogger
    {
        public static ILogger logger; //For program debugging purpose
        public static ILogManager logManager;


        /// <summary>
        /// Gets the static logger with given type.
        /// </summary>
        /// <param name="name">The type of instance that is calling the method,default = null</param>
        /// <returns>The static logger</returns>
        public static ILogger getSharedLogger(Type name = null)
        {
            // If default name is null, gets the type of calling instance.
            // The type of calling instance can be either a class (if predefined in class properties),
            // or the type of method if straight calling by using Metrologger.getsharedlogger() in some method.
            if (name == null) {
                StackFrame frame = new StackFrame(1);
                name = frame.GetMethod().DeclaringType;
            }

            //First time calling, need to create the logger
            if (logger == null)
            {
                //Custom Layout defined in LoggerCustomLayout.cs
                var loggingConfiguration = new LoggingConfiguration { IsEnabled = true };            
                loggingConfiguration.AddTarget(LogLevel.Info, LogLevel.Fatal, new StreamingFileTarget(new CustomLayout()));
                loggingConfiguration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new DebugTarget(new CustomLayout()));
                //
                logManager = LogManagerFactory.CreateLogManager(loggingConfiguration);
                       
            }

            logger = logManager.GetLogger(name);
            return logger;
        }
    }
    
    /// <summary>
    /// A class for custom formatting MetroLog message.
    /// </summary>
    class CustomLayout : MetroLog.Layouts.Layout
    {
        /// <summary>
        /// Create a formatted string based on given informations
        /// </summary>
        /// <param name="context"><see cref="LogWriteContext"/></param>
        /// <param name="info"><see cref="LogEventInfo"/></param>
        /// <returns>Formatted string to log</returns>
        public override string GetFormattedString(LogWriteContext context, LogEventInfo info)
        {
            var formatter = new Windows.Globalization.DateTimeFormatting.DateTimeFormatter("{year.full}-{month.integer}‎-{day.integer}‎ " +
                "{hour.integer}‎:‎{minute.integer(2)}‎:‎{second.integer(2)}");
            DateTime localTime = info.TimeStamp.LocalDateTime;
            var formatted = formatter.Format(localTime);
            return $"{info.SequenceID}|{formatted}|{info.Logger}|{info.Level}|{info.Message}";
        }
    }
}
