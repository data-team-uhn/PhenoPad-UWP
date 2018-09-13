using MetroLog;
using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using PhenoPad.LogService;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhenoPad
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageOverview : Page
    {
        private List<Notebook> notebooks;


        public PageOverview()
        {
            LogService.MetroLogger.getSharedLogger().Info("Initilize PageOverview");

            this.InitializeComponent();
            Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;

            LoadAllNotes();
            LogService.MetroLogger.getSharedLogger(typeof(PageOverview)).Info("Note list loaded.");

            hide_titlebar();

            // https://stackoverflow.com/questions/43699256/how-to-use-acrylic-accent-in-windows-10-creators-update/43711413#43711413


        }

        private void hide_titlebar() {
            //draw into the title bar
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            //remove the solid-colored backgrounds behind the caption controls and system back button
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        #region note loading

        private void LoadAllNotes()
        {
            reloadNotebookList();
        }
        public async void reloadNotebookList()
        {
            notebooks = await FileManager.getSharedFileManager().GetAllNotebookObjects();
            if (notebooks != null)
                notebookList.ItemsSource = notebooks;
        }
        #endregion

        #region button click handlers
        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            //Frame rootFrame = Window.Current.Content as Frame;
            this.Frame.Navigate(typeof(MainPage));
        }

  
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info("Creating a new note...");
            this.Frame.Navigate(typeof(MainPage), "__new__");
        }

        private async void notebookList_ItemClick(object sender, ItemClickEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info("Selecting a notebook for quick view");
            NoteGridView.ItemsSource = new List<NotePage>();
            var clickNotebook = e.ClickedItem as Notebook;
            List<NotePage> pages = await FileManager.getSharedFileManager().GetAllNotePageObjects(clickNotebook.id);

            if (pages != null)
            {
                NoteGridView.ItemsSource = pages;
                MessageGrid.Visibility = Visibility.Collapsed;
            }
            else
                MessageGrid.Visibility = Visibility.Visible;


            List<ImageAndAnnotation> images = await FileManager.getSharedFileManager().GetAllImageAndAnnotationObjects(clickNotebook.id);
            if (images.Count > 0)
            {
                ImageAnnotationGridView.Visibility = Visibility.Visible;
                ImageAnnotationPlaceHoder.Visibility = Visibility.Collapsed;
                ImageAnnotationGridView.ItemsSource = images;
            }
            else
            {
                ImageAnnotationGridView.ItemsSource = new List<ImageAndAnnotation>();
                ImageAnnotationGridView.Visibility = Visibility.Collapsed;

                ImageAnnotationPlaceHoder.Visibility = Visibility.Visible;
            }

        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var notebookId = (sender as Button).Tag;
            LogService.MetroLogger.getSharedLogger().Info($"Opening notebook ID { notebookId }");
            if (notebookId != null)
                this.Frame.Navigate(typeof(MainPage), notebookId);
        }

        private async void UploadServerButton_Click(object sender, RoutedEventArgs e)
        {

            LogService.MetroLogger.getSharedLogger().Info($"Uploading notes to server...");
            Debug.WriteLine("Upload to server");

            try
            {
                await FileServerClient.HTTPPut();
            } catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error("Unable to upload to server due to " + ex.Message);
                Debug.WriteLine("Unable to upload to server due to " + ex.Message);
            }

            LoadAllNotes();
        }

        private async void DownloadServerButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.MetroLogger.getSharedLogger().Info("Dowloading from server...");
            Debug.WriteLine("Download from server");

            try
            {
                await FileServerClient.HTTPGet();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to load from server due to " + ex.Message);
                LogService.MetroLogger.getSharedLogger().Error("Unable to download from server due to " + ex.Message);
            }

            LoadAllNotes();
        }
        private async void Delete_ItemInvoked(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            
            var id = (string)args.SwipeControl.Tag;
            bool isSuccess = await FileManager.getSharedFileManager().DeleteNotebookById(id);
            if (isSuccess)
            {
                reloadNotebookList();
                MessageGrid.Visibility = Visibility.Visible;
                MetroLogger.getSharedLogger().Info($"Deleted {id}.");
            }
        }
        #endregion

        #region navigation handlers
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, hide_titlebar);
            LogService.MetroLogger.getSharedLogger().Info("Navigated to PageOverview");          
            reloadNotebookList();
            
        }
        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.Frame.BackStack.Clear();
            //using await annoynous functions to reduce the delay in titlebar showing
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;
                //Changes the background color of title bar back to default
                ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Black;
                titleBar.ButtonInactiveBackgroundColor = Colors.Black;
            });

        }
        #endregion

        #region search function
        private void autosuggesttextchanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var filtered = notebooks.Where(i => i.name.Contains(this.autoSuggestBox.Text)).ToList();
                //if(filtered != null && filtered.Count() != 0)
                notebookList.ItemsSource = filtered;

            }
        }
        private void autosuggestquerysubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {

        }
        #endregion
    }

}
