/*
 * TKFaxEngine - managed C# port
 *
 * Alloc.cs - combined port of alloc.h and alloc.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2013 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 *
 * Project requirement:
 *   <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
 */

using System.Runtime.InteropServices;

namespace TKFaxEngine;

/// <summary>
/// Allocates an unmanaged block of <paramref name="size"/> bytes.
/// This is the managed equivalent of <c>span_alloc_t</c>.
/// </summary>
public unsafe delegate void* SpanAllocDelegate(nuint size);

/// <summary>
/// Resizes an unmanaged block to <paramref name="size"/> bytes.
/// This is the managed equivalent of <c>span_realloc_t</c>.
/// </summary>
public unsafe delegate void* SpanReallocDelegate(void* pointer, nuint size);

/// <summary>
/// Releases an unmanaged block allocated by <see cref="SpanAllocDelegate"/>
/// or <see cref="SpanReallocDelegate"/>.
/// This is the managed equivalent of <c>span_free_t</c>.
/// </summary>
public unsafe delegate void SpanFreeDelegate(void* pointer);

/// <summary>
/// Allocates an unmanaged block aligned to <paramref name="alignment"/> bytes.
/// The argument order intentionally matches the original C function:
/// alignment first, size second.
/// This is the managed equivalent of <c>span_aligned_alloc_t</c>.
/// </summary>
public unsafe delegate void* SpanAlignedAllocDelegate(nuint alignment, nuint size);

/// <summary>
/// Releases an aligned unmanaged block.
/// This is the managed equivalent of <c>span_aligned_free_t</c>.
/// </summary>
public unsafe delegate void SpanAlignedFreeDelegate(void* pointer);

/// <summary>
/// Central unmanaged-memory allocator used by the managed TKFaxEngine port.
/// </summary>
/// <remarks>
/// <para>
/// The original C implementation stores five process-wide function pointers.
/// This class keeps the same behaviour by storing one process-wide allocator set.
/// </para>
/// <para>
/// Do not replace the allocator set while blocks allocated by the previous set
/// are still alive. A block must always be released by the matching free function.
/// This restriction also exists in the original implementation.
/// </para>
/// </remarks>
public static unsafe class SpanMemory {
    private sealed class AllocatorSet {
        public AllocatorSet(
            SpanAllocDelegate allocate,
            SpanReallocDelegate reallocate,
            SpanFreeDelegate free,
            SpanAlignedAllocDelegate alignedAllocate,
            SpanAlignedFreeDelegate alignedFree) {
            Allocate = allocate;
            Reallocate = reallocate;
            Free = free;
            AlignedAllocate = alignedAllocate;
            AlignedFree = alignedFree;
        }

        public SpanAllocDelegate Allocate { get; }

        public SpanReallocDelegate Reallocate { get; }

        public SpanFreeDelegate Free { get; }

        public SpanAlignedAllocDelegate AlignedAllocate { get; }

        public SpanAlignedFreeDelegate AlignedFree { get; }
    }

    private static AllocatorSet s_allocators = CreateDefaultAllocatorSet();

    /// <summary>
    /// Allocates <paramref name="size"/> bytes of unmanaged memory.
    /// Managed equivalent of <c>span_alloc()</c>.
    /// </summary>
    public static void* Allocate(nuint size) {
        AllocatorSet allocators = Volatile.Read(ref s_allocators);
        return allocators.Allocate(size);
    }

    /// <summary>
    /// Resizes a previously allocated unmanaged block.
    /// Managed equivalent of <c>span_realloc()</c>.
    /// </summary>
    /// <remarks>
    /// The supplied pointer must have been returned by the currently configured
    /// normal allocation or reallocation function. Aligned blocks cannot be
    /// reallocated through this method.
    /// </remarks>
    public static void* Reallocate(void* pointer, nuint size) {
        AllocatorSet allocators = Volatile.Read(ref s_allocators);
        return allocators.Reallocate(pointer, size);
    }

    /// <summary>
    /// Releases a block returned by <see cref="Allocate"/> or
    /// <see cref="Reallocate"/>.
    /// Managed equivalent of <c>span_free()</c>.
    /// </summary>
    public static void Free(void* pointer) {
        AllocatorSet allocators = Volatile.Read(ref s_allocators);
        allocators.Free(pointer);
    }

    /// <summary>
    /// Allocates <paramref name="size"/> bytes aligned to
    /// <paramref name="alignment"/> bytes.
    /// Managed equivalent of <c>span_aligned_alloc()</c>.
    /// </summary>
    /// <remarks>
    /// The alignment must satisfy the requirements of the active allocator.
    /// For the default .NET allocator it must be a power of two.
    /// </remarks>
    public static void* AlignedAllocate(nuint alignment, nuint size) {
        AllocatorSet allocators = Volatile.Read(ref s_allocators);
        return allocators.AlignedAllocate(alignment, size);
    }

    /// <summary>
    /// Releases a block returned by <see cref="AlignedAllocate"/>.
    /// Managed equivalent of <c>span_aligned_free()</c>.
    /// </summary>
    public static void AlignedFree(void* pointer) {
        AllocatorSet allocators = Volatile.Read(ref s_allocators);
        allocators.AlignedFree(pointer);
    }

    /// <summary>
    /// Replaces the process-wide allocator functions.
    /// A <see langword="null"/> argument restores the corresponding default
    /// allocator, matching <c>span_mem_allocators()</c>.
    /// </summary>
    /// <returns>Always 0, matching the original C implementation.</returns>
    public static int SetAllocators(
        SpanAllocDelegate? customAllocate,
        SpanReallocDelegate? customReallocate,
        SpanFreeDelegate? customFree,
        SpanAlignedAllocDelegate? customAlignedAllocate,
        SpanAlignedFreeDelegate? customAlignedFree) {
        var replacement = new AllocatorSet(
            customAllocate ?? DefaultAllocate,
            customReallocate ?? DefaultReallocate,
            customFree ?? DefaultFree,
            customAlignedAllocate ?? DefaultAlignedAllocate,
            customAlignedFree ?? DefaultAlignedFree);

        Interlocked.Exchange(ref s_allocators, replacement);
        return 0;
    }

    /// <summary>
    /// Restores all allocator functions to the .NET unmanaged-memory defaults.
    /// </summary>
    public static void ResetAllocators() {
        Interlocked.Exchange(ref s_allocators, CreateDefaultAllocatorSet());
    }

    private static AllocatorSet CreateDefaultAllocatorSet() {
        return new AllocatorSet(
            DefaultAllocate,
            DefaultReallocate,
            DefaultFree,
            DefaultAlignedAllocate,
            DefaultAlignedFree);
    }

    private static void* DefaultAllocate(nuint size) {
        return NativeMemory.Alloc(size);
    }

    private static void* DefaultReallocate(void* pointer, nuint size) {
        return NativeMemory.Realloc(pointer, size);
    }

    private static void DefaultFree(void* pointer) {
        NativeMemory.Free(pointer);
    }

    private static void* DefaultAlignedAllocate(nuint alignment, nuint size) {
        // NativeMemory uses byte-count first and alignment second.
        // The public TKFaxEngine API keeps the original C argument order.
        return NativeMemory.AlignedAlloc(size, alignment);
    }

    private static void DefaultAlignedFree(void* pointer) {
        NativeMemory.AlignedFree(pointer);
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static unsafe class AllocApi {
    public static void* span_alloc(nuint size) {
        return SpanMemory.Allocate(size);
    }

    public static void* span_realloc(void* pointer, nuint size) {
        return SpanMemory.Reallocate(pointer, size);
    }

    public static void span_free(void* pointer) {
        SpanMemory.Free(pointer);
    }

    public static void* span_aligned_alloc(nuint alignment, nuint size) {
        return SpanMemory.AlignedAllocate(alignment, size);
    }

    public static void span_aligned_free(void* pointer) {
        SpanMemory.AlignedFree(pointer);
    }

    public static int span_mem_allocators(
        SpanAllocDelegate? customAllocate,
        SpanReallocDelegate? customReallocate,
        SpanFreeDelegate? customFree,
        SpanAlignedAllocDelegate? customAlignedAllocate,
        SpanAlignedFreeDelegate? customAlignedFree) {
        return SpanMemory.SetAllocators(
            customAllocate,
            customReallocate,
            customFree,
            customAlignedAllocate,
            customAlignedFree);
    }
}
