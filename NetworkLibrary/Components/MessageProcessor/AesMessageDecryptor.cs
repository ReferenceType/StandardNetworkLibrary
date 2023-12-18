
using System;
using NetworkLibrary.Components.Crypto.Algorithms;

namespace NetworkLibrary.Components
{

    internal class AesMessageDecryptor : IMessageProcessor
    {
        public byte[] Buffer { get; private set; }

        public bool IsHoldingMessage { get; private set; }

        private int offset;
        private int originalOffset;
        public int count;

        byte[] pendingMessage;
        int pendingMessageOffset;
        int pendingRemaining;
        private bool writeHeaderOnflush;

        private AesCbcAlgorithm algorithm;

        public AesMessageDecryptor(AesCbcAlgorithm algorithm)
        {
            this.algorithm = algorithm;
        }

        public bool Flush()
        {
            if (Buffer.Length - offset < pendingRemaining)
            {
                int availableSpace = Buffer.Length - offset;
                int amountToDecrypt = availableSpace - availableSpace % algorithm.DecryptorInputBlockSize;
                int encryptedAmount = algorithm.PartialDecrpytInto(pendingMessage, pendingMessageOffset, amountToDecrypt, Buffer, offset);

                count += amountToDecrypt;
                offset += amountToDecrypt;

                pendingMessageOffset += amountToDecrypt;
                pendingRemaining = pendingMessage.Length - pendingMessageOffset;

                IsHoldingMessage = true;
                return false;
            }

            int encryptedAmount_ = algorithm.DecryptInto(pendingMessage, pendingMessageOffset, pendingRemaining, Buffer, offset);

            count += encryptedAmount_;
            offset += encryptedAmount_;

            pendingMessage = null;
            pendingMessageOffset = 0;
            pendingRemaining = 0;
            IsHoldingMessage = false;
            return true;
        }

        public bool ProcessMessage(byte[] message, int offset_, int count_)
        {
            if (count > Buffer.Length - offset)
            {
                pendingMessage = message;
                pendingMessageOffset = offset_;
                pendingRemaining = count_;

                int availableSpace = Buffer.Length - offset;
                int amountToDecrypt = availableSpace - availableSpace % algorithm.DecryptorInputBlockSize;
                int encryptedAmount = algorithm.PartialDecrpytInto(message, offset_, amountToDecrypt, Buffer, offset);

                count += encryptedAmount;
                offset += encryptedAmount;

                pendingMessageOffset += amountToDecrypt;
                pendingRemaining = count_ - pendingMessageOffset;

                IsHoldingMessage = true;
                return false;
            }
            else
            {
                int amount = algorithm.DecryptInto(message, offset_, message.Length, Buffer, offset);
                count += amount;
                offset += amount;
                return true;
            }
        }

        public bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new InvalidOperationException("You can not process new message before heldover message is fully flushed");
            if (message.Length > Buffer.Length - offset)
            {
                pendingMessage = message;
                int availableSpace = Buffer.Length - offset;
                int amountToDecrypt = availableSpace - availableSpace % algorithm.DecryptorInputBlockSize;
                int encryptedAmount = algorithm.PartialDecrpytInto(message, 0, amountToDecrypt, Buffer, offset);

                count += encryptedAmount;
                offset += encryptedAmount;

                pendingMessageOffset = amountToDecrypt;
                pendingRemaining = pendingMessage.Length - pendingMessageOffset;

                IsHoldingMessage = true;
                return false;
            }
            else
            {
                int Am = algorithm.DecryptInto(message, 0, message.Length, Buffer, offset);
                count += Am;
                offset += Am;
                return true;
            }
        }

        public void SetBuffer(ref byte[] buffer, int offset)
        {
            Buffer = buffer;
            this.offset = offset;
            originalOffset = offset;
            count = 0;
        }

        public void GetBuffer(out byte[] Buffer, out int offset, out int count)
        {
            Buffer = this.Buffer;
            offset = originalOffset;
            count = this.count;
        }

        public void Dispose()
        {
            algorithm.Dispose();
        }
    }
}
