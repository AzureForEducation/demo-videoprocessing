using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VideoProcessing.Entities;
using VideoProcessing.Services;

namespace VideoProcessing
{
    public static class A_EncodeGenerator
    {
        // AD auth variables
        static readonly string _tenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
        static readonly string _restApiUrl = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
        static readonly string _clientId = Environment.GetEnvironmentVariable("AMSClientId");
        static readonly string _clientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
        static readonly string _storageConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

        [FunctionName("A_EncodeGenerator")]
        public static async Task<object> GeneratesEncoder([ActivityTrigger] InitialSetupResult initialSetupResult,TraceWriter log)
        {
            MediaServices mediaService = new MediaServices(_tenantDomain, restApiUrl: _restApiUrl, _clientId, _clientSecret, _storageConnection);
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            JToken result;

            try
            {
                string mediaProcessorId = await mediaService.GetMediaProcessorId("Media Encoder Standard");
                result = await mediaService.CreateJob($"Job - Media Enconder for { initialSetupResult.Video.VideoFileName }", initialSetupResult.Asset.Id, mediaProcessorId, "Adaptive Streaming");
            }
            catch (Exception ex)
            {
                return httpResponse.RequestMessage.CreateResponse(HttpStatusCode.InternalServerError, $"It wasn't possible to start the video encoding processing. \n {ex.StackTrace}");
            }

            return result;
        }
    }
}
