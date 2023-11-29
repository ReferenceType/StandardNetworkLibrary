using NetworkLibrary.Components.Crypto.Algorithms;

namespace NetworkLibrary.Components
{

    public class AesMessageEncryptor : IMessageProcessor
    {
        public byte[] Buffer { get; private set; }
        private int offset;
        public int count;

        public bool IsHoldingMessage { get; private set; }

        byte[] pendingMessage;
        int pendingMessageOffset;
        int pendingRemaining;
        private bool writeHeaderOnflush;
        private AesCbcAlgorithm algorithm;
        private int originalOffset;

        public AesMessageEncryptor(AesCbcAlgorithm algorithm)
        {
            this.algorithm = algorithm;
        }

        public void SetBuffer(ref byte[] buffer, int offset)
        {
            Buffer = buffer;
            this.offset = offset;
            originalOffset = offset;
            count = 0;
        }
        public bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new System.InvalidOperationException("You can not process new message before heldover message is fully flushed");
            if (Buffer.Length - offset > algorithm.EncryptorInputBlockSize * 2 + 4)
            {
                int output = algorithm.GetEncriptorOutputSize(message.Length);
                Buffer[offset++] = (byte)output;
                Buffer[offset++] = (byte)(output >> 8);
                Buffer[offset++] = (byte)(output >> 16);
                Buffer[offset++] = (byte)(output >> 24);
                count += 4;
            }
            else
            {
                pendingMessage = message;
                pendingMessageOffset = 0;
                pendingRemaining = message.Length;
                writeHeaderOnflush = true;
                IsHoldingMessage = true;
                return false;
            }

            if (algorithm.GetEncriptorOutputSize(message.Length) > Buffer.Length - offset)
            {
                int availableSpace = Buffer.Length - offset;
                pendingMessage = message;

                int amountToEncrypt = (availableSpace - (availableSpace % algorithm.DecryptorOutputBlockSize)) - algorithm.DecryptorInputBlockSize;
                int enct = algorithm.PartialEncrpytInto(message, 0, amountToEncrypt, Buffer, offset);

                offset += enct;
                count += enct;

                pendingMessageOffset = amountToEncrypt;
                pendingRemaining = pendingMessage.Length - pendingMessageOffset;

                IsHoldingMessage = true;
                return false;
            }
            else
            {
                int amountEnc = algorithm.EncryptInto(message, 0, message.Length, Buffer, offset);
                offset += amountEnc;
                count += amountEnc;
                return true;
            }
        }

        public bool Flush()
        {
            if (writeHeaderOnflush)
            {
                writeHeaderOnflush = false;
                int output = algorithm.GetEncriptorOutputSize(pendingMessage.Length);
                Buffer[offset++] = (byte)output;
                Buffer[offset++] = (byte)(output >> 8);
                Buffer[offset++] = (byte)(output >> 16);
                Buffer[offset++] = (byte)(output >> 24);
                count += 4;
            }
            if (Buffer.Length - offset <= algorithm.GetEncriptorOutputSize(pendingRemaining))
            {
                int availableSpace = Buffer.Length - offset;
                int amountToEncrypt = (availableSpace - availableSpace % algorithm.DecryptorOutputBlockSize) - algorithm.DecryptorInputBlockSize;
                int enct = algorithm.PartialEncrpytInto(pendingMessage, pendingMessageOffset, amountToEncrypt, Buffer, offset);

                offset += enct;
                count += enct;

                pendingMessageOffset += amountToEncrypt;
                pendingRemaining = pendingMessage.Length - pendingMessageOffset;

                return false;
            }

            int encryptedAmount_ = algorithm.EncryptInto(pendingMessage, pendingMessageOffset, pendingRemaining, Buffer, offset);
            count += encryptedAmount_;
            offset += encryptedAmount_;

            pendingMessage = null;
            pendingRemaining = 0;
            pendingMessageOffset = 0;
            IsHoldingMessage = false;

            return true;
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
