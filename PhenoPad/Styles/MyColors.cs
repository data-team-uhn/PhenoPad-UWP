using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace PhenoPad.Styles
{
    class MyColors
    {
        public MyColors() { }

        public static SolidColorBrush PHENOTYPE_BLUE = new SolidColorBrush(Color.FromArgb(255, 93, 179, 254));
        public static Color PHENOTYPE_BLUE_COLOR = Color.FromArgb(255, 93, 179, 254);
        public static Color PHENOTYPE_BLUE_COLOR_TRANS = Color.FromArgb(120, 93, 179, 254);
        public static Color TITLE_BAR_COLOR = Color.FromArgb(255, 61, 177, 255);
        public static Color TITLE_BAR_WHITE_COLOR = Color.FromArgb(255, 236, 240, 241);
        public static SolidColorBrush TITLE_BAR_COLOR_BRUSH = new SolidColorBrush(Color.FromArgb(255, 61, 177, 255));
        public static SolidColorBrush TITLE_BAR_WHITE_COLOR_BRUSH = new SolidColorBrush(Color.FromArgb(255, 228, 233, 236));
        public static SolidColorBrush BUTTON_GRAY = new SolidColorBrush(Color.FromArgb(255, 227,225,218));
        public static Color SELECTED_STROKE = Colors.OrangeRed;
        public static Color DEFUALT_STROKE = Colors.Black;
    }
}
