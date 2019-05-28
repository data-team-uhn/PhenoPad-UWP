using PhenoPad.HWRService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Serialization;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    [Serializable]
    public sealed partial class WordBlockControl : UserControl
    {
        public int word_index;
        public double left;
        public int line_index;
        public int selected_index;
        public bool corrected;

        public bool is_abbr;

        public string current;
        public List<string> candidates;

        public WordBlockControl()
        {
        }

        public WordBlockControl(int line_index, double left, int word_index, string current, List<string>candidates) {
            this.InitializeComponent();
            this.line_index = line_index;
            this.left = left;
            this.word_index = word_index;
            this.current = current;
            this.candidates = candidates;
            selected_index = 0;
            WordBlock.Text = current;
            corrected = false;
            AlternativeList.ItemsSource = candidates;
            //by default set abbreviation to false;
            is_abbr = false;

        }


        public void UpdateDisplay() {
            WordBlock.Text = current;
            AlternativeList.ItemsSource = candidates;
        }

        private void ShowWordCandidate(object sender, RoutedEventArgs args) {
            AlternativeList.ItemsSource = candidates;
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            f.ShowAt(WordBlock);
        }

        private void ReplaceAlternative(object sender, RoutedEventArgs args)
        {//replace current selected alternative with the custom input word
            string text = AlternativeInput.Text;

            if (text.Length > 0) {
                current = text;
                selected_index = -1;
                WordBlock.Text = current;
                corrected = true;
                //since user manully chose the best choice, auto insert it into first place candidate and
                //remove the last word
                candidates.Insert(0, text);
                candidates.RemoveAt(candidates.Count - 1);
            }

            if (MainPage.Current != null)
            {
                MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
                MainPage.Current.NotifyUser($"Changed to {text}", NotifyType.StatusMessage, 1);
            }
            //for view mode
            else {
                //nothing to do?
            }

        }


        public void ChangeAlterFromStroke(string word)
        {
            if (word.Length > 0)
            {
                current = word;
                selected_index = -1;
                WordBlock.Text = current;
                MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
                corrected = true;
                MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
                if (MainPage.Current.curPage != null)
                    MainPage.Current.curPage.ClearSelectionAsync();
            }
        }

        private void AlternativeList_Click(object sender, ItemClickEventArgs e)
        {//called when user clicks on the alternative from the flyout panel

            int ind = AlternativeList.Items.IndexOf((string)e.ClickedItem);
            current = candidates[ind];
            selected_index = ind;
            WordBlock.Text = current;
            corrected = true;
            if (MainPage.Current != null) {
                MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
                MainPage.Current.curPage.ClearSelectionAsync();
                MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
            }

            UpdateLayout();
        }

        public List<Button> GetCurWordCandidates() {
            //TODO HANDLE ABBR
            List<Button> lst = new List<Button>();
            //if user has manually added alternative from text input, add it to candidates as well
            if (!candidates.Contains(current))
            {

                //Debug.WriteLine("candidates does not contain current");

                Button tb = new Button();
                tb.Style = (Style)Application.Current.Resources["ButtonStyle1"];
                tb.FontSize = 16;
                tb.Background = new SolidColorBrush(Colors.LightGray);
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Content = current;
                lst.Add(tb);
                tb.Tapped += CandidateList_Click;
            }
            foreach (string candidate in candidates)
            {
                Button tb = new Button();
                tb.Style = (Style)Application.Current.Resources["ButtonStyle1"];
                tb.Background = current == candidate ? new SolidColorBrush(Colors.LightGray) : new SolidColorBrush(Colors.Transparent);
                tb.FontSize = 16;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Content = candidate;
                lst.Add(tb);

                tb.Tapped += CandidateList_Click;
            }
            return lst;

        }

        public TextBlock GetCurWordTextBlock() {
            TextBlock tb = new TextBlock();
            tb.FontSize = 18;
            tb.Tapped += ShowWordCandidate;
            tb.Text = current;
            tb.VerticalAlignment = VerticalAlignment.Center;
            if (is_abbr)
            {
                tb.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else {
                tb.Foreground = new SolidColorBrush(Colors.Black);
            }
            return tb;
        }

        public Rect GetUIRect() {
            var trans = this.TransformToVisual(MainPage.Current.curPage);
            Rect bound = trans.TransformBounds(new Rect(0,0,20,20));
            //Debug.WriteLine($"{current}'s rect: x={bound.X}, y={bound.Y}");
            return bound;
        }

        private void CandidateList_Click(object sender, RoutedEventArgs args)
        {//called when user clicks on the alternative from the horizonal word stack panel

            //TODO: HANDLE ABBR
            string content = (string)((Button)sender).Content;
            int ind = candidates.IndexOf(content);
            if (ind == -1)
            {
                current = content;
            }
            else {
                selected_index = ind;
                current = candidates[ind];
            }
            WordBlock.Text = current;
            corrected = true;
            //don't need to check for condition because this function will only be called from note editing mode
            MainPage.Current.curPage.RawStrokeTimer.Stop();
            MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
            MainPage.Current.curPage.HideCurLineStackPanel();
            MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
            MainPage.Current.curPage.ClearSelectionAsync();

            UpdateLayout();
        }


    }
}
