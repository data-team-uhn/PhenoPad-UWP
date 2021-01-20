using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using System.Reflection;
using System.Linq;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Data;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PhenoPad
{
    /// <summary>
    /// Some helper functions.
    /// </summary>
    static class HelperFunctions
    {
        /// <summary>
        /// Updates the size of inkCanvas
        /// </summary>
        public static void UpdateCanvasSize(FrameworkElement root, FrameworkElement output, FrameworkElement inkCanvas)
        {
            output.Width = root.ActualWidth;
            output.Height = root.ActualHeight / 2;
            inkCanvas.Width = root.ActualWidth;
            inkCanvas.Height = root.ActualHeight / 2;
        }
        /// <summary>
        /// Returns the current display size.
        /// </summary>
        public static Size GetCurrentDisplaySize()
        {
            var displayInformation = DisplayInformation.GetForCurrentView();
            TypeInfo t = typeof(DisplayInformation).GetTypeInfo();
            var props = t.DeclaredProperties.Where(x => x.Name.StartsWith("Screen") && x.Name.EndsWith("InRawPixels")).ToArray();
            var w = props.Where(x => x.Name.Contains("Width")).First().GetValue(displayInformation);
            var h = props.Where(x => x.Name.Contains("Height")).First().GetValue(displayInformation);
            var size = new Size(System.Convert.ToDouble(w), System.Convert.ToDouble(h));
            switch (displayInformation.CurrentOrientation)
            {
                case DisplayOrientations.Landscape:
                case DisplayOrientations.LandscapeFlipped:
                    size = new Size(Math.Max(size.Width, size.Height), Math.Min(size.Width, size.Height));
                    break;
                case DisplayOrientations.Portrait:
                case DisplayOrientations.PortraitFlipped:
                    size = new Size(Math.Min(size.Width, size.Height), Math.Max(size.Width, size.Height));
                    break;
            }
            return size;
        }
        /// <summary>
        /// Decodes Base 64 string source to bitmap image
        /// </summary>
        public static async Task<BitmapImage> Base64ToBitmapAsync(string source)
        {
            var byteArray = Convert.FromBase64String(source);
            BitmapImage bitmap = new BitmapImage();
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }
            return bitmap;
        }
    }
}
