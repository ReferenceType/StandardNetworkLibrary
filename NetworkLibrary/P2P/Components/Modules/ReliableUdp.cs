using NetworkLibrary.UDP.Reliable.Components;
using System;

namespace NetworkLibrary.P2P.Components.Modules
{
    internal class ReliableUdpModule
    {
        public enum ConfigType
        {
            Standard,
            Realtime
        }
        SenderModule sender = new SenderModule();
        ReceiverModule receiver = new ReceiverModule();
        public Action<byte[], int, int> OnReceived;
        public Action<byte[], int, int> OnSend;

        public ReliableUdpModule()
        {
            sender.SendRequested += OutputDataBytesToSend;

            receiver.SendFeedback += OutputFeedbacks;
            receiver.OnMessageReceived += OutputMessageReceived;
            sender.ReserveForPrefix = 38;
            receiver.ReserveforPrefix = 38;
        }
        public void Configure(ConfigType configType)
        {
            if(configType == ConfigType.Realtime)
            {
                sender.MinRTO = 1;
                sender.RTOOffset= 0;
                sender.RTO = 20;
            }
        }
        private void OutputMessageReceived(byte[] buffer, int offset, int count)
        {
            OnReceived?.Invoke(buffer, offset, count);
        }

        private void OutputDataBytesToSend(byte[] buffer, int offset, int count)
        {
            OnSend?.Invoke(buffer, offset, count);
        }
        private void OutputFeedbacks(byte[] buffer, int offset, int count)
        {
            OnSend?.Invoke(buffer, offset, count);
        }

        public void HandleBytes(byte[] buffer, int offset, int count)
        {
            try
            {
                switch (buffer[offset])
                {
                    case SenderModule.Ack:
                        sender.HandleFeedback(buffer, offset);
                        break;
                    case SenderModule.AckList:
                        sender.HandleFeedback(buffer, offset);
                        break;
                    case SenderModule.Nack:
                        sender.HandleFeedback(buffer, offset);
                        break;
                    case SenderModule.ProbeResult:
                        sender.HandleFeedback(buffer, offset);
                        break;
                    case SenderModule.SynAck:
                        sender.HandleFeedback(buffer, offset);
                        break;
                    case SenderModule.Head:
                        receiver.HandleBytes(buffer, offset, count);
                        break;
                    case SenderModule.Chunk:
                        receiver.HandleBytes(buffer, offset, count);
                        break;
                    case SenderModule.Probe1:
                        receiver.HandleBytes(buffer, offset, count);
                        break;
                    case SenderModule.Probe2:
                        receiver.HandleBytes(buffer, offset, count);
                        break;
                    default:
                        Console.WriteLine("Reliable Udp Module could not parse incming message OPCode");
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to Read Low Level Message" + ex.Message);
            }
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            sender.ProcessBytesToSend(buffer, offset, count);
        }
        public void Send(in Segment first, in Segment second)
        {
            sender.ProcessBytesToSend(first, second);
        }

        internal void Release()
        {
            OnReceived = null;
            OnSend = null;

            sender.SendRequested = null;
            receiver.SendFeedback = null;
            receiver.OnMessageReceived = null;
            sender.ShutDown();
            receiver.ShutDown();
        }
    }
}
