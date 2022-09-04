using CustomNetworkLib;
using CustomNetworkLib.SocketEventArgsTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace UnitTests
{
    [TestClass]
    public class ByteMessageParserTest
    {

        [TestMethod]
        public void TestMessageParserSpeed()
        {
            Stopwatch sw= new Stopwatch();
            int NumMsg = 0;
            ByteMessageManager msgMan = new ByteMessageManager(new Guid(), 128000);
            msgMan.OnMessageReady += (byte[] msg, int off, int ct) => NumMsg++;

            byte[] data = new byte[108];
            BufferManager.WriteInt32AsBytes(data, 0, 32);
            BufferManager.WriteInt32AsBytes(data, 36, 32);
            BufferManager.WriteInt32AsBytes(data, 72, 32);

            byte[][] data1 = new byte[108][];
            for (int j = 0; j < 108; j++)
            {
                data1[j]=new byte[1] { data[j] };
            }

           
            int cut = 22;
            int iter = 100000;
            for (int i = 0; i < iter; i++)
            {
                for (int j = 0; j < 108; j++)
                {
                    msgMan.ParseBytes(new byte[1] { data[j] }, 0, 1);
                }
                msgMan.ParseBytes(data, 0, cut);
                msgMan.ParseBytes(data, cut, 108 - cut);
            }
            Assert.IsTrue(NumMsg == 6*iter);
        }



    }
}
