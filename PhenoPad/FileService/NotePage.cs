using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.FileService
{
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
        public NotePage()
        {
            
        }

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
            catch (Exception)
            {

            }
        }

    }
}
