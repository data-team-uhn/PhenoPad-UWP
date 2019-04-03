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
        public string current;
        public List<string> candidates;
        public int word_index;
        public double phrase_index;
        public int line_index;
        public int selected_index;
        public bool corrected;

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

        private void ReplaceAlternative(object sender, RoutedEventArgs args) {
            string text = AlternativeInput.Text;
            if (text.Length > 0) {
                current = text;
                selected_index = -1;
                WordBlock.Text = current;
                corrected = true;
            }
        }

        private void CandidateList_Click(object sender, RoutedEventArgs args) {
            int ind = candidates.IndexOf((string)((Button)sender).Content);
            selected_index = ind;
            current = candidates[ind];
            WordBlock.Text = current;
            corrected = true;
            Debug.WriteLine($"candidate word has been changed to={current}");
            MainPage.Current.curPage.HideCurLineStackPanel();
            UpdateLayout();
        }

        private void AlternativeList_Click(object sender, ItemClickEventArgs e) {
            int ind = AlternativeList.Items.IndexOf((string)e.ClickedItem);
            selected_index = ind;
            current = candidates[ind];
            WordBlock.Text = current;
            corrected = true;
            Debug.WriteLine($"alternative word has been changed to={current}");
            UpdateLayout();
        }

        public List<Button> GetCurWordCandidates() {
            List<Button> lst = new List<Button>();
            foreach (string candidate in candidates)
            {
                Button tb = new Button();
                tb.FontSize = 16;
                tb.Content = candidate;
                lst.Add(tb);
                tb.Click += CandidateList_Click;
            }
            return lst;

        }
    }
}
