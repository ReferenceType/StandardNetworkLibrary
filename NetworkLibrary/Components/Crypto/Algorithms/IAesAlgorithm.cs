namespace NetworkLibrary.Components.Crypto.Algorithms
{
    public interface IAesAlgorithm
    {
        int DecryptorInputBlockSize { get; }
        int DecryptorOutputBlockSize { get; }
        int EncryptorInputBlockSize { get; }
        int EncryptorOutputBlockSize { get; }

        byte[] Decrypt(byte[] message);
        byte[] Decrypt(byte[] buffer, int offset, int count);
        int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset);
        void Dispose();
        byte[] Encrypt(byte[] message);
        byte[] Encrypt(byte[] buffer, int offset, int count);
        int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset);
        int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset);
        int GetEncriptorOutputSize(int inputSize);

    }
}