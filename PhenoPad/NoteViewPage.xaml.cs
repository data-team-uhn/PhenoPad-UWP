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
        public static string SERVER_ADDR = "137.135.117.253";
        public static string SERVER_PORT = "8080";
        public SpeechManager speechmana;
        private bool isReading; //flag for reading audio stream
        private DispatcherTimer readTimer;
        private StreamWebSocket streamSocket;
        private CancellationTokenSource cancelSource;
        private CancellationToken token;
        private List<byte> audioBuffer;
        public static NoteViewPage Current;
        public List<TextMessage> conversations;


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
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var nid = e.Parameter as string;
            StorageFile file = e.Parameter as StorageFile;
            BackButton.IsEnabled = true;

            if (e.Parameter == null || file != null)
            {//is a file for importing EHR
                //TO DO for EHR
            }
            else
            {//is a valid note to load
                this.notebookId = nid;
                FileManager.getSharedFileManager().currentNoteboookId = nid;
                //await Dispatcher.RunAsync(CoreDispatcherPriority.High, LoadNotebook);
            }
            //PlayMedia();
            LoadNotebook();
        }

        private async void PlayMedia()
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
                //StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                //StorageFile storageFile = await storageFolder.CreateFileAsync(
                //  "audio.wav", CreationCollisionOption.GenerateUniqueName);
                //=============================
                uint length = 1000000;     // Leave a large buffer
                audioBuffer = new List<Byte>();
                isReading = true;
                cancelSource = new CancellationTokenSource();
                token = cancelSource.Token;
                while (isReading)
                {
                    readTimer.Start();
                    IBuffer op = await streamSocket.InputStream.ReadAsync(new Windows.Storage.Streams.Buffer(length), length, InputStreamOptions.Partial).AsTask(token);
                    if (op.Length > 0)
                        audioBuffer.AddRange(op.ToArray());
                    Debug.WriteLine("------------------" + audioBuffer.Count + "----------------");
                    readTimer.Stop();
                }
            }
            catch (TaskCanceledException) {
                //Plays the audio received from server
                readTimer.Stop();
                Debug.WriteLine("done receiving +++++++++++++++++++++++++");
                MemoryStream mem = new MemoryStream(audioBuffer.ToArray());
                MediaPlayer player = new MediaPlayer();
                player.SetStreamSource(mem.AsRandomAccessStream());
                player.Play();
                Debug.WriteLine("done");
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("file result:" + ex + ex.Message);
                streamSocket.Dispose();
                streamSocket = null;
            }


        }

        /// <summary>
        /// Clearing all cache and index records before leaving MainPage.
        /// </summary>
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //TODO
            //streamSocket.Close(0,"end");
            //streamSocket.Dispose();
            //streamSocket = null;
            //readTimer.Stop();
        }

        public void ShowAllChatAt(object sender, string textMessage) {
            TextMessage mess = conversations.Where(x => x.Body == textMessage).FirstOrDefault();
            AllChatView.ScrollIntoView(mess);
            ChatRecordFlyout.ShowAt((FrameworkElement)sender);
        }

        private async void LoadNotebook() {
            try
            {

                //If notebook file exists, continues with loading...
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);
                //Parsing information from speech conversation, need the time for matching detected phenotype
                List<TextMessage> conversations = await FileManager.getSharedFileManager().GetSavedTranscriptsFromXML(notebookId);
                this.conversations = conversations == null ? this.conversations : conversations;
                AllChatView.ItemsSource = this.conversations;

                noteNameTextBox.Text = notebookObject.name;
                List<NoteLineViewControl> logs = await OperationLogger.getOpLogger().ParseOperationItems(notebookObject,conversations);
                logs = logs.OrderBy(x=>x.keyTime).ToList();
                //foreach (var l in logs)
                //    NoteLineStack.Children.Add(l);
                aaa.ItemsSource = logs;
                UpdateLayout();

                //Gets all stored pages and notebook object from the disk
                //List<string> pageIds = await FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);
                //List<InkStroke> allstrokes = new List<InkStroke>();

                //for (int i = 0; i < pageIds.Count; i++) {
                //    InkCanvas tempCanvas = new InkCanvas();
                //    await FileManager.getSharedFileManager().LoadNotePageStroke(notebookId, i.ToString(), null, tempCanvas);
                //    var strokes = tempCanvas.InkPresenter.StrokeContainer.GetStrokes();
                //    allstrokes.AddRange(strokes.ToList());
                //}

                //TODO: separate operation items based on type, then order by timespan and rearrange
                //List<OperationItem> phenotypes = logs.Where(x => x.type == "Phenotype").ToList();
                //List<OperationItem> handwriting = logs.Where(x => x.type == "Strokes").ToList();
                //List<Phenotype> saved = new List<Phenotype>();

                //process saved phenotypes
                //foreach (OperationItem op in phenotypes) {
                //    if (saved.Contains(op.phenotype))
                //        saved.Remove(op.phenotype);
                //    saved.Add(op.phenotype);
                //}
                //process handwritings
                //handwriting = handwriting.OrderBy(h => h.timestamp).ToList();
                //foreach (OperationItem op in handwriting) {
                //    DateTime start = op.timestamp;
                //    DateTime end = op.timeEnd;
                //    InkCanvas tempCanvas = new InkCanvas();
                //    var strokes = allstrokes.Where(x => (x.StrokeStartedTime >= start && x.StrokeStartedTime <= end));
                //    Debug.WriteLine($"all strokes added {strokes.Count()}.......");

                //    foreach (var s in strokes)
                //    {
                //        var s_clone = s.Clone();
                //        s_clone.Selected = true;
                //        tempCanvas.InkPresenter.StrokeContainer.AddStroke(s_clone);
                //    }
                //    Rect bound = tempCanvas.InkPresenter.StrokeContainer.BoundingRect;
                //    tempCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-bound.Left, -bound.Top));
                //    tempCanvas.Height = bound.Height;
                //    tempCanvas.Width = bound.Width;

                //    //strokesGrid.Children.Add(tempCanvas);
                //}

                //sorts the phenotypes in ascending timeline order
                //saved = saved.OrderBy( p => p.time).ToList();
                //PhenoListView.ItemsSource = saved;
                //TimeListView.ItemsSource = saved;

            }
            catch (NullReferenceException ne)
            {
                ////NullReferenceException is very likely to happen when things aren't saved properlly during debugging state due to force quit
                MetroLogger.getSharedLogger().Error(ne + ne.Message);
                return;
            }
            catch (Exception e)
            {
                MetroLogger.getSharedLogger().Error($"Failed to Initialize Notebook From Disk:{e}:{e.Message}");
            }


        }

        private void BackButton_Clicked(object sender, RoutedEventArgs e) {
            Frame.Navigate(typeof(PageOverview));
        }

    }
}
