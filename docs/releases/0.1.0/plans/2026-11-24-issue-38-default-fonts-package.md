# P1.13 Default fonts package: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `Maviray.Pdf.Fonts.Default` package (L-15): the four Noto Sans faces as embedded resources, and `RegisterNotoSans()`. `dotnet pack` builds both packages, and a document with only `RegisterNotoSans()` set up passes qpdf.

**Architecture:**
- **The project** `src/Maviray.Pdf.Fonts.Default/Maviray.Pdf.Fonts.Default.csproj` references `Maviray.Pdf`, which packing turns into a dependency on the `Maviray.Pdf` package of exactly the same version. It takes the shared settings of `src/Directory.Build.props`: XML docs, AOT-compatible, warnings as errors, and the package metadata.
- **The fonts** in `Fonts/` are embedded under their file names, such as `NotoSans-Regular.ttf`. `RegisterNotoSans` opens each with `Assembly.GetManifestResourceStream`, which trimming and Native AOT keep.
- **The tests** live in `tests/Maviray.Pdf.Tests/FontsDefault/`. The test project references the new project, so the API listing test covers both assemblies (L-16) and the package tests reuse the validators.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf; `dotnet pack`.

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decisions L-15 and L-16, with F-12 of the [P1 fonts spec](../specs/2026-11-20-p1-fonts-design.md). Work item [#38](https://github.com/Maviray/pdf/issues/38). The branch `feature/38-default-fonts-package` is stacked on `feature/37-fluent-api` (PR #47).

## Global Constraints

- `RegisterNotoSans(this FontRegistry registry)` is the package's one public member, in namespace `Maviray.Pdf`, so `using Maviray.Pdf;` is enough. It has XML docs, and checks its argument at the call.
- The package carries `OFL.txt`, `THIRD-PARTY-NOTICES.md` and `LICENSE` (L-15), besides the README the shared props pack.
- The package references only `Maviray.Pdf` and the .NET runtime (NFR-01).
- `Maviray.Pdf`'s own tests keep finding the fonts as files, through `TestFonts` (F-12).
- Each task ends with one commit, subject `#38 <summary>`. The records are the last commit.

## Rulings made while planning

1. **A second call on the same registry does nothing.**
   - `RegisterNotoSans` remembers the registries it has filled, in a `ConditionalWeakTable` under a lock, and returns such a registry unchanged.
   - **Why:** `FontRegistry.Default` is process-wide, so setup code that runs twice would otherwise throw: a test fixture per class, or a method that registers before it writes, as the README's does. L-09's duplicate rule still holds for a face registered by other means: if the registry already has a Noto Sans face from elsewhere, `RegisterNotoSans` throws `ArgumentException`.
2. **The faces' weights and styles are passed explicitly:** Regular 400, Bold 700, Italic 400 italic, Bold Italic 700 italic. They don't depend on the files' own flags.
3. **The dependency is NuGet's default for a project reference,** the same version or later. `RegisterNotoSans` uses only `FontRegistry`'s public API, so a later core still works. An exact pin would need a hand-written nuspec.
   - **Replaced by the final review, 2026-11-24:** the dependency is pinned to exactly the same version, `[X]`. A small target rewrites the version NuGet's pack targets collect, so no nuspec is needed, and before 1.0.0 a later core may change the API `RegisterNotoSans` calls. L-15 says "of the same version".
4. **The tests share `Maviray.Pdf.Tests`** rather than a project of their own: L-16's listing test covers both assemblies, and qpdf's helper is there.
5. **The package test runs `dotnet pack --no-build`** on both projects, in the test run's configuration, into a temporary folder with the version `9.9.9-pack`, and reads the packages as zip files.

## Review Focus

1. **Calling it twice, or from several threads at once,** on the same registry: no exception, and four faces.
2. **A registry that already has a Noto Sans face** registered by other means: `ArgumentException`, as L-09 says.
3. **The resources are the committed files,** byte for byte, so the package can't ship a stale or different font.
4. **The packages' contents:** the DLL, the three notice files and the README; the dependency on `Maviray.Pdf` at the same version; no loose font files.
5. **The built-in default family:** a document with no font settings, after `FontRegistry.Default.RegisterNotoSans()`, renders in Noto Sans and passes qpdf.

---

### Task 1: The project and `RegisterNotoSans`

**Files:**
- Create: `src/Maviray.Pdf.Fonts.Default/Maviray.Pdf.Fonts.Default.csproj`, `src/Maviray.Pdf.Fonts.Default/DefaultFonts.cs`
- Modify: `Maviray.Pdf.slnx` (the project, under `/src/`), `tests/Maviray.Pdf.Tests/Maviray.Pdf.Tests.csproj` (a project reference)
- Test: `tests/Maviray.Pdf.Tests/FontsDefault/DefaultFontsTests.cs`

**Interfaces:**
- Produces: `public static class DefaultFonts` with `public static FontRegistry RegisterNotoSans(this FontRegistry registry)`.
- The csproj: `PackageId` `Maviray.Pdf.Fonts.Default`, a description and tags; `<EmbeddedResource Include="Fonts\NotoSans-*.ttf" LogicalName="%(Filename)%(Extension)" />`; `Fonts\OFL.txt` packed at the package root.

- [ ] **Test:**
  - `TheFourFacesAreRegistered`: a new registry's families are `["Noto Sans"]`, and matching 400, 700, 400 italic and 700 italic each finds the face of that file, by PostScript name.
  - `TheResourcesAreTheCommittedFiles`: each resource's bytes equal `TestFonts.NotoSansPath(style)`'s.
  - `ItReturnsTheRegistry`.
  - `ASecondCallChangesNothing`.
  - `CallsFromManyThreadsRegisterOnce`: eight threads on one new registry, with no exception, and four faces.
  - `ANotoSansFaceRegisteredElsewhereThrows`: Regular registered from its file first, then `RegisterNotoSans` throws `ArgumentException`.
  - `ANullRegistryThrows`: `ArgumentNullException` naming `registry`.
  - `TheAssemblyReferencesOnlyTheCoreAndTheRuntime`, and `IsMarkedTrimmable`, as `CoreAssemblyTests` does for the core.
  - `ADocumentWithOnlyRegisterNotoSansPassesQpdf`: `FontRegistry.Default.RegisterNotoSans()`, then a document with no font settings, written to a file. qpdf accepts it, and pdffonts lists only embedded subsets of Noto Sans.
- [ ] Red, implement, the suite. Commit `#38 add the default fonts package and RegisterNotoSans`.

### Task 2: The second API listing

**Files:**
- Modify: `tests/Maviray.Pdf.Tests/Fluent/PublicApiTests.cs`
- Create: `src/Maviray.Pdf.Fonts.Default/PublicApi.txt`

- [ ] **Test:** `ThePublicApiMatchesItsListing` becomes a theory over the two assemblies, each with its file beside its project.
- [ ] Write the new listing with the variable set, read it through: one type, one member. Commit `#38 add the default fonts package's API listing`.

### Task 3: The packages

**Files:**
- Test: `tests/Maviray.Pdf.Tests/FontsDefault/PackageTests.cs`

- [ ] **Test:** `BothPackagesPack`:
  - `Maviray.Pdf.9.9.9-pack.nupkg` holds `lib/net10.0/Maviray.Pdf.dll`, `README.md`, `LICENSE` and `THIRD-PARTY-NOTICES.md`;
  - `Maviray.Pdf.Fonts.Default.9.9.9-pack.nupkg` holds `lib/net10.0/Maviray.Pdf.Fonts.Default.dll`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `OFL.txt`, and no `.ttf` entry;
  - its nuspec depends on `Maviray.Pdf` at version `[9.9.9-pack]`, exactly (ruling 3 as replaced).
- [ ] Red: without `OFL.txt` packed, the test fails. Then green and the suite. Commit `#38 test what the packages hold`.

### Task 4: The docs

**Files:**
- Modify: `README.md`, `tests/Maviray.Pdf.Tests/Fluent/ReadmeExampleTests.cs`, `THIRD-PARTY-NOTICES.md`, `docs/versions-and-packages.md`

- [ ] The README example registers the fonts with `FontRegistry.Default.RegisterNotoSans()`, and takes only the output path. Its prose names the `Maviray.Pdf.Fonts.Default` package. `TheReadmeExampleRuns` runs it.
- [ ] README's repository layout lists `src/Maviray.Pdf.Fonts.Default/`.
- [ ] `THIRD-PARTY-NOTICES.md` says the fonts are compiled into `Maviray.Pdf.Fonts.Default.dll`.
- [ ] `docs/versions-and-packages.md` says the workflow builds and publishes two packages, at one version. Its status date becomes today's.
- [ ] Commit `#38 use RegisterNotoSans in the README and name the second package in the docs`.

### Task 5: Records

- [ ] The log, the index row, `#38 add the P1.13 plan and execution log`, the PR stacked on #47, and the CI run.
