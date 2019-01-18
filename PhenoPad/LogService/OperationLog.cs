using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.LogService
{
    //So far only tracking the abbreviation,
    //Can add more types for different operations that
    //we want to track
    public enum OperationType{
        AbbreviationTerm,
        AbbreviationExtendedForm
    }
    /// <summary>
    /// A class for logging note taking operations used for statistical purposes
    /// </summary>
    class OperationLogger
    {
        //https://github.com/serilog/serilog/wiki/Structured-Data

        
        private static OperationLogger logger;
        private static List<String> CacheLogs;

        public OperationLogger() {
            CacheLogs = new List<string>();

           
        }

        public OperationLogger getOpLogger() {
            if (logger == null) {
                logger = new OperationLogger();
                logger.InitializeLogFile();
            }
            return logger;
        }

        /// <summary>
        /// Creates an universal log file in local folder with flag to overwirte existing file or not, by default overwrite flag is set to false
        /// In future can pass in type parameter to separate files
        /// for different types of operations
        /// </summary>
        public void InitializeLogFile(bool overwrite = false) {
        }

        /// <summary>
        /// Clears all logs saved in cache
        /// </summary>
        public void ClearAllLogs() { }

        /// <summary>
        /// Flushes the logs in current program to local disk
        /// </summary>
        public void FlushLogToDisk() { }

        public void DeleteLogFile() { }

    }
}
