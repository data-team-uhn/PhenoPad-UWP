using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace PhenoPad.FileService
{
    public class ImageAndAnnotation
    {
        public string name { get; set; }
        public string notebookId { get; set; }
        public string pageId { get; set;  }
        public double canvasLeft { get; set; }
        public double canvasTop { get; set; }
        public double transX { get; set; }
        public double transY { get; set; }
        public double transScale { get; set; }
        public string date { get; set; }


    
        public ImageAndAnnotation()
        {
            
        }

        public ImageAndAnnotation(string name, string notebookId, string pageId, 
                                    double canvasLeft, double canvasTop,
                                    double transX, double transY, double transScale)
        {
            this.name = name;
            this.notebookId = notebookId;
            this.pageId = pageId;
            this.canvasLeft = canvasLeft;
            this.canvasTop = canvasTop;
            this.transX = transX;
            this.transY = transY;
            this.transScale = transScale;
            date = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
        }

    }
}
