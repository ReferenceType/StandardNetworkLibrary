using NetworkLibrary.UDP.Reliable.Components;
using System;
using System.Threading;

namespace NetworkLibrary.UDP.Jumbo
{
    public class JumboModule
    {
        Receiver receiver;
        Sender sender;
        public Action<byte[], int, int> SendToSocket;
        public Action<byte[], int, int> MessageReceived;
        public JumboModule()
        {
            this.receiver = new Receiver();
            this.sender = new Sender();
            sender.OnSend = SendBytesToSocket;
            receiver.OnMessageExtracted = HandleExtractedMessage;
        }

        private void HandleExtractedMessage(byte[] arg1, int arg2, int arg3)
        {
            MessageReceived?.Invoke(arg1, arg2, arg3);
        }

        private void SendBytesToSocket(byte[] arg1, int arg2, int arg3)
        {
            SendToSocket?.Invoke(arg1, arg2, arg3);
        }

        public void Send(byte[] data, int offset, int count)
        {
            sender.ProcessBytes(data, offset, count);
        }

        public void HandleReceivedSegment(byte[] buffer, int offset, int count)
        {
            receiver.ProccesReceivedDatagram(buffer, offset, count);
        }

        public void Release()
        {
            SendToSocket = null;
            MessageReceived = null;
            sender.Release();
            receiver.Release();
        }

        internal void Send(in Segment s1, in Segment s2)
        {
            sender.ProcessBytes(in s1, in s2);
        }
        internal void Send(in SegmentUnsafe s1, in Segment s2)
        {
            sender.ProcessBytes(in s1, in s2);
        }
    }
}
