using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessing
{
    public static class Orchestrator
    {
        [FunctionName("Orchestrator")]
        public static async Task<object> OrchestratesVideoProcessing(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            TraceWriter log
            )
        {
            // Holding the video location through the context
            var videoLocation = context.GetInput<string>();

            // Call activity 1: Generating an asset into AMS
            var assetLocation = await context.CallActivityAsync<string>("1_AssetGenerator", videoLocation);

            // Call activity 2: Enconding to multibit rate
            var encodedVideoLocation = await context.CallActivityAsync<string>("2_EncodeGenerator", assetLocation);

            // Call activity 3: Generating thumbnail
            var thumbnailLocation = await context.CallActivityAsync<string>("3_ThumbnailGenerator", assetLocation);

            // Call activity 4: Generating subtitles through video indexer
            var subtitleLocation = await context.CallActivityAsync<string>("4_SubtitleGenerator", assetLocation);

            // Return a anonymous object with the information received
            return new
            {
                encoded = encodedVideoLocation,
                thumbnail = thumbnailLocation,
                subtitle = subtitleLocation
            };
        }
    }
}
