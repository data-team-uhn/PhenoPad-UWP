using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

using System.Diagnostics;

namespace PhenoPad.SpeechService
{
    public class WorkerInfo
    {
        public int worker_pid { get; set; }
        public int num_speakers { get; set; }
        public int full_diarization_timestamp { get; set; }
    }

    public class WorkerInfo_Wrapper
    {
        public WorkerInfo info { get; set; }
    }

    public class SpeechRESTAPI
    {
        private HttpClient client;

        private String api_start = "/config/api/worker_info/";
        private int rest_port = 5000;
        //return "http://localhost:1000/config/api/worker_info";

        /**
        *   URL contains port localhost:5000
        */
        public SpeechRESTAPI()
        {
        }

        /// <summary>
        /// TODO ...
        /// </summary>
        /// <param name="url"></param>
        public void setupClient(String url)
        {
            client = new HttpClient();
            // Update port # in the following line.
            client.BaseAddress = new Uri("http://" + url + ":" + this.rest_port.ToString());
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // We will never need to create or delete worker :D
        public Task<WorkerInfo> changeNumSpeakers(int pid, int numspeaker)
        {
            WorkerInfo worker = new WorkerInfo
            {
                worker_pid = pid,
                num_speakers = numspeaker,
            };

            Debug.WriteLine("Updating " +
                worker.worker_pid.ToString() + " to " + 
                worker.num_speakers.ToString());

            return this.UpdateProductAsync(worker);
        }

        // We will never need to create or delete worker :D
        public Task<WorkerInfo> getSpeakerInfo(int pid)
        {
            return this.GetProductAsync(pid);
        }


        private async Task<Uri> CreateProductAsync(WorkerInfo info)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(api_start, info);
            response.EnsureSuccessStatusCode();

            // return URI of the created resource.
            return response.Headers.Location;
        }

        private async Task<WorkerInfo> GetProductAsync(int pid)
        {
            WorkerInfo product = null;
            HttpResponseMessage response = await client.GetAsync(api_start + pid.ToString());
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(jsonString);
                product = JsonConvert.DeserializeObject<WorkerInfo_Wrapper>(jsonString).info;
            }
            return product;
        }

        private async Task<WorkerInfo> UpdateProductAsync(WorkerInfo product)
        {
            HttpResponseMessage response = await client.PutAsJsonAsync(api_start + product.worker_pid.ToString(), product);
            response.EnsureSuccessStatusCode();

            // Deserialize the updated product from the response body.
            var jsonString = await response.Content.ReadAsStringAsync();
            Console.WriteLine(jsonString);
            product = JsonConvert.DeserializeObject<WorkerInfo_Wrapper>(jsonString).info;
            return product;
        }

        private async Task<HttpStatusCode> DeleteProductAsync(int pid)
        {
            HttpResponseMessage response = await client.DeleteAsync(api_start + pid.ToString());
            return response.StatusCode;
        }

    }
}
