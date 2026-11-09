using System.Runtime.InteropServices;

namespace TKFaxEngine;

internal static class TurboJpeg
{
    private const string DllName = "libjpeg.dll";

    private const int TJSAMP_444 = 0;
    private const int TJSAMP_420 = 2;
    private const int TJSAMP_GRAY = 3;
    private const int TJPF_RGB = 0;
    private const int TJPF_GRAY = 6;
    private const int TJCS_GRAY = 2;

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr tjInitCompress();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr tjInitDecompress();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjDestroy(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjDecompressHeader3(
        IntPtr handle,
        byte[] jpegBuf,
        uint jpegSize,
        out int width,
        out int height,
        out int jpegSubsamp,
        out int jpegColorspace);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjCompress2(
        IntPtr handle,
        byte[] srcBuf,
        int width,
        int pitch,
        int height,
        int pixelFormat,
        out IntPtr jpegBuf,
        ref uint jpegSize,
        int jpegSubsamp,
        int jpegQual,
        int flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjCompressFromYUVPlanes(
        IntPtr handle,
        [In] IntPtr[] srcPlanes,
        int width,
        [In] int[] strides,
        int height,
        int subsamp,
        out IntPtr jpegBuf,
        ref uint jpegSize,
        int jpegQual,
        int flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjDecompress2(
        IntPtr handle,
        byte[] jpegBuf,
        uint jpegSize,
        byte[] dstBuf,
        int width,
        int pitch,
        int height,
        int pixelFormat,
        int flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjDecompressToYUVPlanes(
        IntPtr handle,
        byte[] jpegBuf,
        uint jpegSize,
        [In, Out] IntPtr[] dstPlanes,
        int width,
        [In] int[] strides,
        int height,
        int flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjPlaneHeight(int componentId, int height, int subsamp);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int tjPlaneWidth(int componentId, int width, int subsamp);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void tjFree(IntPtr buffer);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr tjGetErrorStr2(IntPtr handle);

    internal static byte[] EncodePacked(
        int width,
        int height,
        int components,
        byte[] pixels,
        int quality,
        bool noSubsampling)
    {
        ValidateImage(width, height, components, pixels);
        IntPtr handle = tjInitCompress();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("tjInitCompress failed.");

        IntPtr jpegBuffer = IntPtr.Zero;
        try
        {
            uint jpegSize = 0;
            int pixelFormat = components == 1 ? TJPF_GRAY : TJPF_RGB;
            int subsampling = components == 1
                ? TJSAMP_GRAY
                : noSubsampling ? TJSAMP_444 : TJSAMP_420;
            int pitch = checked(width * components);
            if (tjCompress2(
                    handle,
                    pixels,
                    width,
                    pitch,
                    height,
                    pixelFormat,
                    out jpegBuffer,
                    ref jpegSize,
                    subsampling,
                    Math.Clamp(quality, 1, 100),
                    0) != 0)
            {
                throw CreateException(handle, "tjCompress2");
            }

            return CopyNativeBuffer(jpegBuffer, jpegSize);
        }
        finally
        {
            if (jpegBuffer != IntPtr.Zero)
                tjFree(jpegBuffer);
            _ = tjDestroy(handle);
        }
    }

    internal static byte[] EncodeYuvComponents(
        int width,
        int height,
        int components,
        byte[] pixels,
        int quality,
        bool noSubsampling)
    {
        ValidateImage(width, height, components, pixels);
        if (components == 1)
            return EncodePacked(width, height, 1, pixels, quality, noSubsampling: true);

        int subsampling = noSubsampling ? TJSAMP_444 : TJSAMP_420;
        byte[][] planes = CreateYuvPlanes(width, height, pixels, subsampling);
        IntPtr[] planePointers = new IntPtr[3];
        int[] strides = new int[3];
        IntPtr handle = IntPtr.Zero;
        IntPtr jpegBuffer = IntPtr.Zero;

        try
        {
            for (int component = 0; component < 3; component++)
            {
                int planeWidth = tjPlaneWidth(component, width, subsampling);
                int planeHeight = tjPlaneHeight(component, height, subsampling);
                if (planeWidth <= 0 || planeHeight <= 0)
                    throw new InvalidOperationException("Invalid TurboJPEG YUV plane dimensions.");

                strides[component] = planeWidth;
                planePointers[component] = Marshal.AllocHGlobal(checked(planeWidth * planeHeight));
                Marshal.Copy(planes[component], 0, planePointers[component], planes[component].Length);
            }

            handle = tjInitCompress();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("tjInitCompress failed.");

            uint jpegSize = 0;
            if (tjCompressFromYUVPlanes(
                    handle,
                    planePointers,
                    width,
                    strides,
                    height,
                    subsampling,
                    out jpegBuffer,
                    ref jpegSize,
                    Math.Clamp(quality, 1, 100),
                    0) != 0)
            {
                throw CreateException(handle, "tjCompressFromYUVPlanes");
            }

            return CopyNativeBuffer(jpegBuffer, jpegSize);
        }
        finally
        {
            if (jpegBuffer != IntPtr.Zero)
                tjFree(jpegBuffer);
            if (handle != IntPtr.Zero)
                _ = tjDestroy(handle);
            foreach (IntPtr pointer in planePointers)
            {
                if (pointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(pointer);
            }
        }
    }

    internal static byte[] DecodePacked(
        byte[] jpegData,
        out int width,
        out int height,
        out int components)
    {
        ArgumentNullException.ThrowIfNull(jpegData);
        if (jpegData.Length == 0)
            throw new ArgumentException("JPEG data is empty.", nameof(jpegData));

        IntPtr handle = tjInitDecompress();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("tjInitDecompress failed.");

        try
        {
            ReadHeader(handle, jpegData, out width, out height, out _, out int colorspace);
            components = colorspace == TJCS_GRAY ? 1 : 3;
            int pixelFormat = components == 1 ? TJPF_GRAY : TJPF_RGB;
            int pitch = checked(width * components);
            byte[] pixels = new byte[checked(pitch * height)];
            if (tjDecompress2(
                    handle,
                    jpegData,
                    checked((uint)jpegData.Length),
                    pixels,
                    width,
                    pitch,
                    height,
                    pixelFormat,
                    0) != 0)
            {
                throw CreateException(handle, "tjDecompress2");
            }

            return pixels;
        }
        finally
        {
            _ = tjDestroy(handle);
        }
    }

    internal static byte[] DecodeYuvComponents(
        byte[] jpegData,
        out int width,
        out int height,
        out int components)
    {
        ArgumentNullException.ThrowIfNull(jpegData);
        if (jpegData.Length == 0)
            throw new ArgumentException("JPEG data is empty.", nameof(jpegData));

        IntPtr handle = tjInitDecompress();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("tjInitDecompress failed.");

        IntPtr[] planePointers = new IntPtr[3];
        try
        {
            ReadHeader(handle, jpegData, out width, out height, out int subsampling, out int colorspace);
            components = colorspace == TJCS_GRAY ? 1 : 3;
            int planeCount = components == 1 ? 1 : 3;
            int[] strides = new int[planeCount];
            byte[][] planes = new byte[planeCount][];

            for (int component = 0; component < planeCount; component++)
            {
                int planeWidth = tjPlaneWidth(component, width, subsampling);
                int planeHeight = tjPlaneHeight(component, height, subsampling);
                if (planeWidth <= 0 || planeHeight <= 0)
                    throw new InvalidDataException("Invalid JPEG YUV plane dimensions.");

                strides[component] = planeWidth;
                planes[component] = new byte[checked(planeWidth * planeHeight)];
                planePointers[component] = Marshal.AllocHGlobal(planes[component].Length);
            }

            if (tjDecompressToYUVPlanes(
                    handle,
                    jpegData,
                    checked((uint)jpegData.Length),
                    planePointers,
                    width,
                    strides,
                    height,
                    0) != 0)
            {
                throw CreateException(handle, "tjDecompressToYUVPlanes");
            }

            for (int component = 0; component < planeCount; component++)
                Marshal.Copy(planePointers[component], planes[component], 0, planes[component].Length);

            if (components == 1)
                return planes[0];

            return ExpandYuvPlanes(width, height, subsampling, planes, strides);
        }
        finally
        {
            foreach (IntPtr pointer in planePointers)
            {
                if (pointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(pointer);
            }
            _ = tjDestroy(handle);
        }
    }

    private static void ReadHeader(
        IntPtr handle,
        byte[] jpegData,
        out int width,
        out int height,
        out int subsampling,
        out int colorspace)
    {
        if (tjDecompressHeader3(
                handle,
                jpegData,
                checked((uint)jpegData.Length),
                out width,
                out height,
                out subsampling,
                out colorspace) != 0)
        {
            throw CreateException(handle, "tjDecompressHeader3");
        }

        if (width <= 0 || height <= 0)
            throw new InvalidDataException("The JPEG stream contains invalid dimensions.");
    }

    private static byte[][] CreateYuvPlanes(
        int width,
        int height,
        byte[] pixels,
        int subsampling)
    {
        int chromaWidth = tjPlaneWidth(1, width, subsampling);
        int chromaHeight = tjPlaneHeight(1, height, subsampling);
        byte[][] planes =
        [
            new byte[checked(width * height)],
            new byte[checked(chromaWidth * chromaHeight)],
            new byte[checked(chromaWidth * chromaHeight)]
        ];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                planes[0][y * width + x] = pixels[(y * width + x) * 3];
        }

        int horizontalScale = Math.Max(1, (width + chromaWidth - 1) / chromaWidth);
        int verticalScale = Math.Max(1, (height + chromaHeight - 1) / chromaHeight);
        for (int component = 1; component < 3; component++)
        {
            for (int cy = 0; cy < chromaHeight; cy++)
            {
                for (int cx = 0; cx < chromaWidth; cx++)
                {
                    int sum = 0;
                    int samples = 0;
                    int startY = cy * verticalScale;
                    int endY = Math.Min(height, startY + verticalScale);
                    int startX = cx * horizontalScale;
                    int endX = Math.Min(width, startX + horizontalScale);
                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            sum += pixels[(y * width + x) * 3 + component];
                            samples++;
                        }
                    }
                    planes[component][cy * chromaWidth + cx] =
                        checked((byte)(samples == 0 ? 0 : (sum + samples / 2) / samples));
                }
            }
        }

        return planes;
    }

    private static byte[] ExpandYuvPlanes(
        int width,
        int height,
        int subsampling,
        byte[][] planes,
        int[] strides)
    {
        int chromaWidth = tjPlaneWidth(1, width, subsampling);
        int chromaHeight = tjPlaneHeight(1, height, subsampling);
        byte[] pixels = new byte[checked(width * height * 3)];

        for (int y = 0; y < height; y++)
        {
            int cy = Math.Min(chromaHeight - 1, y * chromaHeight / height);
            for (int x = 0; x < width; x++)
            {
                int cx = Math.Min(chromaWidth - 1, x * chromaWidth / width);
                int output = (y * width + x) * 3;
                pixels[output] = planes[0][y * strides[0] + x];
                pixels[output + 1] = planes[1][cy * strides[1] + cx];
                pixels[output + 2] = planes[2][cy * strides[2] + cx];
            }
        }

        return pixels;
    }

    private static void ValidateImage(
        int width,
        int height,
        int components,
        byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (components is not (1 or 3))
            throw new ArgumentOutOfRangeException(nameof(components));
        if (pixels.Length < checked(width * height * components))
            throw new ArgumentException("The source pixel buffer is too small.", nameof(pixels));
    }

    private static byte[] CopyNativeBuffer(IntPtr buffer, uint size)
    {
        if (buffer == IntPtr.Zero || size == 0)
            return [];
        if (size > int.MaxValue)
            throw new InvalidDataException("The JPEG stream is too large.");

        byte[] result = new byte[checked((int)size)];
        Marshal.Copy(buffer, result, 0, result.Length);
        return result;
    }

    private static Exception CreateException(IntPtr handle, string operation)
    {
        IntPtr messagePointer = tjGetErrorStr2(handle);
        string? message = messagePointer == IntPtr.Zero
            ? null
            : Marshal.PtrToStringAnsi(messagePointer);
        return new InvalidOperationException(
            string.IsNullOrWhiteSpace(message)
                ? $"{operation} failed."
                : $"{operation} failed: {message}");
    }
}
