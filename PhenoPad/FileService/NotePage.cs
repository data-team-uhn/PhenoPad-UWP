using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

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
        public string EHR { get; set; }
        public string name {get; set;}

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
                GetEHRFromFile();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to initialize new NotePage instance: {0}", e);
            }
        }

        public async void GetEHRFromFile()
        {

            var file = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, id, NoteFileType.EHR);
            if (file == null)
                return;
            IBuffer buffer = await FileIO.ReadBufferAsync(file);
            DataReader reader = DataReader.FromBuffer(buffer);
            byte[] fileContent = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(fileContent);
            EHR = Encoding.UTF8.GetString(fileContent, 0, fileContent.Length);
        }
    }
}
