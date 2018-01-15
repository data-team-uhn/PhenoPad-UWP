using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace PhenoPad.PhenotypeService
{
    public enum SourceType { Notes, Speech, Suggested, Search, Saved, None};

    public class PhenotypeManager
    {
        public static PhenotypeManager sharedPhenotypeManager;

        public ObservableCollection<Phenotype> savedPhenotypes { get; }
        public ObservableCollection<Phenotype> suggestedPhenotypes { get; }
        public ObservableCollection<Disease> predictedDiseases;
        public ObservableCollection<Phenotype> phenotypesInNote;
        public ObservableCollection<Phenotype> phenotypesInSpeech;

        public ObservableCollection<Phenotype> phenotypesCandidates;

        public PhenotypeManager()
        {
            savedPhenotypes = new ObservableCollection<Phenotype>();
            suggestedPhenotypes = new ObservableCollection<Phenotype>();
            predictedDiseases = new ObservableCollection<Disease>();
            phenotypesInNote = new ObservableCollection<Phenotype>();
            phenotypesInSpeech = new ObservableCollection<Phenotype>();
            phenotypesCandidates = new ObservableCollection<Phenotype>();
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
            }
        }
        public void addPhenotypeCandidate(Phenotype pheno, SourceType from)
        {
            Phenotype p = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (p != null)
            {
                Phenotype temp = phenotypesCandidates.Where(x => x == p).FirstOrDefault();
                phenotypesCandidates.Remove(temp);
                phenotypesCandidates.Insert(0, p);
            }
            else
            {
                Phenotype temp = phenotypesCandidates.Where(x => x == pheno).FirstOrDefault();
                phenotypesCandidates.Remove(temp);
                phenotypesCandidates.Insert(0, pheno);

            }
            return;
        }
        public async void addPhenotype(Phenotype pheno, SourceType from)
        {
            if (pheno == null || savedPhenotypes.Where(x => x == pheno).FirstOrDefault() != null)
                return;

            Phenotype temp = savedPhenotypes.Where(x => x == pheno).FirstOrDefault();
            if (temp == null)
                savedPhenotypes.Add(pheno);

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

            List<Phenotype> sp = await giveSuggestions();
            suggestedPhenotypes.Clear();
            foreach (var p in sp)
            {
                suggestedPhenotypes.Add(p);
            }

            List<Disease> dis = await predictDisease();
            predictedDiseases.Clear();
            foreach (var d in dis)
            {
                predictedDiseases.Add(d);
            }
        }

        public bool deletePhenotypeByIndex(int idx)
        {
            if (savedPhenotypes == null || idx < 0 || idx >= savedPhenotypes.Count)
                return false;
            savedPhenotypes.RemoveAt(idx);
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
            if (temp != null)
                return savedPhenotypes.Remove(temp);
            return false;
        }
        public void updatePhenotype(Phenotype pheno)
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
        }

        public void removeById(string pid, SourceType type)
        {
            if (type == SourceType.Saved)
            {
                Phenotype temp = savedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
                if (temp != null)
                    savedPhenotypes.Remove(temp);
            }
            if (type == SourceType.Speech)
            {
                Phenotype temp = phenotypesInSpeech.Where(x => x.hpId == pid).FirstOrDefault();
                if (temp != null)
                    phenotypesInSpeech.Remove(temp);
            }
            if (type == SourceType.Notes)
            {
                Phenotype temp = phenotypesInNote.Where(x => x.hpId == pid).FirstOrDefault();
                if (temp != null)
                    phenotypesInNote.Remove(temp);
            }
        }

        public void updatePhenoStateById(string pid, int state, SourceType type)
        {
            Phenotype temp = savedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                if (type != SourceType.Saved)
                {
                    Phenotype pp = temp.Clone();
                    savedPhenotypes.Remove(temp);
                    //savedPhenotypes.Add(pp);
                    savedPhenotypes.Insert(0, pp);
                }
            }
            temp = suggestedPhenotypes.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                if (type != SourceType.Suggested)
                {
                    Phenotype pp = temp.Clone();
                    suggestedPhenotypes.Remove(temp);
                    suggestedPhenotypes.Insert(0, pp);
                }
            }
            temp = phenotypesInNote.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                if (type != SourceType.Notes)
                {
                    Phenotype pp = temp.Clone();
                    phenotypesInNote.Remove(temp);
                    phenotypesInNote.Insert(0, pp);
                }
            }
            temp = phenotypesInSpeech.Where(x => x.hpId == pid).FirstOrDefault();
            if (temp != null)
            {
                temp.state = state;
                if (type != SourceType.Speech)
                {
                    Phenotype pp = temp.Clone();
                    phenotypesInSpeech.Remove(temp);
                    phenotypesInSpeech.Insert(0, pp);
                }
            }
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
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<RootObject>(httpResponseBody);
                List<Phenotype> phenotypes = new List<Phenotype>();
                foreach (var row in result.rows)
                {
                    Phenotype pheno = new Phenotype(row);
                    phenotypes.Add(pheno);
                }
                return phenotypes;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            return null;
        }

        public async Task<List<Phenotype>> annotateByNCRAsync(string str)
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

            Uri requestUri = new Uri("https://ncr.ccm.sickkids.ca/curr/annotate/?text=" + str);

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<NCRResult>(httpResponseBody);
                if (result == null)
                    return null;
                List<Phenotype> phenotypes = new List<Phenotype>();
                foreach (var row in result.matches)
                {
                    Phenotype pheno = new Phenotype(row);
                    phenotypes.Add(pheno);
                }
                return phenotypes;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            return null;
        }

        public async Task<List<Phenotype>> giveSuggestions()
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

            header = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            var urlstr = "https://playground.phenotips.org/get/PhenoTips/DiffDiagnosisService?format=json&limit=15";
            /**
            foreach (var p in savedPhenotypes)
            {
                urlstr += "&symptom=" + p.hpId;
            }**/
            Uri requestUri = new Uri(urlstr);

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                var postData = new List<KeyValuePair<string, string>>();
                foreach (var p in savedPhenotypes)
                {
                    postData.Add(new KeyValuePair<string, string>("symptom", p.hpId));
                }

                var formContent = new HttpFormUrlEncodedContent(postData);
                httpResponse = await httpClient.PostAsync(requestUri, formContent);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
               
                var result = JsonConvert.DeserializeObject<List<SuggestPhenotype>>(httpResponseBody);
                List<Phenotype> phenotypes = new List<Phenotype>();

                foreach (var p in result)
                {
                    Phenotype pheno = new Phenotype(p);
                    phenotypes.Add(pheno);
                }
                return phenotypes;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            return null;
        }

        public async Task<List<Disease>> predictDisease()
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

            header = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            var urlstr = "http://playground.phenotips.org/get/PhenoTips/DiseasePredictService2?format=json&limit=15";
            /**
            foreach (var p in savedPhenotypes)
            {
                urlstr += "&symptom=" + p.hpId;
            }**/
            Uri requestUri = new Uri(urlstr);

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                var postData = new List<KeyValuePair<string, string>>();
                foreach (var p in savedPhenotypes)
                {
                    postData.Add(new KeyValuePair<string, string>("symptom", p.hpId));
                }

                var formContent = new HttpFormUrlEncodedContent(postData);
                httpResponse = await httpClient.PostAsync(requestUri, formContent);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<List<Disease>>(httpResponseBody);

                return result;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            return null;
        }

        public async Task<PhenotypeInfo> getDetailById(string id)
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

            Uri requestUri = new Uri("https://playground.phenotips.org/rest/vocabularies/hpo/" + id);

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PhenotypeInfo>(httpResponseBody);
                return result;
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
            return null;
        }
    }
}
