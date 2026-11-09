# TKFaxEngine source generators

This directory contains C utilities used to generate static data for the TKFaxEngine/spanDSP port. They are preserved for traceability and future development, especially for unfinished modem areas.

## Build relationship

The generators are not part of the normal .NET build. `TKFaxEngine.csproj` neither compiles nor executes them, and the current library does not need them at runtime. Tables required by the current build are already checked in as C# files under `SRC`.

Most generators print C declarations to standard output. Their output is not ready-to-use C# and must not be copied over existing tables without comparison and validation. `make_v34_tx_pre_emphasis_filters.c` is an exception: it writes `v34_tx_pre_emphasis_filters.h` and diagnostic CSV files in its working directory.

## Inventory

| Generator | Purpose and related C# area |
|---|---|
| `Generators/make_at_dictionary.c` | Generates the AT-command trie represented by `SRC/at_interpreter_dictionary.cs` |
| `Generators/make_math_fixed_tables.c` | Generates fixed-point lookup tables used by `SRC/FixedMath.cs` |
| `Generators/FaxImage/make_cielab_luts.c` | Generates CIELab colour-conversion tables represented by `SRC/FaxImage/cielab_luts.cs` |
| `Generators/FaxImage/make_t43_gray_code_tables.c` | Generates the T.43 Gray-code tables represented by `SRC/FaxImage/t43_gray_code_tables.cs` |
| `Generators/Modem/make_modem_filter.c` | Generates pulse-shaping/RRC coefficient sets used by modem TX and RX table files |
| `Generators/Modem/make_modem_godard_descriptor.c` | Generates Godard timing-recovery coefficient descriptions used by modem receivers |
| `Generators/Modem/make_v17_v32_constellation_map.c` | Generates V.17/V.32bis constellation decision tables under `SRC/Modem/V17` |
| `Generators/Modem/make_v17_v32_convolutional_encoder.c` | Generates shared V.17/V.32bis convolutional encoder and decoder tables |
| `Generators/Modem/make_v29_constellation_map.c` | Generates V.29 constellation mapping data under `SRC/Modem/V29` |
| `Generators/Modem/make_v34_convolutional_coders.c` | Generates `SRC/Modem/V34/v34_convolutional_coders.cs` data |
| `Generators/Modem/make_v34_probe_signals.c` | Generates `SRC/Modem/V34/v34_probe_signals.cs` data |
| `Generators/Modem/make_v34_shell_map.c` | Generates `SRC/Modem/V34/v34_shell_map.cs` data |
| `Generators/Modem/make_v34_tx_pre_emphasis_filters.c` | Generates V.34 transmit pre-emphasis filters; no corresponding complete C# coefficient table is currently checked in |

## Requirements and limitations

There is currently no supported one-command build for these utilities. Some use only a C compiler and the standard math library, while others reference files from the original C development environment, including:

- `telephony.h`
- `complex.h`
- `filter_tools.h`
- `godard.h`
- `TKFaxEngine.h`
- `meteor-engine.h`
- platform-specific `getopt`/configuration support

Those dependencies are not all included here. A generator that references them cannot be compiled directly from this directory without reconstructing the matching C toolchain and source dependencies.

## Safe regeneration workflow

1. Build the generator in a matching C development environment.
2. Save its output separately; do not overwrite a C# source file directly.
3. Compare the generated values with the corresponding table under `SRC`.
4. Convert the declarations and numeric types to C# without changing table order or precision.
5. Build the library and run the applicable external TKFaxEngine tests before accepting the replacement.

The V.32bis and V.34 implementations are explicitly marked as unfinished in the current source. Generator presence does not mean that either modem is functional.
