using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
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
        //default Abbreviation detection IP Address
        Uri ipAddr = new Uri("http://137.135.117.253:8000/");

        public static HWRManager sharedHWRManager;
        InkRecognizerContainer inkRecognizerContainer = null;
        List <string> sentence;
        List<List<string>> alternatives;
        Dictionary<string, List<string>> abbrDict;
        SemaphoreSlim serverHWRSem;
        DispatcherTimer updateTimer;
        List<ServerHWRResult> updatePool;

        //=======================================END OF ATTRIBUTES====================================

        public HWRManager()
        {
            inkRecognizerContainer = new InkRecognizerContainer();
            sentence = new List<string>();
            alternatives = new List<List<string>>();
            abbrDict = new Dictionary<string, List<string>>();

            serverHWRSem = new SemaphoreSlim(1);
            updatePool = new List<ServerHWRResult>();
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromSeconds(1.5);
            updateTimer.Tick += UpdateTimer_Tick;
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

        public Dictionary<string,List<string>> getDictionary() {
            return abbrDict;
        }

        public void setIPAddr(Uri newAddr) {
            if (newAddr.IsAbsoluteUri)
                ipAddr = newAddr; 
        }

        public string getIPAddr() {
            return ipAddr.ToString();
        }

        public async Task<List<HWRRecognizedText>> OnRecognizeAsync(InkStrokeContainer container, InkRecognitionTarget target, int lineNum = -1, bool fromEHR = false)
        {
            try
            {
                var recognitionResults = await inkRecognizerContainer.RecognizeAsync(container, target);

                //if there are avilable recognition results, add to recognized text list    
                if ( recognitionResults != null && recognitionResults.Count > 0)
                {
                    //only reorder the wo
                    if (!fromEHR)
                        recognitionResults = recognitionResults.OrderBy(x => x.BoundingRect.X).ToList();

                    List<HWRRecognizedText> recogResults = new List<HWRRecognizedText>();
                    sentence = new List<string>();
                    alternatives = new List<List<string>>();

                    // Display recognition result by words
                    int ind = 0;
                    foreach (var r in recognitionResults)
                    {
                        List<string> parsedRes = StripSymbols(r.GetTextCandidates().ToList());
                        alternatives.Add(parsedRes);
                        //by default selects the most match candidate word 
                        sentence.Add(parsedRes.ElementAt(0));
                        HWRRecognizedText rt = new HWRRecognizedText();
                        rt.candidateList = parsedRes;
                        rt.selectedIndex = 0;
                        rt.selectedCandidate = parsedRes.ElementAt(0);
                        recogResults.Add(rt);
                        ind++;
                    }

                    //triggers server side abbreviation detection
                    if (MainPage.Current.abbreviation_enabled && !fromEHR)
                    {
                        TriggerServerRecognition(lineNum, sentence, alternatives, recogResults);
                    }

                    return recogResults;
                }
                // if no text is recognized, return null
                return null;
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
                return null;
            }
        }

        public string listToString(List<string> lst)
        {
            string sentence = "";
            foreach (string word in lst)
                sentence += word + " ";
            return sentence.Trim();
        }
        public List<String> StripSymbols(List<String> unprocessed) {

            List<String> parsed = new List<string>();
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            foreach (string word in unprocessed) {
                var newWord = rgx.Replace(word, "");
                if (newWord.Length > 0)                  
                    parsed.Add(newWord);
            }
            return parsed;
        }

        public async void TriggerServerRecognition(int lineNum, List<string> sentence, List<List<string>> alternatives, List<HWRRecognizedText>original ) {
            try
            {               
                List<HWRRecognizedText> recogResults = new List<HWRRecognizedText>();
                string fullsentence = listToString(sentence);
                ServerHWRResult final = new ServerHWRResult(lineNum, DateTime.Now);
                HTTPRequest request = new HTTPRequest(fullsentence, alternatives, "true");
                string response = await GetServerRecognition(request);

                if (response.Length > 0)
                {
                    List<HWRRecognizedText> processed = UpdateResultFromServer(response,original);
                    recogResults = processed.Count > 0 ? processed : original;                   
                    if (recogResults.Count > 0)
                    {
                        final.results = recogResults;
                        var lineItem = updatePool.Where(x => x.lineNum == lineNum).FirstOrDefault();

                        if (lineItem != null && lineItem.InitialRequestTime < final.InitialRequestTime)
                        {
                            updatePool[updatePool.IndexOf(lineItem)] = final;
                        }
                        else if (lineItem == null) {
                            updatePool.Add(final);
                        }

                        if (!updateTimer.IsEnabled)
                            updateTimer.Start();
                    }
                }
            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }
            //return null;
        }

        public async Task<string> GetServerRecognition(HTTPRequest rawdata) {
            try
            {
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

                string rawdatastr = JsonConvert.SerializeObject(rawdata);
                HttpStringContent data = new HttpStringContent(rawdatastr, UnicodeEncoding.Utf8, "application/json");
                //Debug.WriteLine(data);
                httpResponse = await httpClient.PostAsync(requestUri, data);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                //Debug.WriteLine(httpResponseBody);
                return httpResponseBody;

            }
            catch (System.Net.Http.HttpRequestException he)
            {
                LogService.MetroLogger.getSharedLogger().Error($"HWR connection: \n {he.Message}");
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error($"HTTP response: \n {e.Message}");
            }
            return "";
        }

        public List<HWRRecognizedText> UpdateResultFromServer(string httpResponse,List<HWRRecognizedText> original)
        {
            List<List<string>> processedAlter = new List<List<string>>();

            HTTPResponse result = JsonConvert.DeserializeObject<HTTPResponse>(httpResponse);
            sentence = result.result.Split(" ").ToList();

            List<HWRRecognizedText> recogResults = new List<HWRRecognizedText>();
            recogResults = processAlternative(result.alternatives);
            //if server don't have any alternatives, process abbreviation using microsoft's result
            if (recogResults == null || recogResults.Count == 0) 
                recogResults = processAbbr(result.abbreviations, original);
            //if server has updated alternatives, use server's result to process abbreviation
            else
                recogResults = processAbbr(result.abbreviations, recogResults);

            return recogResults;           
        }
        public List<HWRRecognizedText> processAbbr(List<Abbreviation> abbrs, List<HWRRecognizedText> original)
        {
            //05/28/2019 revised version of processAbbr, instead of adding both shortform and extended form, add abbr alternatives to candidate List of shortform and return updated list of HWRs
            List<HWRRecognizedText> recogAb = new List<HWRRecognizedText>();
            recogAb.AddRange(original);
            foreach (Abbreviation ab in abbrs)
            {
                //the word index that has abbreviations
                int index = Convert.ToInt32(ab.word_pos);
                HWRRecognizedText word = recogAb[index];
                List<string> res = ab.abbr_list;

                //replacing extended form with bracketed format
                for (int i = 0; i < res.Count; i++)
                {
                    res[i] = "(" + res[i] + ")";
                }
                word.candidateList.AddRange(res);
                word.selectedIndex = 0;
                word.selectedCandidate = res.ElementAt(0);

                //updates abbreviation dictionary
                if (abbrDict.ContainsKey($"{sentence[index].ToLower()}"))
                    abbrDict[$"{sentence[index].ToLower()}"] = ab.abbr_list;
                else
                    abbrDict.Add($"{sentence[index].ToLower()}", ab.abbr_list);
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
        private void UpdateTimer_Tick(object sender, object e)
        {
            updateTimer.Stop();
            //Abort this request if update is already in process
            if (serverHWRSem.CurrentCount == 0)
            {
                updateTimer.Start();
                return;
            }
            serverHWRSem.WaitAsync();
            try
            {
                lock (updatePool)
                {
                    foreach (var item in updatePool)
                    {
                        MainPage.Current.curPage.UpdateRecognitionFromServer(item.lineNum, item.results);
                    }
                    updatePool.Clear();
                }
            }
            catch (Exception ex)
            {
                LogService.MetroLogger.getSharedLogger().Error(ex.Message);
            }
            finally
            {
                serverHWRSem.Release();
            }
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
    //============================================== CLASS FOR SERVER RETURNED HWR ==================================================
    public class ServerHWRResult {
        public int lineNum;
        public DateTime InitialRequestTime;//the initial time set when Phenopad first sends the request to server
        public List<HWRRecognizedText> results; //the server's return result

        public ServerHWRResult(int lineNum, DateTime time) {
            this.lineNum = lineNum;
            InitialRequestTime = time;
            results = new List<HWRRecognizedText>();
        }
    }



}
