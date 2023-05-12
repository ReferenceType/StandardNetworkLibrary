using JsonMessageNetwork.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Generic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using static NetworkLibrary.TCP.Base.TcpServerBase;

namespace JsonMessageNetwork
{
    public class PureJsonServer : GenericServer<JsonSerializer>
    {
        public PureJsonServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
