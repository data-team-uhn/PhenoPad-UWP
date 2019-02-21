using PhenoPad.CustomControl;
using PhenoPad.FileService;
using PhenoPad.LogService;
using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
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

        public NoteViewPage()
        {
            this.InitializeComponent();
        }
        /// <summary>
        /// Initializes the Notebook when user navigated to MainPage.
        /// </summary>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            var nid = e.Parameter as string;
            StorageFile file = e.Parameter as StorageFile;

            if (e.Parameter == null || file != null)
            {//is a file for importing EHR
                //TO DO for EHR
            }
            else
            {//is a valid note to load
                Debug.WriteLine("loading");
                this.notebookId = nid;
                FileManager.getSharedFileManager().currentNoteboookId = nid;
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, LoadNotebook);
            }
            await Task.Delay(TimeSpan.FromSeconds(3));
            StreamWebSocket streamSocket = new StreamWebSocket();
            try
            {
                Uri serverUri = new Uri("ws://" + SERVER_ADDR + ":" + SERVER_PORT + "/client/ws/file_request" +
                                           "?content-type=audio%2Fx-raw%2C+layout%3D%28string%29interleaved%2C+rate%3D%28int%2916000%2C+format%3D%28string%29S16LE%2C+channels%3D%28int%291&manager_id=666");
                //Task connectTask = streamSocket.ConnectAsync(serverUri).AsTask();
                //await connectTask;
                //if (connectTask.Exception != null)
                //    MetroLogger.getSharedLogger().Error("connectTask.Exception:" + connectTask.Exception.Message);
                //Debug.WriteLine("connected");
                //MediaPlayer player = new MediaPlayer();
                //while (true) {
                //    await Task.Delay(TimeSpan.FromSeconds(0.1));

                //    uint length = 1000;     // Leave a large buffer
                //    var readBuf = new Windows.Storage.Streams.Buffer((uint)length);
                //    var readOp = await streamSocket.InputStream.ReadAsync(readBuf, (uint)length, InputStreamOptions.Partial);

                //    DataReader readPacket = DataReader.FromBuffer(readBuf);
                //    uint buffLen = readPacket.UnconsumedBufferLength;
                //    if (buffLen != length)
                //    {
                //        Debug.WriteLine(buffLen);
                //        // Construct the sound player
                //        player.SetStreamSource(readPacket.ReadBuffer(buffLen).AsStream().AsRandomAccessStream());
                //        player.Play();
                //    }
                //}
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("file result:"+ex+ex.Message);
                streamSocket.Dispose();
                streamSocket = null;
            }

            return;
        }

        /// <summary>
        /// Clearing all cache and index records before leaving MainPage.
        /// </summary>
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //TODO
        }

        private async void LoadNotebook() {
            try
            {

                //If notebook file exists, continues with loading...
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);
                //Gets all stored pages and notebook object from the disk
                List<string> pageIds = await FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);
                noteNameTextBox.Text = notebookObject.name;
                List<OperationItem> logs = await OperationLogger.getOpLogger().ParseOperationItems(notebookId);
                List<InkStroke> allstrokes = new List<InkStroke>();

                for (int i = 0; i < pageIds.Count; i++) {
                    InkCanvas tempCanvas = new InkCanvas();
                    await FileManager.getSharedFileManager().LoadNotePageStroke(notebookId, i.ToString(), null, tempCanvas);
                    var strokes = tempCanvas.InkPresenter.StrokeContainer.GetStrokes();
                    allstrokes.AddRange(strokes.ToList());
                }

                foreach (InkStroke s in allstrokes)
                    Debug.WriteLine(s.Id);

                //TODO: separate operation items based on type, then order by timespan and rearrange
                List<OperationItem> phenotypes = logs.Where(x => x.type == "Phenotype").ToList();
                List<OperationItem> handwriting = logs.Where(x => x.type == "Strokes").ToList();
                List<Phenotype> saved = new List<Phenotype>();

                //process saved phenotypes
                foreach (OperationItem op in phenotypes) {
                    if (saved.Contains(op.phenotype))
                        saved.Remove(op.phenotype);
                    saved.Add(op.phenotype);
                }
                //process handwritings
                handwriting = handwriting.OrderBy(h => h.timestamp).ToList();
                foreach (OperationItem op in handwriting) {
                    DateTime start = op.timestamp;
                    DateTime end = op.timeEnd;
                    InkCanvas tempCanvas = new InkCanvas();
                    var strokes = allstrokes.Where(x => (x.StrokeStartedTime >= start && x.StrokeStartedTime <= end));
                    Debug.WriteLine($"all strokes added {strokes.Count()}.......");

                    foreach (var s in strokes)
                    {
                        var s_clone = s.Clone();
                        s_clone.Selected = true;
                        tempCanvas.InkPresenter.StrokeContainer.AddStroke(s_clone);
                    }
                    Rect bound = tempCanvas.InkPresenter.StrokeContainer.BoundingRect;
                    tempCanvas.InkPresenter.StrokeContainer.MoveSelected(new Point(-bound.Left, -bound.Top));
                    tempCanvas.Height = bound.Height;
                    tempCanvas.Width = bound.Width;

                    strokesGrid.Children.Add(tempCanvas);
                }

                //sorts the phenotypes in ascending timeline order
                saved = saved.OrderBy( p => p.time).ToList();
                PhenoListView.ItemsSource = saved;
                TimeListView.ItemsSource = saved;

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
