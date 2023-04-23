using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Diagnostics;

namespace UnitTests
{
    [TestClass]
    public class MessageTests
    {

        [TestMethod]
        public void TestMessageParser()
        {
            Stopwatch sw = new Stopwatch();
            int NumMsg = 0;
            ByteMessageReader msgMan = new ByteMessageReader(new Guid(), 128000);
            msgMan.OnMessageReady += (byte[] msg, int off, int ct) => NumMsg++;

            byte[] data = new byte[108];
            PrefixWriter.WriteInt32AsBytes(data, 0, 32);
            PrefixWriter.WriteInt32AsBytes(data, 36, 32);
            PrefixWriter.WriteInt32AsBytes(data, 72, 32);

            byte[][] data1 = new byte[108][];
            for (int j = 0; j < 108; j++)
            {
                data1[j] = new byte[1] { data[j] };
            }


            int cut = 22;
            int iteration = 1000000;
            for (int i = 0; i < iteration; i++)
            {
                // 1 by 1
                for (int j = 0; j < 108; j++)
                {
                    msgMan.ParseBytes(new byte[1] { data[j] }, 0, 1);
                }
                // sliced chunks
                msgMan.ParseBytes(data, 0, cut);
                msgMan.ParseBytes(data, cut, 108 - cut);
            }
            Assert.IsTrue(NumMsg == 6 * iteration);
        }



    }
}
