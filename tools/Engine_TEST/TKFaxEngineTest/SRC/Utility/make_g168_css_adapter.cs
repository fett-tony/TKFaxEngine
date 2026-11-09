/*
 * Fresh managed adapter port for make_g168_css_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Utility.Test;

public static class make_g168_css_adapter {
    private static int tkfx_make_g168_css_exit_code;

    public static void tkfx_make_g168_css_exit(int code) {
        tkfx_make_g168_css_exit_code = code;
        throw new make_g168_css.MakeG168CssExitException(code);
    }

    public static int tkfx_make_g168_css_main(string[] testArguments) {
        tkfx_make_g168_css_exit_code = 0;
        try {
            return make_g168_css.tkfx_make_g168_css_body_main(testArguments);
        } catch (make_g168_css.MakeG168CssExitException ex) {
            tkfx_make_g168_css_exit_code = ex.ExitCode;
            return tkfx_make_g168_css_exit_code;
        }
    }
}
