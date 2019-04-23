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
        public enum ShowingType {
            WORD,
            ABBR
        }
        public int word_index;
        public double phrase_index;
        public int line_index;
        public int selected_index;
        public bool corrected;
        public ShowingType showing;

        public bool is_abbr;

        public string current;
        public List<string> candidates;

        public string abbr_current;
        public List<string> abbr_candidates;

        public WordBlockControl()
        {
        }

        public WordBlockControl(int line_index, double phrase_index, int word_index, string current, List<string>candidates) {
            this.InitializeComponent();
            this.line_index = line_index;
            this.phrase_index = phrase_index;
            this.word_index = word_index;
            this.current = current;
            this.candidates = candidates;
            selected_index = 0;
            WordBlock.Text = current;
            corrected = false;
            AlternativeList.ItemsSource = candidates;
            //by default set abbreviation to false;
            abbr_current = "";
            abbr_candidates = new List<string>();
            is_abbr = false;
            showing = ShowingType.WORD;

        }

        public void SetAbbreviation(List<string> abbr_alternatives) {

            abbr_candidates.Clear();
            foreach (string s in abbr_alternatives)
                abbr_candidates.Add( "(" + s + ")");
            abbr_current = abbr_candidates[0];
            is_abbr = true;
            AbbreviationBlock.Visibility = Visibility.Visible;
        }

        public void UnsetAbbreviation() {
            is_abbr = false;
            abbr_candidates.Clear();
            abbr_current = "";
            AbbreviationBlock.Visibility = Visibility.Collapsed;
        }


        public void UpdateDisplay() {
            WordBlock.Text = current;
            AlternativeList.ItemsSource = candidates;
        }

        private void ShowWordCandidate(object sender, RoutedEventArgs args) {
            AlternativeList.ItemsSource = candidates;
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            f.ShowAt(WordBlock);
            showing = ShowingType.WORD;
        }

        private void ShowAbbrCandidate(object sender, RoutedEventArgs args)
        {
            AlternativeList.ItemsSource = abbr_candidates;
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            f.ShowAt(WordBlock);
            showing = ShowingType.ABBR;
        }

        private void ReplaceAlternative(object sender, RoutedEventArgs args)
        {//replace current selected alternative with the custom input word
            string text = AlternativeInput.Text;

            if (showing == ShowingType.WORD && text.Length > 0) {
                current = text;
                selected_index = -1;
                WordBlock.Text = current;
                corrected = true;
            }

            else if (showing == ShowingType.ABBR && text.Length > 0)
            {
                abbr_current = "(" + text.Trim() + ")";
                selected_index = -1;
                AbbreviationBlock.Text = abbr_current;
                WordBlock.Text = abbr_current;
                corrected = true;
            }
            MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
            MainPage.Current.NotifyUser($"Changed to {text}", NotifyType.StatusMessage, 1);
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
                MainPage.Current.curPage.ClearSelectionAsync();
            }
        }

        private void AlternativeList_Click(object sender, ItemClickEventArgs e)
        {//called when user clicks on the alternative from the flyout panel

            int ind = AlternativeList.Items.IndexOf((string)e.ClickedItem);

            if (showing == ShowingType.WORD)
            {
                if (candidates[ind] == "None")
                {
                    current = "";
                }
                else {
                    current = candidates[ind];
                }
                selected_index = ind;

                WordBlock.Text = current;
            }
            else if (showing == ShowingType.ABBR) {

                abbr_current = abbr_candidates[ind];
                AbbreviationBlock.Text = abbr_current;
               
            }
            corrected = true;
            MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
            MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
            MainPage.Current.curPage.ClearSelectionAsync();
            UpdateLayout();
        }

        public List<Button> GetCurWordCandidates() {
            //TODO HANDLE ABBR
            List<Button> lst = new List<Button>();
            //if user has manually added alternative from text input, add it to candidates as well
            if (!candidates.Contains(current)) {

                Debug.WriteLine("candidates does not contain current");

                Button tb = new Button();
                tb.FontSize = 16;
                tb.VerticalAlignment = VerticalAlignment.Center;

                if (is_abbr)
                    tb.Content = "(" + current + ")";
                else
                    tb.Content = current;
                lst.Add(tb);
                tb.Click += CandidateList_Click;
            }
            foreach (string candidate in candidates)
            {
                Button tb = new Button();
                tb.FontSize = 16;
                tb.VerticalAlignment = VerticalAlignment.Center;
                if (is_abbr && candidates.IndexOf(candidate) > 0)
                    tb.Content = "(" + candidate + ")";
                else
                    tb.Content = candidate;
                lst.Add(tb);
                tb.Click += CandidateList_Click;
            }
            return lst;

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
            MainPage.Current.curPage.RawStrokeTimer.Stop();
            MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
            MainPage.Current.curPage.HideCurLineStackPanel();
            MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
            MainPage.Current.curPage.ClearSelectionAsync();

            UpdateLayout();
        }


    }
}
