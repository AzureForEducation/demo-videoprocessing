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
        public static string GeneratesInsights([ActivityTrigger] AMSVideo amsVideoPublished, TraceWriter log)
        {
            // Building up Json sentence
            dynamic flexibleObj = new ExpandoObject();
            flexibleObj.assetId = amsVideoPublished.Asset.Id;
            flexibleObj.videoFileName = amsVideoPublished.Video.VideoFileName;
            flexibleObj.streamingVideoURL = amsVideoPublished.StreamingURL;
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
