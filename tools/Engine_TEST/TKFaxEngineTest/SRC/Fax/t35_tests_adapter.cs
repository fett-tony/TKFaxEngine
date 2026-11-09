/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t35_tests_adapter.cs - standalone managed adapter port of t35_tests_adapter.c and t35_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using TKFaxEngine.Daten.T30;

namespace TKFaxEngineTest.Fax.Test;

public static class t35_tests_adapter {
    public static int tkfx_t35_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Console.WriteLine("Sweep through all the possible countries");
        for (int countryCode = 0; countryCode < 256; countryCode++) {
            string? country = T35.t35_country_code_to_str(countryCode, 0);
            string? realCountry = T35.t35_real_country_code_to_str(countryCode, 0);
            if (country is not null || realCountry is not null) {
                Console.WriteLine(
                    $"{countryCode,3} '{country ?? "???"}' " +
                    $"{T35.t35_real_country_code(countryCode, 0)} '{realCountry ?? "???"}'");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Sweep through all the possible vendors within each country");
        var message = new byte[50];
        for (int countryCode = 0; countryCode < 256; countryCode++) {
            message[0] = (byte)countryCode;
            message[1] = 0;
            message[2] = 0;
            bool firstHit = true;

            for (int vendorCode = 0; vendorCode < 65_536; vendorCode++) {
                message[1] = (byte)(vendorCode >> 8);
                message[2] = (byte)vendorCode;
                string? vendor = T35.t35_vendor_to_str(message, 3);
                if (vendor is null)
                    continue;

                if (firstHit) {
                    Console.WriteLine(T35.t35_real_country_code_to_str(countryCode, 0) ?? "???");
                    firstHit = false;
                }

                Console.WriteLine(
                    $"    0x{message[0]:x2} 0x{message[1]:x2} 0x{message[2]:x2} '{vendor}'");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Try a decode of a full NSF string");
        byte[] nsf =
        {
            0x00, 0x00, 0x0E, 0x00, 0x00, 0x00, 0x96, 0x0F, 0x01, 0x02,
            0x00, 0x10, 0x05, 0x02, 0x95, 0xC8, 0x08, 0x01, 0x49, 0x02,
            0x41, 0x53, 0x54, 0x47
        };

        T35.t35_decode(nsf.AsSpan(0, 13), out string? decodedCountry, out string? decodedVendor, out string? decodedModel);
        Console.WriteLine(
            $"Decoded as {decodedCountry ?? "???"} {decodedVendor ?? "???"} {decodedModel ?? "???"}");
        Console.WriteLine("Tests passed");
        return 0;
    }
}
