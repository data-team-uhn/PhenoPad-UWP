using PhenoPad.HWRService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class WordBlockControl : UserControl
    {
        public string current;
        public List<string> candidates;
        public int word_index;
        public int selected_index;

        public WordBlockControl()
        {
            this.InitializeComponent();
        }

        public WordBlockControl(string current, List<string>candidates, int index) {
            this.InitializeComponent();
            this.current = current;
            this.candidates = candidates;
            selected_index = 0;
            word_index = index;
            WordBlock.Text = current;
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
            }
        }

        private void CandidateList_Click(object sender, RoutedEventArgs args) {
            int ind = candidates.IndexOf((string)((Button)sender).Content);
            selected_index = ind;
            current = candidates[ind];
            WordBlock.Text = current;
            Debug.WriteLine($"candidate word has been changed to={current}");
            MainPage.Current.curPage.HideCurLineStackPanel();
            UpdateLayout();
        }

        private void AlternativeList_Click(object sender, ItemClickEventArgs e) {
            int ind = AlternativeList.Items.IndexOf((string)e.ClickedItem);
            selected_index = ind;
            current = candidates[ind];
            WordBlock.Text = current;
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
