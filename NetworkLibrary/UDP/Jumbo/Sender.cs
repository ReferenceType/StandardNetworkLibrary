using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.UDP.Jumbo
{
    internal class Sender
    {
        [ThreadStatic]
        private static byte[] buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetBuffer()
        {
            if (buffer == null)
            {
                buffer = ByteCopy.GetNewArray(65000, true);
            }
            return buffer;
        }
        public static int FragmentSize = 64000;
        int currentMsgNo = 0;
        public int ReserveForPrefix = 38;
        public Action<byte[], int, int> OnSend;
        public void ProcessBytes(byte[] buffer, int offset, int count)
        {
            //if (count < 256000)
            //{
            //    ProcessBytesUnsafe(buffer, offset, count);
            //}
            //else
            {
                ProcessBytesSafe(buffer, offset, count);
            }
        }
        private unsafe void ProcessBytesUnsafe(byte[] buffer, int offset, int count)
        {
            var b = stackalloc byte[count];
            fixed (byte* message_ = &buffer[offset])
                Buffer.MemoryCopy(message_, b, count, count);

            
            int totalNumSeq = count / FragmentSize;
            if (count % FragmentSize != 0)
            {
                totalNumSeq++;
            }

            var msgNo = Interlocked.Increment(ref currentMsgNo);
            byte curresntSeq = 1;

            var tempBuff = GetBuffer();
            while (count > FragmentSize)
            {
                int offset_ = ReserveForPrefix;
                int tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                //  ByteCopy.BlockCopy(buffer, offset, tempBuff, offset_, fragmentsize);
                fixed (byte* dest = &tempBuff[offset_])
                    Buffer.MemoryCopy(b+offset, dest, FragmentSize, FragmentSize);
                offset += FragmentSize;
                count -= FragmentSize;
                tempbuffCnt += FragmentSize;
                var cc = tempBuff[tempbuffCnt-1];
                var a = buffer[offset];

                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            }
            if (count > 0)
            {
                int offset_ = ReserveForPrefix;
                int tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                var a = buffer[offset - 1];
                 a = buffer[offset];
                 a = buffer[offset + 1];

                // ByteCopy.BlockCopy(buffer, offset, tempBuff, offset_, count);
                fixed (byte* dest = &tempBuff[offset_])
                    Buffer.MemoryCopy(b + offset, dest, count, count);
                tempbuffCnt += count;
                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);
            }
        }
        private void ProcessBytesSafe(byte[] buffer, int offset, int count)
        {

            var buff =  BufferPool.RentBuffer(count);
            ByteCopy.BlockCopy(buffer,offset, buff, 0, count);
            offset = 0;
            buffer = buff;
            int totalNumSeq = count / FragmentSize;
            if (count % FragmentSize != 0)
            {
                totalNumSeq++;
            }

            var msgNo = Interlocked.Increment(ref currentMsgNo);
            byte curresntSeq = 1;

            var tempBuff = GetBuffer();
            while (count > FragmentSize)
            {
                int offset_ = ReserveForPrefix;
                int tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(buffer, offset, tempBuff, offset_, FragmentSize);
               
                offset += FragmentSize;
                count -= FragmentSize;
                tempbuffCnt += FragmentSize;
                var cc = tempBuff[tempbuffCnt - 1];
                var a = buffer[offset];

                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            }
            if (count > 0)
            {
                int offset_ = ReserveForPrefix;
                int tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(buffer, offset, tempBuff, offset_, count);
              
                tempbuffCnt += count;
                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);
            }
            BufferPool.ReturnBuffer(buff);

        }

        public void ProcessBytes(in Segment first, in Segment second)
        {
            int count = first.Count + second.Count;
            int totalNumSeq = count / FragmentSize;
            if (count % FragmentSize != 0)
            {
                totalNumSeq++;
            }

            var msgNo = Interlocked.Increment(ref currentMsgNo);
            byte curresntSeq = 1;

            var tempBuff = GetBuffer();
            int offset_ = ReserveForPrefix;
            int tempbuffCnt = 0;

            //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
            //tempBuff[offset_++] = (byte)totalNumSeq;
            //tempBuff[offset_++] = curresntSeq++;
            //tempbuffCnt += offset_;
            WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
            curresntSeq++;
            tempbuffCnt += offset_;

            ByteCopy.BlockCopy(first.Array, first.Offset, tempBuff, offset_, first.Count);
            count-= first.Count;
            offset_ += first.Count;
            tempbuffCnt += first.Count;

            int toCopyMore = FragmentSize - first.Count;
            int secondOffset = second.Offset;
            ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, toCopyMore);

            count -= toCopyMore;
            tempbuffCnt += toCopyMore;
            secondOffset+= toCopyMore;
            OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            while (count > FragmentSize)
            {
                 offset_ = ReserveForPrefix;
                 tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, FragmentSize);

                secondOffset += FragmentSize;
                count -= FragmentSize;
                tempbuffCnt += FragmentSize;

                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            }
            if (count > 0)
            {
                offset_ = ReserveForPrefix;
                tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, count);

                tempbuffCnt += count;
                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);
            }

        }

        public unsafe void ProcessBytes(in SegmentUnsafe first, in Segment second)
        {
            int count = first.Count + second.Count;
            int totalNumSeq = count / FragmentSize;
            if (count % FragmentSize != 0)
            {
                totalNumSeq++;
            }

            var msgNo = Interlocked.Increment(ref currentMsgNo);
            byte curresntSeq = 1;

            var tempBuff = GetBuffer();
            int offset_ = ReserveForPrefix;
            int tempbuffCnt = 0;

            //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
            //tempBuff[offset_++] = (byte)totalNumSeq;
            //tempBuff[offset_++] = curresntSeq++;
            //tempbuffCnt += offset_;
            WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
            curresntSeq++;
            tempbuffCnt += offset_;

            //   ByteCopy.BlockCopy(first.Array, first.Offset, tempBuff, offset_, first.Count);

            fixed (byte* dest = &tempBuff[offset_])
                Buffer.MemoryCopy(first.Array + first.Offset, dest, first.Count, first.Count);

            count -= first.Count;
            offset_ += first.Count;
            tempbuffCnt += first.Count;

            int toCopyMore = FragmentSize - first.Count;
            int secondOffset = second.Offset;
            ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, toCopyMore);

            count -= toCopyMore;
            tempbuffCnt += toCopyMore;
            secondOffset += toCopyMore;
            OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            while (count > FragmentSize)
            {
                offset_ = ReserveForPrefix;
                tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;// this includes reserve
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, FragmentSize);

                secondOffset += FragmentSize;
                count -= FragmentSize;
                tempbuffCnt += FragmentSize;

                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);

            }
            if (count > 0)
            {
                offset_ = ReserveForPrefix;
                tempbuffCnt = 0;

                //PrimitiveEncoder.WriteInt32(tempBuff, ref offset_, msgNo);
                //tempBuff[offset_++] = (byte)totalNumSeq;
                //tempBuff[offset_++] = curresntSeq++;
                //tempbuffCnt += offset_;
                WriteMetadata((ushort)FragmentSize, msgNo, (byte)totalNumSeq, curresntSeq, tempBuff, ref offset_);
                curresntSeq++;
                tempbuffCnt += offset_;

                ByteCopy.BlockCopy(second.Array, secondOffset, tempBuff, offset_, count);

                tempbuffCnt += count;
                OnSend?.Invoke(tempBuff, 0, tempbuffCnt);
            }

        }
        private void WriteMetadata(ushort chunkSize,int messageNo,byte totalNumSeq, byte curresntSeq, byte[] buffer, ref int offset_)
        {
            PrimitiveEncoder.WriteInt32(buffer, ref offset_, messageNo);
            buffer[offset_++] = (byte)totalNumSeq;
            buffer[offset_++] = curresntSeq;
            PrimitiveEncoder.WriteFixedUint16(buffer, offset_, chunkSize);
            offset_ += 2;
        }
        internal void Release()
        {
            OnSend = null;
        }
    }
}
