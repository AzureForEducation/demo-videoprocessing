using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessing
{
    public static class _1_AssetGenerator
    {
        [FunctionName("1_AssetGenerator")]
        public static async Task<object> GeneratesAsset([ActivityTrigger] string inputVideo, TraceWriter log)
        {

        }
    }
}
