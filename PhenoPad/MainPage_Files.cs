using System;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using PhenoPad.PhenotypeService;
using Windows.UI.Xaml.Input;
using System.Collections.Generic;
using Windows.UI.Popups;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using System.Text;
using Windows.Globalization;
using PhenoPad.SpeechService;
using Windows.Devices.Sensors;
using Windows.UI;
using System.Diagnostics;
using PhenoPad.CustomControl;
using Windows.Graphics.Display;
using System.Reflection;
using System.Linq;
using Windows.UI.Xaml.Media.Animation;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using PhenoPad.WebSocketService;
using Windows.ApplicationModel.Core;
using PhenoPad.Styles;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using PhenoPad.FileService;
using Windows.UI.Xaml.Data;
using System.ComponentModel;
using System.Threading;
using PhenoPad.LogService;
using PhenoPad.BluetoothService;
using Windows.System.Threading;
using System.IO;
using Windows.Storage;
using Windows.Media.Editing;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PhenoPad
{
    //This partial class of MainPage mainly contains logic methods for file I/O such as save / load
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        //using a semaphore to ensure only one thread is accessing resources
        //its purpose is to avoid concurrent accesses to ensure saving process
        private SemaphoreSlim savingSemaphoreSlim = new SemaphoreSlim(1);
        public bool loadFromDisk = false;
        string prefix = "transcriptions_";
        public List<TextMessage> conversations;

        //=============================================================================================

        /// <summary>
        /// Initializes the new notebook and creates a locally saved file for it.
        /// </summary>
        public async void InitializeNotebook()
        {
            NotifyUser("Creating Notebook...",NotifyType.StatusMessage,3);
            MetroLogger.getSharedLogger().Info("Initialize a new notebook.");
            PhenoMana.clearCache();

            // Tries to create a file structure for the new notebook.
            {
                notebookId = FileManager.getSharedFileManager().createNotebookId();
                FileManager.getSharedFileManager().currentNoteboookId = notebookId;
                bool result = await FileManager.getSharedFileManager().CreateNotebook(notebookId);
                SpeechManager.getSharedSpeechManager().setAudioIndex(0);
                if (!result)
                    NotifyUser("Failed to create file structure, notes may not be saved.", NotifyType.ErrorMessage, 2);
                else
                    notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

                if (notebookObject != null)
                    noteNameTextBox.Text = notebookObject.name;
            }

            notePages = new List<NotePageControl>();
            pageIndexButtons = new List<Button>();

            NotePageControl aPage = new NotePageControl(notebookId,"0");
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = aPage;
            curPageIndex = 0;
            PageHost.Content = curPage;
            addNoteIndex(curPageIndex);
            setNotePageIndex(curPageIndex);

            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;
            //by default uses internal microphone
            SurfaceMicRadioButton_Checked(null, null);
            // create file sturcture for this page
            await FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());
            curPage.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Initializes the notebook by loading from pre-existing local save file.
        /// </summary>
        public async void InitializeNotebookFromDisk()
        {
            NotifyUser("Loading Notebook ...", NotifyType.StatusMessage, 3);
            MetroLogger.getSharedLogger().Info("Initializing notebook from disk ...");
            PhenoMana.clearCache();
            try
            {

                //If notebook file exists, continues with loading...
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

                if (notebookObject == null) {
                    Debug.WriteLine("notebook Object is null");
                }

                //Gets all stored pages and notebook object from the disk
                List<string> pageIds = await FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);

                SurfaceMicRadioButton_Checked(null, null);
                //if (notebookObject != null)
                noteNameTextBox.Text = notebookObject.name;

                //Gets the possible stored conversation transcripts from XML meta
                SpeechManager.getSharedSpeechManager().setAudioIndex(notebookObject.audioCount);
                String fName = prefix;
                this.conversations = new List<TextMessage>();
                for (int i = 1; i <= notebookObject.audioCount; i++)
                {
                    //the audio index starts with 1 instead of 0
                    fName = prefix + i.ToString();
                    List<TextMessage> messages = await FileManager.getSharedFileManager().GetSavedTranscriptsFromXML(notebookId, fName);
                    if (messages == null)
                    {
                        MetroLogger.getSharedLogger().Error($"Failed to load transcript_{i}, file may not exist or is empty.");
                    }
                    else
                    {
                        Debug.WriteLine($"Loaded transcript_{i}.");
                        this.conversations.AddRange(messages);
                    }
                }
                pastchatView.ItemsSource = this.conversations;
                SpeechPage.Current.updateChat();

                //Gets all saved phenotypes from XML meta
                List<Phenotype> phenos = await FileManager.getSharedFileManager().GetSavedPhenotypeObjectsFromXML(notebookId);
                if (phenos != null && phenos.Count > 0)
                {
                    PhenotypeManager.getSharedPhenotypeManager().addPhenotypesFromFile(phenos);
                }

                // Process loading note pages one by one
                notePages = new List<NotePageControl>();
                pageIndexButtons = new List<Button>();
                bool has_EHR = false;

                for (int i = 0; i < pageIds.Count; ++i)
                {
                    NotePageControl aPage = new NotePageControl(notebookObject.name, i.ToString());
                    notePages.Add(aPage);
                    aPage.pageId = pageIds[i];
                    aPage.notebookId = notebookId;

                    //check if there's an EHR file in the page
                    StorageFile ehr = await FileManager.getSharedFileManager().GetNoteFileNotCreate(notebookId, i.ToString(), NoteFileType.EHR);
                    if (ehr == null) {
                        Debug.WriteLine("EHR null");
                    }
                    else
                    {
                        has_EHR = true;
                        aPage.SwitchToEHR(ehr);
                    }

                    //load strokes
                    bool result = await FileManager.getSharedFileManager().LoadNotePageStroke(notebookId, pageIds[i], aPage);
                    addNoteIndex(i);
                    
                    //load image/drawing addins
                    List<ImageAndAnnotation> imageAndAnno = await FileManager.getSharedFileManager().GetImgageAndAnnotationObjectFromXML(notebookId, pageIds[i]);
                    if (imageAndAnno == null) {
                    }
                    else
                    {
                        //shows add-in icons into side bar
                        aPage.showAddIn(imageAndAnno);
                        //loop to add actual add-in to canvas but hides it depending on its inDock value
                        foreach (var ia in imageAndAnno)
                            aPage.loadAddInControl(ia);

                    }
                }
                curPage = notePages[0];
                curPageIndex = 0;
                PageHost.Content = curPage;
                setNotePageIndex(curPageIndex);

                //setting initial page to first page and auto-start analyzing strokes
                if (!has_EHR)
                {
                    //initializing for regular note page
                    inkCanvas = notePages[0].inkCan;
                    curPage.initialAnalyze();
                }
                else {
                    //current implementation assumes if there's ehr, it must be on first page
                    inkCanvas = notePages[0].ehrPage.annotations;
                    curPage.ehrPage.SlideCommentsToSide();
                }
                MainPageInkBar.TargetInkCanvas = inkCanvas;
                curPage.Visibility = Visibility.Visible;
            }
            catch (NullReferenceException ne)
            {
                ////NullReferenceException is very likely to happen when things aren't saved properlly during debugging state due to force quit
                MetroLogger.getSharedLogger().Error( ne + ne.Message );
                await PromptRemakeNote(notebookId);
                return;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed to Initialize Notebook From Disk:{e}:{e.Message}");
            }

        }

        public async Task InitializeEHRNote(StorageFile file)
        {
            PhenoMana.clearCache();

            //if user cancels choosing a file or file is not valid, just create a new notebook
            if (file == null) {
                NotifyUser("No EHR file, please paste EHR text",NotifyType.StatusMessage,2);
            }

            // Tries to create a file structure for the new notebook.
            {
                notebookId = FileManager.getSharedFileManager().createNotebookId();
                FileManager.getSharedFileManager().currentNoteboookId = notebookId;
                bool result = await FileManager.getSharedFileManager().CreateNotebook(notebookId);
                SpeechManager.getSharedSpeechManager().setAudioIndex(0);
                if (!result)
                    NotifyUser("Failed to create file structure, notes may not be saved.", NotifyType.ErrorMessage, 2);
                else
                    notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

                if (notebookObject != null)
                    noteNameTextBox.Text = notebookObject.name;
            }

            notePages = new List<NotePageControl>();
            pageIndexButtons = new List<Button>();

            NotePageControl aPage = new NotePageControl(notebookId, "0");
            notePages.Add(aPage);
            curPage = aPage;
            curPageIndex = 0;
            PageHost.Content = curPage;
            addNoteIndex(curPageIndex);
            setNotePageIndex(curPageIndex);

            aPage.SwitchToEHR(file);
            inkCanvas = aPage.ehrPage.annotations;
            MainPageInkBar.TargetInkCanvas = inkCanvas;

            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;
            //by default uses internal microphone
            SurfaceMicRadioButton_Checked(null, null);
            AbbreviationON_Checked(null, null);

            // create file sturcture for this page
            await FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());
            curPage.Visibility = Visibility.Visible;
        }


        public async Task PromptRemakeNote(string notebookId) {
            var messageDialog = new MessageDialog("This Notebook seems to be corrupted and cannot be loaded, please recreate a new note.");
            messageDialog.Title = "Error";
            messageDialog.Commands.Add(new UICommand("OK") { Id = 0 });
            // Set the command that will be invoked by default
            messageDialog.DefaultCommandIndex = 0;
            // Show the message dialog
            await messageDialog.ShowAsync();
            await FileManager.getSharedFileManager().DeleteNotebookById(notebookId);
            Frame.Navigate(typeof(PageOverview));
        }


        /// <summary>
        /// Save everything to disk, include: 
        /// handwritten strokes, typing words, photos and annotations, drawing, collected phenotypes
        /// </summary>
        public async Task<bool> saveNoteToDisk()
        {
            //locks semaphore before accessing
            await savingSemaphoreSlim.WaitAsync();
            bool pgResult = true;
            bool flag = false;
            bool result2 = false;
            try
            {
                MetroLogger.getSharedLogger().Info($"Saving notebook {notebookId} ...");
                // save note pages one by one
                foreach (var page in notePages)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        async () =>
                        {
                            flag = await page.SaveToDisk();
                            if (!flag)
                            {
                                MetroLogger.getSharedLogger().Error($"Page {page.pageId} failed to save.");
                                pgResult = false;
                            }
                        }
                    );
                }
                // collected phenotypes
                result2 = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);
                if (!result2)
                    MetroLogger.getSharedLogger().Error($"Failed to save collected phenotypes");

                if (! (pgResult && result2))
                    MetroLogger.getSharedLogger().Info($"Some parts of notebook {notebookId} failed to save.");


            }
            catch (NullReferenceException)
            {
                //This exception may be encountered when attemping to click close during main page and there's no
                //valid notebook id provided. Technically nothing needs to be done here              
            }
            catch (Exception ex)
            {
                MetroLogger.getSharedLogger().Error("Failed to save notebook: " + ex + ex.Message);
            }
            finally
            {
                //unlcoks semaphore 
                savingSemaphoreSlim.Release();               
            }
            return pgResult && result2;
        }


        /// <summary>
        /// Saves current added phenotypes to local file
        /// </summary>
        public async Task<bool> AutoSavePhenotypes()
        {
            bool complete = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);
            if (!complete)
                MetroLogger.getSharedLogger().Error("Failed to auto-save collected phenotypes.");
            return complete;
        }

        /// <summary>
        /// Load everything from disk, include: 
        /// handwritten strokes, typing words, photos and annotations, drawing, collected phenotypes.
        /// </summary>
        private async Task<bool> loadNoteFromDisk()
        {
            bool isSuccessful = true;
            bool result;

            //saving all note pages
            for (int i = 0; i < notePages.Count; ++i)
            {
                // saving handwritten strokes of the page
                result = await FileManager.getSharedFileManager().SaveNotePageStrokes(notebookId, i.ToString(), notePages[i]);
            }

            // collected phenotypes
            result = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);
            if (result)
                Debug.WriteLine("Successfully save collected phenotypes.");
            else
            {
                Debug.WriteLine("Failed to save collected phenotypes.");
                isSuccessful = false;
            }

            return isSuccessful;
        }

        /// <summary>
        /// Gets all strokers from the inkCanvas and saves the strokes as .gif file to user selected folder.
        /// Return 1 if success, 0 if failed and 2 if canceled.
        /// </summary>
        private async Task<int> SaveStroketoDiskAsGif()
        {
            // Get all strokes on the InkCanvas.
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Strokes present on ink canvas.
            if (currentStrokes.Count > 0)
            {
                // Let users choose their ink file using a file picker.
                // Initialize the picker.
                FileSavePicker savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("GIF with embedded ISF", new List<string>() { ".gif" });
                savePicker.DefaultFileExtension = ".gif";
                savePicker.SuggestedFileName = "InkSample";

                // Show the file picker.
                Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
                // When chosen, picker returns a reference to the selected file.
                if (file != null)
                {
                    // Prevent updates to the file until updates are 
                    // finalized with call to CompleteUpdatesAsync.
                    Windows.Storage.CachedFileManager.DeferUpdates(file);
                    // Open a file stream for writing.
                    IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    // Write the ink strokes to the output stream.
                    using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                    {
                        await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                        await outputStream.FlushAsync();
                    }
                    stream.Dispose();

                    // Finalize write so other apps can update file.
                    Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        // File saved.
                        return 1;
                    }
                    else
                    {
                        // File couldn't be saved.
                        return 0;
                    }
                }
            }
            // User selects Cancel and picker returns null.
            return 2;
        }

        /// <summary>
        /// Promts user to select an .gif file to load saved strokes.
        /// </summary>
        private async Task<bool> loadStrokefromGif()
        {
            // Let users choose their ink file using a file picker.
            // Initialize the picker.
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            // Show the file picker.
            StorageFile file = await openPicker.PickSingleFileAsync();
            // User selects a file and picker returns a reference to the selected file.
            if (file != null)
            {
                // Open a file stream for reading.
                IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    await inkCanvas.InkPresenter.StrokeContainer.LoadAsync(inputStream);
                    curPage.initialAnalyze();
                    stream.Dispose();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Saves the strokes into an image format of {gif,jog,tif,png} to disk. Return 1 if success, 0 if failed and 2 if canceled.
        /// </summary>
        private async Task<int> saveImageToDisk()
        {
            // Get all strokes on the InkCanvas.
            IReadOnlyList<InkStroke> currentStrokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Strokes present on ink canvas.
            if (currentStrokes.Count > 0)
            {
                CanvasDevice device = CanvasDevice.GetSharedDevice();
                CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)curPage.PAGE_WIDTH, (int)curPage.PAGE_HEIGHT, 96);
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.White);
                    ds.DrawInk(currentStrokes);
                }
                // Let users choose their ink file using a file picker.
                // Initialize the picker.
                FileSavePicker savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("Images", new List<string>() { ".gif", ".jpg", ".tif", ".png" });
                savePicker.DefaultFileExtension = ".jpg";
                savePicker.SuggestedFileName = "InkImage";

                // Show the file picker.
                StorageFile file =
                    await savePicker.PickSaveFileAsync();
                // When chosen, picker returns a reference to the selected file.
                if (file != null)
                {
                    // Prevent updates to the file until updates are 
                    // finalized with call to CompleteUpdatesAsync.
                    CachedFileManager.DeferUpdates(file);
                    // Open a file stream for writing.
                    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    // Write the ink strokes to the output stream.
                    using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                    {
                        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg, 1f);
                    }
                    stream.Dispose();

                    // Finalize write so other apps can update file.
                    Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        // File saved.
                        return 1;
                    }
                    else
                    {
                        // File couldn't be saved.
                        return 0;
                    }
                }
                // User selects Cancel and picker returns null.
                // Operation cancelled.
                return 2;
            }
            return 2;
        }

        /// <summary>
        /// Promts user to select an image file type {gif,png,jpg,tif} and tries to load into note.
        /// </summary>
        private async Task<bool> loadImagefromDisk()
        {
            // Let users choose their ink file using a file picker.
            // Initialize the picker.
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".tif");
            // Show the file picker.
            StorageFile file = await openPicker.PickSingleFileAsync();
            // User selects a file and picker returns a reference to the selected file.
            if (file != null)
            {
                // Open a file stream for reading.
                IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                // Read from file.
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                    SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
                    await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
                    curPage.AddImageControl("FIXME", bitmapSource);
                }
                stream.Dispose();
                return true;
            }
            // User selects Cancel and picker returns null.
            else
            {
                // Operation cancelled.
                return false;
            }
        }

        /// <summary>
        /// Saves the current audio metadata to disk
        /// </summary>
        public async void updateAudioMeta() {
            notebookObject.audioCount = SpeechManager.getSharedSpeechManager().getAudioCount();
            await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
        }
        /// <summary>
        /// Gets saved transcripts from disk and updates the past conversations panel
        /// </summary>
        public async void updatePastConversation() {
            int count = notebookObject.audioCount;
            //Debug.WriteLine($"audio count = {notebookObject.audioCount}");
            String fName = prefix;
            this.conversations = new List<TextMessage>();
            for (int i = 1 ; i <= count; i++)
            {          
                //the audio index starts with 1 instead of 0
                fName = prefix + i.ToString();
                List<TextMessage> messages = await FileManager.getSharedFileManager().GetSavedTranscriptsFromXML(this.notebookId, fName);
                if (messages == null)
                {
                    MetroLogger.getSharedLogger().Error($"Failed to load transcript_{i}, file may not exist.");
                    NotifyUser($"Failed to load transcript_{i}, file may be empty.", NotifyType.StatusMessage, 2);
                }
                else
                {
                    MetroLogger.getSharedLogger().Error($"Loaded transcript_{i}.");
                    this.conversations.AddRange(messages);
                }
            }
            SpeechPage.Current.updateChat();
            pastchatView.ItemsSource = this.conversations;
        }
    }
}
