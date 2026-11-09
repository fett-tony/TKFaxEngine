/*
 * Managed C# port of TKFX_pthread.h.
 *
 * The API preserves the supplied Windows pthread compatibility behavior:
 * pthread_create starts a native managed thread, join waits for completion,
 * and detach releases ownership without cancelling the running thread.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public delegate object? TKFaxEngine_pthread_start_t(object? argument);

public sealed class pthread_t {
    internal Thread? Thread;
    internal bool Detached;
}

public sealed class pthread_attr_t {
    public int detachstate;
}

public static class PthreadCompat {
    public const int PTHREAD_CREATE_DETACHED = 1;

    public static int pthread_attr_init(pthread_attr_t? attribute) {
        if (attribute is not null)
            attribute.detachstate = 0;
        return 0;
    }

    public static int pthread_attr_setdetachstate(pthread_attr_t? attribute, int state) {
        if (attribute is not null)
            attribute.detachstate = state;
        return 0;
    }

    public static int pthread_create(
        pthread_t? thread,
        pthread_attr_t? attribute,
        TKFaxEngine_pthread_start_t? startRoutine,
        object? argument) {
        _ = attribute;
        if (startRoutine is null)
            return 1;

        try {
            Thread managedThread = new(() => {
                _ = startRoutine(argument);
            }) {
                IsBackground = true
            };

            if (thread is not null) {
                thread.Thread = managedThread;
                thread.Detached = false;
            }

            managedThread.Start();
            return 0;
        } catch (Exception exception) when (
            exception is ThreadStateException or OutOfMemoryException) {
            return 1;
        }
    }

    public static int pthread_join(pthread_t? thread, out object? returnValue) {
        returnValue = null;
        if (thread?.Thread is null)
            return 0;

        try {
            thread.Thread.Join();
        } catch (ThreadStateException) {
            // The supplied Windows compatibility function ignores wait errors.
        }
        thread.Thread = null;
        return 0;
    }

    public static int pthread_join(pthread_t? thread, object? ignoredReturnValue) {
        _ = ignoredReturnValue;
        return pthread_join(thread, out _);
    }

    public static int pthread_detach(pthread_t? thread) {
        if (thread is not null) {
            thread.Detached = true;
            thread.Thread = null;
        }
        return 0;
    }
}
