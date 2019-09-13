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
        static readonly string _storageConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

        // AMS and storage constants
        private static CloudMediaContext _context = null;
        private static CloudQueue _queue = null;
        private static INotificationEndPoint _notificationEndPoint = null;

        [FunctionName("A_PublishesEncodedAsset")]
        public static async Task<bool> PublishesEncodedAsset([ActivityTrigger] InitialSetupResult initialSetupResult, TraceWriter log)
        {
            IJob job;

            // Generating endpoint address
            string endPointAddress = Guid.NewGuid().ToString();
            
            // Converting string path into Uri
            Uri uriSource = new Uri(initialSetupResult.Video.VideoPath, UriKind.Absolute);
            
            // Moving file into the asset
            CloudBlockBlob sourceBlob;
            sourceBlob = new CloudBlockBlob(uriSource);

            // Step 1: Create the context
            AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_tenantDomain, new AzureAdClientSymmetricKey(_clientId, _clientSecret), AzureEnvironments.AzureCloudEnvironment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);
            _context = new CloudMediaContext(new Uri(_restApiUrl), tokenProvider);

            // Step 2: Create the queue that will be receiving the notification messages
            _queue = MediaServices.CreateQueue(_storageConnection, endPointAddress);

            // Step 3: Create the notification point that is mapped to the queue
            _notificationEndPoint = _context.NotificationEndPoints.Create(Guid.NewGuid().ToString(), NotificationEndPointType.AzureQueue, endPointAddress);


            if (_notificationEndPoint != null)
            {
                job = await MediaServices.SubmitEncodingJobWithNotificationEndPoint(_context, "Media Encoder Standard", initialSetupResult, _notificationEndPoint);
                MediaServices.WaitForJobToReachedFinishedState(job.Id, _queue);
            }
            else
            {
                return false;
            }

            // Step 4: Publishes the asset and returns the streaming URL
            string urlStreaming = MediaServices.PublishAndBuildStreamingURLs(job.Id, _context, initialSetupResult);

            // Step 5: Clean up notification queue and endpoint
            _queue.Delete();
            _notificationEndPoint.Delete();

            return true;
        }
    }
}
