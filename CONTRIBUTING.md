# Contributing to Maviray.Pdf

Thank you for helping. Issues and pull requests are both welcome.

## Before you start

- **The licence is not open source.** Maviray.Pdf is source-available under the [Maviray.Pdf Licence](LICENSE). By opening a pull request you accept section 10 of the licence. You give Maviray the right to use your contribution in the library, including under commercial licences, and you confirm that you have the right to contribute it.
- **Discuss larger changes first.** Open an issue so we can agree on the approach before you write code. The [design document](docs/design/Maviray.Pdf-Design-Document.md) sets the scope and the architecture.

## Reporting issues

Report a bug with the smallest document that shows it:

- the C# code that builds the document;
- the generated PDF, if you can share it;
- the viewer and version in which it renders wrongly.

Use synthetic content only, never real customer documents.

## Branches and releases

- `develop` is the integration branch. `main` holds released versions only.
- Releases follow semantic versioning and are tagged `vX.Y.Z`. [Versions and packages](docs/versions-and-packages.md) explains how versions are calculated and how packages are published.

## Pull requests

- **Branch from `develop` and target `develop`.**
- **Every pull request is built and tested.** The *Build* check runs automatically. If this is your first contribution, the run waits until the maintainer approves it.
- **The maintainer approves every merge.** Merging into `develop` or `main` needs the maintainer's approval.
- **Sign your commits.** `develop` and `main` accept only signed commits, which GitHub marks as *Verified*.
- **Write the failing test first,** then the code that makes it pass. A change in behaviour needs a test that pins it.
- **Keep it focused.** Make one change per pull request, and put unrelated fixes in their own.
- **One type per file.** Each class, record, struct, enum and interface lives in its own file, named after the type. Don't use partial classes.
- **Add no runtime dependencies.** The core package references no NuGet packages and no native libraries. Packages used only by tests are fine.

## Running the tests

Run `dotnet build` and `dotnet test` from the repository root.

The tests check every PDF they generate with tools we didn't write: `qpdf --check` for the file structure, and poppler's `pdftoppm` and `pdftotext` for rendering and text. Install them to run these checks locally:

- Linux: `sudo apt-get install qpdf poppler-utils`
- macOS: `brew install qpdf poppler`
- Windows: install qpdf and poppler, and put their `bin` folders on `PATH`.

A check whose tool isn't installed is skipped, with a message naming the tool. CI installs both tools and sets `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`, which turns a missing tool into a failure. Set it yourself to make sure nothing was skipped.

The generated PDFs and their renders are saved under the test output folder, in `generated/`, and CI uploads them as the `generated-pdfs` artifact of each pull request.

## Test data and fonts

- Test data is synthetic. Never commit real customer documents.
- Fonts committed as test data must carry an open licence, such as the SIL Open Font License, with the licence file next to them.

## Clean-room rule

The public API is deliberately similar in shape to QuestPDF, so users can migrate easily. QuestPDF's licence is source-available, not open source, so our implementation must be clean-room:

- Don't read, copy or port QuestPDF source code. Its public API and documentation are the only permitted references.
- Design decisions are recorded independently in this repository, starting with the design document.
