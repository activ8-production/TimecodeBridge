using System.Runtime.InteropServices;

namespace TimecodeBridge.Native;

/// <summary>
/// P/Invoke declarations for libltc native library.
/// These are resolved at runtime; the application will build even without libltc.dll present.
/// </summary>
internal static class LibLtcInterop
{
    private const string LibName = "libltc";

    /// <summary>
    /// Create a new LTC decoder.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ltc_decoder_create(int apv, int queueSize);

    /// <summary>
    /// Feed audio samples to the decoder.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ltc_decoder_write(IntPtr decoder, byte[] buf, nint size, long posinfo);

    /// <summary>
    /// Feed audio samples using pinned memory to prevent GC from moving the buffer.
    /// </summary>
    internal static void ltc_decoder_write_safe(IntPtr decoder, byte[] buf, int size, long posinfo)
    {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            ltc_decoder_write(decoder, buf, (nint)size, posinfo);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Read a decoded LTC frame from the decoder queue.
    /// Uses IntPtr to avoid struct layout issues with stack-allocated out parameter.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ltc_decoder_read(IntPtr decoder, IntPtr frame);

    /// <summary>
    /// Free the decoder and associated resources.
    /// </summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ltc_decoder_free(IntPtr decoder);
}

/// <summary>
/// Helper to extract timecode values from a raw LTCFrameExt buffer.
/// LTC encodes BCD digits in specific bit positions within the 80-bit frame.
///
/// Native LTCFrame layout (C bitfields of unsigned int, little-endian):
///   byte0 bits 0-3: frame units,  bits 4-7: user1
///   byte1 bits 0-1: frame tens,   bit 2: drop frame flag,  bit 3: col_frame,  bits 4-7: user2
///   byte2 bits 0-3: seconds units, bits 4-7: user3
///   byte3 bits 0-2: seconds tens,  bit 3: biphase,  bits 4-7: user4
///   byte4 bits 0-3: minutes units, bits 4-7: user5
///   byte5 bits 0-2: minutes tens,  bit 3: bgf0,  bits 4-7: user6
///   byte6 bits 0-3: hours units,   bits 4-7: user7
///   byte7 bits 0-1: hours tens,    bit 2: bgf1,  bit 3: bgf2,  bits 4-7: user8
/// </summary>
internal static class LtcFrameHelper
{
    // Generous allocation size for LTCFrameExt to account for any version differences
    internal const int FrameExtBufferSize = 128;

    internal static (int hours, int minutes, int seconds, int frames, bool dropFrame) Extract(IntPtr framePtr)
    {
        // Read the first 8 bytes of the LTCFrame (timecode data)
        int frameUnits = Marshal.ReadByte(framePtr, 0) & 0x0F;
        int frameTens = Marshal.ReadByte(framePtr, 1) & 0x03;
        int frames = frameTens * 10 + frameUnits;

        int secUnits = Marshal.ReadByte(framePtr, 2) & 0x0F;
        int secTens = Marshal.ReadByte(framePtr, 3) & 0x07;
        int seconds = secTens * 10 + secUnits;

        int minUnits = Marshal.ReadByte(framePtr, 4) & 0x0F;
        int minTens = Marshal.ReadByte(framePtr, 5) & 0x07;
        int minutes = minTens * 10 + minUnits;

        int hrUnits = Marshal.ReadByte(framePtr, 6) & 0x0F;
        int hrTens = Marshal.ReadByte(framePtr, 7) & 0x03;
        int hours = hrTens * 10 + hrUnits;

        bool dropFrame = (Marshal.ReadByte(framePtr, 1) & 0x04) != 0;

        return (hours, minutes, seconds, frames, dropFrame);
    }
}
