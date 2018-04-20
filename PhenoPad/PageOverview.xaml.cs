using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhenoPad
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageOverview : Page
    {
        public PageOverview()
        {
            this.InitializeComponent();
            Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;

            loadAllNotes();
            
        }
        private async void loadAllNotes()
        {
            List<Notebook> notebooks = await FileManager.getSharedFileManager().GetAllNotebookObjects();

            notebookList.ItemsSource = notebooks;
            
   
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            //Frame rootFrame = Window.Current.Content as Frame;
            this.Frame.Navigate(typeof(MainPage));
        }

  
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            
            this.Frame.Navigate(typeof(MainPage), "__new__");
        }

        private async void notebookList_ItemClick(object sender, ItemClickEventArgs e)
        {
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
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var notebookId = (sender as Button).Tag;
            if(notebookId != null)
                this.Frame.Navigate(typeof(MainPage), notebookId);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.Frame.BackStack.Clear();
        }
    }
    
}
