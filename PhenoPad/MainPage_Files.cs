﻿using System;
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
        private bool loadFromDisk = false;

        /// <summary>
        /// Initializes the new notebook and creates a locally saved file for it.
        /// </summary>
        private async void InitializeNotebook()
        {
            LogService.MetroLogger.getSharedLogger().Info("Initialize a new notebook.");
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
            NotePageControl aPage = new NotePageControl();
            notePages.Add(aPage);
            inkCanvas = aPage.inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = aPage;
            // var screenSize = HelperFunctions.GetCurrentDisplaySize();
            //aPage.Height = screenSize.Height;
            //aPage.Width = screenSize.Width;
            curPageIndex = 0;
            PageHost.Content = curPage;
            addNoteIndex(curPageIndex);
            setNotePageIndex(curPageIndex);

            currentMode = WritingMode;
            modeTextBlock.Text = WritingMode;

            // create file sturcture for this page
            await FileManager.getSharedFileManager().CreateNotePage(notebookObject, curPageIndex.ToString());
        }

        /// <summary>
        /// Initializes the notebook by loading from pre-existing local save file.
        /// </summary>
        private async void InitializeNotebookFromDisk()
        {
            LogService.MetroLogger.getSharedLogger().Info("Open notebook from disk.");
            PhenoMana.clearCache();

            List<string> pageIds = await FileService.FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);
            notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

            if (notebookObject != null)
                noteNameTextBox.Text = notebookObject.name;

            SpeechManager.getSharedSpeechManager().setAudioIndex(notebookObject.audioCount);
            List<Phenotype> phenos = await FileManager.getSharedFileManager().GetSavedPhenotypeObjectsFromXML(notebookId);
            if (phenos != null && phenos.Count > 0)
            {
                PhenotypeManager.getSharedPhenotypeManager().addPhenotypesFromFile(phenos);
            }


            if (pageIds == null || pageIds.Count == 0)
            {
                NotifyUser("Did not find anything in this notebook, will create a new one.", NotifyType.ErrorMessage, 2);
                this.InitializeNotebook();
            }

            notePages = new List<NotePageControl>();
            pageIndexButtons = new List<Button>();

            for (int i = 0; i < pageIds.Count; ++i)
            {
                NotePageControl aPage = new NotePageControl();
                notePages.Add(aPage);
                aPage.pageId = pageIds[i];
                aPage.notebookId = notebookId;
                await FileManager.getSharedFileManager().LoadNotePageStroke(notebookId, pageIds[i], aPage);
                addNoteIndex(i);

                List<ImageAndAnnotation> imageAndAnno = await FileManager.getSharedFileManager().GetImgageAndAnnotationObjectFromXML(notebookId, pageIds[i]);
                if (imageAndAnno != null)
                    foreach (var ia in imageAndAnno)
                    {
                        aPage.addImageAndAnnotationControl(ia.name, ia.canvasLeft, ia.canvasTop, true, null, ia.transX, ia.transY, ia.transScale, width: ia.width, height: ia.height);
                    }
            }

            inkCanvas = notePages[0].inkCan;
            MainPageInkBar.TargetInkCanvas = inkCanvas;
            curPage = notePages[0];
            curPageIndex = 0;
            PageHost.Content = curPage;
            setNotePageIndex(curPageIndex);

            curPage.initialAnalyze();
        }
        /// <summary>
        /// Saves the Notebook to disk after a specified time in seconds.
        /// </summary>
        private void saveNotesTimer(int seconds)
        {
            TimeSpan period = TimeSpan.FromSeconds(seconds);

            ThreadPoolTimer PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                try
                {
                    await this.saveNoteToDisk();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }

                /**
                Dispatcher.RunAsync(CoreDispatcherPriority.High,
                    () =>
                    {
                        NotifyUser("Note " + this.noteNameTextBox.Text + " has been saved", NotifyType.StatusMessage, 1);
                    });
                **/

            }, period);
        }



        /// <summary>
        /// Save everything to disk, include: 
        /// handwritten strokes, typing words, photos and annotations, drawing, collected phenotypes
        /// </summary>
        public async Task<bool> saveNoteToDisk()
        {
            //locks semaphore before accessing
            await savingSemaphoreSlim.WaitAsync();
            try
            {
                LogService.MetroLogger.getSharedLogger().Info($"Saving notebook {notebookId} to disk...");
                bool result = false;

                LogService.MetroLogger.getSharedLogger().Info($"Saving audio");
                // save audio count
                {
                    if (notebookObject != null)
                    {
                        notebookObject.audioCount = SpeechManager.getSharedSpeechManager().getAudioCount();
                        await FileManager.getSharedFileManager().SaveToMetaFile(notebookObject);
                    }

                    // save audio transcriptions
                    await SpeechManager.getSharedSpeechManager().SaveTranscriptions();
                }


                // save note pages one by one
                foreach (var page in notePages)
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        async () =>
                        {
                            LogService.MetroLogger.getSharedLogger().Info($"Saving page {page.pageId}");
                            result = await page.SaveToDisk();
                        }
                    );
                }


                LogService.MetroLogger.getSharedLogger().Info($"Saving phenotypes");
                // collected phenotypes
                bool result2 = await FileManager.getSharedFileManager().saveCollectedPhenotypesToFile(notebookId);

                LogService.MetroLogger.getSharedLogger().Info($"Successfully saved notebook {notebookId} to disk.");
                return result && result2;
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Failed to save notebook: " + ex.Message);
            }
            finally
            {
                //unlcoks semaphore 
                savingSemaphoreSlim.Release();
            }
            return false;
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
                Windows.Storage.Pickers.FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
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
                    await curPage.StartAnalysisAfterLoad();
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

    }
}