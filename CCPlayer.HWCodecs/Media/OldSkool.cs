
namespace MediaParsers
{
    public static class OldSkool
    {
        public static byte hibyte(ushort x)
        {
            return (byte)(0xff & (x >> 8));
        }

        public static ushort hiword(ulong x)
        {
            return (ushort)(((ulong)0xffffL) & (x >> 0x10));
        }

        public static byte lobyte(ushort x)
        {
            return (byte)(0xff & x);
        }

        public static ushort loword(ulong x)
        {
            return (ushort)(((ulong)0xffffL) & x);
        }

        public static ulong makelong(ushort lo, ushort hi)
        {
            return (ulong)((hi << 0x10) | lo);
        }

        public static ushort makeword(byte lo, byte hi)
        {
            return (ushort)((hi << 8) | lo);
        }

        public static ulong swaplong(ulong x)
        {
            return makelong(swapword(hiword(x)), swapword(loword(x)));
        }

        public static ushort swapword(ushort x)
        {
            return makeword(hibyte(x), lobyte(x));
        }
    }
}
