using PhenoPad.PhenotypeService;
using PhenoPad.SpeechService;
using PhenoPad.FileService;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.ApplicationModel.DataTransfer;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhenoPad
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TestPage : Page
    {
        public static TestPage Current;
        public static MainPage mainpage;
        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();
        private int doctor = 0;
        private int curSpeakerCount = 2;
        private string loadedMedia = String.Empty;
        private List<AudioFile> savedAudio;
        private List<Phenotype> phenoInSpeech = new List<Phenotype>();
        private DispatcherTimer playbackTimer;
        // ATTRIBUTES FOR EDITING TRANSCRIPT
        private string originText;
        private TextMessage selectedTM;
        private TextBlock selectedTB;

        public TestPage()
        {
            this.InitializeComponent();

            Current = this;
            mainpage = MainPage.Current;
            chatView.ItemsSource = mainpage.conversations;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            //recognizedPhenoBriefListView.ItemsSource = PhenotypeManager.getSharedPhenotypeManager().searchPhenotypeByPhenotipsAsync(str);

            //var format = textNoteEditBox.Document.GetDefaultParagraphFormat();
            //textNoteEditBox.FontSize = 32;
            //format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
            //textNoteEditBox.Document.SetDefaultParagraphFormat(format);
        }

        public void updateChat()
        {
            chatView.ItemsSource = MainPage.Current.conversations;
            UpdateLayout();
        }

        public void updateNote(List<Phenotype> transcripts)
        {
            recognizedPhenoBriefListView.ItemsSource = transcripts;
            Debug.WriteLine(recognizedPhenoBriefListView.ItemsSource);
            UpdateLayout();
        }

        private void PlaceholderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SpeechTranscriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (speechQuickView.Visibility == Visibility.Collapsed)
            {
                // show note transcript view
                SpeechTranscriptButton.IsChecked = true;
                speechQuickView.Visibility = Visibility.Visible;
                // collapse other views
                NoteTranscriptButton.IsChecked = false;
                noteQuickView.Visibility = Visibility.Collapsed;
            }
            else
            {
                SpeechTranscriptButton.IsChecked = false;
                speechQuickView.Visibility = Visibility.Collapsed;
            }
        }

        private void NoteTranscriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (noteQuickView.Visibility == Visibility.Collapsed)
            {
                // show note transcript view
                NoteTranscriptButton.IsChecked = true;
                noteQuickView.Visibility = Visibility.Visible;
                // collapse other views
                SpeechTranscriptButton.IsChecked = false;
                speechQuickView.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoteTranscriptButton.IsChecked = false;
                noteQuickView.Visibility = Visibility.Collapsed;
            }
        }

        private void TextNoteEdit_TextChanged(object sender, RoutedEventArgs e)
        {

        }

        private void OnChatViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            TextMessage message = (TextMessage)args.Item;

            // Only display message on the right when speaker index = 0

            if (message.IsNotFinal)
            {
                args.ItemContainer.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                args.ItemContainer.HorizontalAlignment = (message.Speaker == doctor) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
        }
        private void TextBlockDoubleTapped(object sender, RoutedEventArgs e)
        {

            TextBlock tb = ((TextBlock)sender);
            string body = tb.Text;
            originText = body.Trim();
            var element_Visual_Relative = tb.TransformToVisual(root);
            Point pos = element_Visual_Relative.TransformPoint(new Point(0, 0));
            TextMessage tm = MainPage.Current.conversations.Where(x => x.Body == body).FirstOrDefault();
            selectedTM = tm;
            selectedTB = tb;
            if (tm != null)
            {
                Canvas.SetLeft(ChatEditPopup, pos.X);
                Canvas.SetTop(ChatEditPopup, pos.Y);
                ChatEditPopup.Width = tb.ActualWidth - 10;
                ChatEditPopup.Height = tb.ActualHeight;
                TranscriptEditBox.Document.SetText(Windows.UI.Text.TextSetOptions.None, originText);
                ChatEditPopup.Visibility = Visibility.Visible;
                TranscriptEditBox.Focus(FocusState.Pointer);
            }
        }
        private void TextBlockTapped(object sender, RoutedEventArgs e)
        {
            TextBlock tb = ((TextBlock)sender);
            string body = tb.Text;
            originText = body.Trim();
            var element_Visual_Relative = tb.TransformToVisual(root);
            Point pos = element_Visual_Relative.TransformPoint(new Point(0, 0));
            TextMessage tm = MainPage.Current.conversations.Where(x => x.Body == body).FirstOrDefault();
            selectedTM = tm;
            selectedTB = tb;
            if (tm != null)
            {
                var phenos = tm.phenotypesInText;
                //speechPhenoListView.ItemsSource = phenos;
                //speechPhenoListView.UpdateLayout();
            }
        }
        private async void EditBoxLostFocus(object sender, RoutedEventArgs e)
        {
            string newText = "";
            TranscriptEditBox.Document.GetText(Windows.UI.Text.TextGetOptions.None, out newText);
            newText = newText.Trim();
            if (newText != originText && newText != "")
            {
                selectedTM.Body = newText;
                selectedTB.Text = newText;
                await MainPage.Current.SaveCurrentConversationsToDisk();
                chatView.ItemsSource = MainPage.Current.conversations;
                UpdateLayout();
                MainPage.Current.NotifyUser("Transcript updated", NotifyType.StatusMessage, 1);
            }
        }

        private void modeTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {
            //TODO?
        }

        private void Drag_Started(UIElement sender, DragStartingEventArgs args)
        {
            TextBlock tb = (TextBlock)sender;
            args.Data.SetData(StandardDataFormats.Text, tb.Text);
            DataPackage dataPackage = new DataPackage();

        }   

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            string orgText = string.Empty;
            RichEditBox editBox = (RichEditBox)sender;
            string draggedText = await e.DataView.GetTextAsync();

            editBox.Document.GetText(Windows.UI.Text.TextGetOptions.AdjustCrlf, out orgText);
            editBox.Document.SetText(Windows.UI.Text.TextSetOptions.None, orgText + "\n" + draggedText);
        } 
    }
}
