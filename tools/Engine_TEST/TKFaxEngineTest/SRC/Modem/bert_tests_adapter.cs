/* Managed adapter port for bert_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class bert_tests_adapter {
    private static int tkfx_bert_tests_exit_code;

    public static void tkfx_bert_tests_exit(int code) {
        tkfx_bert_tests_exit_code = code;
        throw new bert_tests.BertTestsExitException(code);
    }

    public static int tkfx_bert_tests_main(string[] testArguments) {
        tkfx_bert_tests_exit_code = 0;

        try {
            return bert_tests.tkfx_bert_tests_body_main(testArguments);
        } catch (bert_tests.BertTestsExitException ex) {
            tkfx_bert_tests_exit_code = ex.ExitCode;
            return tkfx_bert_tests_exit_code;
        }
    }
}
