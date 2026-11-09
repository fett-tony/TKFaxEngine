# TKFaxEngineTest data package

## Extract before building

`Test-Files.zip` contains the example and test files required by TKFaxEngineTest. Extract its contents directly into this directory before building the test application.

```powershell
Expand-Archive `
  -LiteralPath ".\Test-Files\Test-Files.zip" `
  -DestinationPath ".\Test-Files"
```

After extraction, paths such as `Test-Files/itu`, `Test-Files/etsi`, `Test-Files/local`, and `Test-Files/bellcore` must exist. A nested `Test-Files/Test-Files` directory is incorrect.

## Bellcore files: independently created test signals

> [!CAUTION]
> The included `bellcore/tr-tsy-00763-*.wav` files are **not Bellcore originals** and are not recordings, copies, extracts, derivatives, digitizations, or transcriptions of Bellcore test tapes or other Bellcore source material.

They are independently created, Bellcore-like functional test signals. No Bellcore or Telcordia license, authorization, or ownership of original Bellcore tapes is required to use these WAV files. Only the existing `bellcore` directory name and `tr-tsy-00763-*.wav` filenames were retained because the test application expects these established paths. This avoids unnecessary test-application changes and additional compatibility mapping. No Bellcore content or data was retained. The names are test-category labels only and do not indicate origin, affiliation, authorization, sponsorship, or endorsement.

The legacy `bellcore/README` contained inside the ZIP describes historical Bellcore tape transcriptions. That text does **not** describe the WAV files supplied with this project and is not their applicable provenance or license statement.

This statement applies specifically to the independently created `bellcore/tr-tsy-00763-*.wav` files. Other content in the archive may carry separate notices or usage conditions.
