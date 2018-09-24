using System;
using System.Collections.Generic;

namespace PhenoPad.FileService
{
    /// <summary>
    /// A class that represents a single Notebook item.
    /// </summary>
    public class Notebook
    {
        public string id { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public string patientName { get; set; }
        public List<NotePage> notePages { get; set; }
        public string firstPageUri { get; set; }
        public int audioCount { get; set; }
        public List<TextMessage> transcripts { get; set; }

        /// <summary>
        /// Creates a new empty Notebook instance for serilization.
        /// </summary>
        public Notebook()
        {

        }
        /// <summary>
        /// Creates and initializes default info of a new Notebook instance given its ID.
        /// </summary>
        public Notebook(string id)
        {
            this.id = id;
            name = "Untitled note";
            notePages = new List<NotePage>();
            transcripts = new List<TextMessage>();
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            patientName = "Unknown";
            firstPageUri = "ms-appdata:///local/" + FileManager.getSharedFileManager().GetNoteFilePath(id, "0", NoteFileType.Strokes);
            audioCount = 0;
        }
    }
}
