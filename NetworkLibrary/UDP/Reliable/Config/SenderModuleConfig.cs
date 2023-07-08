namespace NetworkLibrary.UDP.Reliable.Config
{
    public class SenderModuleConfig
    {
        public int MaxSegmentSize = 64000;
        public int MaxRTO = 3000;
        public int RTOOffset = 20;
        public int MinRTO = 300;
        public int MaxWindowSize = 100_000_000;
        public int MinWindowSize = 64000;

        public bool EnableWindowBump = true;
        public int WindowBumpLowerTreshold = 50;
        public float WindowIncrementMultiplier = 1;

        public float TimeoutWindowTrim = 0.80f;
        public float SoftwindowTrim = 0.95f;
        public float PacingThreshold = 0.5f;

    }
    public class ReceiverModuleConfig
    {
        public bool EnableNacks = true;
        public bool EnablePEriodicSync = true;
        public int SyncTime = 200;
    }
}
