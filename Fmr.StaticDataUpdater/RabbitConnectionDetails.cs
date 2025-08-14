using System;
using System.Collections.Generic;
using System.Text;

namespace Fmr.StaticDataUpdater
{
    public class RabbitConnectionDetails
    {
        public string RabbitHost { get; set; }
        public string RabbitUser { get; set; }
        public string RabbitPassword { get; set; }
        public string TopicGetAlerts { get; set; }
        public string RoutingKey { get; set; }
        public string TopicPusblishAlerts { get; set; }
        public bool EnableFmrLogger { get; set; }
        public int KeepAliveInterval { get; set; }
        public string KeepAliveApiEndPoint { get; set; }
    }

}
