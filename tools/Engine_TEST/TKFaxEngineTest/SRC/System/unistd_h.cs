/* Fresh managed compatibility port of the supplied unistd.h test header. */
#nullable enable

namespace TKFaxEngineTest.System;

public static class UnistdCompatibility {
    public static void Sleep(uint seconds) =>
        Thread.Sleep(checked((int)Math.Min((ulong)int.MaxValue, seconds * 1000UL)));

    public static void USleep(uint microseconds) =>
        Thread.Sleep(checked((int)Math.Min((ulong)int.MaxValue, (microseconds + 999UL) / 1000UL)));

    public static bool Access(string path, FileAccess access = FileAccess.Read) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path) && !Directory.Exists(path))
            return false;
        if (Directory.Exists(path))
            return true;
        try {
            using FileStream stream = File.Open(path, FileMode.Open, access, FileShare.ReadWrite);
            return stream.CanRead || stream.CanWrite;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    public static int Close(Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Dispose();
        return 0;
    }

    public static string GetCurrentDirectory() => Directory.GetCurrentDirectory();
    public static void ChangeDirectory(string path) => Directory.SetCurrentDirectory(path);
    public static int Unlink(string path) { File.Delete(path); return 0; }
    public static string StrDup(string value) => new(value.ToCharArray());
}

