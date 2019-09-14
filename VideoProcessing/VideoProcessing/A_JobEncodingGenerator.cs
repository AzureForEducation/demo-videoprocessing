using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
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
    public static class A_JobEncodingGenerator
    {
        // AD auth variables
        static readonly string _tenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
        static readonly string _restApiUrl = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
        static readonly string _clientId = Environment.GetEnvironmentVariable("AMSClientId");
        static readonly string _clientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
        static readonly string _storageConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

        private static CloudQueue _queue = null;
        private static INotificationEndPoint _notificationEndPoint = null;
        private static CloudMediaContext _context = null;

        [FunctionName("A_JobEncodingGenerator")]
        public static string GeneratesEncoder([ActivityTrigger] InitialSetupResult initialSetupResult, TraceWriter log)
        {
            IJob job;

            // Step 1: Setting up queue, context and endpoint
            string endPointAddress = Guid.NewGuid().ToString();

            AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_tenantDomain, new AzureAdClientSymmetricKey(_clientId, _clientSecret), AzureEnvironments.AzureCloudEnvironment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);
            _context = new CloudMediaContext(new Uri(_restApiUrl), tokenProvider);

            // Create the queue that will be receiving the notification messages.
            _queue = MediaServices.CreateQueue(_storageConnection, endPointAddress);

            // Create the notification point that is mapped to the queue.
            _notificationEndPoint = _context.NotificationEndPoints.Create(Guid.NewGuid().ToString(), NotificationEndPointType.AzureQueue, endPointAddress);

            // Step 2: Creating the encoding job
            try
            {
                log.Info("Starting encoding job...");
                IMediaProcessor mediaProcessor = MediaServices.GetLatestMediaProcessorByName("Media Encoder Standard", _context);
                job = MediaServices.SubmitEncodingJobWithNotificationEndPoint(_context, mediaProcessor.Name, initialSetupResult, _notificationEndPoint);
                log.Info("Done. Encoding job successfuly scheduled.");

                log.Info("Waiting the encoding process get completed...");
                MediaServices.WaitForJobToReachedFinishedState(job.Id, _queue, log);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }

            log.Info("Done. Encoding completed.");

            // Step 3: Cleaning up temporary resources
            _queue.Delete();
            _notificationEndPoint.Delete();

            // Step 4: Returns the final result
            return job.Id;
        }
    }
}
