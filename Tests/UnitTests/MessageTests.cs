using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace UnitTests
{
    [TestClass]
    public class MessageTests
    {

        [TestMethod]
        public void TestMessageParser()
        {
            Stopwatch sw= new Stopwatch();
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
                data1[j]=new byte[1] { data[j] };
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
            Assert.IsTrue(NumMsg == 6*iteration);
        }

       // [TestMethod]
       // public void TestEncriptorBuffer()
       // {
       //     var rnd = new RNGCryptoServiceProvider();
       //     var byteKey = new byte[16];

       //     rnd.GetNonZeroBytes(byteKey);
       //     var AesAlgorithm = new AesAlgorithm(byteKey, byteKey);

       //     AesEncryptor processor = new AesEncryptor(AesAlgorithm);

       //     byte[] message= new byte[250];
       //     byte[] buffer= new byte[88];
       //     byte[] output = new byte[256];

       //     int procesed = 0;
       //     int totalProcessed=0;

       //     if (!processor.FlushRequired)
       //     {
       //         procesed = processor.ProcessMessage(message, buffer, 0);
       //         Buffer.BlockCopy(buffer, 0, output, 0, procesed);
       //         totalProcessed += procesed;
       //     }


       //     while (processor.FlushRequired)
       //     {
       //         procesed = processor.FlushPendingMessage(buffer, 0);
       //         Buffer.BlockCopy(buffer, 0, output, totalProcessed, procesed);
       //         totalProcessed += procesed;

       //     }

       //     var result = AesAlgorithm.Decrypt(output);
       //     Assert.IsTrue(VerifyBuffer(result));

       //     bool VerifyBuffer(byte[] buffer)
       //     {
       //         for (int i = 0; i < buffer.Length; i++)
       //         {
       //             if (buffer[i] != 0)
       //                 return false;
       //         }
       //         return true;
       //     }
       // }

       //[TestMethod]
       //public void TestDecryptorBuffer()
       // {
       //     var rnd = new RNGCryptoServiceProvider();
       //     var byteKey = new byte[16];

       //     rnd.GetNonZeroBytes(byteKey);
       //     var AesAlgorithm = new AesAlgorithm(byteKey, byteKey);

       //     var processor = new AesDecryptor(AesAlgorithm);

       //     byte[] message = new byte[250];
       //     byte[] buffer = new byte[88];
       //     byte[] output = new byte[256];
       //     int procesed = 0;
       //     int totalProcessed = 0;

       //    message= AesAlgorithm.Encrypt(message);

       //     procesed = processor.ProcessMessage(message, buffer, 0);
       //     Buffer.BlockCopy(buffer, 0, output, 0, procesed);
       //     totalProcessed += procesed;

       //     while (processor.FlushRequired)
       //     {
       //         procesed = processor.FlushPendingMessage(buffer, 0);
       //         Buffer.BlockCopy(buffer, 0, output, totalProcessed, procesed);
       //         totalProcessed += procesed;

       //     }

       //     Assert.IsTrue(VerifyBuffer(output));

       //     bool VerifyBuffer(byte[] buffer)
       //     {
       //         for (int i = 0; i < buffer.Length; i++)
       //         {
       //             if (buffer[i] != 0)
       //                 return false;
       //         }
       //         return true;
       //     }
       // }

    }
}
