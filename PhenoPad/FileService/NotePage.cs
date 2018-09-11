using System;
using System.Diagnostics;

namespace PhenoPad.FileService
{
    /// <summary>
    /// A class that represents a single NotePage object contained within a Notebook.
    /// </summary>
    public class NotePage
    {
        public string id { get; set; }
        public string notebookId { get; set; }
        public string date { get; set; }
        public string strokeUri { get; set; }


        public string name
        {
            get; set;
        }
        /// <summary>
        /// Creates a new NotePage instance for serilization.
        /// </summary>
        public NotePage()
        {

        }
        /// <summary>
        /// Creates and initializes a new NotePage instance based on given Notebook ID and Notepage ID.
        /// </summary>
        public NotePage(string notebookId, string pageId)
        {
            this.id = pageId;
            this.notebookId = notebookId;
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            strokeUri = "ms-appdata:///local/" + FileManager.getSharedFileManager().GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
            try
            {
                int i = Int32.Parse(id);
                name = $"Page {i + 1}";
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to initialize new NotePage instance: {0}", e);
            }
        }

    }
}
