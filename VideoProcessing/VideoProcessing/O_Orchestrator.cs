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
    public static class O_Orchestrator
    {
        [FunctionName("O_Orchestrator")]
        public static async Task<object> OrchestratesVideoProcessing([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            // Holding the video location through the context
            var videoDto = context.GetInput<VideoAMS>();

            // Call activity 1: Calling activity function which uploads the video into AMS storage, creates Asset and Locator
            var resultInitialSetup = await context.CallActivityAsync<InitialSetupResult>("A_InitialSetupGenerator", videoDto);

            // Return a anonymous object with the information received
            return new
            {
                _asset = resultInitialSetup.Asset,
                _locator = resultInitialSetup.Locator
            };
        }
    }
}
