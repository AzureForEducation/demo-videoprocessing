using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoProcessing.Entities;

namespace VideoProcessing
{
    public static class A_SubtitlesGenerator
    {
        // AD auth variables
        static readonly string _tenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
        static readonly string _restApiUrl = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
        static readonly string _clientId = Environment.GetEnvironmentVariable("AMSClientId");
        static readonly string _clientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
        static readonly string _storageConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

        [FunctionName("A_SubtitlesGenerator")]
        public static async Task<bool> GeneratesSubtitles([ActivityTrigger] InitialSetupResult initialSetupResult, TraceWriter log)
        {
            return true;
        }
    }
}
