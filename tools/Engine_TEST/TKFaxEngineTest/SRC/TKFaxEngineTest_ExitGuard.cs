/*
 * Managed C# port of TKFaxEngineTest_ExitGuard.c.
 *
 * Native setjmp/longjmp process exits are represented by a dedicated exception,
 * so one test cannot terminate the complete OneExe runner.
 */
#nullable enable

namespace TKFaxEngineTest;

public static class TKFaxEngineTest_ExitGuard {
    public delegate int TkfxOneExeTestFunction(string[] testArguments);

    public sealed class TkfxOneExeExitException : Exception {
        public TkfxOneExeExitException(int exitCode)
            : base($"TKFaxEngine test requested process exit {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    public static void tkfx_oneexe_test_exit(int code) =>
        throw new TkfxOneExeExitException(code);

    public static int tkfx_oneexe_test_invoke(
        TkfxOneExeTestFunction function, string[] testArguments) {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(testArguments);

        try {
            return function(testArguments);
        } catch (TkfxOneExeExitException ex) {
            return ex.ExitCode;
        }
    }
}
