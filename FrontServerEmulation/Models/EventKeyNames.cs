using CachingFramework.Redis.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontServerEmulation.Models
{
    public class EventKeyNames
    {
        public string EventKeyFrom { get; set; }
        public string EventFieldFrom { get; set; }
        public KeyEvent EventCmd { get; set; }
        public string EventKeyBackReadiness { get; set; }
        public string EventFieldBack { get; set; }
        public string EventKeyFrontGivesTask { get; set; }
        public string EventFieldFront { get; set; }
        public TimeSpan Ttl { get; set; }
    }
}
