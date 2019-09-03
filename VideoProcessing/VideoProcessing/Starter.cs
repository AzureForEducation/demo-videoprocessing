using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace VideoProcessing
{
    public static class Starter
    {
        [FunctionName("Starter")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, 
            [OrchestrationClient] DurableOrchestrationClient starter,
            TraceWriter log)
        {
            // Parsing query parameter
            string video = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "video", true) == 0).Value;

            // Reads the request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Sets up the content
            video = video ?? data?.video;

            if(video == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please, pass the video location wither through query string or body");
            }

            log.Info($"All set! Starting the orchestration process for {video}...");

            var orchestrationId = await starter.StartNewAsync("Orchestrator", video);

            return starter.CreateCheckStatusResponse(req, orchestrationId);
        }
    }
}
