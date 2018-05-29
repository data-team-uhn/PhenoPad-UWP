using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;

namespace PhenoPad.HWRService
{
    public class HWRRecognizedText
    {
        public List<string> candidateList{ set; get; }
        public int selectedIndex
        {
            set;get;
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
    class HWRManager
    {
        public static HWRManager sharedHWRManager;


        InkRecognizerContainer inkRecognizerContainer = null;
        public HWRManager()
        {
            inkRecognizerContainer = new InkRecognizerContainer();
        }

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

        public async Task<List<HWRRecognizedText>> OnRecognizeAsync(InkStrokeContainer container, InkRecognitionTarget target)
        {
            try
            {
                var recognitionResults = await inkRecognizerContainer.RecognizeAsync(container, target);
                    
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
                Console.WriteLine("HWR error: " + e.Message);
                return null;
            }
        }
    }


}
