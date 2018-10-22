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
using Windows.UI.Xaml;
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
        Dictionary<string, List<string>> abbrDict;
        List<HWRRecognizedText> lastServerRecog;
        //default Abbreviation detection IP Address
        Uri ipAddr = new Uri("http://104.41.139.54:8000/");


        /// <summary>
        /// Creates and initializes a new HWRManager instance.
        /// </summary>
        public HWRManager()
        {
            inkRecognizerContainer = new InkRecognizerContainer();
            sentence = new List<string>();
            alternatives = new List<List<string>>();
            newRequest = true;
            abbrDict = new Dictionary<string, List<string>>();
            lastServerRecog = new List<HWRRecognizedText>();
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

        public void setRequestType(bool type) {
            this.newRequest = type;
        }

        public Dictionary<string,List<string>> getDictionary() {
            return abbrDict;
        }

        public void setIPAddr(Uri newAddr) {
            ipAddr = newAddr; 
        }

        /// <summary>
        /// Gets the components in InkStrokeContainer and tries to recognize and return text, returns null if no text is recognized.
        /// </summary>
        public async Task<List<HWRRecognizedText>> OnRecognizeAsync(InkStrokeContainer container, InkRecognitionTarget target, bool server=false)
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
                        HWRRecognizedText rt = new HWRRecognizedText();
                        List<string> res = new List<string>();
                        res = new List<String>(r.GetTextCandidates());
                        rt.candidateList = res;
                        rt.selectedIndex = 0;
                        rt.selectedCandidate = res.ElementAt(0);
                        recogResults.Add(rt);
                    }
                    //triggers server side abbreviation detection
                    if (server && MainPage.Current.curPage.abbreviation_enabled)
                    {
                        string fullsentence = listToString(sentence);
                        HTTPRequest unprocessed = new HTTPRequest(fullsentence, this.alternatives, this.newRequest.ToString());
                        List<HWRRecognizedText> processed = await UpdateResultFromServer(unprocessed);
                        recogResults = processed == null ? recogResults : processed;
                        lastServerRecog = recogResults;

                    }

                    recogResults = CompareAndUpdateWithServer(recogResults);

                    return recogResults;
                }
                // if no text is recognized, return null
                return null;

            }
            catch (Exception e)
            {
                //MessageDialog dialog = new MessageDialog("No storke selected.");
                //var cmd = await dialog.ShowAsync();
                LogService.MetroLogger.getSharedLogger().Error("HWR error: " + e.Message);
                return null;
            }
        }

        public void clearCache() {
            lastServerRecog.Clear();
            abbrDict.Clear();
        }

        /// <summary>
        /// Compares the current HWR result with last server retrieved result and updates words
        /// </summary>
        private List<HWRRecognizedText> CompareAndUpdateWithServer(List<HWRRecognizedText> recogResults)
        {
            List<HWRRecognizedText> newRecog = new List<HWRRecognizedText>();
            //if nothing has been stored yet, just return original recog results
            if (lastServerRecog.Count == 0)
                return recogResults;

            int indexNew = 0;
            int indexServer = 0;

            while (indexNew < recogResults.Count && indexServer < lastServerRecog.Count)
            {//loop through indexes to compare word to word until one of the list reaches end of index
                string newResult = recogResults[indexNew].selectedCandidate;
                string preResult = lastServerRecog[indexServer].selectedCandidate;

                if (newResult == preResult)
                {//if the current word matches

                    if (abbrDict.ContainsKey(preResult.ToLower()))
                    {//the match is an abbreviation
                        newRecog.Add(lastServerRecog[indexServer]);
                        List<string> abbrs = abbrDict[preResult.ToLower()];
                        if (!abbrs.Contains(recogResults[indexNew + 1].selectedCandidate))
                        {//only add the extended form if it doesn't exist 
                            newRecog.Add(lastServerRecog[indexServer + 1]);
                            indexServer++;
                        }
                    }
                    else
                    {//normal matching word,just add from new recog result
                        newRecog.Add(recogResults[indexNew]);
                    }
                }
                else
                {//if no matches
                    if (abbrDict.ContainsKey(newResult.ToLower()))
                    {//if the non-match word is a known abbreviation, just re add its extended form
                        HWRRecognizedText newAbbr = new HWRRecognizedText();
                        newAbbr.candidateList = abbrDict[newResult.ToLower()];
                        newAbbr.selectedIndex = 0;
                        newAbbr.selectedCandidate = newAbbr.candidateList[0];

                        newRecog.Add(recogResults[indexNew]);
                        newRecog.Add(newAbbr);
                    }
                    else
                    {//normal non-match word, just replace with new recog result
                        newRecog.Add(recogResults[indexNew]);
                    }
                }
                indexServer++;
                indexNew++;
            }
            Debug.WriteLine($"current recog count={recogResults.Count},index={indexNew}");
            if (indexServer == lastServerRecog.Count)
            {//only care if there are new words to be added from new recog result
                for (int i = indexNew; i < recogResults.Count; i++) {
                    newRecog.Add(recogResults[i]);
                }
            }
            return newRecog;
        }

        public string listToString(List<string> lst)
        {
            string sentence = "";
            foreach (string word in lst)
            {
                sentence += word + " ";
            }
            //taking off last space char
            sentence = sentence.Substring(0, sentence.Length - 1);
            return sentence;
        }

        public async Task<List<HWRRecognizedText>> UpdateResultFromServer(HTTPRequest rawdata)
        {
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

            Uri requestUri = ipAddr;

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";
            try
            {
                string rawdatastr = JsonConvert.SerializeObject(rawdata);
                Debug.WriteLine("\n raw: \n"+ rawdatastr + "\n");

                HttpStringContent data = new HttpStringContent(rawdatastr, UnicodeEncoding.Utf8, "application/json");
                httpResponse = await httpClient.PostAsync(requestUri, data);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                Debug.WriteLine("\n res: \n" + httpResponseBody + "\n");

                HTTPResponse result = JsonConvert.DeserializeObject<HTTPResponse>(httpResponseBody);
                List<HWRRecognizedText> recogResults = processAlternative(result.alternatives);
                recogResults = processAbbr(result.abbreviations, recogResults);
                return recogResults;
            }
            catch (System.Net.Http.HttpRequestException he)
            {
                MainPage.Current.NotifyUser("HWR Server connection error", NotifyType.ErrorMessage, 3);
                LogService.MetroLogger.getSharedLogger().Error($"HWR connection:{he}+{he.Message}");
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e + e.Message);
            }
            return null;
        }


        public List<HWRRecognizedText> processAbbr(List<Abbreviation> abbrs, List<HWRRecognizedText> recog)
        {
            abbrDict.Clear();

            int offset = 0;
            List<HWRRecognizedText> recogAb = recog == null? new List<HWRRecognizedText>() : recog;
            foreach (Abbreviation ab in abbrs) {
                int index = Convert.ToInt32(ab.word_pos);
                HWRRecognizedText rt = new HWRRecognizedText();
                List<string> res = ab.abbr_list;
                rt.candidateList = res;
                rt.selectedIndex = 0;
                rt.selectedCandidate = res.ElementAt(0);
                recogAb.Insert(index + offset + 1, rt);
                //adding the abbreviation and its alternatives to a dictionary for later references.
                if (abbrDict.ContainsKey($"{sentence[index].ToLower()}"))
                {
                    abbrDict[$"{sentence[index].ToLower()}"] = ab.abbr_list;
                }
                else {
                    abbrDict.Add($"{sentence[index].ToLower()}", ab.abbr_list);
                }
                offset++;
            }
            return recogAb;
        }
        public List<HWRRecognizedText> processAlternative(List<List<string>> alter)
        {
            if (alter == null)
                return null;
            List<HWRRecognizedText> recogResults = new List<HWRRecognizedText>();

            foreach (List<string> lst in alter)
            {
                HWRRecognizedText rt = new HWRRecognizedText();
                List<string> res = lst;
                rt.candidateList = res;
                rt.selectedIndex = 0;
                rt.selectedCandidate = res.ElementAt(0);
                recogResults.Add(rt);
            }
            return recogResults;

        }

    }

    // ============================================= CLASSES FOR PARSING JSON BODY ==================================================
    public class HTTPRequest {
        public string sentence { get; set; }
        public List<List<string>> alternatives { get; set; }
        public string new_request { get; set; }

        public HTTPRequest() { }
        public HTTPRequest(string sentence, List<List<string>> alter, string rtype) {
            this.sentence = sentence;
            alternatives = alter;
            new_request = rtype;
        }
    }

    public class HTTPResponse {
        public List<Abbreviation> abbreviations { get; set; }
        public List<List<String>> alternatives { get; set; }
        public string result { get; set; }
        public Object annotations { get; set; }
    }

    public class Abbreviation
    {
        public int start { get; set; }
        public string word_pos { get; set; }
        public int end { get; set; }
        public List<string> abbr_list { get; set; }
    }
    public class Annotations
    {
        public int start { get; set; }
        public string score { get; set; }
        public int end { get; set; }
        public string hp_id { get; set; }
        public List<string> names { get; set; }
    }



}
