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
using System.Xml;
using PhenoPad.LogService;
using PhenoPad.SpeechService;
using Windows.UI.Input.Inking;

namespace PhenoPad.FileService
{
    /// <summary>
    /// Specifies PhenoPad file type constants
    /// </summary>
    public enum NoteFileType
    {
        Strokes,
        Image,
        ImageAnnotation,
        Phenotypes,
        PhenotypeCandidates,
        Audio,
        AudioMeta,
        Transcriptions,
        Video,
        Meta,
        ImageAnnotationMeta,
        EHR,
        EHRFormat,
        OperationLog,
        NoteText,
        RecognizedPhraseMeta
    };

    /// <summary>
    /// Class for managing all note file components including all text and media forms.
    /// </summary>
    class FileManager
    {
        #region attributes
        public static FileManager sharedFileManager = null;
        private MainPage rootPage = MainPage.Current;
        private string STROKE_FILE_NAME = "strokes.gif";
        private string EHR_FILE_NAME = "ehr.txt";
        private string EHR_FORMAT_NAME = "ehrformat.xml";
        private string OPERATION_LOG_NAME = "OperationLogs.txt";
        private string PHENOTYPE_FILE_NAME = "phenotypes.txt";
        private string PHENOTYPECANDIDATE_FILE_NAME = "phenotype_candidates.txt";
        private string NOTENOOK_NAME_PREFIX = "note_";
        private string NOTE_META_FILE = "meta.xml";
        private string AUDIO_META_FILE = "AudioMeta.xml";
        private string NOTE_TEXT_FILE = "text.txt";
        public string currentNoteboookId = "";
        //setting application data root folder as the default disk access location
        private StorageFolder ROOT_FOLDER = ApplicationData.Current.LocalFolder;
        
        #endregion

        /// <summary>
        /// Creates a new FileManager instance.
        /// </summary>
        public FileManager()
        {
            Debug.WriteLine("Application local folder is " + ROOT_FOLDER.Path.ToString());
        }

        #region GET methods
        /// <summary>
        /// Returns the file manager object if it exists, otherwise initialize a new file manager.
        /// </summary>
        /// <returns>the shared file manager object</returns>
        public static FileManager getSharedFileManager()
        {
            if (sharedFileManager == null)
            {
                sharedFileManager = new FileManager();
            }
            return sharedFileManager;
        }

        
        public async Task<List<string>> GetAllNotebookIds()
        {
            /// <summary>
            /// Returns all Notebook ids from local notes.Returns null if failed.
            /// </summary>
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
                MetroLogger.getSharedLogger().Error($"Failed to get notebook id list: {ex.Message}");
            }
            return null;
        }

        
        public async Task<List<string>> GetPageIdsByNotebook(string notebookId)
        {
            /// <summary>
            /// Returns all note page IDs by notebook ID.Returns null if failed.
            /// </summary>
            List<string> result = new List<string>();
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(notebookId);
                var folders = await notebookFolder.GetFoldersAsync();
                foreach (var f in folders)
                {
                    // only folders with numeric names are pages
                    if (int.TryParse(f.Name, out int n))
                        result.Add(f.Name);
                }
                return result;
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser("Failed to get all note page ids.", NotifyType.ErrorMessage, 2);
                MetroLogger.getSharedLogger().Error($"Failed to get page ids of {notebookId}: {ex.Message}");
            }
            return null;
        }

        
        public async Task<Notebook> GetNotebookObjectFromXML(string notebookId)
        {
            /// <summary>
            /// Returns the Notebook object from XML meta file by Notebook ID. Returns null if failed.
            /// </summary>
            try
            {
                //Debug.WriteLine($"Fetching notebook meta file object of {notebookId}");
                // meta data
                var metafile = await GetNoteFile(notebookId, "", NoteFileType.Meta);
                Notebook obj = null;
                obj = (Notebook) await LoadObjectFromSerilization(metafile, typeof(Notebook));
                return obj;
            }
            catch (Exception) {
                return null;
            }
        }

        public async Task<List<string>> GetLogStrings(string notebookID) {
            try
            {
                List<string> logs = new List<string>();
                var file = await GetNoteFile(notebookID, "", NoteFileType.OperationLog);
                var log = await FileIO.ReadLinesAsync(file);
                foreach (string line in log)
                    logs.Add(line.TrimEnd());
                return logs;
            }
            catch (Exception e) {
                MetroLogger.getSharedLogger().Error(e + e.Message);
                return null;
            }
        }

        
        public async Task<List<ImageAndAnnotation>> GetImgageAndAnnotationObjectFromXML(string notebookId, string pageId)
        {
            /// <summary>
            /// Returns all Image ad Annotations from XML meta file by Notebook ID and page IDs. Returns null if failed.
            /// </summary>
            try
            {
                var metafile = await GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotationMeta);
                object obj = await LoadObjectFromSerilization(metafile, typeof(List<ImageAndAnnotation>));
                if (obj != null)
                    return obj as List<ImageAndAnnotation>;
                else
                    return null;
            }
            catch (Exception) {
                //LogService.MetroLogger.getSharedLogger().Error($"{notebookId}-{pageId}:{e}:{e.Message}");
                return null;
            }
            
        }
        public async Task<List<RecognizedPhrases>> GetRecognizedPhraseFromXML(string notebookId, string pageId)
        {
            try
            {
                var metafile = await GetNoteFile(notebookId, pageId, NoteFileType.RecognizedPhraseMeta);
                object obj = await LoadObjectFromSerilization(metafile, typeof(List<RecognizedPhrases>));
                if (obj != null)
                    return obj as List<RecognizedPhrases>;
                else
                    return null;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Get recognized phrase from file: {notebookId}-{pageId}:{e}:{e.Message}");
                return null;
            }

        }


        
        public async Task<List<Phenotype>> GetSavedPhenotypeObjectsFromXML(string notebookId, NoteFileType type = NoteFileType.Phenotypes)
        {
            /// <summary>
            /// Returns all saved phenotypes object or phenotype candidates from XML meta file by Notebook ID. Returns null if failed.
            /// </summary>
            try
            {
                // meta data
                var phenofile = await GetNoteFile(notebookId, "", type);
                object obj = await LoadObjectFromSerilization(phenofile, typeof(List<Phenotype>));
                if (obj != null)
                    return obj as List<Phenotype>;
                return null;
            }
            catch (Exception) {
                return null;
            }
        }

        /// <summary>
        /// Returns the locally saved ASR transcirpt as a list of TextMessage objects.
        /// </summary>
        /// <param name="notebookId">The ID of the notebook from which the transcript was saved.</param>
        /// <param name="name">Unused in the function, default value is an empty string.</param>
        /// <returns></returns>
        public async Task<List<TextMessage>> GetSavedTranscriptsFromXML(string notebookId,string name = "")
        {
            try
            {
                List < TextMessage > msgs = new List<TextMessage>();
                string transcriptPath = $"{notebookId}\\Transcripts";
                StorageFolder folder = await ROOT_FOLDER.GetFolderAsync(transcriptPath);
                //Debug.WriteLine("transcript folder ="+folder.Path);
                IReadOnlyList<StorageFile> fileList = await folder.GetFilesAsync();
                SpeechManager.getSharedSpeechManager().setAudioIndex(fileList.Count);
                foreach (StorageFile file in fileList)
                {
                    List<TextMessage> obj = await LoadObjectFromSerilization(file, typeof(List<TextMessage>)) as List<TextMessage>;
                    if (obj != null)
                        msgs.AddRange(obj);
                }
                return msgs;
            }
            catch (Exception e) {
                MetroLogger.getSharedLogger().Error( e + e.Message);
                return null;
            }
        }

        public async Task<List<string>> GetSavedAudioNamesFromXML(string notebookId) {
            try
            {
                // meta data
                var file = await GetNoteFile(notebookId, "", NoteFileType.AudioMeta);
                object obj = await LoadObjectFromSerilization(file, typeof(List<string>));
                if (obj != null)
                    return obj as List<string>;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);          
            }
            return null;
        }

        /// <summary>
        /// Returns the locally saved audio file with the specified name from the specified notebook.
        /// </summary>
        /// <param name="notebookId">The notebook the audio recording is from.</param>
        /// <param name="name">The name of the audio file.</param>
        /// <returns>
        /// A StorageFile object representing the audio file.
        /// If save file does not exist, returns null.
        /// </returns>
        public async Task<StorageFile> GetSavedAudioFile(string notebookId, string name) {
            var path = GetNoteFilePath(notebookId, "", NoteFileType.Audio, name);
            Debug.WriteLine("tring to get " + path);
            try
            {
                var file = await ROOT_FOLDER.TryGetItemAsync(path);
                if (file != null)
                    return file as StorageFile;
                
            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error("Failed to get audio file from:" + path + e.Message);
            }
            return null;
        }

        /// <summary>
        /// Write audio data in the form of bytes to a save file.
        /// </summary>
        /// <param name="notebookId">the unique identifier of the note the audio originated from</param>
        /// <param name="name">name of the audio file</param>
        /// <param name="bytes">byte audio data in a list</param>
        /// <returns></returns>
        public async Task<bool> SaveByteAudioToFile(string notebookId, string name, List<Byte> bytes)
        {
            var path = GetNoteFilePath(notebookId, "", NoteFileType.Audio, name);

            try
            {
                var file = await ROOT_FOLDER.CreateFileAsync(path, CreationCollisionOption.ReplaceExisting);
                using (Stream s = await file.OpenStreamForWriteAsync()) {
                    await s.WriteAsync(bytes.ToArray(), 0, bytes.Count);
                }
                Debug.WriteLine("audio file bytes written to " + path);
                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to get audio file from:" + path + e.Message);
            }
            return false;
        }

        /// <summary>
        /// Stores the names of the saved recordings from this note to an XML meta file.
        /// </summary>
        /// <param name="notebookId">the unique identifier of the note</param>
        /// <param name="names">a list of the names of the saved recordings</param>
        /// <returns>(bool)true if save successful, (bool)false otherwise</returns>
        public async Task<bool> SaveAudioNamesToXML(string notebookId, List<string>names) {
            try
            {
                var path = GetNoteFilePath(notebookId, "", NoteFileType.AudioMeta);
                bool result = await SaveObjectSerilization(path, names, typeof(List<string>));
                return result;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
                return false;
            }
        }

        
        public async Task<StorageFile> GetOperationLogFile(string notebookId)
        {
            /// <summary>
            /// Gets the operation log file under local folder, if such file does not exist, creates a new one and returns it
            /// </summary>
            try
            { 
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                string filepath = GetNoteFilePath(notebookId, "", NoteFileType.OperationLog);
                var item = await ROOT_FOLDER.TryGetItemAsync(filepath);
                if (item == null)
                {
                    StorageFile file = await ROOT_FOLDER.CreateFileAsync(filepath, CreationCollisionOption.OpenIfExists);
                    return file;
                }
                return (StorageFile)item;
           
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to get operation logs: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AppendLogToFile(List<string> cachedLogs, string notebookId) {
            try {
                StorageFile logFile = await GetOperationLogFile(notebookId);
                while (logFile == null)
                    logFile = await GetOperationLogFile(notebookId);
                await FileIO.AppendLinesAsync(logFile, cachedLogs);
                return true;

            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error("FileManager:" + e.Message);
                return false;
            }
        }

       
        public async Task<StorageFile> GetNoteFile(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            /// <summary>
            /// Creates and returns a new local file for given Notebook, returns null if failed.
            /// </summary>
            try
            {
                StorageFile notefile = null;
                //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                string filepath = GetNoteFilePath(notebookId, notePageId, fileType, name);
                //Debug.WriteLine(filepath);
                notefile = await ROOT_FOLDER.CreateFileAsync(filepath, CreationCollisionOption.OpenIfExists);
                return notefile;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to create file: notebook: {notebookId}, " +
                    $"page: {notebookId}, type: {fileType}, name: {name}, details: {ex.Message}");
                return null;
            }
        }

        
        public async Task<StorageFile> GetNoteFileNotCreate(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            /// <summary>
            /// Returns the file given IDs,type and name fron root folder. Return null if failed.
            /// </summary>
            try
            {
                string filepath = GetNoteFilePath(notebookId, notePageId, fileType, name);
                var item = await ROOT_FOLDER.TryGetItemAsync(filepath);
                if (item == null)
                    return null;
                //notefile = await ROOT_FOLDER.GetFileAsync(filepath);
                return (StorageFile)item;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed to get file:{e}+{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the path of a saved file based on file type given the notebook and note page's id.
        /// </summary>
        /// <param name="notebookId">the unique identifier of the note</param>
        /// <param name="notePageId">the identifier of the note page</param>
        /// <param name="fileType">the type of the file in question</param>
        /// <param name="name">the name of the file</param>
        /// <returns></returns>
        public string GetNoteFilePath(string notebookId, string notePageId, NoteFileType fileType, string name = "")
        {
            string foldername = String.Format(@"{0}\{1}\", notebookId, notePageId);
            switch (fileType)
            {
                case NoteFileType.Meta:
                    foldername = String.Format(@"{0}\", notebookId);
                    break;
                case NoteFileType.AudioMeta:
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
                case NoteFileType.RecognizedPhraseMeta:
                    foldername += @"RecognizedPhrases\";
                    break;
                case NoteFileType.Strokes:
                    foldername += @"Strokes\";
                    break;
                case NoteFileType.Phenotypes:
                    foldername = String.Format(@"{0}\", notebookId);
                    break;
                case NoteFileType.PhenotypeCandidates:
                    foldername = String.Format(@"{0}\", notebookId);
                    break;
                case NoteFileType.Audio:
                    foldername += @"Audio\";
                    break;
                case NoteFileType.Transcriptions:
                    foldername += @"Transcripts\";
                    break;
                case NoteFileType.Video:
                    foldername += @"Videos\";
                    break;
            }

            string filename = "";
            switch (fileType)
            {
                case NoteFileType.Meta:
                    filename = NOTE_META_FILE;
                    break;
                case NoteFileType.AudioMeta:
                    filename = AUDIO_META_FILE;
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
                case NoteFileType.RecognizedPhraseMeta:
                    filename = NOTE_META_FILE;
                    break;
                case NoteFileType.Strokes:
                    filename = STROKE_FILE_NAME;
                    break;
                case NoteFileType.Phenotypes:
                    filename = PHENOTYPE_FILE_NAME;
                    break;
                case NoteFileType.PhenotypeCandidates:
                    filename = PHENOTYPECANDIDATE_FILE_NAME;
                    break;
                case NoteFileType.Audio:
                    if (name != "")
                        filename = name + ".wav";
                    break;
                case NoteFileType.Transcriptions:
                    filename = name + ".xml";
                    break;
                case NoteFileType.EHR:
                    filename = EHR_FILE_NAME;
                    break;
                case NoteFileType.EHRFormat:
                    filename = EHR_FORMAT_NAME;
                    break;
                case NoteFileType.OperationLog:
                    filename = OPERATION_LOG_NAME;
                    break;
                case NoteFileType.NoteText:
                    filename = NOTE_TEXT_FILE;
                    break;
                case NoteFileType.Video:
                    filename = name + ".mp4";
                    break;
            }

            return foldername + filename;
        }

        
        public BitmapImage GetStrokeImage(string notebookId, string pageId)
        {
            /// <summary>
            /// Returns stroke bitmap image from file by IDs, returns null if failed
            /// </summary>
            try
            {
                string imagePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
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

        
        public async Task<List<Notebook>> GetAllNotebookObjects()
        {
            /// <summary>
            /// Returns all Notebook objects from root file, returns null if failed.
            /// </summary>
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
            catch (Exception)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to get all notebook ids.");
                rootPage.NotifyUser("Failed to get all notebook ids.", NotifyType.ErrorMessage, 2);
            }
            return null;
        }

        
        public async Task<List<ImageAndAnnotation>> GetAllImageAndAnnotationObjects(string notebookId)
        {
            /// <summary>
            /// Returns all image and annotation objects from root file, returns null if failed.
            /// </summary>
            try
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
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error($"{notebookId}:{e}:{e.Message}");
                return null;
            }
        }

        public async Task<List<AudioFile>> GetAllAudioFileObjects(string notebookId)
        {
            try
            {
               
                List<AudioFile> result = new List<AudioFile>();
                string path = String.Format(@"{0}\Audio\", notebookId);
                //Debug.WriteLine($"audio path = {path}");
                StorageFolder audioFolder = await ROOT_FOLDER.GetFolderAsync(path);
                var audios = await audioFolder.GetFilesAsync();
                foreach (var audio in audios) {
                    AudioFile a = new AudioFile(notebookId, audio);
                    result.Add(a);
                }
                return result;

            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"{notebookId}:{e}:{e.Message}");
                return new List<AudioFile>();
            }
        }

        
        public async Task<List<NotePage>> GetAllNotePageObjects(string notebookId)
        {
            /// <summary>
            /// Returns all NotePage objects of the given Notebook ID from root file, returns null if failed.
            /// </summary>
            try
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
            catch (Exception) {
                return null;
            }

        }
        #endregion
        
        #region CREATE/SAVE methods

        /// <summary>
        /// Creates and returns a unique name based on current time tick.
        /// </summary>
        public string CreateUniqueName()
        {
            return $@"{DateTime.Now.Ticks}";
            //return "goodname";
        }

        /// <summary>
        /// Creates a local folder for this notebook and returns boolean results.
        /// </summary>
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
                await notebookFolder.CreateFolderAsync("Transcripts", CreationCollisionOption.OpenIfExists);

                Debug.WriteLine("Notebook Folder is " + notebookFolder);
                Notebook nb = new Notebook(notebookId);
                var metaFile = GetNoteFilePath(notebookId, "", NoteFileType.Meta);
                result = await SaveObjectSerilization(metaFile, nb, nb.GetType()); // save meta data to xml file
            }
            catch (Exception)
            {
                MetroLogger.getSharedLogger().Error($"Failed to create notebook {notebookId}");
                return false;
            }
            Debug.WriteLine($"Successfully created notebook {notebookId}");
            return result;
        }

        /// <summary>
        /// Created a new file folder for a note page and creates folders to store components on notepage, returns boolean result.
        /// </summary>
        public async Task<bool> CreateNotePage(Notebook note, string pageId)
        {
            Debug.WriteLine($"Creating note page {pageId} for notebook {note.id}");
            if (note == null)
                return false;

            bool result = true;

            try
            {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(note.id);
                var pageFolder = await notebookFolder.CreateFolderAsync(pageId.ToString(), CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Strokes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("ImagesWithAnnotations", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Phenotypes", CreationCollisionOption.OpenIfExists);
                await pageFolder.CreateFolderAsync("Videos", CreationCollisionOption.OpenIfExists);
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

        /// <summary>
        /// Creates file to save images and annotations on note, returns null if failed.
        /// </summary>
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
                MetroLogger.getSharedLogger().Error($"Failed to create image file, notebook:{notebookId}, " +
                    $"page: {pageId}, name: {name}, details: {ex.Message} ");
            }
            return null;
        }

        public async Task<StorageFile> CreateVideoFileForPage(string notebookId, string pageId, string name) {
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string foldername = String.Format("{0}\\{1}\\Videos", notebookId, pageId);
            try
            {
                var imageFolder = await ROOT_FOLDER.GetFolderAsync(foldername);
                if (imageFolder != null)
                {
                    return await imageFolder.CreateFileAsync(name + ".mp4");

                }
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to create video file, notebook:{notebookId}, " +
                    $"page: {pageId}, name: {name}, details: {ex.Message} ");
            }
            return null;


        }

        /// <summary>
        /// Creates and saves the photo file to local disk and returns boolean result
        /// </summary>
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
            catch (Exception ex)
            {
                isSuc = false;
                LogService.MetroLogger.getSharedLogger().Error("Falied to copy photo to local folder: " + ex.Message);
            }
            return isSuc;
        }

        /// <summary>
        /// Saves notebook meta file object to root folder.
        /// </summary>
        public async Task<bool> SaveToMetaFile(Notebook notebook)
        {
            Debug.WriteLine($"Saving notebook meta file object of {notebook.id}");
            try
            {
                var metafile = GetNoteFilePath(notebook.id, "", NoteFileType.Meta);
                return await SaveObjectSerilization(metafile, notebook, typeof(Notebook));
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"{notebook.id}:Failed to save to meta file:{e.Message}");
                return false;
            }
        }

        // save photos and annotations to disk
        /// <summary>
        /// Saves all Images and Annotations to local disk and returns boolean result
        /// </summary>
        public async Task<bool> SaveNotePageDrawingAndPhotos(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            try
            {
                List<ImageAndAnnotation> imageList = await notePage.GetAllAddInObjects();
                throw new Exception("SaveNotePageDrawingAndPhotos need to be implemented, you might want to try CreateImageFileForPage");
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"{notebookId}-{pageId}:Failed to save to meta file:{e}:{e.Message}");
                isSuccessful = false;
            }

            return isSuccessful;
        }

        /// <summary>
        /// Creates and saves all strokes of the notepage to local file and returns boolean result.
        /// </summary>
        public async Task<bool> SaveNotePageStrokes(string notebookId, string pageId, NotePageControl notePage)
        {
            bool isSuccessful = true;
            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            try
            {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(notebookId);
                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                var strokesFolder = await pageFolder.GetFolderAsync("Strokes");
                var strokesFile = await strokesFolder.CreateFileAsync(this.STROKE_FILE_NAME, CreationCollisionOption.OpenIfExists);
                
                //Saves strokes on different canvas based on whether an EHR document exists
                if (notePage.ehrPage == null)
                    isSuccessful = await saveStrokes(strokesFile, notePage.inkCan);
                else
                    isSuccessful = await saveStrokes(strokesFile, notePage.ehrPage.annotations);

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

        /// <summary>
        /// Saves the EHR text file into local .txt file
        /// </summary>
        public async Task<bool> SaveEHRText(string notebookId, string pageId, EHRPageControl ehr) {

            try
            {
                var notebookFolder = await ROOT_FOLDER.GetFolderAsync(notebookId);
                var pageFolder = await notebookFolder.GetFolderAsync(pageId);
                var ehrFile = await pageFolder.CreateFileAsync(this.EHR_FILE_NAME, CreationCollisionOption.OpenIfExists);
                //saves text to local .txt file
                var buffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(ehr.getText(ehr.EHRTextBox), Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
                await FileIO.WriteBufferAsync(ehrFile, buffer);
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(
                    $"Failed to save EHR, notebook: {notebookId}, page: {pageId}, details: "
                    + e.Message);
                return false;
            }
            return true;

        }

        public async Task<bool> SaveNoteText(string notebookId, string pageId, string text) {
            try
            {
                string textPath = GetNoteFilePath(notebookId, pageId, NoteFileType.NoteText);
                StorageFile textFile = await ROOT_FOLDER.CreateFileAsync(textPath, CreationCollisionOption.ReplaceExisting);
                //saves text to local .txt file
                await FileIO.WriteTextAsync(textFile, text);
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(
                    $"Failed to save note text, notebook: {notebookId}, page: {pageId}, details: "
                    + e.Message);
                return false;
            }
            return true;
        }

        SemaphoreSlim serilizationSS = new SemaphoreSlim(1);
        /// <summary>
        /// Saves the state of the object to a XML meta file.
        /// </summary>
        /// <param name="filepath">path to save the XML file to</param>
        /// <param name="tosave">the object to be saved</param>
        /// <param name="type">the type of the object being saved</param>
        /// <returns>(bool)true if save successful, (bool)false otherwise</returns>
        /// <remarks>
        /// Serialize the object into XML document to save the state of the object.
        /// Used to save states and properties of a note for future access.
        /// </remarks>
        public async Task<bool> SaveObjectSerilization(string filepath, Object tosave, Type type)
        {
            await serilizationSS.WaitAsync();
            bool result = true;
            StorageFile sfile = null;
            Stream stream = null;
            try
            {
                sfile = await ROOT_FOLDER.CreateFileAsync(filepath, CreationCollisionOption.ReplaceExisting);
                //Windows.Storage.CachedFileManager.DeferUpdates(sfile);
                stream = await sfile.OpenStreamForWriteAsync();
                var serializer = new XmlSerializer(type);
                using (stream)
                {
                    serializer.Serialize(stream, tosave);
                }
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save object serilization:{e.Message}");
                result = false;
            }
            finally
            {
                serilizationSS.Release();
                if (stream != null)
                {
                    stream.Dispose();
                }
                //TODO: why is this commented out?
                // Finalize write so other apps can update file.
                // Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(sfile);
            }
            return result;
        }

        /// <summary>
        /// Save ink strokes to a gif file.
        /// </summary>
        public async Task<bool> saveStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            try {
                // Prevent updates to the file until updates are 
                // finalized with call to CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(strokesFile);
                // Open a file stream for writing.
                IRandomAccessStream stream = await strokesFile.OpenAsync(FileAccessMode.ReadWrite);
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
                    await CachedFileManager.CompleteUpdatesAsync(strokesFile);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    return true;
                }
                else
                {
                    MetroLogger.getSharedLogger().Error($"Failed to save strokes of InkCanvas.");
                    return false;
                }

            }
            catch (Exception) {
                return false;
            }

        }

        /// <summary>
        /// Encodes and saves BitmapImage to file and returns boolean result.
        /// </summary>
        public async Task<bool> SaveImageForNotepage(string notebookId, string pageId, string name, WriteableBitmap wb)
        {
            bool isSuccess = true;
            try
            {
                Guid bitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
                var imageFile = await getSharedFileManager().CreateImageFileForPage(notebookId, pageId, name);

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
                LogService.MetroLogger.getSharedLogger().Error($"Failed to save BitmapImage: notebook: {notebookId}, page: {notebookId}, name: {name}, details: {ex.Message}");

            }
            return isSuccess;

        }

        public async Task<bool> SaveEHRFormats(string notebookId, string pageId, EHRPageControl ehr) {
            try
            {
                EHRFormats format = new EHRFormats(ehr);
                string xml = format.Serialize();
                string path = GetNoteFilePath(notebookId, pageId, NoteFileType.EHRFormat);
                StorageFile sfile = await ROOT_FOLDER.CreateFileAsync(path, CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteTextAsync(sfile, xml);
                return true;

            }
            catch (Exception e) {
                Debug.WriteLine(e + e.Message);
                return false;
            }
        }
        public async Task<EHRFormats> LoadEHRFormats(string notebookId, string pageId) {
            try
            {
                string path = GetNoteFilePath(notebookId, pageId, NoteFileType.EHRFormat);
                var sfile = await ROOT_FOLDER.TryGetItemAsync(path);
                if (sfile != null) {
                    sfile = await ROOT_FOLDER.GetFileAsync(path);
                    string xml = await Windows.Storage.FileIO.ReadTextAsync((StorageFile)sfile);
                    EHRFormats ehrformat = EHRFormats.Deserialize(xml);
                    return ehrformat;
                }
                return null;
            }
            catch (FileNotFoundException) {
                //this may happen when creating a new ehr and thus can be omitted
                return null;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error("FileManager|"+ e.Message);
                return null;
            }
        }

        /// <summary>
        /// Creates and returns a new Notebook ID
        /// </summary>
        public string createNotebookId()
        {
            return NOTENOOK_NAME_PREFIX + CreateUniqueName();
        }


        /// <summary>
        /// Saves collected phenotypes to file and return boolean results
        /// </summary>
        public async Task<bool> saveCollectedPhenotypesToFile(string notebookId)
        {
            bool result = true;
            try
            {
                //saved phenotypes
                string phenopath = GetNoteFilePath(notebookId, "", NoteFileType.Phenotypes);
                List<Phenotype> saved = new List<Phenotype>(PhenotypeManager.getSharedPhenotypeManager().savedPhenotypes);
                result = await SaveObjectSerilization(phenopath, saved, typeof(List<Phenotype>));
                //candidates
                phenopath = GetNoteFilePath(notebookId, "", NoteFileType.PhenotypeCandidates);
                List<Phenotype> cand = new List<Phenotype>(PhenotypeManager.getSharedPhenotypeManager().phenotypesCandidates);
                result &= await SaveObjectSerilization(phenopath, cand, typeof(List<Phenotype>));
            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error($"Failed to save phenotypes for notebook {notebookId}, error: {ex.Message}");
                result = false;
            }
            return result;
        }

        #endregion

        #region LOAD methods

        /// <summary>
        /// Loads all ink strokes from local file and returns boolean result.
        /// </summary>
        public async Task<bool> LoadNotePageStroke(string notebookId, string pageId, NotePageControl notePage,InkCanvas canvas = null)
        {
            bool isSuccessful = true;
            try
            {
                string strokePath = GetNoteFilePath(notebookId, pageId, NoteFileType.Strokes);
                var s = await ROOT_FOLDER.TryGetItemAsync(strokePath);
                if (s != null)
                {
                    var strokesFile = await ROOT_FOLDER.GetFileAsync(strokePath);
                    //is canvas not null, loading strokes for view mode
                    if (canvas != null)
                        isSuccessful = await loadStrokes(strokesFile, canvas);
                    //loads the stroked based on whether the current note page is an EHR document
                    else
                    {
                        if (notePage.ehrPage == null)
                            isSuccessful = await loadStrokes(strokesFile, notePage.inkCan);
                        else
                            isSuccessful = await loadStrokes(strokesFile, notePage.ehrPage.annotations);
                    }

                }
                else
                    isSuccessful = false;

                return isSuccessful;
            }
            catch (Exception e)
            {
                //rootPage.NotifyUser(e.Message, NotifyType.ErrorMessage, 2);
                LogService.MetroLogger.getSharedLogger().Error("Failed to load note page stroke from disk: "
                    + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Load object from XML meta file, returns null if failed.
        /// </summary>
        public async Task<object> LoadObjectFromSerilization(StorageFile metaFile, Type type)
        {
            Stream stream = null;
            try
            {
                stream = await metaFile.OpenStreamForReadAsync();
                var serializer = new XmlSerializer(type);
                using (stream)
                {
                    object obj = serializer.Deserialize(stream);
                    return obj;
                }
            }
            catch (InvalidOperationException) {     
                return null;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to fetch object from serilization file: {metaFile.Path}, error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Load ink strokes from a gif file.
        /// </summary>
        public async Task<bool> loadStrokes(StorageFile strokesFile, InkCanvas inkcancas)
        {
            // User selects a file and picker returns a reference to the selected file.
            try
            {
                if (strokesFile == null)
                    return false;
                // Open a file stream for reading.
                IRandomAccessStream stream = await strokesFile.OpenAsync(FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    await inkcancas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
                }
                stream.Dispose();
                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to load strokes:{e.Message}.");
                return false;
            }
        }

        public async Task<string> LoadNoteText(string notebookId, string pageId)
        {
            try
            {
                string textPath = GetNoteFilePath(notebookId, pageId, NoteFileType.NoteText);
                StorageFile textFile = await ROOT_FOLDER.GetFileAsync(textPath);
                //saves text to local .txt file
                if (textFile == null)
                    return "";
                string text = await FileIO.ReadTextAsync(textFile);
                return text;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(
                    $"Failed to load note text, notebook: {notebookId}, page: {pageId}, details: "
                    + e.Message);
                return "";
            }
        }

        #endregion

        #region DELETE methods

        /// <summary>
        /// Deletes the Notebook file by ID and returns boolean result.
        /// </summary>
        public async Task<bool> DeleteNotebookById(string id)
        {
            try
            {
                var notebook = await ROOT_FOLDER.GetFolderAsync(id);
                await notebook.DeleteAsync();
                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to delete notebook: {id}:{e.Message}");
                return false;
            }           
        }

        public async Task<bool> DeleteAddInFile(string notebookId, string pageId, string name) {
            try
            {
                string strokepath = GetNoteFilePath(notebookId, pageId, NoteFileType.ImageAnnotation,name);
                string imagepath = GetNoteFilePath(notebookId, pageId, NoteFileType.Image, name);
                var s = await ROOT_FOLDER.TryGetItemAsync(strokepath);
                if (s != null)
                    await s.DeleteAsync();
                var i = await ROOT_FOLDER.TryGetItemAsync(imagepath);
                if (i != null)
                    await i.DeleteAsync();
                return true;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"Failed to delete addin file: {e}:{e.Message}");
                return false;
            }

        }

        #endregion
    }
}
