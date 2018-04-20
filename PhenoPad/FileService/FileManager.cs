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
//using System.Runtime.Serialization;
using System.Xml;
using System.IO;
using PhenoPad.PhenotypeService;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Windows.Storage.Search;

namespace PhenoPad.FileService
{
    public enum NoteFileType
    {
        Strokes,
        Image,
        ImageAnnotation,
        Phenotypes,
        Audio,
        Video,
        Meta
    };

    class FileManager
    {
        public static FileManager sharedFileManager;
        private MainPage rootPage = MainPage.Current;
        private string STROKE_FILE_NAME = "strokes.gif";
        private string NOTENOOK_NAME_PREFIX = "note_";
        private string NOTE_META_FILE = "meta.xml";

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

        // return a unique name based on time tick
        public string CreateUniqueName()
        {
            return $@"{DateTime.Now.Ticks}";
            //return "goodname";
        }

        // create a folder for this notebook
        public async Task<bool> CreateNotebook(string notebookId)
        {
            bool result = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            // create folder structure
            try
            {
                var notebookFolder = await localFolder.CreateFolderAsync(notebookId, CreationCollisionOption.OpenIfExists);
                Notebook nb = new Notebook(notebookId);
                var metaFile = await notebookFolder.CreateFileAsync(NOTE_META_FILE, CreationCollisionOption.ReplaceExisting);
                result = await SaveObjectSerilization(metaFile, nb, nb.GetType()); // save meta data to xml file
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to crate notebook");
                return false;
            }
            return result;
        }

        // Get all notebook ids
        public async Task<List<string>>  GetAllNotebookIds()
        {
            List<string> result = new List<string>();
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var folders = await localFolder.GetFoldersAsync();
                foreach (var f in folders)
                {
                    if (f.Name.IndexOf(NOTENOOK_NAME_PREFIX) == 0)
                        result.Add(f.Name);
                }
                return result;
            }
            catch (FileNotFoundException)
            {
                rootPage.NotifyUser("Failed to get all notebook ids.", NotifyType.ErrorMessage, 2);
            }
            return null;
        }
        // Get note page ids by notebook name
        public async Task<List<string>> GetPageIdsByNotebook(string notebookId)
        {
            List<string> result = new List<string>();
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                var notebookFolder = await localFolder.GetFolderAsync(notebookId);
                var folders = await notebookFolder.GetFoldersAsync();
                foreach (var f in folders)
                {
                    result.Add(f.Name);
                }
                return result;
            }
            catch (FileNotFoundException)
            {
                rootPage.NotifyUser("Failed to get all note page ids.", NotifyType.ErrorMessage, 2);
            }
            return null;
        }

        // create file structure for a note page
        public async Task<bool> CreateNotePage(string id, string pageId)
        {
            bool result = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
            try {
                var notebookFolder = await localFolder.GetFolderAsync(id);
                var pageFolder = await notebookFolder.CreateFolderAsync(pageId.ToString(), CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Strokes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("ImagesWithAnnotations", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Phenotypes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Video", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Audio", CreationCollisionOption.OpenIfExists);


                // update meta data
                var metafile = await GetNoteFile(id, pageId, NoteFileType.Meta);
                object obj = await LoadObjectFromSerilization(metafile, typeof(Notebook));
                if (obj != null)
                {
                    Notebook nb = obj as Notebook;
                    NotePage np = new NotePage(id, pageId);
                    nb.notePages.Add(np);
                    result = await SaveObjectSerilization(metafile, nb, typeof(Notebook));
                }
                else
                {
                    result = false;
                }
                Debug.WriteLine("Successfully created file structure for note");
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to create note page.");
                result = false;
            }
            return result;
        }

      

        // save ink data to disk 
        public async Task<bool> SaveNotePageStrokes(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
          
            try {
                var notebookFolder = await localFolder.GetFolderAsync(notebookId);
                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                var strokesFolder = await pageFolder.GetFolderAsync("Strokes");
                var  strokesFile = await strokesFolder.CreateFileAsync(this.STROKE_FILE_NAME, CreationCollisionOption.OpenIfExists);
                isSuccessful = await saveStrokes(strokesFile, notePage.inkCan);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        // load ink data from disk
        public async Task<bool> LoadNotePageStroke(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
            try
            {
                string strokePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
                var strokesFile = await localFolder.GetFileAsync(strokePath);
                isSuccessful = await loadStrokes(strokesFile, notePage.inkCan);

            }
            catch (Exception e)
            {
                //rootPage.NotifyUser(e.Message, NotifyType.ErrorMessage, 2);
                Debug.WriteLine("Folder not found when trying to load note page.");
                return false;
            }
            return isSuccessful;
        }

        public async Task<object> LoadObjectFromSerilization(StorageFile metaFile, Type type)
        {
            try
            {
                Stream stream = await metaFile.OpenStreamForReadAsync();
                //tosave = new Class1("ididid", "namename");
                var serializer = new XmlSerializer(typeof(Notebook));
                using (stream)
                {
                    object obj = serializer.Deserialize(stream);
                    return obj;
                }
            }
            catch (Exception) {
                return null;
            }
        }

        public async Task<bool> SaveObjectSerilization(StorageFile metaFile, Object tosave, Type type)
        {

            Stream stream = await metaFile.OpenStreamForWriteAsync();
            //tosave = new Class1("ididid", "namename");
            var serializer = new XmlSerializer(typeof(Notebook));
            using (stream)
            {
                serializer.Serialize(stream, tosave);
            }
            stream.Dispose();
            return true;
        }

        // save strokes to gif file
        public async Task<bool> saveStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            // Prevent updates to the file until updates are 
            // finalized with call to CompleteUpdatesAsync.
            //Windows.Storage.CachedFileManager.DeferUpdates(strokesFile);
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

        // load strokes from gif file
        public async Task<bool> loadStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            // User selects a file and picker returns a reference to the selected file.
            if (strokesFile != null)
            {
                try
                {
                    // Open a file stream for reading.
                    IRandomAccessStream stream = await strokesFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    // Read from file.
                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        await inkcancas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
                        Debug.WriteLine($"{strokesFile.Path} has been loaded.");
                    }
                    stream.Dispose();
                }
                catch (Exception)
                {
                    Debug.WriteLine($"Failed to load {strokesFile.Path}.");
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        // create file to save images and annotations on note
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
            StorageFile notefile = null;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string filepath = GetNoteFilePath(notebookId, notePageId, fileType);
            try
            {
                notefile = await localFolder.CreateFileAsync(filepath, CreationCollisionOption.OpenIfExists);
            }
            catch (Exception)
            {
                Debug.WriteLine($"Failed to get {filepath}");
            }
            return notefile;
        }

        // return a file path by notebook and page id, apply to various file types 
        public string GetNoteFilePath(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format(@"{0}\{1}\", notebookId, notePageId);
            switch (fileType)
            {
                case NoteFileType.Meta:
                    foldername = notebookId + "\\";
                    break;
                case NoteFileType.ImageAnnotation:
                    foldername += @"ImagesWithAnnotations\";
                    break;
                case NoteFileType.Strokes:
                    foldername += @"Strokes\";
                    break;
                case NoteFileType.Phenotypes:
                    foldername = String.Format(@"{0}\", notebookId);
                    break;
            }

            string filename = "";
            switch (fileType)
            {
                case NoteFileType.Meta:
                    filename = NOTE_META_FILE;
                    break;
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

        // create note name by id
        public string getNotebookNameById(string notebookId)
        {
            return NOTENOOK_NAME_PREFIX + notebookId;
        }
        
        // Save collected phenotypes to file
        public async Task<bool> saveCollectedPhenotypesToFile(string notebookId)
        {
            Windows.Storage.StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            try
            {
                Windows.Storage.StorageFolder notebookFolder = await localFolder.GetFolderAsync(notebookId);

                Windows.Storage.StorageFile phenoFile = await notebookFolder.CreateFileAsync("phenotypes.txt",
                       Windows.Storage.CreationCollisionOption.ReplaceExisting);

                // Prevent updates to the file until updates are 
                // finalized with call to CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(phenoFile);

                List<string> strToSave = new List<string>();
                foreach (Phenotype pp in PhenotypeManager.getSharedPhenotypeManager().savedPhenotypes)
                {
                    strToSave.Add($"{pp.hpId}\t{pp.state}");
                }
                await Windows.Storage.FileIO.WriteLinesAsync(phenoFile, strToSave);

                // Finalize write so other apps can update file.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(phenoFile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    Debug.WriteLine("Collected phenotypes have been saved.");
                    return true;
                }
                else
                {
                    Debug.WriteLine("Collected phenotypes couldn't be saved.");
                    return false;
                }
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("Failed to found note folder of " + notebookId);
                return false;
            }
        }


        // get stroke image by notebook and note page
        public BitmapImage GetStrokeImage(string notebookId, string pageId)
        {
            string imagePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
            BitmapImage bitmapImage = new BitmapImage(new Uri($"ms-appdata:///local/{imagePath}"));
            return bitmapImage;
        }

        

        // get all notebook objects
        public async Task<List<Notebook>> GetAllNotebookObjects()
        {
            List<Notebook> result = new List<Notebook>();
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                QueryOptions queryOptions = new QueryOptions(CommonFolderQuery.DefaultQuery);
                queryOptions.SortOrder.Clear();
                SortEntry se = new SortEntry();
                se.PropertyName = "System.DateModified";
                se.AscendingOrder = false;
                queryOptions.SortOrder.Add(se);

                StorageFolderQueryResult queryResult = localFolder.CreateFolderQueryWithOptions(queryOptions);
                var folders = await queryResult.GetFoldersAsync();
                foreach (var f in folders)
                {
                    if (f.Name.IndexOf(NOTENOOK_NAME_PREFIX) == 0)
                    {
                        var metafile = await GetNoteFile(f.Name, "", NoteFileType.Meta);
                        object obj = await LoadObjectFromSerilization(metafile, typeof(Notebook));
                        if (obj != null)
                        {
                            Notebook nb = obj as Notebook;
                            result.Add(nb);
                        }
                    }
                }
                return result;
            }
            catch (FileNotFoundException)
            {
                rootPage.NotifyUser("Failed to get all notebook ids.", NotifyType.ErrorMessage, 2);
            }
            return null;
        }

        // get all note page objects
        public async Task<List<NotePage>> GetAllNotePageObjects(string notebookId)
        {
            
            List<string> pageIds = await GetPageIdsByNotebook(notebookId);
            if (pageIds == null)
                return null;

            List<NotePage> result = new List<NotePage>();
            foreach (var pid in pageIds)
            {
                NotePage np = new NotePage(notebookId, pid);
                result.Add(np);
            }
            return result;
        }


    }
}
