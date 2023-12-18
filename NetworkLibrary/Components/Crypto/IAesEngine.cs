namespace NetworkLibrary.Components.Crypto
{
    public enum AesMode
    {
        /// <summary>
        /// No Encyption is used
        /// </summary>
        None,
       
        /// <summary>
        /// IV is generated wirh secure random and send as is.
        /// 16 byte overhead no authentication.
        /// Secure but no authentication
        /// </summary>
        CBCRandomIV,
        /// <summary>
        /// An encoded counter is send then passed through Cipher with different key to generate an IV.
        /// Secure but no authentication
        /// </summary>
        CBCCtrIV,
        /// <summary>
        /// CBC with counter mode and HMAC authentication. This is hw accelerated also on .Net standard.
        /// has more byte overhead than GCM.
        /// </summary>
        CBCCtrIVHMAC,
        /// <summary>
        /// Standard GCM with 14 byte tag. This mode is reccomended to be used
        /// Note that on .net standard 2 this mode is managed without HW acceleration.
        /// </summary>
        GCM
    }
    public interface IAesEngine
    {
        byte[] Decrypt(byte[] bytes);
        byte[] Decrypt(byte[] bytes, int offset, int count);
        int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset);
        byte[] Encrypt(byte[] bytes);
        byte[] Encrypt(byte[] bytes, int offset, int count);
        int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset);
        int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset);
    }
}