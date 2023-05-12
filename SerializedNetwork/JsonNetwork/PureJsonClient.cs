using JsonMessageNetwork.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.TcpClientBase;

namespace JsonMessageNetwork
{
    public class PureJsonClient : GenericClient<JsonSerializer>
    {
    }
}
