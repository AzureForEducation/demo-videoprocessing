using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using VideoProcessing.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Runtime.Serialization.Json;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Auth;

namespace VideoProcessing.Services
{
    class MediaServices
    {
        private string _tenantDomain;
        private string _restApiUrl;
        private string _clientId;
        private string _clientSecret;
        public HttpClient _httpClient;
        public static CloudStorageAccount _destinationStorageAccount;

        public MediaServices(string tenantDomain, string restApiUrl, string clientId, string clientSecret, string storageConn)
        {
            _tenantDomain = tenantDomain;
            _restApiUrl = restApiUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;

            //Initiating the HttpClient that will communicate with AMS API
            _httpClient = new HttpClient { BaseAddress = new Uri(restApiUrl) };
            _httpClient.DefaultRequestHeaders.Add("x-ms-version", "2.15");
            _httpClient.DefaultRequestHeaders.Add("DataServiceVersion", "3.0");
            _httpClient.DefaultRequestHeaders.Add("MaxDataServiceVersion", "3.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json;odata=verbose");
        }

        public async Task InitializeAccessTokenAsync()
        {
            // Generate access token
            var body = $"resource={HttpUtility.UrlEncode("https://rest.media.azure.net")}&client_id={_clientId}&client_secret={HttpUtility.UrlEncode(_clientSecret)}&grant_type=client_credentials";
            var httpContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _httpClient.PostAsync($"https://login.microsoftonline.com/{_tenantDomain}/oauth2/token", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception();
            }

            var resultBody = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(resultBody);

            // set internal httpClient authorization headers to access token
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", obj["access_token"].ToString());
        }

        public async Task<Asset> GenerateAsset(string name, string storageAccountName)
        {
            var body = new
            {
                Name = name,
                Options = 0,
                StorageAccountName = storageAccountName
            };

            var bodyContent = JsonConvert.SerializeObject(body);
            HttpResponseMessage response = await _httpClient.PostAsync("Assets", new StringContent(bodyContent, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(responseContent);

            return new Asset
            {
                Id = obj["d"]["Id"].ToString(),
                Uri = obj["d"]["__metadata"]["uri"].ToString()
            };
        }

        public async Task<string> GenerateAccessPolicy(string name, int durationInMinutes, int permissions)
        {
            // create access policy
            var accessPolicyBody = new
            {
                Name = name,
                DurationInMinutes = durationInMinutes,
                Permissions = permissions
            };

            var bodyContent = JsonConvert.SerializeObject(accessPolicyBody);
            HttpResponseMessage accessPolicyResponse = await _httpClient.PostAsync("AccessPolicies", new StringContent(bodyContent, Encoding.UTF8, "application/json"));
            string responseContent = await accessPolicyResponse.Content.ReadAsStringAsync();

            var obj = JObject.Parse(responseContent);
            return obj["d"]["Id"].ToString();
        }

        public async Task<Locator> GenerateLocator(string accessPolicyId, string assetId, DateTime startTime, int type)
        {
            var body = new
            {
                AccessPolicyId = accessPolicyId,
                AssetId = assetId,
                StartTime = startTime,
                Type = type
            };

            var bodyContent = JsonConvert.SerializeObject(body);
            HttpResponseMessage response = await _httpClient.PostAsync("Locators", new StringContent(bodyContent, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(responseContent);
            return new Locator
            {
                Id = obj["d"]["Id"].ToString(),
                BaseUri = obj["d"]["BaseUri"].ToString(),
                ContentAccessComponent = obj["d"]["ContentAccessComponent"].ToString()
            };
        }

        public static async Task<CloudBlockBlob> MoveVideoToAssetLocator(CloudBlockBlob sourceBlob, Locator locator, string storageConn)
        {
            // Defining URIs
            string destinationContainer = $"{locator.BaseUri}";
            Uri uriDestinationContainer = new Uri(destinationContainer, UriKind.Absolute);
            CloudBlobContainer destContainer = new CloudBlobContainer(uriDestinationContainer);

            // Interacting with the blob
            string strgConn = storageConn;
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(strgConn);
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer sourceContainer = cloudBlobClient.GetContainerReference("incoming-videos");
            CloudBlobContainer targetContainer = cloudBlobClient.GetContainerReference(destContainer.Name);
            CloudBlockBlob srcBlob = sourceContainer.GetBlockBlobReference(sourceBlob.Name);
            CloudBlockBlob targetBlob = targetContainer.GetBlockBlobReference(sourceBlob.Name);
            await targetBlob.StartCopyAsync(srcBlob);

            // Remove source blob after copy is done.
            //sourceBlob.Delete();
            return targetBlob;
        }

        public async Task<string> GenerateFileInfo(string assetId)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"CreateFileInfos?assetid='{Uri.EscapeDataString(assetId)}'");
            string responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public async Task<string> GetMediaProcessorId(string mediaProcessorName)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("MediaProcessors");
            string responseContent = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(responseContent);
            JToken processor = obj["d"]["results"].Where(p => (string)p["Name"] == mediaProcessorName).First();
            return processor["Id"].ToString();
        }

        public async Task<JToken> CreateJob(string name, string inputAssetUri, string mediaProcessorId, string configuration)
        {
            var body = new
            {
                Name = name,
                InputMediaAssets = new[] {
                    new {
                        __metadata = new { uri = inputAssetUri }
                    }
                },
                Tasks = new[] {
                    new {
                        Configuration = configuration,
                        MediaProcessorId = mediaProcessorId,
                        TaskBody = "<?xml version=\"1.0\" encoding=\"utf-8\"?><taskBody><inputAsset>JobInputAsset(0)</inputAsset><outputAsset>JobOutputAsset(0)</outputAsset></taskBody>"
                    }
                }
            };

            var bodyContent = JsonConvert.SerializeObject(body);
            var stringContent = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;odata=verbose");
            HttpResponseMessage response = await _httpClient.PostAsync("Jobs", stringContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(responseContent);
            return obj["d"];
        }

        ////////////////////////////////////////////////////////////// NEW VERSION ////////////////////////////////////////////////////////////////////////////////////

        public static IJob SubmitEncodingJobWithNotificationEndPoint(CloudMediaContext _context, string mediaProcessorName, InitialSetupResult initialSetup, INotificationEndPoint _notificationEndPoint)
        {
            // Declare a new job.
            IJob job = _context.Jobs.Create($"Job_Encoding_{initialSetup.Video.VideoFileName}");

            //Create an encrypted asset and upload the mp4
            IAsset asset = LoadExistingAsset(initialSetup.Asset.Id, _context);

            // Get a media processor reference, and pass to it the name of the
            // processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName(mediaProcessorName, _context);

            // Create a task with the conversion details, using a configuration file.
            ITask task = job.Tasks.AddNew($"Job_Encoding_Task_{initialSetup.Video.VideoFileName}", processor, "Adaptive Streaming", Microsoft.WindowsAzure.MediaServices.Client.TaskOptions.None);

            // Specify the input asset to be encoded.
            task.InputAssets.Add(asset);

            // Add an output asset to contain the results of the job.
            task.OutputAssets.AddNew($"Output_Encoding_{initialSetup.Video.VideoFileName}", AssetCreationOptions.None);

            // Add a notification point to the job. You can add multiple notification points.  
            job.JobNotificationSubscriptions.AddNew(NotificationJobState.FinalStatesOnly, _notificationEndPoint);

            job.Submit();

            return job;
        }

        public static CloudQueue CreateQueue(string storageAccountConnectionString, string endPointAddress)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);

            // Create the queue client
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            // Retrieve a reference to a queue
            CloudQueue queue = queueClient.GetQueueReference(endPointAddress);

            // Create the queue if it doesn't already exist
            queue.CreateIfNotExists();

            return queue;
        }

        public static void WaitForJobToReachedFinishedState(string jobId, CloudQueue _queue, TraceWriter log)
        {
            int expectedState = (int)JobState.Finished;
            int timeOutInSeconds = 60000;

            bool jobReachedExpectedState = false;
            DateTime startTime = DateTime.Now;
            int jobState = -1;

            while (!jobReachedExpectedState)
            {
                // Specify how often you want to get messages from the queue.
                Thread.Sleep(TimeSpan.FromSeconds(10));

                foreach (var message in _queue.GetMessages(10))
                {
                    using (Stream stream = new MemoryStream(message.AsBytes))
                    {
                        DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings();
                        settings.UseSimpleDictionaryFormat = true;
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(EncodingJobMessage), settings);
                        EncodingJobMessage encodingJobMsg = (EncodingJobMessage)ser.ReadObject(stream);

                        log.Info("");

                        // Display the message information.
                        log.Info("EventType: {0}", encodingJobMsg.EventType);
                        log.Info("MessageVersion: {0}", encodingJobMsg.MessageVersion);
                        log.Info("ETag: {0}", encodingJobMsg.ETag);
                        log.Info("TimeStamp: {0}", encodingJobMsg.TimeStamp);
                        foreach (var property in encodingJobMsg.Properties)
                        {
                            log.Info($"    {property.Key}: {property.Value}");
                        }

                        // We are only interested in messages
                        // where EventType is "JobStateChange".
                        if (encodingJobMsg.EventType == "JobStateChange")
                        {
                            string JobId = (String)encodingJobMsg.Properties.Where(j => j.Key == "JobId").FirstOrDefault().Value;
                            if (JobId == jobId)
                            {
                                string oldJobStateStr = (String)encodingJobMsg.Properties.Where(j => j.Key == "OldState").FirstOrDefault().Value;
                                string newJobStateStr = (String)encodingJobMsg.Properties.Where(j => j.Key == "NewState").FirstOrDefault().Value;

                                JobState oldJobState = (JobState)Enum.Parse(typeof(JobState), oldJobStateStr);
                                JobState newJobState = (JobState)Enum.Parse(typeof(JobState), newJobStateStr);

                                if (newJobState == (JobState)expectedState)
                                {
                                    log.Info($"job with Id: {jobId} reached expected state: {newJobState}");
                                    jobReachedExpectedState = true;
                                    break;
                                }
                            }
                        }
                    }
                    // Delete the message after we've read it.
                    _queue.DeleteMessage(message);
                }

                // Wait until timeout
                TimeSpan timeDiff = DateTime.Now - startTime;
                bool timedOut = (timeDiff.TotalSeconds > timeOutInSeconds);
                if (timedOut)
                {
                    log.Info($"Timeout for checking job notification messages, latest found state ='{jobState}', wait time = {timeDiff.TotalSeconds} secs");

                    throw new TimeoutException();
                }
            }
        }

        public static async Task<IAsset> CreateAssetFromBlobAsync(CloudBlockBlob blob, string assetName, TraceWriter log, string _storageAccountName, string _storageAccountKey, CloudMediaContext _context)
        {
            //Get a reference to the storage account that is associated with the Media Services account
            StorageCredentials mediaServicesStorageCredentials = new StorageCredentials(_storageAccountName, _storageAccountKey);
            _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

            // Create a new asset
            var asset = _context.Assets.Create(blob.Name, AssetCreationOptions.None);
            log.Info($"Creating asset {asset.Name}...");

            // Creates access policy, locator and destination blob
            IAccessPolicy writePolicy = _context.AccessPolicies.Create("writePolicy", TimeSpan.FromHours(4), AccessPermissions.Write);
            ILocator destinationLocator = _context.Locators.CreateLocator(LocatorType.Sas, asset, writePolicy);
            CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

            // Get the destination asset container reference
            string destinationContainerName = (new Uri(destinationLocator.Path)).Segments[1];
            CloudBlobContainer assetContainer = destBlobStorage.GetContainerReference(destinationContainerName);

            try
            {
                assetContainer.CreateIfNotExists();
            }
            catch (Exception ex)
            {
                log.Error("ERROR:" + ex.Message);
            }

            log.Info("Done. Asset created.");

            // Get hold of the destination blob
            CloudBlockBlob destinationBlob = assetContainer.GetBlockBlobReference(blob.Name);

            // Copy Blob
            try
            {
                log.Info("Starting copying the video file into the asset blob...");

                using (var stream = await blob.OpenReadAsync())
                {
                    await destinationBlob.UploadFromStreamAsync(stream);
                }

                log.Info("Done. Copy complete.");

                var assetFile = asset.AssetFiles.Create(blob.Name);
                assetFile.ContentFileSize = blob.Properties.Length;
                //assetFile.MimeType = "video/mp4";
                assetFile.IsPrimary = true;
                assetFile.Update();
                asset.Update();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                log.Info(ex.StackTrace);
                log.Info("Copy Failed.");
                throw;
            }

            destinationLocator.Delete();
            writePolicy.Delete();

            return asset;
        }

        public static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName, CloudMediaContext _context)
        {
            var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
                ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        public static IAsset LoadExistingAsset(string AssetId, CloudMediaContext _context)
        {
            var matchingAssets = (from a in _context.Assets where a.Id.Equals(AssetId) select a);

            IAsset asset = null;
            foreach (IAsset ia in matchingAssets)
            {
                asset = ia;
            }

            return asset;
        }

        public static void DownloadAsset(IAsset asset, string outputDirectory, CloudMediaContext _context)
        {
            foreach (IAssetFile file in asset.AssetFiles)
            {
                file.Download(Path.Combine(outputDirectory, file.Name));
            }
        }

        public static string PublishAndBuildStreamingURLs(String jobID, CloudMediaContext _context)
        {
            string urlForClientStreaming;

            try
            {
                IJob job = _context.Jobs.Where(j => j.Id == jobID).FirstOrDefault();
                IAsset asset = job.OutputMediaAssets.FirstOrDefault();

                // Create a 30-day readonly access policy. 
                // You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
                IAccessPolicy policy = _context.AccessPolicies.Create($"{asset.Name}_Streaming_Policy", TimeSpan.FromDays(30), AccessPermissions.Read);

                // Create a locator to the streaming content on an origin. 
                ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset, policy, DateTime.UtcNow.AddMinutes(-5));

                // Get a reference to the streaming manifest file from the  
                // collection of files in the asset. 
                var manifestFile = asset.AssetFiles.ToList().Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();

                // Create a full URL to the manifest file. Use this for playback
                // in streaming media clients. 
                //urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest";
                urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest" + "(format=m3u8-aapl)?video.m3u8";
            }
            catch (Exception)
            {
                return null;
            }

            return urlForClientStreaming;
        }
    }
}
