//using CustomNetworkLib;
//using CustomNetworkLib.SocketEventArgsTests;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System;
//using System.Net.Sockets;
//using System.Threading;

//namespace UnitTests
//{
//    [TestClass]
//    public class UnitTest1
//    {
//        [TestMethod]
//        public void TestMethod1()
//        {
//            AsyncTcpServer se = new AsyncTcpServer(2008);
//            AsyncTpcClient cl = new AsyncTpcClient();
//            cl.ConnectAsync("127.0.0.1", 2008);
            
//            cl.OnConnected += ()=> 
//            cl.SendAsync(new byte[11]); ;
//            Thread.Sleep(100);
//            cl.SendAsync(new byte[11]); ;
//            //se.OnClientAccepted += (Socket ClientSocket) =>
//            // {
//            //     se.SendBytesToAllClients(new byte[111]);
//            // };
//            se.OnBytesRecieved += AA;
//            Thread.Sleep(1000);
           
//        }


//        private void AA(int id, byte[] bytes)
//        {
//            Console.WriteLine(" server got bytes");
//        }
//    }
//}
