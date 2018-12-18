using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Input.Inking;
using WobbrockLib;

namespace PhenoPad.Gestures
{
    public enum StrokeType
    {
        UnRecognized,
        VerticalLine,
        HorizontalLine,
        Zigzag,
        Dot,
        Rectangle
    };

    class GestureManager
    {
        private static GestureManager sharedGestureManager = null;
        private static StorageFolder ROOT_FOLDER = ApplicationData.Current.LocalFolder;
        private static Recognizer recognizer;
        private string GESTURE_PATH = ROOT_FOLDER.Path + "\\Gestures";

        public GestureManager()
        {            
        }

        public static GestureManager GetSharedGestureManager() {
            if (sharedGestureManager == null) {
                sharedGestureManager = new GestureManager();
                recognizer = new Recognizer();
                sharedGestureManager.InitializeRecognizer();
                return sharedGestureManager;
            }
            else              
                return sharedGestureManager;
        }

        private async void InitializeRecognizer() {
            Debug.WriteLine($"strokes path = {GESTURE_PATH}");
            await recognizer.LoadGestureFromPath(GESTURE_PATH);
        }

        private string PreCheckGesture(InkStroke s)
        {/// <summary>Pre-checks the stroke gesture before passing in to $1</summary>

            Rect bound = s.BoundingRect;
            if (bound.Width < 20 && bound.Height < 20)
                return "dot";
            else if (bound.Height / bound.Width > 3 && bound.Width < 20)
                return "vline";
            else if (bound.Width / bound.Height > 3)
            {
                List<InkPoint> pts = s.GetInkPoints().ToList();
                InkPoint pre_point = pts[0];
                int spike_count = 0;
                int direction = 1;
                for (int i = 0; i < pts.Count; i += 3)
                {
                    if (direction == 1 && pts[i].Position.X < pre_point.Position.X)
                    {
                        spike_count++;
                        direction = -1;
                    }
                    else if (direction == -1 && pts[i].Position.X > pre_point.Position.X)
                    {
                        spike_count++;
                        direction = 1;
                    }
                    pre_point = pts[i];
                }
                if (spike_count > 3)
                    return "zigzag";
                else
                    return "hline";
            }
            else
                return null;
        }

        private List<TimePointR> GetStrokePoints(InkStroke s)
        {/// <summary>Converts a stroke's InkPoint to TimePointF for gesture recognition</summary>

            List<TimePointR> points = new List<TimePointR>();
            foreach (InkPoint p in s.GetInkPoints())
                points.Add(new TimePointR((float)p.Position.X, (float)p.Position.Y, (long)p.Timestamp));
            return points;

        }

        public StrokeType GetGestureFromStroke(InkStroke s) {
            string is_line = null;
            is_line = PreCheckGesture(s);
            if (is_line == null)
            {
                List<TimePointR> pts = GetStrokePoints(s);
                NBestList result = recognizer.Recognize(pts, false);
                Debug.WriteLine($" $1 recognizer = {result.Name}, score = {result.Score}");
                var resultges = Regex.Replace(result.Name, @"[\d-]", string.Empty);
                if (resultges == "zigzag")
                    return StrokeType.Zigzag;
                else if (resultges == "line")
                    return StrokeType.HorizontalLine;
                else if (resultges == "rectangle")
                    return StrokeType.Rectangle;
                else
                    return StrokeType.UnRecognized;
            }
            else {
                switch (is_line)
                {
                    case ("vline"):
                        return StrokeType.VerticalLine;
                    case ("hline"):
                        return StrokeType.HorizontalLine;
                    case ("zigzag"):
                        return StrokeType.Zigzag;
                    case ("dot"):
                        return StrokeType.Dot;
                    default:
                        return StrokeType.UnRecognized;
                }
            }
        }

    }
}
