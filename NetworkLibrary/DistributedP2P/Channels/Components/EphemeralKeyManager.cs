using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    internal class EphemeralKeyManager
    {
        public Action<MessageFlags, byte[], int, int> SendData;

        DiffieHellman df = new DiffieHellman();

        internal ConcurrentDictionary<byte, ConcurrentAesAlgorithm> keyStore = new ConcurrentDictionary<byte, ConcurrentAesAlgorithm>();

        byte[] innerBuffer = new byte[1024];

        byte currKeyNumber = 0;
        internal byte CurrKeyNumber = 0;
        private int keyRotationPeriod;
        private bool closed = false;
        RandomNumberGenerator rng = RandomNumberGenerator.Create();
        Guid timerGuid = Guid.NewGuid();
        public EphemeralKeyManager(ConcurrentAesAlgorithm initialKey, int keyRotationPeriod = 1000)
        {
            keyStore[0] = initialKey;
            this.keyRotationPeriod = keyRotationPeriod;
            TimedKeyExchange();
        }

        private void TimedKeyExchange()
        {
            if (keyRotationPeriod < 0)
                return;
            TimerService.RegisterTimer(timerGuid, keyRotationPeriod, () =>
            {
                RequestKeyExchange();
            });
        }

        public void HandleMessage(MessageFlags flag, byte[] buffer, int offset, int count)
        {

            if (closed) return;

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
            if (closed) return;

            df = new DiffieHellman();
            byte[] myPublic = df.GetPublicKey();
            SendData?.Invoke(MessageFlags.KeyExchange, myPublic, 0, myPublic.Length);

        }

        //[Bob]
        private void HandleKeyExchangeReq(byte[] buffer, int offset, int count)
        {
            if (closed) return;

            currKeyNumber++;

            df = new DiffieHellman();
            var sharedSecret = df.CalculateSharedSecret(ByteCopy.ToArray(buffer, offset, count));
            var privKey = HKDFLite.DeriveKey(sharedSecret, outputLength: 16);
            var algo = new ConcurrentAesAlgorithm(privKey, AesMode.GCM);
            keyStore[currKeyNumber] = algo;

            byte[] myPublic = df.GetPublicKey();
            SendData?.Invoke(MessageFlags.KeyExchangeAck, myPublic, 0, myPublic.Length);
        }

        //[Alice]
        private void HandleKeyExchangeAck(byte[] buffer, int offset, int count)
        {
            if (closed) return;

            currKeyNumber++;

            var sharedSecret = df.CalculateSharedSecret(ByteCopy.ToArray(buffer, offset, count));
            var privKey = HKDFLite.DeriveKey(sharedSecret, outputLength: 16);
            var algo = new ConcurrentAesAlgorithm(privKey, AesMode.GCM);
            keyStore[currKeyNumber] = algo;

            rng.GetBytes(innerBuffer, 0, 32);
            SendData?.Invoke(MessageFlags.KeyExchangeFin, innerBuffer, 0, 32);

            CurrKeyNumber = currKeyNumber;
            TimedKeyExchange();
        }

        //[Bob]
        private void HandleFinalize()
        {
            Console.WriteLine("KeyExchanged");
            if (closed) return;

            CurrKeyNumber = currKeyNumber;
        }


        public bool GetAlgorithm(byte keyNum, out ConcurrentAesAlgorithm algo)
        {
            return keyStore.TryGetValue(keyNum, out algo);
        }

        internal void Close()
        {
            closed = true;
        }

        internal void SetKeyRotationTime(int timeMs)
        {
            TimerService.CancelTimeout(timerGuid);
            keyRotationPeriod = timeMs;
            TimedKeyExchange();
        }
    }
}
