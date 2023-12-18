
using NetworkLibrary.Components.Crypto.ManagedGCM;
using System;

public class AesGcmManaged
{
    private const int BlockSize = 16;

    private readonly AesCipher cipher;
    private readonly Tables4kGcmMultiplier multiplier;
    private BasicGcmExponentiator exp;

    // These fields are set by Init and not modified by processing
    private bool forEncryption;
    private bool initialised;
    private int macSize;
    private byte[] lastKey;
    private byte[] nonce;
    private byte[] initialAssociatedText;
    private byte[] H;
    private byte[] J0;

    // These fields are modified during processing
    private byte[] bufBlock;
    private byte[] macBlock;
    private byte[] S, S_at, S_atPre;
    private byte[] counter;
    private uint blocksRemaining;
    private int bufOff;
    private ulong totalLength;
    private byte[] atBlock;
    private int atBlockPos;
    private ulong atLength;
    private ulong atLengthPre;



    public AesGcmManaged(AesCipher c, int macSize, byte[] key, bool forEncryption)
    {
        if (c.GetBlockSize() != BlockSize)
            throw new ArgumentException("cipher required with a block size of " + BlockSize + ".");
        this.cipher = c;
        this.multiplier = new Tables4kGcmMultiplier();
        this.macSize = macSize;
        lastKey = key;
        cipher.Init(true, key);

        this.H = new byte[BlockSize];
        cipher.ProcessBlock(H, 0, H, 0);

        // if keyParam is null we're reusing the last key and the multiplier doesn't need re-init
        multiplier.Init(H);
        exp = null;
        this.initialised = true;

        this.forEncryption = forEncryption;
        int bufLength = forEncryption ? BlockSize : (BlockSize + macSize);
        this.bufBlock = new byte[bufLength];

        this.S = new byte[BlockSize];
        this.S_at = new byte[BlockSize];
        this.S_atPre = new byte[BlockSize];
        this.atBlock = new byte[BlockSize];
        this.J0 = new byte[BlockSize];

    }

    public virtual string AlgorithmName
    {
        get { return cipher.AlgorithmName + "/GCM"; }
    }

    public AesCipher GetUnderlyingCipher()
    {
        return cipher;
    }

    public virtual int GetBlockSize()
    {
        return BlockSize;
    }
    static readonly byte[] zeros = new byte[BlockSize];
    private void ClearArray(byte[] array)
    {
        Buffer.BlockCopy(zeros, 0, array, 0, BlockSize);
    }
    /// <remarks>
    /// MAC sizes from 32 bits to 128 bits (must be a multiple of 8) are supported. The default is 128 bits.
    /// Sizes less than 96 are not recommended, but are supported for specialized applications.
    /// </remarks>
    public virtual void Init(
        byte[] nonce)
    {
        this.macBlock = null;
        ClearArray(J0);

        if (nonce.Length == 12)
        {
            Array.Copy(nonce, 0, J0, 0, nonce.Length);
            this.J0[BlockSize - 1] = 0x01;
        }
        else
        {
            gHASH(J0, nonce, nonce.Length);
            byte[] X = new byte[BlockSize];
            GcmUtilities.UInt64_To_BE((ulong)nonce.Length * 8UL, X, 8);
            gHASHBlock(J0, X);
        }

        ClearArray(S);
        ClearArray(S_at);
        ClearArray(S_atPre);
        ClearArray(atBlock);
        ClearArray(bufBlock);
        this.atBlockPos = 0;
        this.atLength = 0;
        this.atLengthPre = 0;
        this.counter = GcmUtilities.Clone(J0);
        this.blocksRemaining = uint.MaxValue - 1; // page 8, len(P) <= 2^39 - 256, 1 block used by tag
        this.bufOff = 0;
        this.totalLength = 0;

        initialised = true;
    }

    public virtual byte[] GetMac()
    {
        return macBlock == null
            ? new byte[macSize]
            : GcmUtilities.Clone(macBlock);
    }

    public virtual int GetOutputSize(
        int len)
    {
        int totalData = len + bufOff;

        if (forEncryption)
        {
            return totalData + macSize;
        }

        return totalData < macSize ? 0 : totalData - macSize;
    }

    public virtual int GetUpdateOutputSize(
        int len)
    {
        int totalData = len + bufOff;
        if (!forEncryption)
        {
            if (totalData < macSize)
            {
                return 0;
            }
            totalData -= macSize;
        }
        return totalData - totalData % BlockSize;
    }

    public virtual void ProcessAadByte(byte input)
    {
        CheckStatus();

        atBlock[atBlockPos] = input;
        if (++atBlockPos == BlockSize)
        {
            // Hash each block as it fills
            gHASHBlock(S_at, atBlock);
            atBlockPos = 0;
            atLength += BlockSize;
        }
    }

    public virtual void ProcessAadBytes(byte[] inBytes, int inOff, int len)
    {
        CheckStatus();

        for (int i = 0; i < len; ++i)
        {
            atBlock[atBlockPos] = inBytes[inOff + i];
            if (++atBlockPos == BlockSize)
            {
                // Hash each block as it fills
                gHASHBlock(S_at, atBlock);
                atBlockPos = 0;
                atLength += BlockSize;
            }
        }
    }

    private void InitCipher()
    {
        if (atLength > 0)
        {
            Array.Copy(S_at, 0, S_atPre, 0, BlockSize);
            atLengthPre = atLength;
        }

        // Finish hash for partial AAD block
        if (atBlockPos > 0)
        {
            gHASHPartial(S_atPre, atBlock, 0, atBlockPos);
            atLengthPre += (uint)atBlockPos;
        }

        if (atLengthPre > 0)
        {
            Array.Copy(S_atPre, 0, S, 0, BlockSize);
        }
    }

    public virtual int ProcessByte(
        byte input,
        byte[] output,
        int outOff)
    {
        CheckStatus();

        bufBlock[bufOff] = input;
        if (++bufOff == bufBlock.Length)
        {
            ProcessBlock(bufBlock, 0, output, outOff);
            if (forEncryption)
            {
                bufOff = 0;
            }
            else
            {
                Array.Copy(bufBlock, BlockSize, bufBlock, 0, macSize);
                bufOff = macSize;
            }
            return BlockSize;
        }
        return 0;
    }

    public virtual int ProcessBytes(
        byte[] input,
        int inOff,
        int len,
        byte[] output,
        int outOff)
    {
        CheckStatus();


        int resultLen = 0;

        if (forEncryption)
        {
            if (bufOff != 0)
            {
                while (len > 0)
                {
                    --len;
                    bufBlock[bufOff] = input[inOff++];
                    if (++bufOff == BlockSize)
                    {
                        ProcessBlock(bufBlock, 0, output, outOff);
                        bufOff = 0;
                        resultLen += BlockSize;
                        break;
                    }
                }
            }

            while (len >= BlockSize)
            {
                ProcessBlock(input, inOff, output, outOff + resultLen);
                inOff += BlockSize;
                len -= BlockSize;
                resultLen += BlockSize;
            }

            if (len > 0)
            {
                Array.Copy(input, inOff, bufBlock, 0, len);
                bufOff = len;
            }
        }
        else
        {
            for (int i = 0; i < len; ++i)
            {
                bufBlock[bufOff] = input[inOff + i];
                if (++bufOff == bufBlock.Length)
                {
                    ProcessBlock(bufBlock, 0, output, outOff + resultLen);
                    Array.Copy(bufBlock, BlockSize, bufBlock, 0, macSize);
                    bufOff = macSize;
                    resultLen += BlockSize;
                }
            }
        }

        return resultLen;
    }

    public int DoFinal(byte[] output, int outOff)
    {
        CheckStatus();

        if (totalLength == 0)
        {
            InitCipher();
        }

        int extra = bufOff;

        if (!forEncryption)
        {
            extra -= macSize;
        }


        if (extra > 0)
        {
            ProcessPartial(bufBlock, 0, extra, output, outOff);
        }

        atLength += (uint)atBlockPos;

        if (atLength > atLengthPre)
        {
            /*
             *  Some AAD was sent after the cipher started. We determine the difference b/w the hash value
             *  we actually used when the cipher started (S_atPre) and the final hash value calculated (S_at).
             *  Then we carry this difference forward by multiplying by H^c, where c is the number of (full or
             *  partial) cipher-text blocks produced, and adjust the current hash.
             */

            // Finish hash for partial AAD block
            if (atBlockPos > 0)
            {
                gHASHPartial(S_at, atBlock, 0, atBlockPos);
            }

            // Find the difference between the AAD hashes
            if (atLengthPre > 0)
            {
                GcmUtilities.Xor16(S_at, S_atPre);
            }

            // Number of cipher-text blocks produced
            long c = (long)(((totalLength * 8) + 127) >> 7);

            // Calculate the adjustment factor
            byte[] H_c = new byte[16];
            if (exp == null)
            {
                exp = new BasicGcmExponentiator();
                exp.Init(H);
            }
            exp.ExponentiateX(c, H_c);

            // Carry the difference forward
            GcmUtilities.Multiply(S_at, H_c);

            // Adjust the current hash
            GcmUtilities.Xor(S, S_at);
        }

        // Final gHASH
        byte[] X = new byte[BlockSize];
        GcmUtilities.UInt64_To_BE(atLength * 8UL, X, 0);
        GcmUtilities.UInt64_To_BE(totalLength * 8UL, X, 8);

        gHASHBlock(S, X);

        // T = MSBt(GCTRk(J0,S))
        byte[] tag = new byte[BlockSize];
        cipher.ProcessBlock(J0, 0, tag, 0);
        GcmUtilities.Xor16(tag, S);

        int resultLen = extra;

        // We place into macBlock our calculated value for T
        this.macBlock = new byte[macSize];
        Array.Copy(tag, 0, macBlock, 0, macSize);

        if (forEncryption)
        {
            // Append T to the message
            Array.Copy(tag, 0, output, outOff + bufOff, macSize);
            resultLen += macSize;
        }
        else
        {
            // Retrieve the T value from the message and compare to calculated one
            //byte[] msgMac = new byte[macSize];
            // Array.Copy(bufBlock, extra, msgMac, 0, macSize);
            if (!IsEqual(macBlock, bufBlock, extra))
                throw new InvalidOperationException("mac check in GCM failed");
        }

        Reset(false);

        return resultLen;
    }
    bool IsEqual(byte[] t1, byte[] t2, int t2off)
    {

        unsafe
        {
            fixed (void* p1 = &t1[0])
            {
                fixed (byte* p2 = &t2[t2off])
                {
                    if (*(long*)p1 != *(long*)p2)
                        return false;
                }
            }
            fixed (void* p1 = &t1[8])
            {
                fixed (byte* p2 = &t2[t2off + 8])
                {
                    if (*(int*)p1 != *(int*)p2)
                        return false;
                }
            }
        }
        for (int i = 12; i < t1.Length; i++)
        {
            if (t1[i] != t2[i + t2off])
                return false;
        }
        return true;
    }

    public virtual void Reset()
    {
        Reset(true);
    }

    private void Reset(
        bool clearMac)
    {
        cipher.Reset();

        // note: we do not reset the nonce.

        ClearArray(S);
        ClearArray(S_at);

        ClearArray(S_atPre);
        ClearArray(atBlock);
        atBlockPos = 0;
        atLength = 0;
        atLengthPre = 0;
        counter = GcmUtilities.Clone(J0);
        blocksRemaining = uint.MaxValue - 1;
        bufOff = 0;
        totalLength = 0;

        if (bufBlock != null)
        {
            ClearArray(bufBlock);
        }

        if (clearMac)
        {
            macBlock = null;
        }

        if (forEncryption)
        {
            initialised = false;
        }
        else
        {
            if (initialAssociatedText != null)
            {
                ProcessAadBytes(initialAssociatedText, 0, initialAssociatedText.Length);
            }
        }
    }
    private void ProcessBlock(byte[] buf, int bufOff, byte[] output, int outOff)
    {
        //  Check.OutputLength(output, outOff, BlockSize, "Output buffer too short");

        if (totalLength == 0)
        {
            InitCipher();
        }

        //byte[] ctrBlock = new byte[BlockSize];
        GetNextCtrBlock(output, outOff);

        if (forEncryption)
        {
            GcmUtilities.Xor16(output, outOff, buf, bufOff);
            gHASHBlock(S, output, outOff);
            //Array.Copy(ctrBlock, 0, output, outOff, BlockSize);
        }
        else
        {
            gHASHBlock(S, buf, bufOff);
            GcmUtilities.Xor16(output, outOff, buf, bufOff);
        }

        totalLength += BlockSize;
    }

    private void ProcessPartial(byte[] buf, int off, int len, byte[] output, int outOff)
    {
        byte[] ctrBlock = new byte[BlockSize];
        GetNextCtrBlock(ctrBlock, 0);

        if (forEncryption)
        {
            GcmUtilities.Xor(buf, off, ctrBlock, 0, len);
            gHASHPartial(S, buf, off, len);
        }
        else
        {
            gHASHPartial(S, buf, off, len);
            GcmUtilities.Xor(buf, off, ctrBlock, 0, len);
        }

        Array.Copy(buf, off, output, outOff, len);
        totalLength += (uint)len;
    }

    private void gHASH(byte[] Y, byte[] b, int len)
    {
        for (int pos = 0; pos < len; pos += BlockSize)
        {
            int num = System.Math.Min(len - pos, BlockSize);
            gHASHPartial(Y, b, pos, num);
        }
    }

    private void gHASHBlock(byte[] Y, byte[] b)
    {
        GcmUtilities.Xor16(Y, 0, b, 0);
        multiplier.MultiplyH(Y);
    }

    private void gHASHBlock(byte[] Y, byte[] b, int off)
    {
        GcmUtilities.Xor16(Y, 0, b, off);
        multiplier.MultiplyH(Y);
    }

    private void gHASHPartial(byte[] Y, byte[] b, int off, int len)
    {
        GcmUtilities.Xor(Y, b, off, len);
        multiplier.MultiplyH(Y);
    }

    private void GetNextCtrBlock(byte[] block, int blockOffset)
    {
        if (blocksRemaining == 0)
            throw new InvalidOperationException("Attempt to process too many blocks");

        blocksRemaining--;

        uint c = 1;
        c += counter[15]; counter[15] = (byte)c; c >>= 8;
        c += counter[14]; counter[14] = (byte)c; c >>= 8;
        c += counter[13]; counter[13] = (byte)c; c >>= 8;
        c += counter[12]; counter[12] = (byte)c;

        cipher.ProcessBlock(counter, 0, block, blockOffset);
    }

    private void CheckStatus()
    {
        if (!initialised)
        {
            if (forEncryption)
            {
                throw new InvalidOperationException("GCM cipher cannot be reused for encryption");
            }
            throw new InvalidOperationException("GCM cipher needs to be initialised");
        }
    }


}
