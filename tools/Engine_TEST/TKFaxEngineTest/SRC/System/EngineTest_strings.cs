/* Managed C# port of TKFX_strings.h. */

#nullable enable

namespace TKFaxEngineTest.System;

public static class TKFXStrings {
    public static int strcasecmp(string? left, string? right) {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static int strncasecmp(string? left, string? right, int count) {
        if (count <= 0 || ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;

        int compareLength = Math.Min(count, Math.Min(left.Length, right.Length));
        int result = string.Compare(
            left,
            0,
            right,
            0,
            compareLength,
            StringComparison.OrdinalIgnoreCase);
        if (result != 0 || compareLength == count)
            return result;

        if (left.Length == right.Length)
            return 0;
        return left.Length < right.Length ? -1 : 1;
    }
}
