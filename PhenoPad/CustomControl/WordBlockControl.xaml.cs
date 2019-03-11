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
            candidateList.ItemsSource = candidates;
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            f.ShowAt(WordBlock);
        }

        private void CandidateList_Click(object sender, RoutedEventArgs args) {
            Debug.WriteLine(sender.GetType().ToString());
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
