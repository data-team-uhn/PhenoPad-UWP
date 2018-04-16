using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using PhenoPad.CustomControl;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;

namespace PhenoPad.FileService
{
    public enum NoteFileType
    {
        Strokes,
        Image,
        ImageAnnotation,
        Phenotypes,
        Audio,
        Video
    };

    class FileManager
    {
        public static FileManager sharedFileManager;

        private string strokeFileName = "strokes.gif";

        public static FileManager getSharedFileManager()
        {
            if (sharedFileManager == null)
            {
                sharedFileManager = new FileManager();
                return sharedFileManager;
            }
            else
            {
                return sharedFileManager;
            }
        }
        public FileManager()
        {
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        }

        public string CreateUniqueName()
        {
            return $@"{DateTime.Now.Ticks}";
        }

        private async Task<string> CreateNotebook()
        {
            var notebookId = CreateUniqueName();

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            // create folder structure
            var notebookFolder = await localFolder.CreateFolderAsync(notebookId);

            return notebookId;
        }

        private async Task<bool> CreateNotePage(string id, string pageId)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
            var notebookFolder = await localFolder.GetFolderAsync(id);
            if (notebookFolder != null)
            {
                // create note structure

                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                if (pageFolder == null)
                {
                    pageFolder = await notebookFolder.CreateFolderAsync(pageId);
                    await pageFolder.CreateFolderAsync("Strokes");
                    await pageFolder.CreateFolderAsync("ImagesWithAnnotations");
                    await pageFolder.CreateFolderAsync("Phenotypes");
                    await pageFolder.CreateFolderAsync("Video");
                    await pageFolder.CreateFolderAsync("Audio");
                    Debug.WriteLine("Successfully created notebook file structure");
                }
            }
            else
            {
                // notebook does not exists
                Debug.WriteLine("notebook does not exist");
                return false;
            }
            return true;
        }

        public async Task<bool> SaveNotePage(string id, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
            var notebookFolder = await localFolder.GetFolderAsync(id);
            if (notebookFolder != null)
            {
                // create note structure

                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                if (pageFolder != null)
                {
                    pageFolder = await notebookFolder.CreateFolderAsync(pageId);

                    // save strokes
                    var strokesFolder = await pageFolder.CreateFolderAsync("Strokes");
                    var strokesFile = await strokesFolder.GetFileAsync(this.strokeFileName);
                    if (strokesFile == null)
                    {
                        strokesFile = await strokesFolder.CreateFileAsync(this.strokeFileName);
                    }
                    isSuccessful = await saveStrokes(strokesFile, notePage.inkCan);

                    // save images with annotations
                    //var imageFolder = await pageFolder.CreateFolderAsync("ImagesWithAnnotations");
                    //isSuccessful = await saveImagesWithAnnotations(imageFolder, notePage);


                    //var phenotypeFolder =  await pageFolder.CreateFolderAsync("Phenotypes");
                    //var videoFolder = await pageFolder.CreateFolderAsync("Video");
                    //var audioFolder = await pageFolder.CreateFolderAsync("Audio");
                }
                else
                {
                    Debug.WriteLine("note page does not exist");
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("notebook does not exist");
                return false;
            }
            return true;
        }

        public bool SaveObjectSerilization(string path, Object tosave, Type type)
        {
            FileStream fs = new FileStream(path, FileMode.Create);
            DataContractSerializer dcs =  new DataContractSerializer(type);
            XmlDictionaryWriter xdw =
                XmlDictionaryWriter.CreateTextWriter(fs, Encoding.UTF8);
            dcs.WriteObject(xdw, tosave);
            fs.Dispose();
            return true;
        }


        public async Task<bool> saveStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            // Prevent updates to the file until updates are 
            // finalized with call to CompleteUpdatesAsync.
            Windows.Storage.CachedFileManager.DeferUpdates(strokesFile);
            // Open a file stream for writing.
            IRandomAccessStream stream = await strokesFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            // Write the ink strokes to the output stream.
            using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
            {
                await inkcancas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                await outputStream.FlushAsync();
            }
            stream.Dispose();

            // Finalize write so other apps can update file.
            Windows.Storage.Provider.FileUpdateStatus status =
                await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(strokesFile);

            if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
            {
                Debug.WriteLine("strokes has been saved.");
                return true;
            }
            else
            {
                Debug.WriteLine("strokes couldn't be saved.");
                return false;
            }
        }


      

        public async Task<StorageFile> CreateImageFileForPage(string notebookId, string pageId, string name)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format("{0}\\{1}\\ImagesWithAnnotations", notebookId, pageId);
            var imageFolder = await localFolder.GetFolderAsync(foldername);
            if (imageFolder != null)
            {

                return await imageFolder.CreateFileAsync(name);

            }
            return null;
        }

        public async Task<StorageFile> GetNoteFile(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format("{0}\\{1}\\", notebookId, notePageId);
            switch (fileType)
            {
                case NoteFileType.ImageAnnotation:
                    foldername += "ImagesWithAnnotations";
                    break;
                case NoteFileType.Strokes:
                    break;
                case NoteFileType.Phenotypes:
                    foldername = String.Format("{0}\\", notebookId);
                    break;
            }
            var folder = await localFolder.GetFolderAsync(foldername);
            if (folder == null)
                folder = await localFolder.CreateFolderAsync(foldername);

            string filename = "";
            switch (fileType)
            {
                case NoteFileType.ImageAnnotation:
                    filename = name + ".gif";
                    break;
                case NoteFileType.Image:
                    filename = name + ".jpg";
                    break;
                case NoteFileType.Strokes:
                    filename = "strokes.gif";
                    break;
                case NoteFileType.Phenotypes:
                    filename = "phenotypes.txt";
                    break;
            }
            return await folder.CreateFileAsync(filename);
        }

        public string GetNoteFilePath(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format("{0}\\{1}\\", notebookId, notePageId);
            switch (fileType)
            {
                case NoteFileType.ImageAnnotation:
                    foldername += "ImagesWithAnnotations\\";
                    break;
                case NoteFileType.Strokes:
                    break;
                case NoteFileType.Phenotypes:
                    foldername = String.Format("{0}\\", notebookId);
                    break;
            }

            string filename = "";
            switch (fileType)
            {
                case NoteFileType.ImageAnnotation:
                    filename = name + ".gif";
                    break;
                case NoteFileType.Image:
                    filename = name + ".jpg";
                    break;
                case NoteFileType.Strokes:
                    filename = "strokes.gif";
                    break;
                case NoteFileType.Phenotypes:
                    filename = "phenotypes.txt";
                    break;
            }

            return foldername + filename;
        }

    }
}
