# TKFaxEngineTest

`TKFaxEngineTest` is the separate console-based test application for TKFaxEngine. It contains tests for DSP helpers, audio codecs, fax image processing, T.30/T.38, modem paths, network simulation, and supporting utilities.

## Required first step: extract the test files

> [!CAUTION]
> `TKFaxEngineTest/Test-Files/Test-Files.zip` must be extracted **before the test application is built**. The ZIP itself is not a runtime replacement for the extracted files.

Extract the contents directly into the existing `Test-Files` directory. The correct layout is:

```text
TKFaxEngineTest/
|-- Test-Files/
|   |-- Test-Files.zip
|   |-- bellcore/
|   |-- etsi/
|   |-- itu/
|   |-- local/
|   |-- global-tones.xml
|   |-- tsb85.xml
|   `-- ...
|-- SRC/
|-- TKFaxEngineTest.csproj
`-- TKFaxEngineTest.slnx
```

Do not create an additional nested directory such as `Test-Files/Test-Files/`.

From the `TKFaxEngineTest` project directory, the PowerShell command is:

```powershell
Expand-Archive `
  -LiteralPath ".\Test-Files\Test-Files.zip" `
  -DestinationPath ".\Test-Files"
```

Verify at least these required runtime markers before building:

```powershell
Test-Path ".\Test-Files\global-tones.xml"
Test-Path ".\Test-Files\tsb85.xml"
Test-Path ".\Test-Files\itu\fax\itutests.tif"
Test-Path ".\Test-Files\local\short_nb_voice.wav"
```

All four checks should return `True`. The project copies the extracted `Test-Files` tree into the build output. At startup, the test application checks for these files and stops with an error if the build output is incomplete.

## Important Bellcore provenance statement

> [!CAUTION]
> The files `Test-Files/bellcore/tr-tsy-00763-1.wav` through `tr-tsy-00763-6.wav` included with this project are **not original Bellcore recordings or Bellcore test-tape data**.

The included Bellcore-like WAV files:

- were independently created specifically as similar functional test signals;
- were not supplied, published, licensed, or authorized by Bellcore;
- are not copies, extracts, conversions, derivatives, tape transfers, digitizations, or transcriptions of Bellcore recordings or test tapes;
- contain no audio copied from Bellcore source material;
- do not require ownership of Bellcore test tapes;
- do not require a Bellcore or Telcordia license or authorization for their use.

The directory name `bellcore` and the filenames beginning with `tr-tsy-00763-` are compatibility-oriented test labels only. Only these existing names and paths were retained because the test application already expects them. Renaming the files would require unnecessary changes or additional name-mapping logic in the test application without changing the purpose of the tests. Retaining the names does not retain any Bellcore content or data. The names do not claim that the files originate from Bellcore, and they do not imply endorsement, authorization, sponsorship, or affiliation with Bellcore, Telcordia, or their successors.

The archive also contains a legacy `bellcore/README` text inherited from the historical test-data directory layout. Its description of transcriptions made from Bellcore tapes and its associated Bellcore licensing warning **does not describe the WAV files included in this project and is not their provenance or license statement**. The provenance statement in this README is the applicable statement for the included `tr-tsy-00763-*.wav` files.

This Bellcore clarification applies only to those independently created Bellcore-like WAV files. Other files under `Test-Files`, including ITU-, ETSI-, Mitel-, TIFF-FX-, audio-, image-, XML-, and PCAP-based material, may have their own origin, notices, or usage conditions.

## Build requirements

- Windows x64
- .NET 10 SDK
- a built x64 `TKFaxEngine.dll`
- a compatible x64 `libjpeg.dll`
- `TiffCmp.exe` for the tests that invoke the TIFF comparison utility
- restored NuGet packages `BitMiracle.LibTiff.NET` and `libsndfile.NET8`

`TKFaxEngineTest.csproj` currently contains development-machine-specific paths for `libjpeg.dll` and `TiffCmp.exe`. It also contains a relative `HintPath` for `TKFaxEngine.dll` based on the original sibling-project layout. When building this copy from inside `tools/Engine_TEST`, verify or update those paths so they point to the actual files on the build machine.

## Build order

1. Extract `Test-Files.zip` as described above.
2. Build TKFaxEngine for `Release`, `x64`, and `win-x64`.
3. Verify the `TKFaxEngine.dll`, `libjpeg.dll`, and `TiffCmp.exe` paths in `TKFaxEngineTest.csproj`.
4. Restore and build the test project.

```powershell
dotnet restore ".\TKFaxEngineTest.csproj"
dotnet build ".\TKFaxEngineTest.csproj" -c Release -p:Platform=x64
```

The test executable uses an interactive menu and creates separate run directories containing logs and generated output. Test results describe the state of the referenced TKFaxEngine build; source presence alone is not proof that an unfinished modem is functional.
