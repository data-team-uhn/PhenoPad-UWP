using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.IO.Compression;

using Windows.Storage;
using Windows.Storage.Streams;

namespace PhenoPad.FileService
{
    public class FileServerClient
    {

        static string serverAddress = "54.166.237.238";
        static string serverPort = "8888";
        static string fileManagerAddress = "http://" + 
                                FileServerClient.serverAddress + 
                                ":" + 
                                FileServerClient.serverPort + 
                                "/file_manager";
        private static StorageFolder localFolder = ApplicationData.Current.LocalFolder;

        /*
         * Use parameter path when you know exactly the path to visit
         * Use parameter user-id when there is a known user id
         */
        public static async Task HTTPGet(string path = "1", string user_id = "12345")
        {
            using (HttpClient client = new HttpClient())
            {
                string url = FileServerClient.fileManagerAddress + "/get/" + path;
                client.DefaultRequestHeaders.Add("user-id", user_id);

                using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    string fileToWriteTo = Path.GetTempFileName();
                    Debug.WriteLine(fileToWriteTo);
                    using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    }

                    string extractPath = localFolder.Path.ToString();

                    try
                    {
                        ZipFile.ExtractToDirectory(fileToWriteTo, extractPath, true);
                    } catch (Exception e)
                    {
                        Debug.WriteLine("Unable to unzip file " + extractPath + " because " + e.Message);
                    }
                    
                }
            }
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static async Task HTTPPut(string path = "1", string user_id = "12345")
        {
            Debug.WriteLine("Zip all files first");

            string fileToWriteTo = Path.GetTempPath() + RandomString(10);

            /*using (ZipArchive archive = ZipFile.Open(fileToWriteTo, ZipArchiveMode.Create))
            {
                archive.CreateEntry(newFile, "NewEntry.txt");
            }*/
            try
            {
                ZipFile.CreateFromDirectory(localFolder.Path.ToString(), fileToWriteTo, CompressionLevel.Fastest, false);
                Debug.WriteLine("Zipping all notes to " + fileToWriteTo);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to create zip file because " + e.Message);
            }

            // int SIZE_GB = 1024 * 1024 * 1024;
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                //handler.MaxRequestContentBufferSize = SIZE_GB;
                using (HttpClient client = new HttpClient(handler))
                {
                    string url = FileServerClient.fileManagerAddress + "/put/" + path;
                    client.DefaultRequestHeaders.Add("user-id", user_id);

                    IStorageFile file = await StorageFile.GetFileFromPathAsync(fileToWriteTo);
                    IInputStream inputStream = await file.OpenAsync(FileAccessMode.Read);

                    MultipartFormDataContent multipartContent = new MultipartFormDataContent();

                    using (FileStream fs = File.Open(fileToWriteTo, FileMode.Open))
                    {
                        //fs.SetLength(SIZE_GB);
                        multipartContent.Add(new StreamContent(fs), fileToWriteTo);

                        HttpResponseMessage response = await client.PutAsync(url, multipartContent);
                        response.EnsureSuccessStatusCode();

                        string sd = response.Content.ReadAsStringAsync().Result;
                        Debug.WriteLine("Upload response is " + sd);
                    }
                }
            }
        }
    }
}
