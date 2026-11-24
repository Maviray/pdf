# P1.13 Default fonts package: execution log

Issue [#38](https://github.com/Maviray/pdf/issues/38). Plan: [2026-11-24-issue-38-default-fonts-package.md](../plans/2026-11-24-issue-38-default-fonts-package.md).

The branch `feature/38-default-fonts-package` is stacked on `feature/37-fluent-api` (PR #47), and so on #46 to #41.

## 2026-11-24

- **Planning.** The plan's rulings:
  - a second `RegisterNotoSans()` on the same registry does nothing, because `FontRegistry.Default` is process-wide and setup code may run twice;
  - the faces' weights and styles are passed explicitly;
  - the dependency on `Maviray.Pdf` is NuGet's default for a project reference. The review replaced this ruling: the dependency is now an exact pin;
  - the tests share `Maviray.Pdf.Tests`, so the API listing test covers both assemblies and the package tests reuse the validators;
  - the package test runs `dotnet pack --no-build` on both projects.
- **Task 1, the project and `RegisterNotoSans`:**
  - the four faces are embedded under their file names;
  - each resource equals its committed file, and there are no other resources;
  - eight threads at once register the family once;
  - a Noto Sans face registered elsewhere makes the call throw;
  - the assembly references only `Maviray.Pdf` and the runtime, and is marked trimmable;
  - a document with no font settings, after `FontRegistry.Default.RegisterNotoSans()`, passes qpdf, with only embedded Noto Sans subsets.
  - A `Lock` can't be the weak table's value, because that converts it to `object` for monitor locking (CS9216), so a plain marker object is used.
  - **Mutants**, each failing the test meant for it:
    - no second-call check;
    - the bold and italic styles swapped.
- **Task 2, the second API listing:** one type, `DefaultFonts`, and one member, `RegisterNotoSans(this FontRegistry registry)`. The core's listing is unchanged.
- **Task 3, the packages:**
  - the test packs both projects from the run's own build into a temporary folder;
  - it found `OFL.txt` packed at `Fonts/OFL.txt`, because a folder as the package path keeps the item's own folder. It is now packed at the root;
  - no MSBuild node outlives the pack, because one would hold the test's output pipes.
- **Task 4, the docs:**
  - the README example calls `FontRegistry.Default.RegisterNotoSans()` and takes only the output path;
  - the README's layout, `THIRD-PARTY-NOTICES.md`, `docs/versions-and-packages.md` and the publish workflow's header comment name the second package.
- **Full suite:**
  - Release: 1,142 of 1,142;
  - Debug: 1,141 passed and 1 skipped.
  - A solution-level `dotnet pack` builds exactly the two packages.
- **Whole-branch review** by a fresh reviewer: ready to merge, with no critical and no important finding.
  - It ran the build and publish workflows' steps in a Linux container, 1,142 of 1,142.
  - It built and ran a Native AOT app from the packed nupkg. The app calls `RegisterNotoSans()` twice, and its PDF passes qpdf.
  - **Fixed. The test was seen failing first:** the dependency on `Maviray.Pdf` is pinned to exactly the package's version, `[X]`.
    - The reviewer disagreed with the plan's ruling, and showed that its premise was false: a pin needs no hand-written nuspec.
    - Before 1.0.0, a core-only upgrade could change the API `RegisterNotoSans` calls, and the break would show only at run time. L-15 says "of the same version".
    - A small target rewrites the version NuGet's pack targets collect. `BothPackagesPack` now expects `[9.9.9-pack]`, and fails if a later SDK stops honouring the target.
  - **Full suite:** Release 1,142 of 1,142, and Debug 1,141 passed with 1 skipped.
  - **Deferred minors:**
    - a clash on a later face leaves the earlier faces registered, and the error names `family`;
    - the exception doc says any Noto Sans face from elsewhere clashes, when only a face of weight 400 or 700 does.
  - **Rulings on what the reviewer set aside:**
    - each registry holds its own parsed copy of the fonts;
    - a race with another thread registering Noto Sans decides which call throws;
    - two load contexts don't share the table;
    - OFL compliance in apps that ship the DLL alone is for the release check;
    - the new package's visibility on GitHub Packages is checked after the first publish;
    - the pack test assumes the SDK of `global.json`;
    - both packages carry the same README;
    - the stream copies happen once per registration;
    - the README's "one assembly" still describes the core;
    - two tests fill `FontRegistry.Default`, which no test expects to be empty.
