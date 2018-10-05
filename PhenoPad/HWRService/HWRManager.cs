using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;

namespace PhenoPad.HWRService
{
    public class HWRRecognizedText
    {
        public List<string> candidateList { set; get; }
        public int selectedIndex
        {
            set; get;
        }
        public string selectedCandidate
        {
            set; get;
        }

        public HWRRecognizedText()
        {
            candidateList = new List<string>();
            selectedIndex = 0;
            selectedCandidate = "";
        }
    }
    /// <summary>
    /// Controller class for managing hand written recognitions.
    /// </summary>
    class HWRManager
    {
        public static HWRManager sharedHWRManager;

        InkRecognizerContainer inkRecognizerContainer = null;

        /// <summary>
        /// Creates and initializes a new HWRManager instance.
        /// </summary>
        public HWRManager()
        {
            inkRecognizerContainer = new InkRecognizerContainer();
        }
        /// <summary>
        /// Returns the shared HWRManager object, will create a new instance if no shared HWRManager is available.
        /// </summary>
        public static HWRManager getSharedHWRManager()
        {
            if (sharedHWRManager == null)
            {
                sharedHWRManager = new HWRManager();
                return sharedHWRManager;
            }
            else
            {
                return sharedHWRManager;
            }
        }
        /// <summary>
        /// Gets the components in InkStrokeContainer and tries to recognize and return text, returns null if no text is recognized.
        /// </summary>
        public async Task<List<HWRRecognizedText>> OnRecognizeAsync(InkStrokeContainer container, InkRecognitionTarget target)
        {
            try
            {
                var recognitionResults = await inkRecognizerContainer.RecognizeAsync(container, target);
                //if there are avilable recognition results, add to recognized text list    
                if (recognitionResults.Count > 0)
                {
                    List<HWRRecognizedText> recogResults = new List<HWRRecognizedText>();

                    // Display recognition result
                    foreach (var r in recognitionResults)
                    {
                        HWRRecognizedText rt = new HWRRecognizedText();
                        List<string> res = new List<string>();
                        res = new List<String>(r.GetTextCandidates());
                        rt.candidateList = res;
                        rt.selectedIndex = 0;
                        rt.selectedCandidate = res.ElementAt(0);
                        recogResults.Add(rt);
                        //str += " " + r.GetTextCandidates()[0];
                    }
                    //this.NotifyUser(str, NotifyType.StatusMessage);
                    //recognizedResultTextBlock.Text = "Recognized result: " + str;
                    return recogResults;
                }
                // if no text is recognized, return null
                else
                {
                    //rootPage.NotifyUser("No text recognized.", NotifyType.StatusMessage);
                    //MessageDialog dialog = new MessageDialog("No text recognized");
                    //var cmd = await dialog.ShowAsync();
                    return null;
                }
            }
            catch (System.Exception e)
            {
                //MessageDialog dialog = new MessageDialog("No storke selected.");
                //var cmd = await dialog.ShowAsync();
                Debug.WriteLine("HWR error: " + e.Message);
                return null;
            }
        }
    }


}
