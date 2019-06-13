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
        public int line_index;
        public int word_index;//The word index within a phrase
        public double left;//The leading canvasLeft coordinate of a phrase, all words of the same phrase will have the same left value

        public bool corrected;//Whether this word has been manually corrected
        public bool is_abbr;//whether this word is detected as an abbreviation

        public string current;
        public List<string> candidates;
        public bool flyoutOpening;

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
            WordBlock.Text = current;
            corrected = false;
            //AlternativeList.ItemsSource = candidates;
            //by default set abbreviation to false;
            is_abbr = false;
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            //f.Closed += ResetUIVisibility;
            f.Opened += SetFlyoutOpening;
            //this.LostFocus += ResetUIVisibility;

        }

        private void SetFlyoutOpening(object sender, object e)
        {
            flyoutOpening = true;
        }

        private void ResetUIVisibility(object sender, object e)
        {
            AlternativeStack.Visibility = Visibility.Visible;
            AlternativeList.Visibility = Visibility.Visible;
            AlternativeInput.Visibility = Visibility.Visible;
            flyoutOpening = false;
            //Debug.WriteLine("UI RESET");
        }

        public void UpdateDisplay() {
            WordBlock.Text = current;
            //AlternativeList.ItemsSource = candidates;
        }

        private void ShowWordCandidate(object sender, RoutedEventArgs args) {
            //Debug.WriteLine("textblock tapped");
            AlternativeList.Visibility = Visibility.Collapsed;
            AlternativeStack.Children.Clear();
            var buttons = this.GetCurWordCandidates();
            foreach (var b in buttons)
                AlternativeStack.Children.Add(b);
            Flyout f = (Flyout)this.Resources["AlternativeFlyout"];
            f.ShowAt(WordBlock);
        }

        private void ReplaceAlternative(object sender, RoutedEventArgs args)
        {//replace current selected alternative with the custom input word
            string text = AlternativeInput.Text;

            if (text.Length > 0) {
                current = text;
                WordBlock.Text = current;
                corrected = true;
                //since user manully chose the best choice, auto insert it into first place candidate and
                //remove the last word
                candidates.Insert(0, text);
                candidates.RemoveAt(candidates.Count - 1);
                if (MainPage.Current != null)
                {
                    MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
                    MainPage.Current.NotifyUser($"Changed to {text}", NotifyType.StatusMessage, 1);
                }
            }

            //for view mode
            else {
                //nothing to do?
            }
            UpdateLayout();
        }

        public void ChangeAlterFromTextInput(string word)
        {
            if (word.Length > 0)
            {
                current = word;
                WordBlock.Text = current;
                candidates.RemoveAt(candidates.Count - 1);
                candidates.Insert(0, current);
                MainPage.Current.curPage.annotateCurrentLineAndUpdateUI(line_index: line_index);
                corrected = true;
                MainPage.Current.NotifyUser($"Changed to {current}", NotifyType.StatusMessage, 1);
                if (MainPage.Current.curPage != null)
                    MainPage.Current.curPage.ClearSelectionAsync();
            }
        }

        private void AlternativeList_Click(object sender, ItemClickEventArgs e)
        {//called when user clicks on the alternative from the flyout panel

            int ind = candidates.IndexOf((string)e.ClickedItem);
            current = candidates[ind];
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
            List<Button> lst = new List<Button>();
            //if user has manually added alternative from text input, add it to candidates as well

            for (int i = 0; i < candidates.Count; i++) {

                Button tb = new Button();
                tb.Style = (Style)Application.Current.Resources["ButtonStyle1"];
                tb.Background = current == candidates[i] ? new SolidColorBrush(Colors.LightGray) : new SolidColorBrush(Colors.Transparent);
                tb.FontSize = 16;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Content = candidates[i];
                lst.Add(tb);
                if (candidates[i].Contains("(") && candidates[i].Contains(")") && is_abbr)
                {
                    //parse all extended forms of abbreviation as one button with flyout and returns directly
                    tb.Background = candidates.GetRange(i,candidates.Count-i).Contains(current) ? 
                        new SolidColorBrush(Colors.LightGray) : new SolidColorBrush(Colors.Transparent);
                    tb.Tapped += ShowAbbrCandidatesOnFlyout;
                    return lst;
                }
                tb.Tapped += CandidateList_Click;
            }
            Debug.WriteLine(lst.Count);
            return lst;
        }

        public void ShowAbbrCandidatesOnFlyout(object sender, TappedRoutedEventArgs e) {
            var allAlters = candidates.Where(x => x.Contains("(") && x.Contains(")")).ToList();
            //Debug.WriteLine($"showing {allAlters.Count} alters");
            AlternativeList.ItemsSource = allAlters;
            AlternativeList.Visibility = Visibility.Visible;
            AlternativeStack.Visibility = Visibility.Collapsed;
            AlternativeInput.Visibility = Visibility.Collapsed;
            Flyout fo = (Flyout)this.Resources["AlternativeFlyout"];
            UpdateLayout();

            if (!flyoutOpening)
            {
                if (((FrameworkElement)sender).Parent != AlternativeStack)
                    fo.ShowAt((FrameworkElement)sender);
                else
                    fo.ShowAt(this);
            }
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

        public HWRRecognizedText ConvertToHWRRecognizedText()
        {
            HWRRecognizedText txt = new HWRRecognizedText();
            txt.candidateList = candidates;
            txt.selectedCandidate = current;
            txt.selectedIndex = candidates.IndexOf(current);
            return txt;
        }


    }
}
