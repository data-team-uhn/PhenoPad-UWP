﻿using PhenoPad.FileService;
using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
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
        ASR,
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
        //this is for removing duplicate HWR logs
        private string lastHWRLog;

        public OperationLogger() {
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
            FlushTimer.Tick += FlushLogToDisk;
            //triggers flush log every second
            FlushTimer.Interval = TimeSpan.FromSeconds(1);
            CurrentNotebook = null;
            lastHWRLog = "";
            string time = GetTimeStamp();
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
                    //args format= (args0:strokeID)
                    Debug.Assert(args.Count() == 1);
                    log = $"{GetTimeStamp()}|Stroke| {args[0]}";
                    break;
                case OperationType.Recognition:
                    //args format= ( args0:recognized text, args1: {keyword:Phenotype}) 
                    Debug.Assert(args.Count() == 2);
                    log = $"{GetTimeStamp()}|HWRRecognition| {args[0]} | {args[1]}";
                    break;
                case OperationType.Speech:
                    //args format = ( args0:transcript, args1: {keyword:Phenotype}) 
                    Debug.Assert(args.Count() == 2);
                    log = $"{GetTimeStamp()}|Speech| {args[0]} | {args[1]}";
                    break;
                case OperationType.ASR:
                    Debug.Assert(args.Count() == 1);
                    log = $"{GetTimeStamp()}|ASR| {args[0]} ";
                    break;
                case OperationType.Phenotype:
                    //args format= (args0:source, args1:{added/deleted/Y/N}, args2:{keyword:Phenotype} )
                    Debug.Assert(args.Count() == 3);
                    log = $"{GetTimeStamp()}|Phenotype| {args[0]} | {args[1]} | {args[2]}";
                    break;
                case OperationType.Abbreviation:
                    //args format= (string: context sentence, string:shortTerm, string:DefaultExtendedForm, string:selectedExtendedForm, string:selectedRank, string: list of candidates )
                    Debug.Assert(args.Count() == 6);
                    log = $"{GetTimeStamp()}|Abbreviation| {args[0]} | {args[1]}, {args[2]} | {args[3]} | {args[4]} | {args[5]}";
                    break;
                case OperationType.Alternative:
                    //args format= (args0:original text, args1:selected text, args2:candidate rank )
                    Debug.Assert(args.Count() == 3);
                    log = $"{GetTimeStamp()}|Alternative| {args[0]} | {args[1]} | {args[2]}";
                    break;
            }
            //only adds the log if it's got different content from the previous log
            if (!CheckIfSameLog(log)) {
                CacheLogs.Add(log);
                lastHWRLog = log;
                Debug.WriteLine(log);
            }
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    if (!FlushTimer.IsEnabled)
                        FlushTimer.Start();
                }
                );
            
        }

        private bool CheckIfSameLog(string log) {
            if (lastHWRLog == string.Empty)
                return false;

            var lastLog = lastHWRLog.Split('|');
            var curLog = log.Split('|');
            for (int i = 1; i < lastLog.Count(); i++) {
                if (lastLog[i] != curLog[i])
                    return false;
            }
            return true;
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
        public async void FlushLogToDisk(object sender = null, object e = null) {
            FlushTimer.Stop();
            if (CacheLogs.Count > 0) {
                bool done = await FileManager.getSharedFileManager().AppendLogToFile(CacheLogs, CurrentNotebook);
                if (done)
                    CacheLogs.Clear();
                else
                    MetroLogger.getSharedLogger().Error($"{this.GetType().ToString()},failed to flush cachelogs, will try to flush in the next flushing interval");
            }
        }      

        internal void Log(OperationType type, string recognized,Dictionary<string, Phenotype> annoResult)
        {
            string pairs = "";
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

        /// <summary>
        /// parses a log line to operationitem to be displayed in view mode,
        /// currently not all logs will be displayed
        /// </summary>
        /// <returns></returns>
        public async Task<List<OperationItem>> ParseOperationItems(string notebookID) {
            List<OperationItem> opitems = new List<OperationItem>();

            List<string> logs = await FileManager.getSharedFileManager().GetLogStrings(notebookID);
            if (logs != null)
            {
                //selective parse useful log for display
                foreach (string line in logs) {
                    Debug.WriteLine(line);

                }
            }




            return opitems;
        }
    }

    /// <summary>
    /// A class that contains useful information of a logged operation
    /// </summary>
    class OperationItem {

        public string notebookID;
        public string pageID;
        public OperationType type;
        public TimeSpan timestamp;

        //attributes for stroke
        //two ways of arranging strokes:by lines recognized using HWR or timestamp (display whenever there's a gap of time)
        public uint strokeID;
        public int lineID; // probably need this for line ordering

        //attributes for HWR/Speech
        public string context;
        public Dictionary<string, string> phenotypes;

        //attributes for phenotypes
        public string source;
        public string phenotype;// maybe need to check with saved phenotypes and only display saved

        public OperationItem()
        {
        }

        public OperationItem(string notebookID, string pageID, OperationType type, TimeSpan time) {
            this.notebookID = notebookID;
            this.pageID = pageID;
            this.type = type;
            timestamp = time;
            context = null;
            phenotypes = new Dictionary<string, string>();
            source = null;
            phenotype = null;
        }

    }
}
