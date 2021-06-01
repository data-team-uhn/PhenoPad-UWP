using PhenoPad.PhenotypeService;
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
    public sealed partial class PhenotypeDetail : UserControl
    {
        public float TAG_WIDTH = 100;
        public PhenotypeDetail()
        {
            this.InitializeComponent();
            ProgressRingGrid.Visibility = Visibility.Visible ;
        }

        public void navigateTo(String hpid)
        {
            this.webView.Navigate(new Uri("https://hpo.jax.org/app/browse/term/" + hpid));
            ProgressRingGrid.Visibility = Visibility.Collapsed;
        }
    }
}
