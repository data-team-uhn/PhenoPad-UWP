﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using PhenoPad.SpeechService;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using System.Diagnostics;

using PhenoPad.PhenotypeService;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Windows.Media.Core;
using Windows.UI.Core;

namespace PhenoPad
{
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
        
        public TimeInterval Interval { get; set; }
        public int ConversationIndex { get; set; }

        //public string DisplayTime { get; set; }

        // Bind to phenotype display in conversation
        public ObservableCollection<Phenotype> phenotypesInText { get; set; }

        // Now that we support more than 2 users, we need to have speaker index
        public uint Speaker { get; set; }

        // Has finalized content of the string
        public bool IsFinal { get; set; }
        public bool IsNotFinal { get { return !IsFinal; } }         // This variable requires no setter

        public bool OnLeft { get; set; }

        public bool OnRight { get { return !OnLeft; } }

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

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
                );
            }
        }
    }

    class BackgroundColorConverter : IValueConverter
    {
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

            if (m.Interval != null && m.Interval.start != -1)
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

    // Observable collection representing a text message conversation
    // that can load more items incrementally.
    public class Conversation : ObservableCollection<TextMessage>, ISupportIncrementalLoading
    {
        private uint messageCount = 0;

        public Conversation()
        {
        }

        public bool HasMoreItems { get; } = true;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            this.CreateMessages(count);

            return Task.FromResult<LoadMoreItemsResult>(
                new LoadMoreItemsResult()
                {
                    Count = count
                }).AsAsyncOperation();
        }

        private void CreateMessages(uint count)
        {
            for (uint i = 0; i < count; i++)
            {
                this.Insert(0, new TextMessage()
                {
                    Body = $"{messageCount}: {CreateRandomMessage()}",
                    Speaker = (messageCount++) % 3,
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

        // A method to avoid firing collection changed events when adding a bunch of items
        // https://forums.xamarin.com/discussion/29925/observablecollection-addrange
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
                m.PropertyChanged += Item_PropertyChanged;
            } else
            {
                Items.RemoveAt(Items.Count - 1);
                Items.Add(m);
                m.PropertyChanged += Item_PropertyChanged;
            }
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

    public sealed partial class SpeechPage : Page
    {
        public static SpeechPage Current;
        public PhenotypeManager PhenoMana => PhenotypeManager.getSharedPhenotypeManager();

        public SpeechPage()
        {
            this.InitializeComponent();
            SpeechPage.Current = this;

            chatView.ItemsSource = SpeechManager.getSharedSpeechManager().conversation;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            realtimeChatView.ItemsSource = SpeechManager.getSharedSpeechManager().realtimeConversation;

            SpeechManager.getSharedSpeechManager().EngineHasResult += SpeechPage_EngineHasResult;
            SpeechManager.getSharedSpeechManager().RecordingCreated += SpeechPage_RecordingCreated;
        }

        private string loadedMedia = String.Empty;
        private void SpeechPage_RecordingCreated(SpeechManager sender, Windows.Storage.StorageFile args)
        {
            this._mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(args);
            this._mediaPlayerElement.Visibility = Visibility.Visible;
            this.mediaText.Visibility = Visibility.Visible;
            this.loadedMedia = args.Name;
            this.mediaText.Text = args.Name;
        }

        private int doctor = 0;
        private int curSpeakerCount = 2;

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
                args.ItemContainer.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
            }
            else
            {
                args.ItemContainer.HorizontalAlignment = (message.Speaker == doctor) ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;
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

        private void speakerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox senderBox = (ComboBox)sender;
            doctor = senderBox.SelectedIndex;
            if (senderBox.SelectedItem != null)
            {
                senderBox.Background = ((ComboBoxItem)(senderBox.SelectedItem)).Background;
            }
            
            // Do not change combobox label after selection
            //speakerTxt.Text = "doctor: " + (doctor + 1).ToString();

            for (int i = 0; i < SpeechManager.getSharedSpeechManager().conversation.Count; i++)
            //foreach (TextMessage item in chatView.ItemsSource.Items)
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

            var temp = chatView.ItemsSource;
            chatView.ItemsSource = null;
            chatView.ItemsSource = temp;
        }

        private async void MessageButtonClick(object sender, RoutedEventArgs e)
        {
            if (this._mediaPlayerElement != null)
            {
                Button srcButton = (Button)sender;
                var m = (TextMessage)srcButton.DataContext;
                Debug.WriteLine(m.Body);

                // check for current source
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                var savedFile =
                    await storageFolder.GetFileAsync("sample_" + m.ConversationIndex + ".wav");

                if (savedFile.Name != this.loadedMedia)
                {
                    this._mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(savedFile);
                    this.loadedMedia = savedFile.Name;
                    this.mediaText.Text = savedFile.Name;
                }

                // Overloaded constructor takes the arguments days, hours, minutes, seconds, miniseconds.
                // Create a TimeSpan with miliseconds equal to the slider value.

                double actual_start = Math.Max(0, m.Interval.start - 5);
                int start_second = (int)(actual_start);
                int start_minute = start_second / 60;
                start_second = start_second - 60 * start_minute;
                int start_mili = (int)(100 * (actual_start - 60 * start_minute - start_second));

                TimeSpan ts = new TimeSpan(0, 0, start_minute, start_second, start_mili);
                this._mediaPlayerElement.MediaPlayer.Position = ts;
            }
        }

        /**
         * true = up
         * false = down
         */
        private String changeNumSpeakers(String text, bool direction)
        {
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
                } else
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
            } catch (Exception ex)
            {
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
                Debug.WriteLine("Unable to update");
            }
            //0, Int32.Parse(proposedText));

            Debug.WriteLine("Old text: " + proposedText + "\tNew text: " + this.numSpeakerBox.Text);
            if (proposedText != this.numSpeakerBox.Text)
            {
                this.adjustSpeakerCount(Int32.Parse(this.numSpeakerBox.Text));
            }
        }

        public void setSpeakerButtonEnabled(bool enabled)
        {
            this.addSpeakerBtn.IsEnabled = enabled;
            this.removeSpeakerBtn.IsEnabled = enabled;
        }

        public void adjustSpeakerCount(int newCount)
        {
            Debug.WriteLine("New Count: " + newCount.ToString() + "\tCurSpeaker Count: " + this.curSpeakerCount.ToString());
            while (newCount != this.curSpeakerCount)
            {
                if (newCount > this.curSpeakerCount)
                {
                    Debug.WriteLine("Incrementing speaker count to " + newCount.ToString());
                    this.curSpeakerCount += 1;
                    ComboBoxItem item = new ComboBoxItem();
                    item.Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["Background_" + (this.curSpeakerCount - 1).ToString()];
                    item.Background.Opacity = 0.75;
                    item.Content = "Speaker " + curSpeakerCount.ToString();

                    this.speakerBox.Items.Add(item);
                }
                else
                {
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
    }
}
