using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using PhenoPad.CustomControl;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using System.IO;
using PhenoPad.PhenotypeService;
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
        Meta,
        ImageAnnotationMeta
    };

    class FileManager
    {
        public static FileManager sharedFileManager;
        private MainPage rootPage = MainPage.Current;
        private string STROKE_FILE_NAME = "strokes.gif";
        private string PHENOTYPE_FILE_NAME = "phenotypes.txt";
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
                var metaFile = GetNoteFilePath(notebookId, "", NoteFileType.Meta);
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
        public async Task<bool> CreateNotePage(Notebook note, string pageId)
        {
            if (note == null)
                return false;

            bool result = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + localFolder.Path);
            try {
                var notebookFolder = await localFolder.GetFolderAsync(note.id);
                var pageFolder = await notebookFolder.CreateFolderAsync(pageId.ToString(), CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Strokes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("ImagesWithAnnotations", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Phenotypes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Video", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Audio", CreationCollisionOption.OpenIfExists);


                // update meta data
                if (note != null)
                {
                    NotePage np = new NotePage(note.id, pageId);
                    note.notePages.Add(np);
                    result = await SaveToMetaFile(note);
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


        public async Task<bool> SaveToMetaFile(Notebook notebook)
        {
            var metafile = GetNoteFilePath(notebook.id, "", NoteFileType.Meta);
            return await SaveObjectSerilization(metafile, notebook, typeof(Notebook));
        }

        // get notebook object from meta file
        public async Task<Notebook> GetNotebookObjectFromXML(string notebookId)
        {
            // meta data
            var metafile = await GetNoteFile(notebookId, "", NoteFileType.Meta);
            object obj = await LoadObjectFromSerilization(metafile, typeof(Notebook));
            if (obj != null)
            {
                return obj as Notebook;
            }
            return null;
        }

        public async Task<List<ImageAndAnnotation>> GetImgageAndAnnotationObjectFromXML(string notebookId, string pageId)
        {
            var metafile = await GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
            object obj = await LoadObjectFromSerilization(metafile, typeof(List<ImageAndAnnotation>));
            if (obj != null)
            {
                return obj as List<ImageAndAnnotation>;
            }
            return null;
        }

        // get saved phenotypes object from meta file
        public async Task<List<Phenotype>> GetSavedPhenotypeObjectsFromXML(string notebookId)
        {
            // meta data
            var phenofile = await GetNoteFile(notebookId, "", NoteFileType.Phenotypes);
            object obj = await LoadObjectFromSerilization(phenofile, typeof(List<Phenotype>));
            if (obj != null)
            {
                return obj as List<Phenotype>;
            }
            return null;
        }

        //copy photo to local folder
        public async Task<bool> CopyPhotoToLocal(StorageFile photo, string notebookId, string pageid, string name)
        {
            bool isSuc = true;
            try
            {
                string filename = GetNoteFilePath(notebookId, pageid, NoteFileType.Image, name);
                string path = System.IO.Path.GetDirectoryName(filename);
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var pfile = await localFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await photo.CopyAndReplaceAsync(pfile);
            }
            catch
            {
                isSuc = false;
            }
            return isSuc;
        }

        // save photos and annotations to disk
        public async Task<bool> SaveNotePageDrawingAndPhotos(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            try
            {
                List<ImageAndAnnotation> imageList = new List<ImageAndAnnotation>();
                foreach (AddInControl con in notePage.GetAllAddInControls())
                {
                    ImageAndAnnotation temp = new ImageAndAnnotation(con.name, notebookId, pageId, con.canvasLeft, con.canvasTop);
                    imageList.Add(temp);
                    // image
                    if (con.type == "photo")
                    {
                        //saved after insertion


                        //string imagePath = GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotation, con.name);
                        //var imageFile = await localFolder.CreateFileAsync(imagePath, CreationCollisionOption.ReplaceExisting);
                    }
                    // annotations
                    string strokePath = GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotation, con.name);
                    var strokesFile = await localFolder.CreateFileAsync(strokePath, CreationCollisionOption.ReplaceExisting);
                    isSuccessful = await saveStrokes(strokesFile, con.inkCan);
                    
                }
                string metapath = GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                isSuccessful = await SaveObjectSerilization(metapath, imageList, typeof(List<ImageAndAnnotation>));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                isSuccessful = false;
            }

            return isSuccessful;
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
                var serializer = new XmlSerializer(type);
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

        public async Task<bool> SaveObjectSerilization(string filepath, Object tosave, Type type)
        {
            bool result = true;
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile sfile = await localFolder.CreateFileAsync(filepath, CreationCollisionOption.ReplaceExisting);
                Windows.Storage.CachedFileManager.DeferUpdates(sfile);
                Stream stream = await sfile.OpenStreamForWriteAsync();
                var serializer = new XmlSerializer(type);
                using (stream)
                {
                    serializer.Serialize(stream, tosave);
                }
                stream.Dispose();

                // Finalize write so other apps can update file.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(sfile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                }
                else
                {
                    Debug.WriteLine(status.ToString());
                    result = false;
                }
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
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
                return null;
            }
            return notefile;
        }

        public async Task<StorageFile> GetNoteFileNotCreate(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFile notefile = null;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string filepath = GetNoteFilePath(notebookId, notePageId, fileType, name);
            try
            {
                notefile = await localFolder.GetFileAsync(filepath);
            }
            catch (Exception)
            {
                Debug.WriteLine($"Failed to get {filepath}");
                return null;
            }
            return notefile;
        }

        // return a file path by notebook and page id, apply to various file types 
        public string GetNoteFilePath(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            string foldername = String.Format(@"{0}\{1}\", notebookId, notePageId);
            switch (fileType)
            {
                case NoteFileType.Meta:
                    foldername = String.Format(@"{0}\", notebookId);
                    break;
                case NoteFileType.ImageAnnotation:
                    foldername += @"ImagesWithAnnotations\";
                    break;

                case NoteFileType.ImageAnnotationMeta:
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
                case NoteFileType.ImageAnnotationMeta:
                    filename = NOTE_META_FILE;
                    break;
                case NoteFileType.Strokes:
                    filename = STROKE_FILE_NAME;
                    break;
                case NoteFileType.Phenotypes:
                    filename = PHENOTYPE_FILE_NAME;
                    break;
            }

            return foldername + filename;
        }

        // create note name by id
        public string createNotebookId()
        {
            return NOTENOOK_NAME_PREFIX + CreateUniqueName();
        }
        
        // Save collected phenotypes to file
        public async Task<bool> saveCollectedPhenotypesToFile(string notebookId)
        {
            bool result = true;
            try
            {
                string phenopath = GetNoteFilePath(notebookId, "", NoteFileType.Phenotypes);
                List<Phenotype> saved = new List<Phenotype>(PhenotypeManager.getSharedPhenotypeManager().savedPhenotypes);
                result = await SaveObjectSerilization(phenopath, saved, typeof(List<Phenotype>));

               
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("Failed to found note folder of " + notebookId);
                result = false;
            }
            return result;
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


        // get all image and annotation objects
        public async Task<List<ImageAndAnnotation>> GetAllImageAndAnnotationObjects(string notebookId)
        {
            List<string> pageIds = await GetPageIdsByNotebook(notebookId);
            if (pageIds == null)
                return null;

            List<ImageAndAnnotation> result = new List<ImageAndAnnotation>();
            foreach (var pid in pageIds)
            {
                List<ImageAndAnnotation> imageAndAnno = await FileManager.getSharedFileManager().GetImgageAndAnnotationObjectFromXML(notebookId, pid);
                if (imageAndAnno != null && imageAndAnno.Count != 0)
                    result.AddRange(imageAndAnno);
            }
            return result;
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
