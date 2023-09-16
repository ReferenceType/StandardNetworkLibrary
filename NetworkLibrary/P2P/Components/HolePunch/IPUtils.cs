using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NetworkLibrary
{
    internal class IPUtils
    {
        //10.0.0.0 - 10.255.255.255
        //172.16.0.0 - 172.31.255.255
        //192.168.0.0 - 192.168.255.255
        public static bool IsPrivate(byte[] ipBytes)
        {
            if (ipBytes[0]== 192 && ipBytes[1] == 168)
            {
                return true;
            }
            else if (ipBytes[0] == 10)
            {
                return true;
            }
            else if (ipBytes[0] ==172 && (ipBytes[1]>15 && ipBytes[1] < 31))
            {
                return true;
            }
            else { return false;}
        }

        public static bool IsLoopback(byte[] ipBtyes)
        {
            var addr = new IPAddress(ipBtyes);
            if (IPAddress.IsLoopback(addr))
            {
                return true;
            }
            return false;
        }
    }
}
