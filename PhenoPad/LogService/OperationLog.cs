using PhenoPad.FileService;
using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace PhenoPad.LogService
{
    //So far only tracking the abbreviation,
    //Can add more types for different operations that
    //we want to track
    public enum OperationType{
        Abbreviation,
        Alternative,
        Phenotype,
        Stroke,
        Speech,
        Recognition
    }
    /// <summary>
    /// A class for logging note taking operations used for statistical purposes
    /// </summary>
    class OperationLogger
    {
        //https://github.com/serilog/serilog/wiki/Structured-Data
      
        public static OperationLogger logger;
        public static List<string> CacheLogs;
        public static string CurrentNotebook;
        private DispatcherTimer FlushTimer;


        public OperationLogger() {
            CacheLogs = new List<string>();

           
        }

        public static OperationLogger getOpLogger() {
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
            CacheLogs = new List<string>();
            FlushTimer = new DispatcherTimer();
            //FlushTimer.Tick += FlushLogToDisk;
            FlushTimer.Interval = TimeSpan.FromSeconds(10);
            CurrentNotebook = null;
            string time = GetTimeStamp();
            Debug.WriteLine($"initialized shared oplogger, time = {time}");
        }

        public void SetCurrentNoteID(string notebookId) {
            CurrentNotebook = notebookId;
        }

        public List<string> GetPhenotypeNames(List<Phenotype> phenos) {
            List<string> names = new List<string>();
            foreach (Phenotype p in phenos) {
                names.Add(p.name);
            }
            return names;

        }

        /// <summary>
        /// Logs an operation based on its type with varying number of arguments
        /// </summary>
        public async void Log(OperationType opType, params string[] args) {
            string log = "";
            switch (opType) {
                case OperationType.Stroke:
                    //args format= (string:numberOfStrokes)
                    Debug.Assert(args.Count() == 1);
                    log = $"{GetTimeStamp()}|Stroke|{args[0]}";
                    break;
                case OperationType.Recognition:
                    //args format= ( string:recognized text, string: parsed pair of phenotype extraction in format key:phenotype) 
                    Debug.Assert(args.Count() == 2);
                    log = $"{GetTimeStamp()}|HWRRecognition|\"{args[0]}\" {args[1]}";
                    break;
                case OperationType.Speech:
                    //args format = ( string:recognized text, string: parsed pair of phenotype extraction in format key:phenotype) 
                    Debug.Assert(args.Count() == 2);
                    log = $"{GetTimeStamp()}|Speech|From \"{args[0]}\" detected {args[1]}";
                    break;
                case OperationType.Phenotype:
                    //args[3] format= (string:source, string:{added/deleted/Y/N}, string:{Phenotype} )
                    Debug.Assert(args.Count() == 3);
                    log = $"{GetTimeStamp()}|Phenotype|From {args[0]} {args[1]} <{args[2]}>";
                    break;
                case OperationType.Abbreviation:
                    //args[3] format= (string: context sentence, string:shortTerm, string:DefaultExtendedForm, string:selectedExtendedForm, string:selectedRank, string: list of candidates )
                    Debug.Assert(args.Count() == 6);
                    log = $"{GetTimeStamp()}|Abbreviation|{args[0]} | {args[1]}, {args[2]} | {args[3]} |{args[4]}| {args[5]}";
                    break;
                case OperationType.Alternative:
                    Debug.Assert(args.Count() == 3);
                    log = $"{GetTimeStamp()}|Alternative|{args[0]} was changed to {args[1]} at rank {args[2]}";
                    break;
            }
            CacheLogs.Add(log);
            Debug.WriteLine(log);
            //for now triggers instant line logging
            await FlushLogToDisk();
        }

        private string GetTimeStamp()
        {
            DateTime now = DateTime.Now;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0:D4}{1:D2}{2:D2}-{3:D2}{4:D2}{5:D2}",
                                 now.Year,
                                 now.Month,
                                 now.Day,
                                 now.Hour,
                                 now.Minute,
                                 now.Second);
        }

        /// <summary>
        /// Clears all logs saved in cache
        /// </summary>
        public void ClearAllLogs() {
            CacheLogs.Clear();
        }

        /// <summary>
        /// Flushes the logs in current program to local disk
        /// </summary>
        public async Task FlushLogToDisk(object sender = null, object e = null) {
            if (CacheLogs.Count > 0) {
                bool done = await FileManager.getSharedFileManager().AppendLogToFile(CacheLogs, CurrentNotebook);
                if (done)
                    CacheLogs.Clear();
                else
                    MetroLogger.getSharedLogger().Error($"{this.GetType().ToString()},failed to flush cachelogs");
            }
        }

        public void DeleteLogFile() {
            //TODO 
        }

        internal void Log(OperationType type, string recognized,Dictionary<string, Phenotype> annoResult)
        {
            string pairs = "|";
            foreach (var item in annoResult) {
                pairs += $"{item.Key}:{item.Value.name},";
            }
            pairs = pairs.TrimEnd(',');
            Log(type, recognized, pairs);
        }

        public string ParseCandidateList(List<string> str) {
            string parsed = "[";
            foreach (string s in str) {
                parsed += s + ",";
            }
            parsed = parsed.TrimEnd(',') + "]";
            return parsed;
        }
    }
}
