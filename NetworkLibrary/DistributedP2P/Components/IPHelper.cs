using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

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

        // based on reserved private networks by RFC 1918
        public static bool IsPrivateIPAddress(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out IPAddress address))
                return false;

            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();


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



    }
}
