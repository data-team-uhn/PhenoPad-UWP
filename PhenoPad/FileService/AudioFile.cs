using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace PhenoPad.FileService
{
    /// <summary>
    /// A class that represents a single NotePage object contained within a Notebook.
    /// </summary>
    public class AudioFile
    {
        public string notebookId { get; set; }
        public string date { get; set; }
        public string path { get; set; }
        public string name { get; set; }
        public MediaSource source { get; set; }


        /// <summary>
        /// Creates a new NotePage instance for serilization.
        /// </summary>
        public AudioFile()
        {
        }

        /// <summary>
        /// Creates and initializes a new NotePage instance based on given Notebook ID and Notepage ID.
        /// </summary>
        public AudioFile(string notebookId, StorageFile audioFile)
        {
            this.notebookId = notebookId;
            date = audioFile.DateCreated.DateTime.ToString();
            path = audioFile.Path;
            name = audioFile.Name;
            source = MediaSource.CreateFromStorageFile(audioFile);
        }
    }
}
