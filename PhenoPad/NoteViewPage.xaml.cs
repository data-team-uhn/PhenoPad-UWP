using PhenoPad.CustomControl;
using PhenoPad.FileService;
using PhenoPad.LogService;
using PhenoPad.PhenotypeService;
using PhenoPad.SpeechService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhenoPad
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NoteViewPage : Page
    {
        public string notebookId;
        private Notebook notebookObject;
        public int pageCount;
        public SpeechManager speechmana;


        public static string SERVER_ADDR = "137.135.117.253";
        public static string SERVER_PORT = "8080";
        private bool isReading; //flag for reading audio stream
        private DispatcherTimer readTimer;
        private StreamWebSocket streamSocket;
        private CancellationTokenSource cancelSource;
        private CancellationToken token;
        private List<byte> audioBuffer;
        public static NoteViewPage Current;
        public List<TextMessage> conversations;
        public List<NoteLineViewControl> logs;


        public NoteViewPage()
        {
            this.InitializeComponent();
            Current = this;
            isReading = false;
            readTimer = new DispatcherTimer();
            readTimer.Interval = TimeSpan.FromSeconds(5);
            readTimer.Tick += EndAudioStream;
            cancelSource = new CancellationTokenSource();
            conversations = new List<TextMessage>();
            logs = new List<NoteLineViewControl>();
            token = cancelSource.Token;
            BackButton.PointerPressed += aaaa;

            var style = new Style(typeof(FlyoutPresenter));
            style.Setters.Add(new Setter(FlyoutPresenter.MinWidthProperty, 1200));
            ChatRecordFlyout.SetValue(Flyout.FlyoutPresenterStyleProperty, style);

        }
     
        private void aaaa(object obk, PointerRoutedEventArgs e) {
            Debug.WriteLine("hit");
        }

        private void EndAudioStream(object sender, object e)
        {
            isReading = false;
            cancelSource.Cancel();
            Debug.WriteLine("Timer tick, will stop reading");
        }

        /// <summary>
        /// Initializes the Notebook when user navigated to MainPage.
        /// </summary>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            var nid = e.Parameter as string;
            StorageFile file = e.Parameter as StorageFile;
            BackButton.IsEnabled = true;

            if (e.Parameter == null || file != null) //is a file for importing EHR
            {
                // TODO for EHR
            }
            else //is a valid note to load
            {
                this.notebookId = nid;
                FileManager.getSharedFileManager().currentNoteboookId = nid;
            }
            LoadNotebook();
        }

        public async Task<List<RecognizedPhrases>> GetAllRecognizedPhrases()
        {
            List<RecognizedPhrases> phrases = new List<RecognizedPhrases>();

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                foreach (NoteLineViewControl noteline in logs )
                {
                    if (noteline.HWRs == null)
                        continue;
                    for (int i = 0; i < noteline.HWRs.Count; i++)
                    {
                        WordBlockControl wb = noteline.HWRs[i];

                        RecognizedPhrases ph = new RecognizedPhrases(notebookId, wb.pageId.ToString(), wb.line_index, i, wb.current, wb.candidates, wb.strokes, wb.corrected, wb.is_abbr);

                        phrases.Add(ph);
                    }
                }
            }
            );

            return phrases;
        }


        public async void PlayMedia(double start, double end)
        {
            streamSocket = new StreamWebSocket();
            try
            {
                Uri serverUri = new Uri("ws://" + SERVER_ADDR + ":" + SERVER_PORT + "/client/ws/file_request" +
                                           "?content-type=audio%2Fx-raw%2C+layout%3D%28string%29interleaved%2C+rate%3D%28int%2916000%2C+format%3D%28string%29S16LE%2C+channels%3D%28int%291&manager_id=666");
                Task connectTask = streamSocket.ConnectAsync(serverUri).AsTask();
                await connectTask;
                if (connectTask.Exception != null)
                    MetroLogger.getSharedLogger().Error("connectTask.Exception:" + connectTask.Exception.Message);
                Debug.WriteLine("connected, will begin receiving data");

                uint length = 1000000; // Leave a large buffer
                audioBuffer = new List<Byte>();
                isReading = true;
                cancelSource = new CancellationTokenSource();
                token = cancelSource.Token;
                while (isReading)
                {
                    readTimer.Start();
                    IBuffer op = await streamSocket.InputStream.ReadAsync(new Windows.Storage.Streams.Buffer(length), length, InputStreamOptions.Partial).AsTask(token);
                    if (op.Length > 0)
                    { 
                        audioBuffer.AddRange(op.ToArray());
                    }
                    readTimer.Stop();
                }
            }
            catch (TaskCanceledException)
            {
                // Plays the audio received from server
                readTimer.Stop();
                Debug.WriteLine("------------------" + audioBuffer.Count + "----------------");
                MemoryStream mem = new MemoryStream(audioBuffer.ToArray());
                MediaPlayer player = new MediaPlayer();
                player.SetStreamSource(mem.AsRandomAccessStream());
                player.Play();
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("file result:" + ex + ex.Message);
            }
            streamSocket.Dispose();
            streamSocket = null;
        }

        /// <summary>
        /// Clearing all cache and index records before leaving MainPage.
        /// </summary>
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
        }

        public void ShowAllChatAt(object sender, TextMessage mess)
        {        
            int index = this.conversations.IndexOf(mess);
            Debug.WriteLine(index+"kkkk");

            AllChatView.ScrollIntoView(AllChatView.Items[index], ScrollIntoViewAlignment.Leading);

            ChatRecordFlyout.ShowAt((FrameworkElement)sender);
        }

        private async void LoadNotebook()
        {
            try
            {
                ViewLoadingPopup.IsOpen = false;

                // If notebook file exists, continues with loading...
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

                // Parsing information from speech conversation, need the time for matching detected phenotype
                List<TextMessage> conversations = await FileManager.getSharedFileManager().GetSavedTranscriptsFromXML(notebookId);
                this.conversations = conversations == null ? this.conversations : conversations;
                AllChatView.ItemsSource = this.conversations;

                noteNameTextBox.Text = notebookObject.name;
                pageCount = notebookObject.notePages.Count;
                Debug.WriteLine($"page count = {pageCount}");
                logs = await OperationLogger.getOpLogger().ParseOperationItems(notebookObject,conversations);
                logs = logs.OrderBy(x=>x.keyTime).ToList();

                aaa.ItemsSource = logs;
                
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                    {
                     // Your UI update code goes here
                     UpdateLayout();
                    }
                );

                ViewLoadingPopup.IsOpen = false;

            }
            catch (NullReferenceException ne)
            {
                // Note: NullReferenceException is very likely to happen when things 
                //       aren't saved properlly during debugging state due to force quit
                MetroLogger.getSharedLogger().Error(ne + ne.Message);
                return;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed to Initialize Notebook From Disk:{e}:{e.Message}");
            }
        }

        private async void BackButton_Clicked(object sender, RoutedEventArgs e)
        {
            
            List<RecognizedPhrases> phrases = await GetAllRecognizedPhrases();
            var result = true;
            for(int i = 0; i< pageCount; i++)
            { 
                string path = FileManager.getSharedFileManager().GetNoteFilePath(notebookId, i.ToString(), NoteFileType.RecognizedPhraseMeta);
                var pagePhrases = phrases.Where(x=>x.pageId == i.ToString()).ToList();
                result &= await FileManager.getSharedFileManager().SaveObjectSerilization(path, pagePhrases, typeof(List<RecognizedPhrases>));
            }
            Debug.WriteLine($"phrase saving successful = {result}");
  
            readTimer.Stop();
            Frame.Navigate(typeof(PageOverview));
        }
    }
}
