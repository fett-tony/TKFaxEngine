using System.Runtime.InteropServices;

namespace TKFaxEngineTest;

internal static class TurboJpeg
{
    private const string DllName = "libjpeg.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr tjInitDecompress();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int tjDestroy(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int tjDecompressHeader3(
        IntPtr handle,
        byte[] jpegBuf,
        ulong jpegSize,
        out int width,
        out int height,
        out int jpegSubsamp,
        out int jpegColorspace);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr tjGetErrorStr();
}
