using PhenoPad.SpeechService;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using PhenoPad.PhenotypeService;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Windows.Media.Core;
using Windows.UI.Core;
using System.Xml.Serialization;
using PhenoPad.FileService;
using Windows.UI.Popups;
using Windows.UI.Xaml.Input;
using System.Linq;

namespace PhenoPad
{
    public sealed partial class SpeechPage : Page
    {
        public static SpeechPage Current;
        public static MainPage mainpage;
        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();
        private int doctor = 0;
        private int curSpeakerCount = 2;
        private string loadedMedia = String.Empty;
        private List<AudioFile> savedAudio;
        private List<Phenotype> phenoInSpeech = new List<Phenotype>();
        private DispatcherTimer playbackTimer;

        //ATTRIBUTES FOR EDITING TRANSCRIPT
        private string originText;
        private TextMessage selectedTM;
        private TextBlock selectedTB;


        //=================================METHODS==============================================
        public SpeechPage()
        {
            this.InitializeComponent();
            playbackTimer = new DispatcherTimer();
            playbackTimer.Tick += PlaybackTimer_Tick;
            Current = this;
            mainpage = MainPage.Current;
            phenoInSpeech = new List<Phenotype>();
            chatView.ItemsSource = mainpage.conversations;
            //chatView.ItemsSource = SpeechManager.getSharedSpeechManager().conversation;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            //realtimeChatView.Children.Add(MainPage.Current.speechQuickView.c);
            savedAudio = new List<AudioFile>();
            //NOTE: method SpeechPage_EngineHasResult is empty
            SpeechManager.getSharedSpeechManager().EngineHasResult += SpeechPage_EngineHasResult;
            SpeechManager.getSharedSpeechManager().RecordingCreated += SpeechPage_RecordingCreated;
            this.Tapped += HidePopups;
        }

        public void PlaybackTimer_Tick(object sender, object e)
        {
            //stops mediaplayback when timer ticks
            playbackTimer.Stop();
            if (_mediaPlayerElement.MediaPlayer != null)
                _mediaPlayerElement.MediaPlayer.Pause();
        }

        private void HidePopups(object sender, TappedRoutedEventArgs e)
        {
            //PhenotypePopup.Visibility = Visibility.Collapsed;
            ChatEditPopup.Visibility = Visibility.Collapsed;
        }

        public async void LoadSavedAudio()
        {
            List<string> audioNames = MainPage.Current.SavedAudios;
            AudioDropdownList.ItemsSource = audioNames;
            //if (audioNames.Count > 0) {
            //    AudioDropdownList.SelectedIndex = 0;
            //    var audioFile = await FileManager.getSharedFileManager().GetSavedAudioFile(MainPage.Current.notebookId, audioNames[0]);
            //    if (audioFile != null)
            //    {
            //        _mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(audioFile);
            //        _mediaPlayerElement.Visibility = Visibility.Visible;
            //    }
            //}
            UpdateLayout();
        }

        
        public void updateChat()
        {
            chatView.ItemsSource = MainPage.Current.conversations;
            if (MainPage.Current != null && !MainPage.Current.speechEngineRunning)
                LoadSavedAudio();
            UpdateLayout();
        }

        private void SpeechPage_RecordingCreated(SpeechManager sender, Windows.Storage.StorageFile args)
        {
            //Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            //() =>
            //{
            this._mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(args);
            this._mediaPlayerElement.Visibility = Visibility.Visible;
            this.mediaText.Visibility = Visibility.Visible;
            this.loadedMedia = args.Name;
            this.mediaText.Text = args.Name;
            //}
            //);
        }

        /// <summary>
        /// Note: This method is empty.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SpeechPage_EngineHasResult(SpeechManager sender, SpeechEngineInterpreter args)
        {
            //this.tempSentenceTextBlock.Text = args.tempSentence;
        }

        private void OnChatViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            TextMessage message = (TextMessage)args.Item;

            // Only display message on the right when speaker index = 0
            //args.ItemContainer.HorizontalAlignment = (message.Speaker == 0) ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;

            if (message.IsNotFinal)
            {
                args.ItemContainer.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                args.ItemContainer.HorizontalAlignment = (message.Speaker == doctor) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }

            /*if (message.Speaker != 99 && message.Speaker != -1 && message.Speaker > maxSpeaker)
            {
                Debug.WriteLine("Detected speaker " + message.Speaker.ToString());
                for (var i = maxSpeaker + 1; i <= message.Speaker; i++)
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["Background_" + i.ToString()];
                    item.Content = "Speaker " + (i + 1).ToString();
                    this.speakerBox.Items.Add(item);
                }
                maxSpeaker = (int)message.Speaker;
            }*/
        }

        private void BackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
                {
                    var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                    if (titleBar != null)
                    {
                        titleBar.BackgroundColor = Colors.White;
                        titleBar.ButtonBackgroundColor = Colors.White;
                    }
                }
            }
        }

        /// <summary>
        /// TODO...
        /// </summary>
        /// <param name="sender">The speaker selection box.</param>
        /// <param name="e">Contains info about added or removed items.</param>
        /// <remarks>
        /// Called when the speaker selected in the "Select Doctor" box on the speech page
        /// is changed.
        /// </remarks>
        private void speakerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox senderBox = (ComboBox)sender;
            doctor = senderBox.SelectedIndex;

            if (senderBox.SelectedItem != null)
            {
                senderBox.Background = ( (ComboBoxItem)(senderBox.SelectedItem) ).Background;
            }

            for (int i = 0; i < SpeechManager.getSharedSpeechManager().conversation.Count; i++)
            {
                if (SpeechManager.getSharedSpeechManager().conversation[i].IsNotFinal)
                {
                    SpeechManager.getSharedSpeechManager().conversation[i].OnLeft = false;
                }
                else
                {
                    SpeechManager.getSharedSpeechManager().conversation[i].OnLeft = (SpeechManager.getSharedSpeechManager().conversation[i].Speaker != doctor);
                }
            }

            //NOTE: related to https://social.msdn.microsoft.com/Forums/vstudio/en-US/be9f1c8c-d60c-490a-890a-dc7bdb41c545/listviewitemssource-does-not-refresh-when-the-underlying-observable-collection-is-updated?forum=wpf
            var temp = chatView.ItemsSource;
            chatView.ItemsSource = null;
            chatView.ItemsSource = temp;
        }

        private void TextBlockDoubleTapped(object sender, RoutedEventArgs e) {

            TextBlock tb = ((TextBlock)sender);
            string body = tb.Text;
            originText = body.Trim();
            var element_Visual_Relative = tb.TransformToVisual(root);
            Point pos = element_Visual_Relative.TransformPoint(new Point(0, 0));
            TextMessage tm = MainPage.Current.conversations.Where(x => x.Body == body).FirstOrDefault();
            selectedTM = tm;
            selectedTB = tb;
            if (tm != null) {
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
                speechPhenoListView.ItemsSource = phenos;
                speechPhenoListView.UpdateLayout();
            }

        }

        private async void EditBoxLostFocus(object sender, RoutedEventArgs e) {
            string newText = "";
            TranscriptEditBox.Document.GetText(Windows.UI.Text.TextGetOptions.None, out newText);
            newText = newText.Trim();
            if (newText != originText && newText!= "") {
                selectedTM.Body = newText;
                selectedTB.Text = newText;
                await MainPage.Current.SaveCurrentConversationsToDisk();
                chatView.ItemsSource = MainPage.Current.conversations;
                UpdateLayout();
                MainPage.Current.NotifyUser("Transcript updated", NotifyType.StatusMessage, 1);
            }
        }

        private async void MessageAudioButtonClick(object sender, RoutedEventArgs e)
        {
            //only allow audio playback when there's no 
            if (this._mediaPlayerElement != null )
            {
                Button srcButton = (Button)sender;
                var m = (TextMessage)srcButton.DataContext;

                // Overloaded constructor takes the arguments days, hours, minutes, seconds, miniseconds.
                // Create a TimeSpan with miliseconds equal to the slider value.

                double actual_start = Math.Max(0, m.start);
                //by default set playback length to 1 second
                double actual_end = Math.Max(m.start + 1, m.end);

                int start_second = (int)(actual_start);
                int end_second = (int)(actual_end);

                int start_minute = start_second / 60;
                start_second = start_second - 60 * start_minute;
                int start_mili = (int)(100 * (actual_start - 60 * start_minute - start_second));

                // check for current source
                var savedFile = await FileManager.getSharedFileManager().GetSavedAudioFile(MainPage.Current.notebookId, m.AudioFile);


                // If audio is saved locally, get the local audio file and play based on saved interval
                // NOTE: seems the audio starts playing at the starting timestamp but doesn't auto-end at the ending timestamp
                // TODO: instead the playbacktimer is set to tick after ts which is defined using the starting timestamp
                //       wouldn't this cause problem if starting time is like 1s
                // TODO: Investigate
                if (savedFile != null)
                {
                    if (loadedMedia != savedFile.Name)
                    {
                        _mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(savedFile);
                        loadedMedia = savedFile.Name;
                        mediaText.Text = savedFile.Name;
                    }
                    TimeSpan ts = new TimeSpan(0, 0, start_minute, start_second, start_mili); //TODO: better not hardcode hour to 0 since it's possible for the recording to exceed 1 hour
                    playbackTimer.Interval = ts;
                    //Debug.WriteLine(ts);
                    _mediaPlayerElement.MediaPlayer.Position = ts;
                    _mediaPlayerElement.MediaPlayer.Play();
                    playbackTimer.Start();
                }
                // If no local save exists, try to get file from server and play.
                else if (savedFile == null)
                {
                    Debug.WriteLine("requesting from server");
                    int ind = m.ConversationIndex;
                    MainPage.Current.PlayMedia(m.AudioFile, actual_start, actual_end);
                }

            }
        }

        private void ListButtonClick(object sender, RoutedEventArgs e) {

        }

        private String changeNumSpeakers(String text, bool direction)
        {
            //true = up, false = down
            int proposed = Int32.Parse(text);
            if (direction)
            {
                proposed++;
                if (proposed > 5)
                {
                    proposed = 5;
                }

                if (proposed == 5)
                {
                    this.addSpeakerBtn.IsEnabled = false;
                }
                else
                {
                    this.addSpeakerBtn.IsEnabled = true;
                }
            }
            else
            {
                proposed--;
                if (proposed < 1)
                {
                    proposed = 1;
                }

                if (proposed == 1)
                {
                    this.removeSpeakerBtn.IsEnabled = false;
                }
                else
                {
                    this.removeSpeakerBtn.IsEnabled = true;
                }
            }

            return proposed.ToString();
        }

        private void addSpeakerBtn_Click(object sender, RoutedEventArgs e)
        {
            String proposedText = this.numSpeakerBox.Text;
            this.numSpeakerBox.Text = changeNumSpeakers(proposedText, true);

            try
            {
                SpeechManager.getSharedSpeechManager().speechAPI.changeNumSpeakers(
                SpeechManager.getSharedSpeechManager().speechInterpreter.worker_pid, Int32.Parse(proposedText));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("Unable to update");
            }

            //0, Int32.Parse(proposedText));

            //Debug.WriteLine("Detected speaker " + message.Speaker.ToString());
            //for (var i = maxSpeaker + 1; i <= message.Speaker; i++)
            //{

            Debug.WriteLine("Old text: " + proposedText + "\tNew text: " + this.numSpeakerBox.Text);
            if (proposedText != this.numSpeakerBox.Text)
            {
                this.adjustSpeakerCount(Int32.Parse(this.numSpeakerBox.Text));
            }
            //}
            //this.maxSpeaker = (int)message.Speaker;
        }

        private void removeSpeakerBtn_Click(object sender, RoutedEventArgs e)
        {
            String proposedText = this.numSpeakerBox.Text;
            this.numSpeakerBox.Text = changeNumSpeakers(proposedText, false);

            try
            {
                SpeechManager.getSharedSpeechManager().speechAPI.changeNumSpeakers(
                SpeechManager.getSharedSpeechManager().speechInterpreter.worker_pid, Int32.Parse(proposedText));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine("Unable to update");
            }
            //0, Int32.Parse(proposedText));

            Debug.WriteLine("Old text: " + proposedText + "\tNew text: " + this.numSpeakerBox.Text);
            if (proposedText != this.numSpeakerBox.Text)
            {
                this.adjustSpeakerCount(Int32.Parse(this.numSpeakerBox.Text));
            }
        }

        //TODO: the name of this function is misleading, 
        //      a better name would be something like setSpeakerNumButtonsEnabled
        /// <summary>
        /// Enable/Disable the buttons for manually adjusting the number of speakers on ASR result page.
        /// </summary>
        /// <param name="enabled">(bool)true to enable, (bool)false to disable</param>
        public void setSpeakerButtonEnabled(bool enabled)
        {
            this.addSpeakerBtn.IsEnabled = enabled;
            this.removeSpeakerBtn.IsEnabled = enabled;
        }

        /// <summary>
        /// Sets the number of speaker(s) on the ASR result page.
        /// </summary>
        /// <param name="newCount">the new number of speaker(s)</param>
        public void adjustSpeakerCount(int newCount)
        {
            Debug.WriteLine("New Count: " + newCount.ToString() + "\tCurSpeaker Count: " + this.curSpeakerCount.ToString());
            while (newCount != this.curSpeakerCount)
            {
                // speaker count increased
                if (newCount > this.curSpeakerCount)
                {
                    // Add new option to the combo box for selecting speaker as doctor.
                    Debug.WriteLine("Incrementing speaker count to " + newCount.ToString());
                    this.curSpeakerCount += 1;
                    ComboBoxItem item = new ComboBoxItem();
                    item.Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["Background_" + (this.curSpeakerCount - 1).ToString()];
                    item.Background.Opacity = 0.75;
                    item.Content = "Speaker " + curSpeakerCount.ToString();

                    this.speakerBox.Items.Add(item);
                }
                // speaker count decreased
                else
                {
                    // Remove an option in the combo box for selecting speaker as doctor.
                    Debug.WriteLine("Decrementing speaker count to " + newCount.ToString());
                    this.curSpeakerCount -= 1;
                    if (this.speakerBox.SelectedIndex + 1 > this.curSpeakerCount)
                    {
                        this.speakerBox.SelectedIndex--;
                    }
                    this.speakerBox.Items.RemoveAt(this.speakerBox.Items.Count - 1);
                }
            }
        }

        /// <summary>
        /// Handler function called when the selected audio in the "Recorded Audio" drop down list is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// Subscribed to (PhenoPad.SpeechPage.AudioDropdownList).SelectionChanged event
        /// </remarks>
        private async void AudioDropdownList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // proceed if the list of selected item isn't empty
            if (e.AddedItems.Count > 0)
            {
                string audioName = e.AddedItems[0].ToString();
                Debug.WriteLine("selection changed"+audioName);
                var audioFile = await FileManager.getSharedFileManager().GetSavedAudioFile(MainPage.Current.notebookId, audioName);
                if (audioFile != null)
                {
                    _mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(audioFile);
                    _mediaPlayerElement.Visibility = Visibility.Visible;
                }
                else
                {
                    bool success = await TryGetAudioFromServer(audioName);
                    Debug.WriteLine($"trygetaudiofrom server success = {success}");
                    if (success)
                    {
                        audioFile = await FileManager.getSharedFileManager().GetSavedAudioFile(MainPage.Current.notebookId, audioName);
                        _mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(audioFile);
                        _mediaPlayerElement.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        /// <summary>
        /// Initiates sequence for downloading an audio file from the ASR server and saving it to a local save file.
        /// </summary>
        /// <param name="name">The name of the audio file.</param>
        /// <returns>(bool)true if download and save successful, 
        /// (bool)false if download/save failed or if the user cancels request.
        /// </returns>
        /// <remarks>
        /// Displays a prompt box which asks the user to confirm downloading audio from ASR server.
        /// If the user confirms, downloads and saves the requested audio file.
        /// </remarks>
        public async Task<bool> TryGetAudioFromServer(string name)
        {
            try
            {
                var messageDialog = new MessageDialog("Getting audio file from server may take a while, continue?");
                messageDialog.Title = "PhenoPad";
                messageDialog.Commands.Add(new UICommand("Yes") { Id = 0 });
                messageDialog.Commands.Add(new UICommand("No") { Id = 2 });
                // Set the command that will be invoked by default
                messageDialog.DefaultCommandIndex = 2;
                // Set the command to be invoked when escape is pressed
                messageDialog.CancelCommandIndex = 2;
                // Show the message dialog
                var result = await messageDialog.ShowAsync();
                if ((int)result.Id == 0)
                {
                    bool success = await MainPage.Current.GetRemoteAudioAndSave(name);
                    return success;
                }
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }
            return false;
        }
    }

    // Bindable class representing a single text message.
    // Several fields are created to save the hassel of creating binding converters :D
    public class TextMessage : INotifyPropertyChanged
    {
        //public string Body { get; set; }

        private string _body;
        public string Body
        {
            get
            {
                return _body;
            }
            set
            {
                this._body = value;
                this.NotifyPropertyChanged("Body");
                //TODO: this code is disabled, learn more about this.
                /*
                Task<List<Phenotype>> phenosTask = PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(this._body);
                
                phenosTask.ContinueWith(_ =>
                {
                    List<Phenotype> list = phenosTask.Result;
                    this.phenotypesInText = new ObservableCollection<Phenotype>(list);
                    
                    if (list != null && list.Count > 0)
                    {
                        Debug.WriteLine("We detected at least " + list[0].name);
                        this.NotifyPropertyChanged("phenotypesInText");

                        list.Reverse();

                        Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            foreach (var p in list)
                            {
                                PhenotypeManager.getSharedPhenotypeManager().addPhenotypeCandidate(p, SourceType.Speech);
                            }
                        }
                        );
                    }
                });
                //phenosTask.Start();*/
            }
        }
        public double start;
        public double end;
        [XmlIgnore]
        private TimeInterval _interval;
        [XmlIgnore]
        public TimeInterval Interval {
            get {
                return _interval;
            }
            set {
                _interval = value;
                start = value.start;
                end = value.end;
            }
        }
        public int ConversationIndex { get; set; }
        public DateTime DisplayTime;
        public string AudioFile;

        //public string DisplayTime { get; set; }

        // Bind to phenotype display in conversation
        //[XmlIgnore]
        //public ObservableCollection<Phenotype> phenotypesInText { get; set; }


        [XmlArray("Phenotypes")]
        [XmlArrayItem("phenotype")]
        public List<Phenotype> phenotypesInText;

        public bool hasPhenotype { get; set; }
        public string numPhenotype
        {
            get
            {
                return phenotypesInText.Count.ToString();
            }
        }
        // Now that we support more than 2 speakers, we need to have speaker index
        public uint Speaker { get; set; }

        // Has finalized content of the string
        public bool IsFinal { get; set; }
        public bool IsNotFinal { get { return !IsFinal; } }         // This variable requires no setter

        public bool OnLeft { get; set; }

        public bool OnRight { get { return !OnLeft; } }

        // NOTE: no references found to this
        public int TextColumn
        {
            get
            {
                if (OnLeft)
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
        }

        //TODO: maybe this can be a future feature?
        public void fileterMessage() {
            // TODO: need to filter useless phrases of message for better display
        }

        // NOTE: no references found to this
        public int PhenoColumn
        {
            get
            {
                if (OnLeft)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
        
        //TODO: Question: this delegate points to an empty method?
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        /// <summary>
        /// Triggers TextMessage.PropertyChanged Event.
        /// </summary>
        /// <param name="propertyName"></param>
        private async void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            // This method is called by the Set accessor of each property.
            // The CallerMemberName attribute that is applied to the optional propertyName
            // parameter causes the property name of the caller to be substituted as an argument.
            if (PropertyChanged != null)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,() =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }
    }

    class BackgroundColorConverter : IValueConverter
    {
        // TODO: experiment with this
        /// <summary>
        /// Converts a text message object to an object representing a background color based on the speaker id.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType">NOTE: this parameter is never used</param>
        /// <param name="parameter">NOTE: this parameter is never used</param>
        /// <param name="language">NOTE: this parameter is never used</param>
        /// <returns>Object representing a background color (?)</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var m = ((TextMessage)value);

            string resourceKey;
            if (m.IsFinal)
            {
                resourceKey = "Background_" + m.Speaker.ToString();
            }
            else
            {
                resourceKey = "Background_99";
            }
            
            return Application.Current.Resources[resourceKey];
        }

        /// <summary>
        /// UNIMPLEMENTED
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
  
    class IntervalDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var m = ((TextMessage)value);

            string now = DateTime.Today.ToString("D");

            if ((m.Interval != null && m.Interval.start != -1))
            {
                double start_time = m.Interval.start;
                double end_time = m.Interval.end;

                int start_second = (int)(start_time);
                int start_minute = start_second / 60;
                start_second = start_second - 60 * start_minute;
                int start_mili = (int)(100 * (start_time - 60 * start_minute - start_second));

                int end_second = (int)(end_time);
                int end_minute = end_second / 60;
                end_second = end_second - 60 * end_minute;
                int end_mili = (int)(100 * (end_time - 60 * end_minute - end_second));

                string result = start_minute.ToString("D2") + ":" + start_second.ToString("D2") + "." + start_mili.ToString("D2") + " - " +
                    end_minute.ToString("D2") + ":" + end_second.ToString("D2") + "." + end_mili.ToString("D2");

                return now + "\tConversation(" + m.ConversationIndex + ")\t" + result;
            }
            //for displaying historical conversations
            else if (m.IsFinal) {
                //gets the date in mm/dd/yyyy HH:MM AM/PM format
                string date = String.Format("{0:g}", m.DisplayTime);
                //determines whos speaking: todo: clsssify who is doctor/patient
                string speaker = m.Speaker == 0 ? "Doctor" : "Patient:" + m.Speaker;
                return date+"-Session#" + m.ConversationIndex+"-Speaker "+ m.Speaker;
            }
            else
            {
                return now + " Processing ...";
            }

            
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Observable collection representing a text message conversation
    /// that can load more items incrementally.
    /// </summary>
    public class Conversation : ObservableCollection<TextMessage>, ISupportIncrementalLoading
    {
        private uint messageCount = 0;

        public Conversation()
        {
        }

        public bool HasMoreItems { get; } = true;

        //===========================================================

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            this.CreateMessages(count);

            return Task.FromResult<LoadMoreItemsResult>(
                new LoadMoreItemsResult()
                {
                    Count = count
                }).AsAsyncOperation();
        }

        public void CreateMessages(uint count)
        {
            for (uint i = 0; i < count; i++)
            {
                this.Insert(0, new TextMessage()
                {
                    Body = $"{messageCount}: {CreateRandomMessage()}",
                    Speaker = (messageCount++) % 2,
                    AudioFile = SpeechManager.getSharedSpeechManager().GetAudioName(),
                    //DisplayTime = DateTime.Now.ToString(),
                    IsFinal = true
                });
            }
        }

        private static Random rand = new Random();
        private static string fillerText = 
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

        public static string CreateRandomMessage()
        {
            return fillerText.Substring(0, rand.Next(5, fillerText.Length));
        }

        /// <summary>
        /// Removes all TextMessage instances in the collection and add a list of new ones. 
        /// </summary>
        /// <param name="range">the list of items to be added</param>
        /// <remarks>
        /// A method to avoid firing collection changed events when adding a bunch of items
        /// https://forums.xamarin.com/discussion/29925/observablecollection-addrange
        /// </remarks>
        public void ClearThenAddRange(List<TextMessage> range)
        {
            
            Items.Clear();
            foreach (var item in range)
            {
                Items.Add(item);
                item.PropertyChanged += Item_PropertyChanged;
            }

            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Adds a list of TextMessage instances to conversation and
        /// triggers PropertyChanged and CollectionChanged events.
        /// </summary>
        /// <param name="range">
        /// The list of TextMessage instances to be added.
        /// </param>
        /// <remarks>
        /// This method subscribes the handler function Item_PropertyChanged
        /// to each TextMessage instance's PropertyChanged event when it's 
        /// added.
        /// After all the instances in the list is added, raises 
        /// ObserverbleCollection.PropertyChanged and 
        /// ObserverbleCollection.CollectionChanged events.
        /// </remarks>
        public void AddRange(List<TextMessage> range)
        {
            foreach (var item in range)
            {
                Items.Add(item);
                item.PropertyChanged += Item_PropertyChanged;
            }

            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Updates an TextMessage instance in the collection when its property changes.
        /// </summary>
        /// <param name="sender">The TextMessage instance whose property changed</param>
        /// <param name="e">Contains info about the property that changed.</param>
        /// <remarks>
        /// Called when the property of a TextMessage instance changes (by the TM instance).
        /// Iterates through each item in the collection to find the index of the collection
        /// to find the index of the sender TextMessage in the collection, then replaces it
        /// with the altered TextMessage.
        /// </remarks> 
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var m = (TextMessage)sender;

            int i = -1;
            for (i = 0; i < this.Count; i++)
            {
                if (this[i].Body == m.Body)
                {
                    break;
                }
            }

            if (i != -1 && i < this.Count)
            {
                this.RemoveAt(i);
                this.Insert(i, m);
            } 
        }

        public void UpdateLastMessage(TextMessage m, bool addNew)
        {
            if (addNew || Items.Count == 0)
            {
                Items.Add(m);
            }
            else
            {
                Items.RemoveAt(Items.Count - 1);
                Items.Add(m);
            }
            m.PropertyChanged += Item_PropertyChanged;

            //var changedItems = new List<TextMessage>(m);
            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            //this.OnCollectionChanged(changedItems, Items.Count - 1);
        }
    }

    /// <summary>
    /// This ListView is tailored to a Chat experience where the focus is on the last item in the list
    /// and as the user scrolls up the older messages are incrementally loaded.  We're performing our
    /// own logic to trigger loading more data.
    /// //
    /// Note: This is just delay loading the data, but isn't true data virtualization.  A user that
    /// scrolls all the way to the beginning of the list will cause all the data to be loaded.
    /// </summary>
    public class ChatListView : ListView
    {
        private uint itemsSeen;
        private double averageContainerHeight;
        private bool processingScrollOffsets = false;
        private bool processingScrollOffsetsDeferred = false;

        // So that we only generate 10 messages, just in case
        private int randomMessageCount = 0;

        public ChatListView()
        {
            // We'll manually trigger the loading of data incrementally and buffer for 2 pages worth of data
            this.IncrementalLoadingTrigger = IncrementalLoadingTrigger.None;

            // Since we'll have variable sized items we compute a running average of height to help estimate
            // how much data to request for incremental loading
            this.ContainerContentChanging += this.UpdateRunningAverageContainerHeight;
        }

        protected override void OnApplyTemplate()
        {
            var scrollViewer = this.GetTemplateChild("ScrollViewer") as ScrollViewer;

            if (scrollViewer != null)
            {
                scrollViewer.ViewChanged += (s, a) =>
                {
                    // Check if we should load more data when the scroll position changes.
                    // We only get this once the content/panel is large enough to be scrollable.
                    this.StartProcessingDataVirtualizationScrollOffsets(this.ActualHeight);
                };
            }

            base.OnApplyTemplate();
        }

        // We use ArrangeOverride to trigger incrementally loading data (if needed) when the panel is too small to be scrollable.
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Allow the panel to arrange first
            var result = base.ArrangeOverride(finalSize);

            StartProcessingDataVirtualizationScrollOffsets(finalSize.Height);

            return result;
        }

        private async void StartProcessingDataVirtualizationScrollOffsets(double actualHeight)
        {
            // Avoid re-entrancy. If we are already processing, then defer this request.
            if (processingScrollOffsets)
            {
                processingScrollOffsetsDeferred = true;
                return;
            }

            this.processingScrollOffsets = true;

            do
            {
                processingScrollOffsetsDeferred = false;
                await ProcessDataVirtualizationScrollOffsetsAsync(actualHeight);

                // If a request to process scroll offsets occurred while we were processing
                // the previous request, then process the deferred request now.
            }
            while (processingScrollOffsetsDeferred);

            // We have finished. Allow new requests to be processed.
            this.processingScrollOffsets = false;
        }

        private async Task ProcessDataVirtualizationScrollOffsetsAsync(double actualHeight)
        {
            var panel = this.ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                if ((panel.FirstVisibleIndex != -1 && panel.FirstVisibleIndex * this.averageContainerHeight < actualHeight * this.IncrementalLoadingThreshold) ||
                    (Items.Count == 0))
                {
                    var virtualizingDataSource = this.ItemsSource as ISupportIncrementalLoading;
                    if (virtualizingDataSource != null)
                    {
                        if (virtualizingDataSource.HasMoreItems)
                        {
                            uint itemsToLoad;
                            if (this.averageContainerHeight == 0.0)
                            {
                                // We don't have any items yet. Load the first one so we can get an
                                // estimate of the height of one item, and then we can load the rest.
                                itemsToLoad = 1;
                            }
                            else
                            {
                                double avgItemsPerPage = actualHeight / this.averageContainerHeight;
                                // We know there's data to be loaded so load at least one item
                                itemsToLoad = Math.Max((uint)(this.DataFetchSize * avgItemsPerPage), 1);
                            }

                            
                            // Only for debugging purpose without a server
                            if (randomMessageCount > 0)
                            {
                                await virtualizingDataSource.LoadMoreItemsAsync(itemsToLoad);
                                randomMessageCount--;
                            }
                        }
                    }
                }
            }
        }

        private void UpdateRunningAverageContainerHeight(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer != null && !args.InRecycleQueue)
            {
                switch (args.Phase)
                {
                    case 0:
                        // use the size of the very first placeholder as a starting point until
                        // we've seen the first item
                        if (this.averageContainerHeight == 0)
                        {
                            this.averageContainerHeight = args.ItemContainer.DesiredSize.Height;
                        }

                        args.RegisterUpdateCallback(1, this.UpdateRunningAverageContainerHeight);
                        args.Handled = true;
                        break;

                    case 1:
                        // set the content
                        args.ItemContainer.Content = args.Item;
                        args.RegisterUpdateCallback(2, this.UpdateRunningAverageContainerHeight);
                        args.Handled = true;
                        break;

                    case 2:
                        // refine the estimate based on the item's DesiredSize
                        this.averageContainerHeight = (this.averageContainerHeight * itemsSeen + args.ItemContainer.DesiredSize.Height) / ++itemsSeen;
                        args.Handled = true;
                        break;
                }
            }
        }
    }

}
