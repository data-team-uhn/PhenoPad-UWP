using System;
using System.Collections.Generic;

namespace PhenoPad.FileService
{
    public class Notebook
    {
        public string id { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public string patientName { get; set; }
        public List<NotePage> notePages { get; set; }
        public string firstPageUri { get; set; }
        
        // this is a must if for serilization
        public Notebook()
        {

        }

        public Notebook(string id)
        {
            this.id = id;
            name = "Untitled note";
            notePages = new List<NotePage>();
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            patientName = "Unknown";
            firstPageUri = "ms-appdata:///local/" + FileManager.getSharedFileManager().GetNoteFilePath(id, "0", NoteFileType.Strokes);
        }
    }
}
