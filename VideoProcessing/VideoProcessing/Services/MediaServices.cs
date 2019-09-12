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

namespace VideoProcessing.Services
{
    class MediaServices
    {
        private string _tenantDomain;
        private string _restApiUrl;
        private string _clientId;
        private string _clientSecret;
        private HttpClient _httpClient;

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

        public async Task<CloudBlockBlob> MoveVideoToAssetLocator(CloudBlockBlob sourceBlob, Locator locator, string storageConn)
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

        public static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName, CloudMediaContext _context)
        {
            var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }
    }
}
