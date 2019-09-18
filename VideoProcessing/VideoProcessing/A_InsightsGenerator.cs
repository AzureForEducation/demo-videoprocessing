using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VideoProcessing.Entities;

namespace VideoProcessing
{
    public static class A_InsightsGenerator
    {
        // Reads the Logic App URI to be called
        static readonly string _logicappuri = Environment.GetEnvironmentVariable("LogicAppVideoIndexerFlowURI");

        [FunctionName("A_InsightsGenerator")]
        public static string GeneratesInsights([ActivityTrigger] InitialSetupResult initialSetup, TraceWriter log)
        {
            // Building up Json sentence
            dynamic flexibleObj = new ExpandoObject();
            flexibleObj.assetId = initialSetup.Asset.Id;
            flexibleObj.videoFileName = initialSetup.Video.VideoFileName;
            var jsonStr = JsonConvert.SerializeObject(flexibleObj);

            try
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonStr);
                    content.Headers.ContentType.CharSet = string.Empty;
                    content.Headers.ContentType.MediaType = "application/json";

                    var response = client.PostAsync(_logicappuri, content);
                    log.Info(response.Result.ToString());

                    return response.Result.ToString();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            
        }
    }
}
