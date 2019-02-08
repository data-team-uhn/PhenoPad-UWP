using PhenoPad.FileService;
using PhenoPad.LogService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
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
            return;
        }

        /// <summary>
        /// Clearing all cache and index records before leaving MainPage.
        /// </summary>
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {

        }

        private async void LoadNotebook() {
            try
            {

                //If notebook file exists, continues with loading...
                notebookObject = await FileManager.getSharedFileManager().GetNotebookObjectFromXML(notebookId);

                if (notebookObject == null)
                {
                    Debug.WriteLine("notebook Object is null");
                }

                //Gets all stored pages and notebook object from the disk
                List<string> pageIds = await FileManager.getSharedFileManager().GetPageIdsByNotebook(notebookId);

                //if (notebookObject != null)
                noteNameTextBox.Text = notebookObject.name;

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
