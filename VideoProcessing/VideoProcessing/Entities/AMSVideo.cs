using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessing.Entities
{
    public class AMSVideo
    {
        public Asset Asset { get; set; }
        public Locator Locator { get; set; }
        public VideoAMS Video { get; set; }
        public string StreamingURL { get; set; }
    }
}
