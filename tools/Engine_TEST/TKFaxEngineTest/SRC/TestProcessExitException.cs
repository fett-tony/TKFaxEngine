/*
 * Managed test-process exit signal.
 *
 * A dedicated exception keeps an individual C# test from terminating the
 * complete test runner while retaining the original test exit code.
 */
#nullable enable

namespace TKFaxEngineTest;

internal sealed class TestProcessExitException : Exception {
    public TestProcessExitException(int exitCode)
        : base($"Test requested process exit with code {exitCode}.") {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
