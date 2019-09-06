using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessing.Entities
{
    public class VideoAMS
    {
        public string AccessPolicyName { get; set; }
        public string AssetName { get; set; }
        public string StorageAccountName { get; set; }
        public string VideoPath { get; set; }
        public string VideoFileName { get; set; }
    }
}
