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

namespace PhenoPad.CustomContorl
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
            if(sourceType == SourceType.Speech)
                PhenotypeManager.getSharedPhenotypeManager().removeById(phenotypeId, SourceType.Speech);
            else
                PhenotypeManager.getSharedPhenotypeManager().updatePhenoStateById(phenotypeId, -1, sourceType);

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
            }
            
        }

        private void setYNSwitchColor(int state)
        {
            NSwitchBtn.Background = MyColors.BUTTON_GRAY;
            YSwitchBtn.Background = MyColors.BUTTON_GRAY;
            switch (state)
            {
                case 0:
                    NSwitchBtn.Background = (SolidColorBrush)Application.Current.Resources["PHENOTYPE_BLUE"];
                    break;
                case 1:
                    YSwitchBtn.Background = (SolidColorBrush)Application.Current.Resources["PHENOTYPE_BLUE"];
                    break;
                default:
                    break;
            }
        }

        private async void nameTextBlockTapped(object sender, TappedRoutedEventArgs e)
        {
            PhenotypeInfo pinfo = await PhenotypeManager.getSharedPhenotypeManager().getDetailById(phenotypeId);
            
            var recogPhenoFlyout = (Flyout)this.Resources["PhenotypeDetailFlyout"];
            phenotypeDetailControl.setByPhenotypeInfo(pinfo);
            recogPhenoFlyout.ShowAt((TextBlock)sender);
        }
    }
}
