# Maviray.Pdf

A fully managed .NET library for generating PDF documents from C# code. You compose documents through a fluent layout API, and every layer is written in C#: the PDF writer, the font parser and subsetter, text layout, image handling and the layout engine.

> **Status: early development.** The published version, 0.0.1, contains only the build and publishing set-up. Release [0.1.0](docs/releases/0.1.0/README.md) is in progress on `develop`. It brings the PDF writer, embedded TrueType fonts, text layout and the fluent layout API that [A first document](#a-first-document) shows. The [design document](docs/design/Maviray.Pdf-Design-Document.md) is a draft (v0.6).

## Why fully managed

- **No native dependencies.** One package runs wherever .NET 10 runs: Windows, Linux and macOS on x64 and ARM64, containers, Azure App Service and Functions, Native AOT and Blazor WebAssembly.
- **No runtime package dependencies.** The core uses only the .NET base class library.
- **Full control of the output.** Our own writer decides file size, deterministic output, PDF/A and tagged PDF. No rendering backend gets in the way.
- **Our own IP.** We can license the library on any terms.

The cost is that we own the hard parts a native backend would otherwise supply: font subsetting, text shaping and line breaking. Scope is the main risk, so the roadmap is split into phases that each end in a usable library.

## What v1 covers

Version 1 targets data-driven business documents in European languages: invoices, reports, contracts, certificates and policy summaries.

- Code-first documents built from data, with reusable components and no templates or markup.
- Automatic paging, with repeating headers, footers, page numbers and table headers.
- Embedded, subsetted fonts. Copy, paste and search work for Latin (including German, Turkish and Azerbaijani), Cyrillic and Greek text.
- Tables, JPEG and PNG images, and vector drawing.
- Small, standards-conformant files: every file passes `qpdf --check`, and PDF/A output passes veraPDF.
- Memory bounded by the page being written, not by the whole document.

**Not in v1:**

- HTML or CSS conversion.
- Reading, editing, merging or splitting existing PDFs.
- Rendering PDFs to images.
- Right-to-left and complex scripts (Arabic, Hebrew, Indic).
- Form fields and digital signatures.
- A visual designer.

## Planned API

This is the v1 API as the design document (draft v0.6) describes it. P1 brings pages, containers, text and fonts; tables and images come in P2.

```csharp
FontRegistry.Default.Register("Inter", File.OpenRead("Inter-Regular.ttf"));
FontRegistry.Default.Register("Inter", File.OpenRead("Inter-Bold.ttf"), FontWeight.Bold);

Document.Create(doc =>
{
    doc.Metadata(m => m.Title("Invoice 2026-0042").Language("en-GB"));

    doc.Page(page =>
    {
        page.Size(PageSize.A4);
        page.Margin(20, Unit.Millimetre);
        page.DefaultTextStyle(t => t.FontFamily("Inter").FontSize(10));

        page.Header().Text("Invoice").FontSize(20).Bold();

        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Spacing(8);
            col.Item().Text(customer.Name).Bold();
            col.Item().Table(table =>
            {
                table.Columns(c => { c.Flex(3); c.Fixed(80); });
                table.Header(h => { h.Cell().Text("Item"); h.Cell().AlignRight().Text("Amount"); });
                foreach (var line in invoice.Lines)
                {
                    table.Cell().Text(line.Description);
                    table.Cell().AlignRight().Text(line.Amount.ToString("C"));
                }
            });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Page "); t.PageNumber(); t.Span(" of "); t.PageCount();
        });
    });
}).Save(outputStream);
```

## Architecture

The library is one assembly, `Maviray.Pdf`, organised in layers. Dependencies point only downward, so each layer can be tested on its own. Each layer has its own namespace under `Maviray.Pdf`.

| Layer | Namespace | Responsibility |
| --- | --- | --- |
| Fluent API | `Maviray.Pdf` | Builds the element tree; holds no layout logic |
| Element tree | `Maviray.Pdf.Elements` | Column, Row, Text, Table, Image and the other elements |
| Layout engine | `Maviray.Pdf.Layout` | Measures and draws elements, and runs the paging loop |
| Canvas | `Maviray.Pdf.Drawing` | Drawing API in points with a top-left origin; the seam between layout and output |
| PDF writer | `Maviray.Pdf.Writer` | Objects, content streams, compression and cross-reference table; writes each page out as soon as it is finished |

Three shared services sit alongside the layers:

- **Text engine** (`Maviray.Pdf.Text`): itemizing, shaping and line breaking.
- **Font engine** (`Maviray.Pdf.Fonts`): OpenType parsing and subsetting.
- **Image codecs** (`Maviray.Pdf.Imaging`): JPEG pass-through and PNG decoding.

Every other project follows the same pattern, `Maviray.Pdf.<Name>`. That covers add-on packages such as `Maviray.Pdf.Fonts.System` and test projects such as `Maviray.Pdf.Tests`.

The [design document](docs/design/Maviray.Pdf-Design-Document.md) covers the details: the layout contract, the text pipeline, font subsetting, the writer and the test strategy.

## Roadmap

| Phase | Delivers |
| --- | --- |
| P0 · Foundations | PDF object model, streaming writer, content-stream builder, text in the standard 14 fonts; solution layout and CI |
| P1 · MVP layout | Fluent API, pages with header and footer, core containers, rich text across pages, TrueType subsetting, page numbers |
| P2 · Business documents | Tables, images, keep-together rules, components, font fallback, vector canvas |
| P3 · Typography and navigation | Kerning, ligatures, hyphenation, OpenType CFF fonts, links, bookmarks, table of contents |
| P4 · Compliance | PDF/A-2b and PDF/A-3b, embedded e-invoice XML (ZUGFeRD, Factur-X) |
| P5 · Advanced | Tagged PDF and PDF/UA, AES-256 encryption, SVG subset, right-to-left text |

Each phase ends at a gate with a testable criterion; the design document lists them. Phases have no dates yet. We size them after P0.

## Dependency policy

- **The core package ships with nothing else.** It references no NuGet packages and no native libraries, and makes no P/Invoke calls.
- **Trimming and Native AOT stay safe.** No runtime reflection and no dynamic code generation.
- **Tooling is allowed, because it never ships.** Test, benchmark and fuzzing tools are fine in test projects.
- **External validators check our output in CI.** These are qpdf, poppler (`pdftoppm`, `pdftotext`) and veraPDF.
- **Optional features go in add-on packages,** such as `Maviray.Pdf.Fonts.System` for system font discovery. This keeps the core small.

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The library targets `net10.0`, the current long-term support release, and `global.json` pins the SDK version.

To build the solution and run the tests, from the repository root:

```bash
dotnet build
dotnet test
```

Packages are published to GitHub Packages. [Versions and packages](docs/versions-and-packages.md) explains how to add the feed and how versions are numbered.

### A first document

This method writes a one-page invoice. It needs the `Maviray.Pdf` and `Maviray.Pdf.Fonts.Default` packages, and `using Maviray.Pdf;`. `RegisterNotoSans()` adds Noto Sans, the family of the default text style, to the process-wide font registry; a second call does nothing. Noto Sans is licensed under the SIL Open Font License.

```csharp
static void SaveInvoice(string path)
{
    FontRegistry.Default.RegisterNotoSans();

    (string Item, string Amount)[] lines =
    [
        ("Server maintenance, November", "€1,200.00"),
        ("Backup storage, 2 TB", "€80.00"),
        ("Support, 6 hours × €95.00", "€570.00"),
    ];

    Document.Create(document =>
    {
        document.Metadata(metadata => metadata.Title("Invoice 2026-0042").Author("Maviray GmbH").Language("en-GB"));
        document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(20, Unit.Millimetre);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().PaddingBottom(12).Text("Invoice 2026-0042").FontSize(20).Bold();

            page.Content().Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Maviray GmbH · Musterstraße 1 · 10115 Berlin").FontColor(Color.FromHex("#666666"));
                column.Item().PaddingTop(12).BorderBottom(1, Color.Black).Row(row =>
                {
                    row.Flex().Text("Item").Bold();
                    row.Fixed(100).AlignRight().Text("Amount").Bold();
                });
                foreach (var (item, amount) in lines)
                {
                    column.Item().Row(row =>
                    {
                        row.Flex().Text(item);
                        row.Fixed(100).AlignRight().Text(amount);
                    });
                }

                column.Item().BorderTop(1, Color.Black).AlignRight().Text("Total: €1,850.00").Bold();
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.PageNumber();
                text.Span(" of ");
                text.PageCount();
            });
        });
    }).Save(path);
}
```

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Maviray.Pdf/` | The library |
| `src/Maviray.Pdf.Fonts.Default/` | The default fonts package: Noto Sans and `RegisterNotoSans()` |
| `tests/Maviray.Pdf.Tests/` | Its tests |
| `docs/` | The design document (draft v0.6), the release records, the QA material, and how versions and packages work |
| `.github/workflows/` | The build check for pull requests, and package publishing |
| `GitVersion.yml` | The version rules |
| `global.json`, `nuget.config`, `Directory.Packages.props` | The SDK version, the package source and the package versions |
| `CONTRIBUTING.md` | How to report issues and contribute |
| `LICENSE` | Maviray.Pdf Licence (revenue-gated) |

## Contributing

Issues and pull requests are welcome. Pull requests target `develop`, and every merge into `develop` or `main` needs the maintainer's approval.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before you start. It covers branches, tests, signed commits and the clean-room rule. It also covers the terms under which contributions are accepted.

## Licence

Maviray.Pdf is licensed under the [Maviray.Pdf Licence](LICENSE), a revenue-gated licence. The source is public, so you can read it and debug into it, but the licence is not an open-source licence. Use is free in these cases:

- Organisations with annual gross revenue below EUR 1,000,000.
- Individuals, for personal, non-commercial use.
- Registered charities and non-profits.
- A 30-day evaluation that doesn't go into production.

Everyone else needs a commercial licence; contact devops@maviray.com. Documents you generate with the library are yours, with no restrictions.
