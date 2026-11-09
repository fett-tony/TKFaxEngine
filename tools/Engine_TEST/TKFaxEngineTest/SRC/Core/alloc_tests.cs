/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * alloc_tests.cs - memory allocation handling tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2013 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. The allocator replacement, aligned allocation, normal
 * allocation, NULL reallocation and matching release order are retained.
 */

#nullable enable

using System.Runtime.InteropServices;
using static TKFaxEngine.AllocApi;

namespace TKFaxEngineTest.Core.Test;

public static unsafe class alloc_tests {
    public static int main(string[] testArguments) {
        try {
            return tkfx_alloc_tests_body_main(testArguments);
        } catch (AllocTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_alloc_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        if (span_mem_allocators(
                TestAllocate,
                TestReallocate,
                TestFree,
                TestAlignedAllocate,
                TestAlignedFree) != 0) {
            Console.WriteLine("Failed");
            Exit(2);
        }

        void* a = span_aligned_alloc(8, 42);
        void* b = span_alloc(42);
        void* c = span_realloc(null, 42);

        Console.WriteLine($"0x{(nuint)a:X} 0x{(nuint)b:X} 0x{(nuint)c:X}");

        span_aligned_free(a);
        span_free(b);
        span_free(c);
        return 0;
    }

    private static void* TestAllocate(nuint size) {
        return NativeMemory.Alloc(size);
    }

    private static void* TestReallocate(void* pointer, nuint size) {
        return NativeMemory.Realloc(pointer, size);
    }

    private static void TestFree(void* pointer) {
        NativeMemory.Free(pointer);
    }

    private static void* TestAlignedAllocate(nuint alignment, nuint size) {
        return NativeMemory.AlignedAlloc(size, alignment);
    }

    private static void TestAlignedFree(void* pointer) {
        NativeMemory.AlignedFree(pointer);
    }

    private static void Exit(int code) {
        throw new AllocTestExitException(code);
    }

    public sealed class AllocTestExitException : Exception {
        public AllocTestExitException(int exitCode)
            : base($"alloc_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
