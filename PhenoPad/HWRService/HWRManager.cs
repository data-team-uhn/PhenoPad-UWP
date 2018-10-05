using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.Web.Http;

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

        //==========================================================================
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
        List <string> sentence;
        List<List<string>> alternatives;
        bool newRequest;

        /// <summary>
        /// Creates and initializes a new HWRManager instance.
        /// </summary>
        public HWRManager()
        {
            inkRecognizerContainer = new InkRecognizerContainer();
            sentence = new List<string>();
            alternatives = new List<List<string>>();
            newRequest = true;
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
                    sentence = new List<string>();
                    alternatives = new List<List<string>>();

                    // Display recognition result by words
                    foreach (var r in recognitionResults)
                    {
                        List<string> unprocessedRes = new List<string>(r.GetTextCandidates());
                        alternatives.Add(unprocessedRes);
                        //by default selects the most match candidate word 
                        sentence.Add(unprocessedRes.ElementAt(0));
                        //HWRRecognizedText rt = new HWRRecognizedText();
                        //List<string> res = new List<string>();
                        //res = new List<String>(r.GetTextCandidates());
                        //rt.candidateList = res;
                        //rt.selectedIndex = 0;
                        //rt.selectedCandidate = res.ElementAt(0);
                        //recogResults.Add(rt);
                    }

                    Alternatives package = new Alternatives(sentence, alternatives, newRequest);
                    await package.UpdateAlter();
                    foreach (List<string> alter in package.alternatives) {
                        HWRRecognizedText rt = new HWRRecognizedText();
                        List<string> res = alter;
                        rt.candidateList = res;
                        rt.selectedIndex = 0;
                        rt.selectedCandidate = res.ElementAt(0);
                        recogResults.Add(rt);
                    }


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

    public class Alternatives {
        public string sentence;
        public List<string> sentencelst;
        public List<List<string>> alternatives;
        public bool newRequest;

        //public Alternatives() { }

        public Alternatives(List<string> sentence, List<List<string>> alter, bool requestType) {
            this.sentencelst = sentence;
            this.sentence = "";
            foreach (string word in sentence) {
                this.sentence += word + " ";
            }
            //taking off last space char
            this.sentence = this.sentence.Substring(0, this.sentence.Length - 1);
            this.alternatives = alter;      
            this.newRequest = requestType;
        }

        public async Task UpdateAlter() {
            List<List<string>> processedAlter = new List<List<string>>();
            //Create an HTTP client object
            HttpClient httpClient = new HttpClient();
            //Add a user-agent header to the GET request. 
            var headers = httpClient.DefaultRequestHeaders;
            //The safe way to add a header value is to use the TryParseAdd method and verify the return value is true,
            //especially if the header value is coming from user input.
            string header = "ie";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }
            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            Uri requestUri = new Uri("http://phenopad.ccm.sickkids.ca:8000/");

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";
            try
            {
                HttpStringContent data = new HttpStringContent( parseJSON(this), UnicodeEncoding.Utf8, "application/json");
                httpResponse = await httpClient.PostAsync(requestUri, data);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(httpResponseBody);
                processAbbr(result["abbreviations"]);
                processAlternative(result["alternatives"]);
                processResult(result["result"]);
                processAnnotation(result["annotations"]);
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e + e.Message);
            }
        }

        public void processAbbr(object abbr)
        {

        }
        public void processAlternative(object alter)
        {
           List<List<string>> updated = new List<List<string>>();
           List<object> newAlter = new List<object>((IEnumerable<object>)alter);
            foreach (var alterlst in newAlter) {
                updated.Add(new List<string>((IEnumerable<string>)alterlst));
            }
            this.alternatives = updated;
        }
        public void processResult(object result)
        {
            this.sentence = (string)result;
        }
        public void processAnnotation(object anno)
        {
        }

        public string parseJSON(Alternatives alter)
        {
            string jsonfile = "";
            jsonfile += "{";
            jsonfile += $" \"sentence\": \"{this.sentence}\",";
            jsonfile += $" \"alternatives\": [";
            foreach (List<string> alter_list in this.alternatives)
            {
                string array = "[";
                foreach (string w in alter_list)
                    array += $"\"{w}\",";
                array = array.Substring(0, array.Length - 1);
                array += "],";
                jsonfile += array;
            }
            jsonfile = jsonfile.Substring(0, jsonfile.Length - 1);
            jsonfile += $"], \"new_request\":\"{this.newRequest}\"";
            jsonfile += "}";
            return (string)jsonfile;
        }




    }


}
