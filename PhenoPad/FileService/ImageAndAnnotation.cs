using PhenoPad.CustomControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

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
        public double zoomFactorX { get; set; }
        public double zoomFactorY { get; set; }
        public string date { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public double heightOrigin { get; set; }
        public double widthOrigin { get; set; }
        public bool inDock { get; set; }
        public double slideX { get; set; }
        public double slideY { get; set; }
        public int commentID { get; set; }
        public AnnotationType anno_type { get; set; }
        public string commentText;
        public double inkRatio { get; set; }


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
                                    double transX, double transY, ScaleTransform zoomFactor,
                                    double widthOrigin, double heightOrigin,
                                    double width, double height, bool inDock,
                                    int commentID, double slideX, double slideY, double inkRatio, AnnotationType type, string commentText)
        {
            this.name = name;
            this.notebookId = notebookId;
            this.pageId = pageId;
            this.canvasLeft = canvasLeft;
            this.canvasTop = canvasTop;
            this.transX = transX;
            this.transY = transY;
            this.widthOrigin = widthOrigin;//this is the original width when add-in is first created
            this.heightOrigin = heightOrigin;//this is the original height when add-in is first created
            this.height = height;
            this.width = width;
            this.zoomFactorX = zoomFactor.ScaleX;
            this.zoomFactorY = zoomFactor.ScaleY;
            this.inDock = inDock;
            this.commentID = commentID;
            this.slideX = slideX;
            this.slideY = slideY;
            this.inkRatio = inkRatio;
            this.anno_type = type;
            this.commentText = commentText;
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
        }

    }
}
