using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PhenoPad.LogService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.Web.Http;

namespace PhenoPad.PhenotypeService
{
    public enum SourceType { Notes, Speech, Suggested, Search, Saved, None};
    // position of phenotype control, whether inside notes or inside ListView
    public enum PresentPosition { Inline, List }; 
    public class PhenotypeManager
    {
        public static PhenotypeManager sharedPhenotypeManager;

        public ObservableCollection<Phenotype> savedPhenotypes;
        public ObservableCollection<Phenotype> suggestedPhenotypes;
        public ObservableCollection<Disease> predictedDiseases;
        public ObservableCollection<Phenotype> phenotypesInNote;
        public static string PHENOTYPEINFO_ADDR = "https://playground.phenotips.org/rest/vocabularies/hpo/";
        public static string DIFFERENTIAL_ADDR = "https://services.phenotips.org/get/PhenoTips/DiseasePredictService2?format=json&limit=15";
        //original https://playground.phenotips.org/get/PhenoTips/DiffDiagnosisService?format=json&limit=15
        public static string SUGGESTION_ADDR = "https://services.phenotips.org/get/PhenoTips/DiffDiagnosisService?format=json&limit=15";
        public ObservableCollection<Phenotype> phenotypesInSpeech;
        public ObservableCollection<Phenotype> phenotypesSpeechCandidates;

        //private MainPage rootPage = MainPage.Current;
        public ObservableCollection<Phenotype> phenotypesCandidates;

        DispatcherTimer autosavetimer;
        //===========================================METHODS===============================================
        public PhenotypeManager()
        {
            savedPhenotypes = new ObservableCollection<Phenotype>();
            suggestedPhenotypes = new ObservableCollection<Phenotype>();
            predictedDiseases = new ObservableCollection<Disease>();
            phenotypesInNote = new ObservableCollection<Phenotype>();
            phenotypesInSpeech = new ObservableCollection<Phenotype>();
            phenotypesCandidates = new ObservableCollection<Phenotype>();
            autosavetimer = new DispatcherTimer();
            autosavetimer.Tick += autosaver_tick;
            //setting autosave phonetype interval to be about 0.1 seconds
            autosavetimer.Interval = TimeSpan.FromSeconds(0.1);
        }
        public static PhenotypeManager getSharedPhenotypeManager()
        {
            if (sharedPhenotypeManager == null)
            {
                sharedPhenotypeManager = new PhenotypeManager();
                return sharedPhenotypeManager;
            }
            else
            {
                return sharedPhenotypeManager;
            }
        }

        public void clearCache()
        {
            savedPhenotypes.Clear();
            suggestedPhenotypes.Clear();
            predictedDiseases.Clear();
            phenotypesInNote.Clear();
            phenotypesInSpeech.Clear();
            phenotypesCandidates.Clear();
            //rootPage = MainPage.Current;
        }

        public int ShowPhenoCandAtPage(int pgId) {
            var curPageCand = phenotypesCandidates.Where(x => x.pageSource == pgId).OrderBy(x=>x.state).ToList();
            if (curPageCand == null)
                return 0;
            foreach (var p in curPageCand) {
                Phenotype pp = p.Clone();
                phenotypesCandidates.Remove(p);
                phenotypesCandidates.Insert(0, pp);

                //addPhenotypeCandidate(p, p.sourceType);
            }
            return curPageCand.Count;
        }

        public async void autosaver_tick(object sender = null, object e = null) {
            await MainPage.Current.AutoSavePhenotypes();
            autosavetimer.Stop();
        }

        // return # of added phenotypes
        public int CountSavedPhenotypes()
        {
            if (savedPhenotypes == null)
            {
                return 0;
            }
            return savedPhenotypes.Count;
        }
        public Phenotype GetSavedPhenotypeByIndex(int idx)
        {
            if (savedPhenotypes == null || idx < 0 || idx >= savedPhenotypes.Count)
                return null;
            return savedPhenotypes.ElementAt(idx);
        }
        public Phenotype GetSuggestedPhenotypeByIndex(int idx)
        {
            if (suggestedPhenotypes == null || idx < 0 || idx >= suggestedPhenotypes.Count)
                return null;
            return suggestedPhenotypes.ElementAt(idx);
        }
        public Disease GetPredictedDiseaseByIndex(int idx)
        {
            if (predictedDiseases == null || idx < 0 || idx >= predictedDiseases.Count)
                return null;
            return predictedDiseases.ElementAt(idx);
        }
        public bool checkIfSaved(Phenotype pheno)
        {
            if (savedPhenotypes.Where(x => x == pheno).FirstOrDefault() == null)
                return false;
            return true;
        }

        public void addPhenotypeInSpeech(List<Phenotype> phenos)
        {
            foreach (var p in phenos)
            {
                if (phenotypesInSpeech.Where(x => x == p).FirstOrDefault() == null)
                {
                    phenotypesInSpeech.Add(p);
                }
                addPhenotypeCandidate(p, SourceType.Speech);
            }
            autosavetimer.Start();
        }

        public async void addPhenotypeCandidate(Phenotype pheno, SourceType from)
        {//addes a phenotype tag to the candidate list UI

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,() =>
            {
                Phenotype pp = pheno.Clone();
                Phenotype temp = null;
                Phenotype tempInCand = phenotypesCandidates.Where(x => x == pheno).FirstOrDefault();

                temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
                if (temp != null)
                    pp.state = temp.state;
                else
                    pp.state = -1;


                if (from == SourceType.Speech)
                {
                    temp = phenotypesInSpeech.Where(x => x == pheno).FirstOrDefault();
                    if (temp != null)
                        temp.state = pheno.state;
                }


                //phenotype is not saved and not in candidate list
                if (temp == null && tempInCand == null)
                    phenotypesCandidates.Insert(0, pp);
                //phenotype is in candidate list, only reinserts it if the ordering is really behind
                else if (tempInCand != null && phenotypesCandidates.IndexOf(tempInCand) > 5) {
                    phenotypesCandidates.Remove(pheno);
                    phenotypesCandidates.Insert(0, pp);
                }

                if (!MainPage.Current.CandidateIsOpened())
                    MainPage.Current.OpenCandidate();
                return;

            });

        }

        public void addPhenotype(Phenotype pheno, SourceType from)
        {
            if (pheno == null || savedPhenotypes.Where(x => x == pheno).FirstOrDefault() != null)
                return;

            pheno.sourceType = from;
            //OperationLogger.getOpLogger().Log(OperationType.Phenotype, from.ToString(), "added", pheno.name);

            Phenotype temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp == null)
            {
                //savedPhenotypes.Add(pheno);
                savedPhenotypes.Insert(0, pheno);
                MainPage.Current.NotifyUser(pheno.name + " is added.", NotifyType.StatusMessage, 2);
            }


            if(from == SourceType.Notes)
            {
                temp = phenotypesInNote.Where(x => x == pheno).FirstOrDefault();
                if (temp == null)
                    phenotypesInNote.Add(pheno);
                else
                    temp.state = 1;
            }
            if (from == SourceType.Speech)
            {
                temp = phenotypesInSpeech.Where(x => x == pheno).FirstOrDefault();
                if (temp == null)
                    phenotypesInNote.Add(pheno);
                else
                    temp.state = pheno.state;
            }

            temp = phenotypesCandidates.Where(x => x == pheno).FirstOrDefault();
            int ind = phenotypesCandidates.IndexOf(temp);
            if (temp != null)
            {
                temp.state = 1;
                Phenotype pp = temp.Clone();
                pp.sourceType = SourceType.Suggested;
                phenotypesCandidates.Remove(temp);
                phenotypesCandidates.Insert(ind, pp);
            }
            

            updateSuggestionAndDifferential();
            updatePhenoStateById(pheno.hpId, 1, pheno.sourceType);
        }

        public int getStateByHpid(string hpid)
        {
            var temp = savedPhenotypes.Where(x => x.hpId == hpid).FirstOrDefault();
            if (temp == null)
                return -1;
            return temp.state;
        }

        public void addPhenotypesFromFile(List<Phenotype> phenos)
        {
            foreach(var pheno in phenos)
            {
                var from = pheno.sourceType;   
                
                Phenotype temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
                if (temp == null)
                {
                    savedPhenotypes.Insert(0, pheno);   
                }

                if (from == SourceType.Notes)
                {
                    phenotypesInNote.Add(pheno);
                }
                if (from == SourceType.Speech)
                {
                    phenotypesInSpeech.Add(pheno);
                   
                }
            }
            MainPage.Current.NotifyUser("Saved phenotypes are loaded.", NotifyType.StatusMessage, 1);
            updateSuggestionAndDifferential();
        }

        public void addPhenotypeCandidateFromFile(List<Phenotype> phenos) {
            foreach (Phenotype p in phenos) {
                //addPhenotypeCandidate(p, p.sourceType);
                Phenotype pp = p.Clone();
                //only insert at front if it is a saved phenotype with valid state
                if (p.state == -1)
                    phenotypesCandidates.Add(pp);
                else
                    phenotypesCandidates.Insert(0, pp);
            }
            if (phenos.Count > 0) {
                MainPage.Current.NotifyUser("Phenotype candidates are loaded.", NotifyType.StatusMessage, 1);
                MainPage.Current.OpenCandidate();
                updateSuggestionAndDifferential();
            }
        }

        public async void updateSuggestionAndDifferential()
        {
            List<Phenotype> sp = await giveSuggestions();
            suggestedPhenotypes.Clear();
            if (sp != null)
            {
                foreach (var p in sp)
                {
                    suggestedPhenotypes.Add(p);
                }
            }
            

            List<Disease> dis = await predictDisease();
            predictedDiseases.Clear();
            if (dis != null)
            { 
                foreach (var d in dis)
                {
                    predictedDiseases.Add(d);
                }
            }
            autosavetimer.Start();
        }

        public bool deletePhenotypeByIndex(int idx)
        {
            if (savedPhenotypes == null || idx < 0 || idx >= savedPhenotypes.Count)
                return false;
            
            savedPhenotypes.RemoveAt(idx);
            autosavetimer.Start();
            return true;
        }

        public bool deletePhenotype(Phenotype pheno)
        {
            Phenotype temp = suggestedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = -1;
            temp = phenotypesInNote.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = -1;
            temp = phenotypesInSpeech.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = -1;
            temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp != null) {
                MainPage.Current.NotifyUser(temp.name + " is deleted.", NotifyType.StatusMessage, 2);
                return savedPhenotypes.Remove(temp);
            }
            temp = phenotypesCandidates.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
            {
                temp.state = -1;
                Phenotype pp = temp.Clone();
                pp.sourceType = SourceType.Suggested;
                phenotypesCandidates.Remove(temp);
                phenotypesCandidates.Insert(0, pp);
            }
            autosavetimer.Start();
            return false;
        }

        public void updatePhenotypeAsync(Phenotype pheno)
        {
            Phenotype temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = pheno.state;
            temp = suggestedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = pheno.state;
            temp = phenotypesInNote.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = pheno.state;
            temp = phenotypesInSpeech.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = pheno.state;
            temp = phenotypesCandidates.Where(x => x == pheno).FirstOrDefault();
            if (temp != null)
                temp.state = pheno.state;

            //autosavetimer.Start();
        }

        public void removeByIdAsync(string pid, SourceType type)
        {
            Phenotype temp = savedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            Phenotype target = temp;
            if (temp != null)
            {
                target = temp;
                savedPhenotypes.Remove(temp);
            }

            temp = phenotypesInSpeech.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                phenotypesInSpeech.Remove(temp);
            }

            temp = phenotypesInNote.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                phenotypesInNote.Remove(temp);
                target = temp;
            }

            temp = phenotypesCandidates.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                if (type == SourceType.Suggested)
                {
                    phenotypesCandidates.Remove(temp);
                }
                else
                {
                    Phenotype pp = temp.Clone();
                    pp.state = -1;
                    int ind = phenotypesCandidates.IndexOf(temp);
                    phenotypesCandidates.Remove(temp);
                    //phenotypesCandidates.Insert(ind, pp);
                }
            }
            //target is null only when deleting a phenotype with state -1 in the curline recognition bar
            if (target != null)
                //OperationLogger.getOpLogger().Log(OperationType.Phenotype, type.ToString(), "removed", target.name);

            autosavetimer.Start();

        }


        public void updatePhenoStateById(string pid, int state, SourceType type)
        {
            int ind = -1;
            Phenotype temp = savedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            Phenotype target = temp;
            if (temp != null)
            {
                temp.state = state;
                target = temp;
               // if (type != SourceType.Saved)
                {
                    Phenotype pp = temp.Clone();
                    ind = savedPhenotypes.IndexOf(temp);
                    savedPhenotypes.Remove(temp);
                    //savedPhenotypes.Add(pp);
                    savedPhenotypes.Insert(ind, pp);
                }
            }
            temp = suggestedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                temp.state = state;
                //if (type != SourceType.Suggested)
                {
                    Phenotype pp = temp.Clone();
                    ind = suggestedPhenotypes.IndexOf(temp);
                    suggestedPhenotypes.Remove(temp);
                    suggestedPhenotypes.Insert(ind, pp);
                }
            }
            temp = phenotypesInNote.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                temp.state = state;
                //if (type != SourceType.Notes)
                {
                    Phenotype pp = temp.Clone();
                    ind = phenotypesInNote.IndexOf(temp);
                    phenotypesInNote.Remove(temp);
                    phenotypesInNote.Insert(ind, pp);
                    //MainPage.Current.curPage.updatePhenotypeLine(pp, ind);
                }
            }
            temp = phenotypesInSpeech.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                temp.state = state;
               // if (type != SourceType.Speech)
                {
                    Phenotype pp = temp.Clone();
                    ind = phenotypesInSpeech.IndexOf(temp);
                    phenotypesInSpeech.Remove(temp);
                    phenotypesInSpeech.Insert(ind, pp);
                }
            }

            temp = phenotypesCandidates.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                target = temp;
                temp.state = state;
                Phenotype pp = temp.Clone();
                pp.sourceType = SourceType.Suggested;
                ind = phenotypesCandidates.IndexOf(temp);
                phenotypesCandidates.Remove(temp);
                phenotypesCandidates.Insert(ind, pp);
            }
            switch (state)
            {
                case 1:
                    //OperationLogger.getOpLogger().Log(OperationType.Phenotype, type.ToString(), "Y", target.name);
                    break;
                case 0:
                    //OperationLogger.getOpLogger().Log(OperationType.Phenotype, type.ToString(), "N", target.name);
                    break;
                case -1:
                    //shouldn't reach this case but added checker just in case
                    Debug.WriteLine("status set to -1 phenotype");
                    break;
            }
            updateSuggestionAndDifferential();


        }

        public void updatePhenoStateByIdFromCandidate(string pid, int state, SourceType type)
        {
            Debug.WriteLine("***update by id from candidate is called ***");

            int ind = -1;
            Phenotype temp = savedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                
                    Phenotype pp = temp.Clone();
                    ind = savedPhenotypes.IndexOf(temp);
                    savedPhenotypes.Remove(temp);
                    //savedPhenotypes.Add(pp);
                    savedPhenotypes.Insert(ind, pp);
                
            }
            temp = suggestedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                
                    Phenotype pp = temp.Clone();
                    ind = suggestedPhenotypes.IndexOf(temp);
                    suggestedPhenotypes.Remove(temp);
                    suggestedPhenotypes.Insert(ind, pp);
                
            }
            temp = phenotypesInNote.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
               
                    Phenotype pp = temp.Clone();
                    ind = phenotypesInNote.IndexOf(temp);
                    phenotypesInNote.Remove(temp);
                    phenotypesInNote.Insert(ind, pp);
                
            }
            temp = phenotypesInSpeech.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
               
                    Phenotype pp = temp.Clone();
                    ind = phenotypesInSpeech.IndexOf(temp);
                    phenotypesInSpeech.Remove(temp);
                    phenotypesInSpeech.Insert(ind, pp);
                
            }
            updateSuggestionAndDifferential();
        }

        //https://playground.phenotips.org/rest/vocabularies/hpo/suggest?input=qwe
        public async Task<List<Phenotype>> searchPhenotypeByPhenotipsAsync(string str)
        {
            //Create an HTTP client object
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();

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

            Uri requestUri = new Uri("https://playground.phenotips.org/rest/vocabularies/hpo/suggest?input="+str);

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                httpResponseBody = httpResponseBody.Replace("_", "-");
                var result = JsonConvert.DeserializeObject<RootObject>(httpResponseBody);
                List<Phenotype> phenotypes = new List<Phenotype>();
                foreach (var row in result.rows)
                {
                    Phenotype pheno = new Phenotype(row);
                    pheno.pageSource = MainPage.Current.curPageIndex;
                    Phenotype temp = savedPhenotypes.Where(x => x.hpId == pheno.hpId).FirstOrDefault();
                    if (temp != null)
                    {
                        pheno.state = temp.state;
                    }
                    phenotypes.Add(pheno);
                }
                return phenotypes;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
                MainPage.Current.NotifyUser("Error: failed to parse results from PhenoTips", NotifyType.ErrorMessage, 2);
                MetroLogger.getSharedLogger().Error("Failed to search phenotypes by PhenoTips, " + httpResponseBody);
            }
            return null;
        }


        public async Task<Dictionary<string, Phenotype>> annotateByNCRAsync(string str)
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

            Uri requestUri = new Uri("https://ncr.ccm.sickkids.ca/curr/annotate/?text=" + str);

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<NCRResult>(httpResponseBody);
                Dictionary<string, Phenotype> returnResult = new Dictionary<string, Phenotype>();
                foreach (var res in result.matches)
                {
                    var keystr = str.Substring(res.start, res.end - res.start);
                    if (!returnResult.ContainsKey(keystr))
                    {
                        Phenotype pp = new Phenotype(res);
                        if (MainPage.Current != null)
                            pp.pageSource = MainPage.Current.curPageIndex;
                        returnResult.Add(keystr, pp);
                    }
                }
                return returnResult;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
                MetroLogger.getSharedLogger().Error("Failed to annotate by NCR, " + httpResponseBody);
            }
            return null;
        }

        public async Task<List<Phenotype>> giveSuggestions()
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

            header = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            var urlstr = SUGGESTION_ADDR;
            /**
            foreach (var p in savedPhenotypes)
            {
                urlstr += "&symptom=" + p.hpId;
            }**/
            Uri requestUri = new Uri(urlstr);

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                List<Phenotype> phenotypes = new List<Phenotype>();
                //Send the GET request
                var postData = new List<KeyValuePair<string, string>>();
                foreach (var p in savedPhenotypes)
                {
                    if (p.state == 1 && p.hpId != null)
                    {
                        postData.Add(new KeyValuePair<string, string>("symptom", p.hpId));
                    }
                }
                if (postData.Count == 0) {
                    return phenotypes;
                }


                var formContent = new HttpFormUrlEncodedContent(postData);
                httpResponse = await httpClient.PostAsync(requestUri, formContent);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
               
                var result = JsonConvert.DeserializeObject<List<SuggestPhenotype>>(httpResponseBody);
                

                foreach (var p in result)
                {
                    Phenotype pheno = new Phenotype(p);
                    pheno.pageSource = MainPage.Current.curPageIndex;
                    phenotypes.Add(pheno);
                }
                return phenotypes;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
                LogService.MetroLogger.getSharedLogger().Error("Failed to give suggestion, " + httpResponseBody);
            }
            return null;
        }

        public async Task<List<Disease>> predictDisease()
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

            header = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            var urlstr = DIFFERENTIAL_ADDR;
            Uri requestUri = new Uri(urlstr);

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                var postData = new List<KeyValuePair<string, string>>();
                foreach (var p in savedPhenotypes)
                {
                    if(p.state == 1)
                        postData.Add(new KeyValuePair<string, string>("symptom", p.hpId));
                }

                var formContent = new HttpFormUrlEncodedContent(postData);
                httpResponse = await httpClient.PostAsync(requestUri, formContent);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                //Debug.WriteLine($"differential diagnosis: code={httpResponse.StatusCode}, Body = {httpResponseBody}" );
                var result = JsonConvert.DeserializeObject<List<Disease>>(httpResponseBody);

                return result;
            }
            catch (Exception ex)
            {
                string errormsg = " Message: " + ex.Message + " Body: " + httpResponseBody;
                MetroLogger.getSharedLogger().Error("Failed to give differential diagnosis, " + errormsg);
            }
            return null;
        }

        public async Task<Row> getDetailById(string id)
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

            Uri requestUri = new Uri(PHENOTYPEINFO_ADDR + id);

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Row>(httpResponseBody);
                return result;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
                LogService.MetroLogger.getSharedLogger().Error("Failed to fetch phenotype information, " + httpResponseBody);
            }
            return null;
        }

        public void AddFakePhenotypesInSpeech()
        {
            foreach (var pheno in savedPhenotypes)
            {
                phenotypesInSpeech.Add(pheno);
            }
        }
       
    }
}
