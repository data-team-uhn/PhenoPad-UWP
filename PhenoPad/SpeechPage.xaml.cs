//*********************************************************
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
            }
        }

        public string DisplayTime { get; set; }

        // Bind to phenotype display in conversation
        public ObservableCollection<Phenotype> phenotypesInText;

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

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
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
                    DisplayTime = DateTime.Now.ToString(),
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
            }

            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void UpdateLastMessage(TextMessage m, bool doRemove)
        {
            /*
            if (doRemove && Items.Count > 0)
            {
                Items.RemoveAt(Items.Count - 1);
            }
            Items.Add(m);

            if (doRemove)
            {
                this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            }
            */

            if (doRemove == false || Items.Count == 0)
            {
                Items.Add(m);

                this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            else
            {
                Items[Items.Count - 1].Body = m.Body;
                Items[Items.Count - 1] = m;
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

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
      
        public SpeechPage()
        {
            this.InitializeComponent();

            chatView.ItemsSource = SpeechManager.getSharedSpeechManager().conversation;
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;
            realtimeChatView.ItemsSource = SpeechManager.getSharedSpeechManager().realtimeConversation;

            SpeechManager.getSharedSpeechManager().EngineHasResult += SpeechPage_EngineHasResult;
        }

        private int doctor = 0;
        private int maxSpeaker = 0;

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

            if (message.Speaker != 99 && message.Speaker != -1 && message.Speaker > maxSpeaker)
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
            }
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
            doctor = ((ComboBox)sender).SelectedIndex;

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
    }
}
