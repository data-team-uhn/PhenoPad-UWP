using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PhenoPad.HWRService;
using PhenoPad.Styles;
using Windows.UI.Input.Inking.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using PhenoPad.PhotoVideoService;
using Windows.UI.Notifications;
using Windows.ApplicationModel.Core;
using System.Threading;
using System.Collections;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using PhenoPad.FileService;
using System.Numerics;
using Windows.UI.Xaml.Hosting;
using Windows.Graphics.Display;
using PhenoPad.LogService;
using MetroLog;

namespace PhenoPad.FileService
{
    /// <summary>
    /// Represents any image / ink annotations within an add-in panel.
    /// </summary>
    public class ImageAndAnnotation
    {
        public string name { get; set; }
        public string notebookId { get; set; }
        public string pageId { get; set; }
        public double canvasLeft { get; set; }
        public double canvasTop { get; set; }
        public double transX { get; set; }
        public double transY { get; set; }
        public double transScale { get; set; }
        public string date { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public bool inDock { get; set; }
        public InkCanvas canvas;


        /// <summary>
        /// Empty construtor for serialization.
        /// </summary>
        public ImageAndAnnotation()
        {

        }
        /// <summary>
        /// Creates and initializes a new Image and Annotation instance with given parameters.
        /// </summary>
        public ImageAndAnnotation(string name, string notebookId, string pageId,
                                    double canvasLeft, double canvasTop,
                                    double transX, double transY, double transScale, double width, double height,bool inDock)
        {
            this.name = name;
            this.notebookId = notebookId;
            this.pageId = pageId;
            this.canvasLeft = canvasLeft;
            this.canvasTop = canvasTop;
            this.transX = transX;
            this.transY = transY;
            this.height = height;
            this.width = width;
            this.transScale = transScale;
            this.inDock = inDock;
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
        }

    }
}
