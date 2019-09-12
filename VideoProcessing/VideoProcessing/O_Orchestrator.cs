using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
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

            // Call activity 2: Calling activity function which encodes the video
            var resultEncoding = await context.CallActivityAsync<object>("A_EncodeGenerator", resultInitialSetup);

            // Call activity 3: Calling activity function which indexes the video
            var resultIndexer = await context.CallActivityAsync<bool>("A_SubtitlesGenerator", resultInitialSetup);

            // Return a anonymous object based upon the information received back from the orchestration process
            return new
            {
                _asset = resultInitialSetup.Asset,
                _locator = resultInitialSetup.Locator,
                _encoding = resultEncoding,
                _indexer = resultIndexer
            };
        }
    }
}
