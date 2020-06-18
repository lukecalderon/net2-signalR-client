using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace net2_event_push.Classes
{
    class LiveEvent
    { 
        public DateTime EventDateTime { get; set; }
        public string Site { get; set; }
        public string EventLocation { get; set; }
        public string RfidTag { get; set; }
        public string CustomAttribute1 { get; set; }
        public string EventType { get; set; }

    }
}
