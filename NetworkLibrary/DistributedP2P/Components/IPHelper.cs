using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class IPHelper
    {
        public static List<string> GetLocalIPAddresses4()
        {
            List<string> ipAddresses = new List<string>();
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.Description.Contains("VMware") ||
                    ni.Description.Contains("Hyper-V") ||
                    ni.Description.Contains("VirtualBox") ||
                    ni.Name.Contains("vEthernet"))
                    continue;

                IPInterfaceProperties ipProps = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ipAddresses.Add(addr.Address.ToString());
                    }
                }
            }
            return ipAddresses;
        }

        // based on reserved private networks by RFC 1918
        public static bool IsPrivateIPAddress(IPEndPoint endpont)
        {
            IPAddress address = endpont.Address;
            byte[] bytes = address.GetAddressBytes();

            return IsPrivateIPAddress(bytes);

        }

        // based on reserved private networks by RFC 1918
        public static bool IsPrivateIPAddress(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out IPAddress address))
                return false;

            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();

            return IsPrivateIPAddress(bytes);
        }

        public static bool IsPrivateIPAddress(byte[] bytes)
        {
            // 10.0.0.0 - 10.255.255.255 (10/8 prefix)
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0 - 172.31.255.255 (172.16/12 prefix)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0 - 192.168.255.255 (192.168/16 prefix)
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 169.254.0.0 - 169.254.255.255 (169.254/16 prefix, link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            // 127.0.0.0 - 127.255.255.255 (127/8 prefix, loopback)
            if (bytes[0] == 127)
                return true;

            return false;
        }

        public static void ExtractLocalIpsWithMatchingSubnet(List<string> fromLocals, List<string> ToLocals, out List<string> toFrom, out List<string> toTo)
        {
            // Get all possible subnet pairs between the two lists
            var subnetPairs = from a in fromLocals
                              from b in ToLocals
                              let subnetA = GetSubnet(a)
                              let subnetB = GetSubnet(b)
                              where subnetA == subnetB
                              select new { a, b };

            // Group by the matching subnets to maintain order
            var matchedSubnets = subnetPairs
                .GroupBy(pair => GetSubnet(pair.a))
                .OrderByDescending(g => g.Key) // Just to have consistent ordering
                .SelectMany(g => g.Select(p => p));

            // First add all IPs with matching subnets (ordered by subnet matches)
            var orderedToFrom = new List<string>();
            var orderedToTo = new List<string>();

            foreach (var pair in matchedSubnets)
            {
                if (!orderedToTo.Contains(pair.a))
                {
                    orderedToTo.Add(pair.a);
                }
                if (!orderedToFrom.Contains(pair.b))
                {
                    orderedToFrom.Add(pair.b);
                }
            }

            // Then add remaining IPs that didn't have matches
            foreach (var ip in ToLocals)
            {
                if (!orderedToFrom.Contains(ip))
                {
                    orderedToFrom.Add(ip);
                }
            }

            foreach (var ip in fromLocals)
            {
                if (!orderedToTo.Contains(ip))
                {
                    orderedToTo.Add(ip);
                }
            }

            toFrom = orderedToFrom;
            toTo = orderedToTo;
        }

        // Helper method to get the first two octets (subnet /16)
        private static string GetSubnet(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }
            return ip; // fallback for invalid IPs (though shouldn't happen with valid IPs)
        }

        public static bool IsZero(byte[] ipRemote)
        {
            if (ipRemote.Length != 4)
                throw new InvalidOperationException("Ip must be 4 bytes");

            unsafe
            {
                fixed (byte* ptr = ipRemote)
                {
                    return *((int*)ptr) == 0;
                }
            }
        }

        public static bool AreEqual(byte[] ip1, byte[] ip2)
        {

            unsafe
            {
                fixed (byte* ptr1 = ip1)
                fixed (byte* ptr2 = ip2)
                {
                    return *((int*)ptr1) == *((int*)ptr2);
                }
            }
        }

        public static void ObtainIpEndpoints(int fromPort, int toPort, ServerSession sesFrom, ServerSession sesTo, out EndpointTransferMessage FromNeedsToKnow, out EndpointTransferMessage ToNeedsToKnow)
        {
            FromNeedsToKnow = new EndpointTransferMessage();
            ToNeedsToKnow = new EndpointTransferMessage();

            IPHelper.ExtractLocalIpsWithMatchingSubnet(sesFrom.ClientLocalIps,
                                                       sesTo.ClientLocalIps,
                                                       out List<string> Locals_From_NeedsToKnow,
                                                       out List<string> Locals_To_NeedsToKnow);


            //They need to know locals when both peers have public IPs same(coming from same NAT)
            // or peers and server inside LAN, means both peers have private IPs on public ip.

            if ((sesFrom.ClientPublicIp.Address.Equals(sesTo.ClientPublicIp.Address)) ||
                (IPHelper.IsPrivateIPAddress(sesFrom.ClientPublicIp) && IPHelper.IsPrivateIPAddress(sesTo.ClientPublicIp)))
            {
                foreach (var local in Locals_From_NeedsToKnow)
                {
                    EndpointData data = new EndpointData(local, toPort);
                    FromNeedsToKnow.LocalEndpoints.Add(data);
                }

                foreach (var local in Locals_To_NeedsToKnow)
                {
                    EndpointData data = new EndpointData(local, fromPort);
                    ToNeedsToKnow.LocalEndpoints.Add(data);
                }
            }


            // "From" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(sesFrom.ClientPublicIp))
            {
                // "To" needs to get server adress to connect 
                // 0.0.0.0:0 means serverIp 
                ToNeedsToKnow.IpRemote = new byte[4];
                ToNeedsToKnow.PortRemote = fromPort;

            }
            else
            {
                // send just the publicIp of "From" to "To"
                ToNeedsToKnow.IpRemote = sesFrom.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();
                ToNeedsToKnow.PortRemote = fromPort;
            }

            // "To" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(sesTo.ClientPublicIp))
            {
                //"From" needs to get Server adress
                FromNeedsToKnow.IpRemote = new byte[4];
                FromNeedsToKnow.PortRemote = toPort;
            }
            else
            {
                // send just the public to "From"
                FromNeedsToKnow.IpRemote = sesTo.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();
                FromNeedsToKnow.PortRemote = toPort;
            }

        }

        public static void ObtainIpEndpoints(EndpointTransferMessage fromAdresses, EndpointTransferMessage toAdresses,
            out EndpointTransferMessage FromNeedsToKnow, out EndpointTransferMessage ToNeedsToKnow)
        {
            FromNeedsToKnow = new EndpointTransferMessage();
            ToNeedsToKnow = new EndpointTransferMessage();

            var fromLocalIps = GetAdressesAsStrings(fromAdresses.LocalEndpoints);
            var toLocalIps = GetAdressesAsStrings(toAdresses.LocalEndpoints);
            int toPort = toAdresses.LocalEndpoints.First().Port;
            int fromPort = fromAdresses.LocalEndpoints.First().Port;


            IPHelper.ExtractLocalIpsWithMatchingSubnet(fromLocalIps,
                                                       toLocalIps,
                                                       out List<string> Locals_From_NeedsToKnow,
                                                       out List<string> Locals_To_NeedsToKnow);


            //They need to know locals when both peers have public IPs same(coming from same NAT)
            // or peers and server inside LAN, means both peers have private IPs on public ip.

            if (AreEqual(fromAdresses.IpRemote, toAdresses.IpRemote) ||
                (IPHelper.IsPrivateIPAddress(fromAdresses.IpRemote) && IPHelper.IsPrivateIPAddress(toAdresses.IpRemote)))
            {
                foreach (var local in Locals_From_NeedsToKnow)
                {
                    EndpointData data = new EndpointData(local, toPort);
                    FromNeedsToKnow.LocalEndpoints.Add(data);
                }

                foreach (var local in Locals_To_NeedsToKnow)
                {
                    EndpointData data = new EndpointData(local, fromPort);
                    ToNeedsToKnow.LocalEndpoints.Add(data);
                }
            }


            // "From" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(fromAdresses.IpRemote))
            {
                // "To" needs to get server adress to connect 
                // 0.0.0.0:0 means serverIp 
                ToNeedsToKnow.IpRemote = new byte[4];
                ToNeedsToKnow.PortRemote = fromPort;

            }
            else
            {
                // send just the publicIp of "From" to "To"
                ToNeedsToKnow.IpRemote = fromAdresses.IpRemote;
                ToNeedsToKnow.PortRemote = fromPort;
            }

            // "To" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(toAdresses.IpRemote))
            {
                //"From" needs to get Server adress
                FromNeedsToKnow.IpRemote = new byte[4];
                FromNeedsToKnow.PortRemote = toPort;
            }
            else
            {
                // send just the public to "From"
                FromNeedsToKnow.IpRemote = toAdresses.IpRemote;
                FromNeedsToKnow.PortRemote = toPort;
            }

        }

        private static List<string> GetAdressesAsStrings(List<EndpointData> localEndpoints)
        {
            List<string> result = new List<string>();
            foreach (var local in localEndpoints)
            {
                result.Add(Byte2Sting(local.Ip));
            }
            return result;
        }

        public static string Byte2Sting(byte[] ip)
        {
            return $"{ip[0]}.{ip[1]}.{ip[2]}.{ip[3]}";
        }

        public static byte[] String2Byte(string ip)
        {

            var parts = ip.Split('.');

            return new byte[] {
                byte.Parse(parts[0]),
                byte.Parse(parts[1]),
                byte.Parse(parts[2]),
                byte.Parse(parts[3])
            };
        }
    }
}
