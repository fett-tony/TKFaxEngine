# TKFaxEngine

TKFaxEngine is a managed C# port of [spanDSP](https://www.soft-switch.org/), built as a Windows x64 class library.

The project name was changed only for development purposes. TKFaxEngine is intended as a 100% source-level port of spanDSP to C#: its algorithms, state machines, signal paths, and C-style compatibility APIs are preserved as directly as practical instead of being replaced by unrelated managed abstractions. Here, "100% port" describes the porting objective and architecture; it does not mean that upstream work-in-progress modules have become functional merely by being translated.

> [!IMPORTANT]
> This repository is a development snapshot, not a claim that every upstream module is production-ready. In particular, V.32bis and V.34 are explicitly marked as work in progress and not yet functional in the source; V.42 is also marked unfinished.

## Platform and project type

- Managed C# class library (`TKFaxEngine.dll`)
- .NET 10.0
- Windows only
- Minimum Windows target: `10.0.17763.0` (Windows 10 version 1809 / Windows Server 2019)
- x64 / `win-x64` only
- Nullable reference types and implicit usings enabled
- Unsafe code enabled where required by the port
- No standalone executable or UI is produced

## Required libraries

These libraries are essential to build or run the current project:

| Library | Source | Purpose |
|---|---|---|
| `BitMiracle.LibTiff.NET.dll` | NuGet package `BitMiracle.LibTiff.NET` 2.4.660 | TIFF processing used by the fax image path |
| `libjpeg.dll` | External native x64 DLL | Native JPEG/T.42 image processing |
| `libsndfile.NET8.dll` | NuGet package `libsndfile.NET8` 1.0.1 | Managed libsndfile binding |
| `libsndfile-1.dll` | Supplied by the libsndfile package | Native x64 audio-file support |

`libjpeg.dll` is not stored in this repository. `TKFaxEngine.csproj` validates the configured DLL before compilation and copies it beside `TKFaxEngine.dll` after a successful build.

## Build requirements

1. Windows x64.
2. The .NET 10 SDK.
3. NuGet access for package restore.
4. A compatible x64 `libjpeg.dll`.

The project currently contains an environment-specific default path in `LibJpegNativeDll`. On another machine, override that MSBuild property with the actual DLL location.

### Command line

```powershell
dotnet restore .\TKFaxEngine.csproj
dotnet build .\TKFaxEngine.csproj `
  -c Release `
  -p:Platform=x64 `
  -p:LibJpegNativeDll="C:\path\to\libjpeg.dll"
```

### Visual Studio

Open `TKFaxEngine.slnx`, select the `x64` platform, and set the `LibJpegNativeDll` MSBuild property if the default native-library path is unavailable.

The normal Release output directory is:

```text
bin\x64\Release\net10.0-windows10.0.17763.0\win-x64\
```

The build is intentionally configured to remove `.pdb`, `.json`, and `.exe` side files. The generated XML API documentation is retained.

## Architecture

TKFaxEngine is a streaming signal-processing library. Audio modems consume and produce real 8 kHz PCM samples, while protocol state machines consume and produce bits, frames, or transport payloads.

The core audio-fax path is:

```text
T.30 state machine
    |-- V.21 / HDLC control channel
    `-- V.17, V.29, or V.27ter page-data modem
            |
            `-- 8 kHz PCM audio samples
```

For normal audio fax:

- V.21 is the T.30 control channel.
- V.17, V.29, and V.27ter carry high-speed page data.
- ECM changes the framing/bit source, not the selected physical modem.
- T.38 is a separate packet transport path and does not run an audio modem.

Network framing is deliberately outside the engine. The library does not provide a complete SIP, SDP, UDP, or general RTP stack. T.38 and V.150.1 interfaces exchange protocol payloads and the required metadata with the calling application; the outer application remains responsible for the actual network transport.

## Main source areas

| Path | Contents |
|---|---|
| `SRC/Audio` | Codecs and DSP utilities, including G.711, G.722, G.726, GSM 06.10, LPC10, ADPCM, DTMF, tone generation/detection, echo, PLC, and playout |
| `SRC/Daten/T30` | T.30 fax control, T.31, and T.35 |
| `SRC/Daten/T38` | T.38 core, terminal, gateway, and non-ECM buffering |
| `SRC/Daten/T140` | T.140 text encoding and decoding |
| `SRC/FaxImage` | T.4/T.6, T.42/T.43, T.81/T.82 arithmetic coding, and T.85 image processing |
| `SRC/Modem` | FSK, HDLC, fax modem coordination, connect tones, and data-modem integration |
| `SRC/Modem/V17` | V.17 fax modem TX/RX and shared V.17/V.32bis tables |
| `SRC/Modem/V18` | V.18 text telephone and automoding implementation |
| `SRC/Modem/V22` | V.22bis TX/RX |
| `SRC/Modem/V27` | V.27ter fax modem TX/RX |
| `SRC/Modem/V29` | V.29 fax modem TX/RX |
| `SRC/Modem/V32` | V.32bis development source; currently not functional |
| `SRC/Modem/V34` | V.34 development source and generated tables; currently not functional |
| `SRC/Modem/V42` | V.42/V.42bis development source; unfinished |
| `SRC/Modem/V8` | V.8 modem negotiation |
| `SRC/Modem/V80` | V.80 support |
| `SRC/Modem/V150` | V.150.1 and SSE payload/state handling |
| `SRC/TKFaxEngine.xml` | Generated XML API documentation for the public and internal C# API |
| `tools/Generators` | Original C utilities used to generate lookup tables, constellations, filters, and dictionaries |
| `tools/Engine_TEST` | Separate TKFaxEngineTest console application and its compressed test-data package |

Source presence should not be read as a guarantee that every listed standard is complete or validated. The status markers in the source and the current test results take precedence.

## Source generators

The C programs under `tools/Generators` are retained so that generated tables and dictionaries can be traced back to their generation logic. They are development tools only:

- they are not compiled or executed by `TKFaxEngine.csproj`;
- they are not required to build or run `TKFaxEngine.dll`;
- the generated tables needed by the current library are already present as C# source under `SRC`;
- several generators require headers or auxiliary tools from the original C development environment and are therefore not standalone in this repository;
- generated C output must be reviewed and converted carefully before replacing an existing C# table.

See [`tools/README.md`](tools/README.md) for the generator inventory, dependencies, and corresponding C# source areas.

## Test application

The separate `TKFaxEngineTest` console application is stored under `tools/Engine_TEST`. It is not part of the main `TKFaxEngine.slnx` build.

> [!IMPORTANT]
> Before building the test application, extract `tools/Engine_TEST/TKFaxEngineTest/Test-Files/Test-Files.zip` directly into the surrounding `Test-Files` directory. The build copies the extracted directory tree to its output, and the application refuses to start when required runtime files are missing.

> [!IMPORTANT]
> The WAV files stored under the archive path `bellcore/` are **not original Bellcore files**. They are independently created, Bellcore-like test signals. They are not copies, extracts, conversions, derivatives, tape transfers, or transcriptions of Bellcore material and do not require a Bellcore or Telcordia license. Only the existing directory and filenames were retained so that the test application can continue to use its established paths without unnecessary code changes or additional compatibility mapping. These names do not imply origin, authorization, or affiliation.

See [`tools/Engine_TEST/README.md`](tools/Engine_TEST/README.md) for complete preparation, build, path, and test-data information.

## Development status

The current codebase is a compilable development state of the C# port. The following limitations are explicit in the repository:

- `SRC/Modem/V32/V32Bis.cs`: work in progress, not yet functional.
- `SRC/Modem/V34`: work in progress, not yet functional.
- `SRC/Modem/V42/V42.cs`: work in progress and unfinished.
- `TKFaxEngineBuildInfo` currently disables the V.32bis and V.34 feature flags.
- The separate test application is included under `tools/Engine_TEST`; it is not built by the main solution. The assembly exposes internals to this `TKFaxEngineTest` project.

For build configuration, `TKFaxEngine.csproj` is authoritative. The generator utilities document how static data was produced, but they do not define the runtime feature status.

## Build output and deployment

Deploy the managed library together with all native and managed dependencies copied to its output directory. At minimum, the application must be able to load:

```text
TKFaxEngine.dll
BitMiracle.LibTiff.NET.dll
libjpeg.dll
libsndfile.NET8.dll
libsndfile-1.dll
```

Keep the process architecture at x64. An x86 host cannot load the native x64 libraries.

## Porting rules

When extending the port:

- preserve the existing spanDSP-compatible function names and behavior;
- keep TX and RX paths separated;
- keep modem paths streaming-based and operate on real samples/bits;
- do not replace unfinished algorithms with fake success paths, placeholder payloads, or unrelated backends;
- keep full SIP/SDP/RTP/UDP transport responsibilities outside the modem and fax engine;
- compare behavior with the applicable spanDSP source before changing protocol or DSP logic.

## Licensing

This repository currently has no standalone `LICENSE` file. Converted source files retain or describe the applicable upstream notices. Review the notices of the corresponding spanDSP and third-party source before redistribution.

## Acknowledgement

The C# conversion and development of TKFaxEngine were created with OpenAI assistance.
