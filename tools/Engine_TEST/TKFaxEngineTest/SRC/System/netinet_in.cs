/*
 * Managed declarations corresponding to netinet/in.h compatibility use in
 * the supplied TKFaxEngine test helpers.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public static class NetinetInCompat {
    public const int INET_ADDRSTRLEN = 16;
    public const int INET6_ADDRSTRLEN = 46;

    // The supplied Windows compatibility header defines socklen_t as int.
    public const int SOCKLEN_T_SIZE = sizeof(int);
}
