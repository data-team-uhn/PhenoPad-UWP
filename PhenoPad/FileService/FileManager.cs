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
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Display;
using System.Threading;

namespace PhenoPad.FileService
{
    public enum NoteFileType
    {
        Strokes,
        Image,
        ImageAnnotation,
        Phenotypes,
        Audio,
        Transcriptions,
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
        public string currentNoteboookId = "";

        private StorageFolder ROOT_FOLDER = ApplicationData.Current.LocalFolder;

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
            Debug.WriteLine("Application local folder is " + ROOT_FOLDER.Path.ToString());
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
            LogService.MetroLogger.getSharedLogger().Info($"Creating notebook {notebookId}");
            bool result = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            // create folder structure
            try
            {
                var notebookFolder = await ROOT_FOLDER.CreateFolderAsync(notebookId, CreationCollisionOption.OpenIfExists);
                await notebookFolder.CreateFolderAsync("Audio", CreationCollisionOption.OpenIfExists);

                Debug.WriteLine("Notebook Folder is " + notebookFolder);
                Notebook nb = new Notebook(notebookId);
                var metaFile = GetNoteFilePath(notebookId, "", NoteFileType.Meta);
                result = await SaveObjectSerilization(metaFile, nb, nb.GetType()); // save meta data to xml file
            }
            catch (Exception)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to create notebook {notebookId}");
                Debug.WriteLine("Failed to crate notebook");
                return false;
            }
            LogService.MetroLogger.getSharedLogger().Info($"Successfully created notebook {notebookId}");
            return result;
        }

        // Get all notebook ids
        public async Task<List<string>>  GetAllNotebookIds()
        {
            List<string> result = new List<string>();
            try
            {
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var folders = await ROOT_FOLDER.GetFoldersAsync();
                foreach (var f in folders)
                {
                    if (f.Name.IndexOf(NOTENOOK_NAME_PREFIX) == 0)
                        result.Add(f.Name);
                }
                return result;
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Failed to get all notebook ids.", NotifyType.ErrorMessage, 2);
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get notebook id list: {ex.Message}");
            }
            return null;
        }
        // Get note page ids by notebook name
        public async Task<List<string>> GetPageIdsByNotebook(string notebookId)
        {
            List<string> result = new List<string>();
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(notebookId);
                var folders = await notebookFolder.GetFoldersAsync();
                foreach (var f in folders)
                {
                    // only folders with numeric names are pages
                    if(int.TryParse(f.Name, out int n))
                        result.Add(f.Name);
                }
                return result;
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Failed to get all note page ids.", NotifyType.ErrorMessage, 2);
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get page ids of {notebookId}: {ex.Message}");
            }
            return null;
        }

        // create file structure for a note page
        public async Task<bool> CreateNotePage(Notebook note, string pageId)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Creating note page {pageId} for notebook {note.id}");
            if (note == null)
                return false;

            bool result = true;

            try {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(note.id);
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
                LogService.MetroLogger.getSharedLogger().Info($"Successfully created note page {pageId} for notebook {note.id}");
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to create note page.");
                LogService.MetroLogger.getSharedLogger().Error($"Failed to create note page {pageId} for notebook {note.id}");
                result = false;
            }
            return result;
        }


        public async Task<bool> SaveToMetaFile(Notebook notebook)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Saving notebook meta file object of {notebook.id}");
            var metafile = GetNoteFilePath(notebook.id, "", NoteFileType.Meta);
            return await SaveObjectSerilization(metafile, notebook, typeof(Notebook));
        }

        // get notebook object from meta file
        public async Task<Notebook> GetNotebookObjectFromXML(string notebookId)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Fetching notebook meta file object of {notebookId}");
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
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var pfile = await ROOT_FOLDER.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await photo.CopyAndReplaceAsync(pfile);
            }
            catch(Exception ex)
            {
                isSuc = false;
                LogService.MetroLogger.getSharedLogger().Error("Falied to copy photo to local folder: " + ex.Message);
            }
            return isSuc;
        }

        // save photos and annotations to disk
        public async Task<bool> SaveNotePageDrawingAndPhotos(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            try
            {
                List<ImageAndAnnotation> imageList = await notePage.GetAllAddInObjects();
               
              
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                isSuccessful = false;
            }

            return isSuccessful;
        }

        // save notepage strokes 
        public async Task<bool> SaveNotePageStrokes(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
          
            try {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(notebookId);
                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                var strokesFolder = await pageFolder.GetFolderAsync("Strokes");
                var  strokesFile = await strokesFolder.CreateFileAsync(this.STROKE_FILE_NAME, CreationCollisionOption.OpenIfExists);
                isSuccessful = await saveStrokes(strokesFile, notePage.inkCan);
               
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(
                    $"Failed to save strokes, notebook: {notebookId}, page: {pageId}, details: " 
                    + e.Message);
                return false;
            }
            return true;
        }

        // load ink data from disk
        public async Task<bool> LoadNotePageStroke(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Debug.WriteLine("Local Path: " + ROOT_FOLDER.Path);
            try
            {
                string strokePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
                var strokesFile = await ROOT_FOLDER.GetFileAsync(strokePath);
                isSuccessful = await loadStrokes(strokesFile, notePage.inkCan);
            }
            catch (Exception e)
            {
                //rootPage.NotifyUser(e.Message, NotifyType.ErrorMessage, 2);
                Debug.WriteLine("Failed to load note page stroke from disk: " + e.Message);
                LogService.MetroLogger.getSharedLogger().Error("Failed to load note page stroke from disk: "
                    + e.Message);
                return false;
            }
            return isSuccessful;
        }

        /// <summary>
        /// Load object from XML file
        /// </summary>
        /// <param name="metaFile"></param>
        /// <param name="type"></param>
        /// <returns></returns>
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
            catch (Exception ex) {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to fetch object from serilization file: {metaFile.Path}, error: {ex.Message} " );
                return null;
            }
        }

        SemaphoreSlim serilizationSS = new SemaphoreSlim(1);
        /// <summary>
        /// Save object to XML file
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="tosave"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<bool> SaveObjectSerilization(string filepath, Object tosave, Type type)
        {
            await serilizationSS.WaitAsync();
            bool result = true;
            StorageFile sfile = null;
            Stream stream = null;
            try
            {
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                sfile = await ROOT_FOLDER.CreateFileAsync(filepath, CreationCollisionOption.ReplaceExisting);
                //Windows.Storage.CachedFileManager.DeferUpdates(sfile);
                stream = await sfile.OpenStreamForWriteAsync();
                var serializer = new XmlSerializer(type);
                using (stream)
                {
                    serializer.Serialize(stream, tosave);
                }
               
                /**
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                }
                else
                {
                    Debug.WriteLine(status.ToString());
                    result = false;
                }**/
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                result = false;
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save object to {filepath}: {e.Message}");
            }
            finally
            {
                serilizationSS.Release();
                if (stream != null)
                    stream.Dispose();

                // Finalize write so other apps can update file.
                // Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(sfile);
                
            }
            return result;
        }

        // save strokes to gif file
        public async Task<bool> saveStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Saving strokes of InkCanvas...");

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
                outputStream.Dispose();
            }
            stream.Dispose();

            // Finalize write so other apps can update file.
            Windows.Storage.Provider.FileUpdateStatus status =
                await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(strokesFile);

            if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
            {
                LogService.MetroLogger.getSharedLogger().Info($"Successfully saved strokes of InkCanvas.");
                return true;
            }
            else
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save strokes of InkCanvas.");
                return false;
            }
        }

        // load strokes from gif file
        public async Task<bool> loadStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            LogService.MetroLogger.getSharedLogger().Info($"Loading strokes from {strokesFile.Path}");
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
                        LogService.MetroLogger.getSharedLogger().Info($"{strokesFile.Path} has been loaded.");
                    }
                    stream.Dispose();
                }
                catch (Exception)
                {
                    Debug.WriteLine($"Failed to load {strokesFile.Path}.");
                    LogService.MetroLogger.getSharedLogger().Error($"Failed to load {strokesFile.Path}.");
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
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format("{0}\\{1}\\ImagesWithAnnotations", notebookId, pageId);
            try
            {
                var imageFolder = await ROOT_FOLDER.GetFolderAsync(foldername);
                if (imageFolder != null)
                {

                    return await imageFolder.CreateFileAsync(name + ".jpg");

                }
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to create image file, notebook:{notebookId}, " +
                    $"page: {pageId}, name: {name}, details: {ex.Message} ");
            }
            return null;
        }

        public async Task<StorageFile> GetNoteFile(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFile notefile = null;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string filepath = GetNoteFilePath(notebookId, notePageId, fileType, name);
            try
            {
                notefile = await ROOT_FOLDER.CreateFileAsync(filepath, CreationCollisionOption.OpenIfExists);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get {filepath}");
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get file: notebook: {notebookId}, page: {notebookId}, type: {fileType}, name: {name}, details: {ex.Message}");
                return null;
            }
            return notefile;
        }

        /// <summary>
        /// Save BitmapImage
        /// </summary>
        /// <param name="notebookId"></param>
        /// <param name="pageId"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<bool> SaveImageForNotepage(string notebookId, string pageId, string name, WriteableBitmap wb)
        {
            bool isSuccess = true;
            try
            {
                Guid bitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
                var imageFile = await FileService.FileManager.getSharedFileManager().CreateImageFileForPage(notebookId, pageId, name);

                using (IRandomAccessStream stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(bitmapEncoderGuid, stream);
                    Stream pixelStream = wb.PixelBuffer.AsStream();
                    byte[] pixels = new byte[pixelStream.Length];
                    await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                              (uint)wb.PixelWidth,
                              (uint)wb.PixelHeight,
                              96.0,
                              96.0,
                              pixels);
                    //Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(imgstream);
                    //Windows.Graphics.Imaging.PixelDataProvider pxprd = await decoder.GetPixelDataAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Straight, new Windows.Graphics.Imaging.BitmapTransform(), Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation, Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

                    await encoder.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                Debug.WriteLine("Failed to save image");
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save BitmapImage: notebook: {notebookId}, page: {notebookId}, name: {name}, details: {ex.Message}");

            }
            return isSuccess;

        }

        public async Task<StorageFile> GetNoteFileNotCreate(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            StorageFile notefile = null;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string filepath = GetNoteFilePath(notebookId, notePageId, fileType, name);
            try
            {
                notefile = await ROOT_FOLDER.GetFileAsync(filepath);
            }
            catch (Exception)
            {
                Debug.WriteLine($"Failed to get {filepath}");
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get {filepath}");
                return null;
            }
            return notefile;
        }

        /// <summary>
        /// return a file path by notebook and page id, apply to various file types 
        /// </summary>
        /// <param name="notebookId"></param>
        /// <param name="notePageId"></param>
        /// <param name="fileType"></param>
        /// <param name="name"></param>
        /// <returns></returns>
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
                case NoteFileType.Image:
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
                case NoteFileType.Audio:
                    foldername += @"Audio\";
                    break;
                case NoteFileType.Transcriptions:
                    foldername += @"Audio\";
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
                case NoteFileType.Audio:
                    filename = name + ".wav";
                    break;
                case NoteFileType.Transcriptions:
                    filename = name + ".xml";
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
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save phenotypes for notebook {notebookId}, error: {ex.Message}");
                result = false;
            }
            return result;
        }


        // get stroke image by notebook and note page
        public BitmapImage GetStrokeImage(string notebookId, string pageId)
        {
            string imagePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
            try
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri($"ms-appdata:///local/{imagePath}"));
                return bitmapImage;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get stroke image, notebook: {notebookId}, page: {pageId}" + 
                    $"details: {ex.Message}");
            }
            return null;
        }

        

        // get all notebook objects
        public async Task<List<Notebook>> GetAllNotebookObjects()
        {
            List<Notebook> result = new List<Notebook>();
            try
            {
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                QueryOptions queryOptions = new QueryOptions(CommonFolderQuery.DefaultQuery);
                queryOptions.SortOrder.Clear();
                SortEntry se = new SortEntry();
                se.PropertyName = "System.DateModified";
                se.AscendingOrder = false;
                queryOptions.SortOrder.Add(se);

                StorageFolderQueryResult queryResult = ROOT_FOLDER.CreateFolderQueryWithOptions(queryOptions);
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
                LogService.MetroLogger.getSharedLogger().Error("Failed to get all notebook ids.");
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

        public async Task<bool> DeleteNotebookById(string id)
        {
            try
            {
                var notebook = await ROOT_FOLDER.GetFolderAsync(id);
                await notebook.DeleteAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                LogService.MetroLogger.getSharedLogger().Error($"Failed to delete notebook: {id}");

            }
            return true;
        }

    }
}
