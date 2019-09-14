using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoProcessing.Entities;
using VideoProcessing.Services;

namespace VideoProcessing
{
    public static class A_PublishesEncodedAsset
    {
        // AD auth variables
        static readonly string _tenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
        static readonly string _restApiUrl = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
        static readonly string _clientId = Environment.GetEnvironmentVariable("AMSClientId");
        static readonly string _clientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");

        // AMS and storage constants
        private static CloudMediaContext _context = null;

        [FunctionName("A_PublishesEncodedAsset")]
        public static async Task<string> PublishesEncodedAsset([ActivityTrigger] string resultEncoding, TraceWriter log)
        {
            // Step 1: Create the context
            AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_tenantDomain, new AzureAdClientSymmetricKey(_clientId, _clientSecret), AzureEnvironments.AzureCloudEnvironment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);
            _context = new CloudMediaContext(new Uri(_restApiUrl), tokenProvider);
            string streamingUrl;

            // Step 2: Builds the streaming url for the encoded and published asset
            try
            {
                log.Info("Publishing the asset and building up the streaming url...");
                streamingUrl = MediaServices.PublishAndBuildStreamingURLs(resultEncoding, _context); ;
                log.Info("Done. Asset published.");
                log.Info($"Public URL: {streamingUrl}");
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return streamingUrl;
        }
    }
}
