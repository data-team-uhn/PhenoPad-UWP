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
using PhenoPad.PhenotypeService;
using Windows.UI;
using PhenoPad.Styles;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class PhenotypeControl : UserControl
    {
        public String phenotypeName
        {
            get { return (String)GetValue(phenotypeNameProperty); }
            set
            {
                phenotypeNameTextBlock.Text = value;
                SetValue(phenotypeNameProperty, value);
            }
        }
        public String phenotypeId
        {
            get { return (String)GetValue(phenotypeIdProperty); }
            set
            {
                SetValue(phenotypeIdProperty, value);
            }
        }
        public int phenotypeState
        {
            get { return (int)GetValue(phenotypeStateProperty); }
            set
            {
                setPhenotypeState(value);
                SetValue(phenotypeStateProperty, value);
            }
        }

        public SourceType sourceType
        {
            get { return (SourceType)GetValue(sourceTypeProperty); }
            set
            {
                SetValue(sourceTypeProperty, value);
            }
        }

        public static readonly DependencyProperty phenotypeNameProperty = DependencyProperty.Register(
          "phenotypeName",
          typeof(String),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );

        public static readonly DependencyProperty phenotypeIdProperty = DependencyProperty.Register(
          "phenotypeId",
          typeof(String),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );
        public static readonly DependencyProperty phenotypeStateProperty = DependencyProperty.Register(
          "phenotypeState",
          typeof(int),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );
        public static readonly DependencyProperty sourceTypeProperty = DependencyProperty.Register(
          "sourceType",
          typeof(SourceType),
          typeof(TextBlock),
          new PropertyMetadata(null)
        );

        public PhenotypeControl()
        {
            this.InitializeComponent();
            setPhenotypeState(phenotypeState);
            //DeletePhenotypeSB.Begin();
        }

        // Add a phenotype
        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            //AddPhenotypeSB.Begin();
            setPhenotypeState(1);
            //YNSwitch.Margin = new Thickness(0, 0, 0, 0);
            PhenotypeManager.getSharedPhenotypeManager().addPhenotype(new Phenotype(phenotypeId, phenotypeName, 1), sourceType);
        }
        
        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            //DeletePhenotypeSB.Begin();   
            //YNSwitch.Margin = new Thickness(-100, 0, 0, 0);
            YNSwitch.Visibility = Visibility.Collapsed;
            DeleteBtn.Visibility = Visibility.Collapsed;
            //if(sourceType == SourceType.Speech)
            //    PhenotypeManager.getSharedPhenotypeManager().removeById(phenotypeId, SourceType.Speech);
            //else
            //    PhenotypeManager.getSharedPhenotypeManager().updatePhenoStateById(phenotypeId, -1, sourceType);
             PhenotypeManager.getSharedPhenotypeManager().removeById(phenotypeId, SourceType.Saved);
        }
        
        private void YSwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            setPhenotypeState(1);
            PhenotypeManager.getSharedPhenotypeManager().updatePhenoStateById(phenotypeId, 1, sourceType);

        }
        private void NSwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            setPhenotypeState(0);
            PhenotypeManager.getSharedPhenotypeManager().updatePhenoStateById(phenotypeId, 0, sourceType);
        }

        private void setPhenotypeState(int state)
        {
            if (state == -1)
            {
                NameCrossLine.Visibility = Visibility.Collapsed;
                phenotypeNameTextBlock.Text = phenotypeName;
                phenotypeNameTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                YNSwitch.Visibility = Visibility.Collapsed;
                if(sourceType == SourceType.Speech)
                    DeleteBtn.Visibility = Visibility.Visible;
                else
                    DeleteBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                YNSwitch.Visibility = Visibility.Visible;
                DeleteBtn.Visibility = Visibility.Visible;
                setYNSwitchColor(state);

                if (state == 1)
                {
                    NameCrossLine.Visibility = Visibility.Collapsed;
                    phenotypeNameTextBlock.Text = phenotypeName;
                    phenotypeNameTextBlock.Foreground = Application.Current.Resources["WORD_DARK"] as SolidColorBrush;
                }
                else
                {
                    NameCrossLine.Visibility = Visibility.Visible;
                    phenotypeNameTextBlock.Text = "NO " + phenotypeName;
                    phenotypeNameTextBlock.Foreground = new SolidColorBrush(Colors.LightCoral);
                }
            }
            
        }

        private void setYNSwitchColor(int state)
        {
            switch (state)
            {
                case 0:

                    NSwitchBtn.Background = new SolidColorBrush(Colors.PaleVioletRed);
                    NSwitchBtn.Foreground = new SolidColorBrush(Colors.White);
                    YSwitchBtn.Background = new SolidColorBrush(Colors.White);
                    YSwitchBtn.Foreground = new SolidColorBrush(Colors.Gray);
                    YNSwitch.BorderBrush = new SolidColorBrush(Colors.PaleVioletRed);

                    break;
                case 1:
                    NSwitchBtn.Background = new SolidColorBrush(Colors.White);
                    NSwitchBtn.Foreground = new SolidColorBrush(Colors.Gray);
                    YSwitchBtn.Background = new SolidColorBrush(Colors.CornflowerBlue);
                    YSwitchBtn.Foreground = new SolidColorBrush(Colors.White);
                    YNSwitch.BorderBrush = new SolidColorBrush(Colors.CornflowerBlue);
                    break;
                default:
                    break;
            }
        }

        private async void nameTextBlockTapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private async void DetailButton_Click(object sender, RoutedEventArgs e)
        {
            var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeDetailFlyout"];
            recogPhenoFlyout.ShowAt((Button)sender);
            Row pinfo = await PhenotypeManager.getSharedPhenotypeManager().getDetailById(phenotypeId);

            
            phenotypeDetailControl.setByPhenotypeInfo(pinfo);
            
        }
    }
}
