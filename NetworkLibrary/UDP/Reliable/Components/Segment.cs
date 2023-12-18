namespace NetworkLibrary.UDP.Reliable.Components
{
    public readonly struct Segment
    {
        public readonly byte[] Array;
        public readonly int Offset;
        public readonly int Count;

        public Segment(byte[] array, int offset, int count)
        {
            Array = array;
            Offset = offset;
            Count = count;
        }
        public Segment(byte[] array)
        {
            Array = array;
            Offset = 0;
            Count = array.Length;
        }
    }
    internal readonly unsafe struct SegmentUnsafe
    {
        public readonly byte* Array;
        public readonly int Offset;
        public readonly int Count;

        public SegmentUnsafe(byte* array, int offset, int count)
        {
            Array = array;
            Offset = offset;
            Count = count;
        }
    }
}
