using System;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class DiseaseControl : UserControl
    {

        public String id
        {
            get { return (String)GetValue(idProperty); }
            set
            {
               
                SetValue(idProperty, value);
            }
        }
        public String name
        {
            get { return (String)GetValue(nameProperty); }
            set
            {
                disNameTextBlock.Text = value;
                disNameTextBlockTooltip.Text = value;
                SetValue(nameProperty, value);
            }
        }
        public string url
        {
            get { return (String)GetValue(urlProperty); }
            set
            {
                SetValue(urlProperty, value);
            }
        }

        public double score
        {
            get { return (double)GetValue(scoreProperty); }
            set
            {
                SetValue(scoreProperty, value);
            }
        }

        public static readonly DependencyProperty idProperty = DependencyProperty.Register(
          "id",
          typeof(String),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );

        public static readonly DependencyProperty nameProperty = DependencyProperty.Register(
          "name",
          typeof(String),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );

        public static readonly DependencyProperty urlProperty = DependencyProperty.Register(
          "url",
          typeof(int),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );
        public static readonly DependencyProperty scoreProperty = DependencyProperty.Register(
          "score",
          typeof(double),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );


        public DiseaseControl()
        {
            this.InitializeComponent();
        }

        private void DetailButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = (Flyout)this.Resources["DiseaseDetailFlyout"];
            disDetailControl.navigateTo(url);
            flyout.ShowAt(this);
        }

        private void NameGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var flyout = (Flyout)this.Resources["DiseaseDetailFlyout"];
            disDetailControl.navigateTo(url);
            flyout.ShowAt(this);
        }

        private void Flyout_Opened(object sender, object e)
        {

        }
    }
}
