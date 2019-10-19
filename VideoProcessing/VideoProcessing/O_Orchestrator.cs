using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using VideoProcessing.Entities;

namespace VideoProcessing
{
    public static class O_Orchestrator
    {
        [FunctionName("O_Orchestrator")]
        public static async Task<object> OrchestratesVideoProcessing([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            // Holding the video location through the context
            var videoDto = context.GetInput<VideoAMS>();
            InitialSetupResult resultInitialSetup;
            AMSVideo amsVideoPublished;
            string resultEncoding, resultPublishing, resultInsights;

            try
            {
                // Call activity 1: Calling activity function which uploads the video into AMS storage, creates Asset and Locator
                if (!context.IsReplaying)
                    log.Info("Starting initial setup...");

                resultInitialSetup = await context.CallActivityAsync<InitialSetupResult>("A_InitialSetupGenerator", videoDto);

                // Call activity 2: Calling activity function which asynchronously creates an encoding job
                if (!context.IsReplaying)
                    log.Info("Starting the encoding job...");

                resultEncoding = await context.CallActivityAsync<string>("A_JobEncodingGenerator", resultInitialSetup);

                // Call activity 3: Calling activity function which publishes the encoded asset
                if (!context.IsReplaying)
                    log.Info("Publishing the encoded package...");

                resultPublishing = await context.CallActivityAsync<string>("A_PublishesEncodedAsset", resultEncoding);

                if (!context.IsReplaying)
                    log.Info("Extracting insights...");

                amsVideoPublished = new AMSVideo { Asset = resultInitialSetup.Asset, Locator = resultInitialSetup.Locator, Video = resultInitialSetup.Video, StreamingURL = resultPublishing };
                resultInsights = await context.CallActivityAsync<string>("A_InsightsGenerator", amsVideoPublished);

                // Finishes the flow by returning the info object
                return new 
                {
                    _Asset = resultInitialSetup.Asset,
                    _Locator = resultInitialSetup.Locator,
                    _Video = videoDto,
                    _URLStreaming = resultPublishing
                };
            }
            catch (Exception ex)
            {
                 return httpResponse.RequestMessage.CreateResponse(HttpStatusCode.InternalServerError, $"It wasn't possible to go through the flow. \n {ex.StackTrace}");
            }
        }
    }
}
