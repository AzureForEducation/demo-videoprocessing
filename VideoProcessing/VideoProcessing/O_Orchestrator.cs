using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoProcessing.Entities;

namespace VideoProcessing
{
    public static class O_Orchestrator
    {
        [FunctionName("O_Orchestrator")]
        public static async Task<object> OrchestratesVideoProcessing([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            // Holding the video location through the context
            var videoDto = context.GetInput<VideoAMS>();

            // Call activity 1: Calling activity function which uploads the video into AMS storage, creates Asset and Locator
            var resultInitialSetup = await context.CallActivityAsync<InitialSetupResult>("A_InitialSetupGenerator", videoDto);

            // Call activity 2: Calling activity function which asynchronously creates an encoding job
            var resultEncoding = await context.CallActivityAsync<bool>("A_JobEncodingGenerator", resultInitialSetup);

            // Call activity 3: Calling the activity function which asynchronously listen a job notification webhook and publishes the content at the end
            //var statusEncodingPublishing = await context.CallActivityAsync<bool>("A_PublishesEncodedAsset", resultInitialSetup);

            // Call activity 3: Calling activity function which indexes the video
            //var resultIndexer = await context.CallActivityAsync<bool>("A_SubtitlesGenerator", resultInitialSetup);

            if(resultInitialSetup != null)
            {
                return new InitialSetupResult
                {
                    Asset = resultInitialSetup.Asset,
                    Locator = resultInitialSetup.Locator,
                    Video = videoDto
                };
            }
            else
            {
                return null;
            }
        }
    }
}
