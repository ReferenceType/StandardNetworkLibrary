using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary.Components;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class EncryptionTest
    {
        [TestMethod]
        public void EncryptTest()
        {
            var key = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var iv = new byte[16] { 21, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            ConcurrentAesAlgorithm alg = new ConcurrentAesAlgorithm(key,NetworkLibrary.Components.Crypto.AesMode.GCM);

            byte[] data = new byte[100];
            data[1] = 99;
            byte[] out1 = new byte[150];
            byte[] out2 = new byte[150];

            int am=alg.EncryptInto(data,0,data.Length,out1,0);
            int dec=alg.DecryptInto(out1, 0, am, out2, 0);

            Assert.AreEqual(out2[1], data[1]);
            Assert.AreEqual(dec,data.Length);
        }
    }
}
