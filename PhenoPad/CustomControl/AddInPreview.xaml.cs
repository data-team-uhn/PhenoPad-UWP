using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using PhenoPad.CustomControl;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PhenoPad.CustomControl
{
    enum AlignDirection
    {
        WIDTH,
        HEIGHT
    }
    /// <summary>
    /// A class used for processing the preview icon of an add-in control
    /// </summary>
    public sealed partial class AddInPreview
    {


        private AddInControl addin { get; set; }

        private double height { get; set; }


        private bool hasImage;

        public AddInPreview() {
        }
      
        public AddInPreview(AddInControl addIn)
        {
            this.InitializeComponent();
            
            this.addin = addIn; //Binding the addin control to this preview instance
            this.strokeCanvas = addIn.inkCan;
            if (addin.hasImage) {
                this.hasImage = true;
                this.photo = new Image();
            }

        }

        public async void initiatePreview() {
            //NOTE: Don't have to worry about offset between strokes and photo here because
            //it will be taken care in addincontrol class

            //The default size preview is set to 200x200, any strokes/images that don't fit this ratio will be manually
            //set to a fill display mode

            //Loades strokes in a smaller zoom factor

            if (hasImage) {
                try
                {
                    var file = await FileManager.getSharedFileManager().GetNoteFileNotCreate(addin.notebookId, addin.pageId,
                        NoteFileType.Image, addin.name);

                    // Open a file stream for reading.
                    IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    // Read from file.
                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                        SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                        SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
                        await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
                        photo.Source = bitmapSource;
                    }
                    stream.Dispose();
                }
                catch (Exception e) {
                    LogService.MetroLogger.getSharedLogger().Error(e + e.Message);
                }

            }
            UpdateStrokes();
        }

        public void UpdateStrokes() {           
            Rect bounding = addin.inkCan.InkPresenter.StrokeContainer.BoundingRect;
            double ratio = bounding.Width / bounding.Height;
            InkCanvasScaleTransform.ScaleX = 200;
            InkCanvasScaleTransform.ScaleY = 200 * bounding.Height / bounding.Width; 
        }

    }
}
