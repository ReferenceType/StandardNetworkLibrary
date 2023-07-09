using NetworkLibrary.UDP.Reliable.Components;
using System;

namespace NetworkLibrary.UDP.Jumbo
{
    public class Mock
    {
        JumboModule J1 = new JumboModule();
        JumboModule J2 = new JumboModule();
        public Action<byte[], int, int> J1Received;
        public Action<byte[], int, int> J2Received;
        public Mock()
        {
            J1.SendToSocket = J1Send;
            J2.SendToSocket = J2Send;
            J1.MessageReceived += (b, o, c) => J1Received?.Invoke(b, o, c);
            J2.MessageReceived += (b, o, c) => J2Received?.Invoke(b, o, c);
        }

        private void J2Send(byte[] arg1, int arg2, int arg3)
        {
            J1.HandleReceivedSegment(arg1, arg2, arg3);
        }

        private void J1Send(byte[] arg1, int arg2, int arg3)
        {
            J2.HandleReceivedSegment(arg1, arg2, arg3);
        }

        public void SendAsJ1(byte[] arg1, int arg2, int arg3)
        {
            J1.Send(arg1, arg2, arg3);
        }
        public void SendAsJ1(Segment s1, Segment s2)
        {
            J1.Send(s1,s2);
        }
        public void SendAsJ2(byte[] arg1, int arg2, int arg3)
        {
            J2.Send(arg1, arg2, arg3);
        }
    }
}
