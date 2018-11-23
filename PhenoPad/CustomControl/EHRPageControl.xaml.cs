using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class EHRPageControl : UserControl
    {
        //By default sets the EHR template size to be A4 in pixels
        private double EHR_HEIGHT = 2200;
        private double EHR_WIDTH = 1700;
        private float LINE_HEIGHT = 50;


        public EHRPageControl() {
            this.InitializeComponent();
        }

        public EHRPageControl(StorageFile file)
        {
            this.InitializeComponent();
            //setting the text grid line format
            {
                var format = EHRTextBox.Document.GetDefaultParagraphFormat();
                EHRTextBox.FontSize = 32;
                //somehow with LINE_HEIGHT=50 we must adjust the format accordingly to 37.5f for a match
                format.SetLineSpacing(LineSpacingRule.Exactly, 37.5f);
                EHRTextBox.Document.SetDefaultParagraphFormat(format);
            }
            this.SetUpEHRFile(file);
            this.DrawBackgroundLines();
        }

        //=======================================
        //      METHODS BEGIN HERE
        //=======================================

        public void DrawBackgroundLines()
        {
            for (int i = 1; i <= backgroundCanvas.RenderSize.Height / LINE_HEIGHT; ++i)
            {
                var line = new Line()
                {
                    Stroke = new SolidColorBrush(Windows.UI.Colors.LightGray),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 5, 2 },
                    X1 = 0,
                    X2 = backgroundCanvas.RenderSize.Width,
                    Y1 = i * LINE_HEIGHT,
                    Y2 = i * LINE_HEIGHT
                };
                backgroundCanvas.Children.Add(line);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Draw background lines
            DrawBackgroundLines();
        }

        /// <summary>
        /// Takes the EHR text file and converts content onto template
        /// </summary>
        /// <param name="file"></param>
        public async void SetUpEHRFile(StorageFile file) {
            try {
                string text = await Windows.Storage.FileIO.ReadTextAsync(file);
                EHRTextBox.Document.SetText(TextSetOptions.None, text);
                Debug.WriteLine(text);
            }
            catch (Exception ex) {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
            }


        }





    }
}
