using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Components;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    internal class EphemeralKeyManager
    {
        public Action<MessageFlags,byte[], int, int> SendData;

        DiffieHellman df =  new DiffieHellman();

        internal ConcurrentDictionary<byte,ConcurrentAesAlgorithm> keyStore = new ConcurrentDictionary<byte, ConcurrentAesAlgorithm>();

        byte[] innerBuffer = new byte[1024];

        byte currKeyNumber = 0; 
        internal byte CurrKeyNumber = 0;
        private readonly int rotateEveryMs;

        public EphemeralKeyManager(ConcurrentAesAlgorithm initialKey, int rotateEveryMs = 1000 )
        {
            keyStore[0] = initialKey;
            this.rotateEveryMs = rotateEveryMs;
            TimedKeyExchange();
        }

        private void TimedKeyExchange()
        {
            if (rotateEveryMs < 0)
                return;
            TimerService.RegisterTimer(Guid.NewGuid(), rotateEveryMs, () =>
            {
                RequestKeyExchange();
            });
        }

        public void HandleMessage(MessageFlags flag, byte[] buffer, int offset, int count )
        {
     
            switch (flag)
            {
                case MessageFlags.KeyExchange:
                    HandleKeyExchangeReq(buffer, offset, count);
                    break;

                case MessageFlags.KeyExchangeAck:
                    HandleKeyExchangeAck(buffer, offset, count);
                    break;

                case MessageFlags.KeyExchangeFin:
                    HandleFinalize();
                    break;
            }
        }


        //[Alice]
        private void RequestKeyExchange()
        {
            df =  new DiffieHellman();
            byte[] myPublic = df.GetPublicKey();
            SendData?.Invoke(MessageFlags.KeyExchange, myPublic, 0, myPublic.Length);
            TimedKeyExchange();
        }

        //[Bob]
        private void HandleKeyExchangeReq(byte[] buffer, int offset, int count)
        {
            currKeyNumber++;

            df = new DiffieHellman();
            var sharedSecret = df.CalculateSharedSecret(ByteCopy.ToArray(buffer, offset, count));
            var privKey = HKDFLite.DeriveKey(sharedSecret,outputLength:16);
            var algo = new ConcurrentAesAlgorithm(privKey, AesMode.GCM);
            keyStore[currKeyNumber] =  algo;

            byte[] myPublic = df.GetPublicKey();
            SendData?.Invoke(MessageFlags.KeyExchangeAck, myPublic, 0, myPublic.Length);
        }

        //[Alice]
        private void HandleKeyExchangeAck(byte[] buffer, int offset, int count)
        {

            currKeyNumber++;

            var sharedSecret = df.CalculateSharedSecret(ByteCopy.ToArray(buffer, offset, count));
            var privKey = HKDFLite.DeriveKey(sharedSecret, outputLength: 16);
            var algo = new ConcurrentAesAlgorithm(privKey, AesMode.GCM);
            keyStore[currKeyNumber] = algo;

            SendData?.Invoke(MessageFlags.KeyExchangeFin, innerBuffer,0,1);

            CurrKeyNumber = currKeyNumber;
        }

        //[Bob]
        private void HandleFinalize()
        {
            CurrKeyNumber = currKeyNumber;
        }

       
        public bool GetAlgorithm(byte keyNum, out ConcurrentAesAlgorithm algo)
        {
            return keyStore.TryGetValue(keyNum, out algo);
        }

    }
}
