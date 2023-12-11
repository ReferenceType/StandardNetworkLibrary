
using System;
using System.Runtime.InteropServices;

namespace NetworkLibrary.Components.Crypto.Certificate.Native
{
    internal static class Win32ErrorHelper
    {
        internal static void ThrowExceptionIfGetLastErrorIsNotZero()
        {
            int win32ErrorCode = Marshal.GetLastWin32Error();
            if (0 != win32ErrorCode)
                Marshal.ThrowExceptionForHR(HResultFromWin32(win32ErrorCode));
        }

        private static int HResultFromWin32(int win32ErrorCode)
        {
            if (win32ErrorCode > 0)
                return (int)((((uint)win32ErrorCode) & 0x0000FFFF) | 0x80070000U);
            else return win32ErrorCode;
        }
    }
}
