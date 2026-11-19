# P0.4 Helvetica text: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Text in the standard Helvetica font. The PDF holds the text as WinAnsi bytes from our own table, and `pdftotext` reads the source text back exactly, for ASCII and for Latin-1.

**Architecture:**
- `WinAnsiEncoding` maps Unicode to bytes with a 256-entry table taken from Annex D of ISO 32000.
- `StandardFont.Helvetica` describes the font dictionary.
- A per-document `FontResources` reserves each font's object on first use, names it `/F1`, and writes it once at `Close`.
- `ContentStreamBuilder` gains `BT`, `ET`, `Tf`, `Tm` and `Tj`, with the text-object sequencing rules from D-09. It also records which fonts the page used, so `EndPage` lists only those in `/Resources`.

**Tech Stack:** .NET 10, C#, xUnit v3, `pdftotext` (poppler in CI; xpdf from Git Bash locally), and qpdf in CI.

**Spec:** [P0 Foundations](../specs/2026-11-19-p0-foundations-design.md), decisions D-09 (the text operators) and D-10. Work item [#12](https://github.com/Maviray/pdf/issues/12). Builds on P0.3 ([plan](2026-11-19-issue-11-content-stream-builder.md)).

## Global Constraints

- The P0.1 to P0.3 constraints still hold.
- `System.Text.Encoding.CodePages` isn't used: it needs a process-wide registration (D-10).
- No NFC normalization in P0. A character outside the table is refused, and that includes a decomposed `ü`.
- `SetFont`, `SetTextMatrix` and `ShowText` are allowed only inside `BT`…`ET`. D-09 lists "text operators outside it" among the errors.
- The tracked font and size belong to the graphics state. They survive `ET` and are saved and restored by `q`/`Q`.

## Review Focus

1. **Characters that look encodable but aren't:** Turkish `ğ` and `ı`, Cyrillic, a decomposed `u` followed by U+0308, and a lone surrogate. Each must be refused with its position.
2. **Bytes the table must never produce:** the undefined codes `0x81`, `0x8D`, `0x8F`, `0x90` and `0x9D`, plus `0x7F` and the control codes. An exhaustive test over all 65,536 UTF-16 code units pins this.
3. **Text containing `(`, `)` or `\`.** `ShowText` must escape them, because it reuses `PdfString.WriteLiteral`.
4. **The font written once.** With two pages, the font dictionary is one object, and each page's `/Resources` names it. A page without text has no `/Font`.
5. **Mixing path and text.** `re`, `f`, `q`, `Q` and `cm` inside `BT` must throw. A colour change inside `BT` is allowed, because it colours the text.

---

### Task 1: `WinAnsiEncoding`

**Files:** `src/Maviray.Pdf/Writer/WinAnsiEncoding.cs`, test `tests/Maviray.Pdf.Tests/Writer/WinAnsiEncodingTests.cs`

**Interfaces:**
- Produces: `internal static class WinAnsiEncoding`
  - `static bool TryEncode(char character, out byte code)`
  - `static byte[] Encode(string text)`: throws `ArgumentException` for the first character outside the table. The message contains the character as `U+XXXX`, and its index.
- The table:
  - `0x20`–`0x7E` map to themselves.
  - `0xA0`–`0xFF` map to themselves: Latin-1, including U+00A0 (no-break space) and U+00AD (soft hyphen).
  - `0x80`–`0x9F` map as follows:

| Code | Char | Code | Char | Code | Char | Code | Char |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 80 | U+20AC € | 87 | U+2021 ‡ | 8E | U+017D Ž | 97 | U+2014 — |
| 82 | U+201A ‚ | 88 | U+02C6 ˆ | 91 | U+2018 ‘ | 98 | U+02DC ˜ |
| 83 | U+0192 ƒ | 89 | U+2030 ‰ | 92 | U+2019 ’ | 99 | U+2122 ™ |
| 84 | U+201E „ | 8A | U+0160 Š | 93 | U+201C “ | 9A | U+0161 š |
| 85 | U+2026 … | 8B | U+2039 ‹ | 94 | U+201D ” | 9B | U+203A › |
| 86 | U+2020 † | 8C | U+0152 Œ | 95 | U+2022 • | 9C | U+0153 œ |
|  |  |  |  | 96 | U+2013 – | 9E | U+017E ž |
|  |  |  |  |  |  | 9F | U+0178 Ÿ |

  `0x81`, `0x8D`, `0x8F`, `0x90` and `0x9D` are undefined. Characters below `0x20`, and `0x7F`, aren't in the table.

- [ ] **Step 1: Failing tests:**
  - `MapsAsciiToItself`: every char from `0x20` to `0x7E`.
  - `MapsLatin1ToItself`: every char from `0xA0` to `0xFF`.
  - `MapsTheWindows1252Characters`: a theory over the 27 rows of the table.
  - `NeverProducesUndefinedOrControlCodes`: for every char from 0 to 65535, `TryEncode` either fails, or gives a byte that isn't `0x81`, `0x8D`, `0x8F`, `0x90`, `0x9D` or `0x7F`, and isn't below `0x20`. The 27 mapped chars plus the two identity ranges give exactly 27 + 95 + 96 = 218 successes.
  - `RefusesCharactersOutsideTheTable`: a theory over `ğ` (U+011F), `ı` (U+0131), `Ж` (U+0416), `\n`, and `"ü"` (refused at index 1). Each throws `ArgumentException`, whose message contains the `U+` code and the index.
  - `EncodesAString`: `"Grüße aus Köln – 25 €"` gives the expected bytes.
- [ ] **Step 2:** Verify they fail. Expected: CS0103.
- [ ] **Step 3:** Implement: a static 65,536-entry lookup isn't needed. Check the ranges, and use a `switch` for the 27 characters.
- [ ] **Step 4:** Verify they pass.
- [ ] **Step 5:** Checkpoint.

### Task 2: `StandardFont` and `FontResources`

**Files:** `src/Maviray.Pdf/Writer/StandardFont.cs`, `src/Maviray.Pdf/Writer/FontResources.cs`, plus names in `PdfName.cs` (`Font`, `Subtype`, `Type1`, `BaseFont`, `Encoding`, `WinAnsiEncoding`, `Helvetica`). Tests: `StandardFontTests.cs` and `FontResourcesTests.cs`.

**Interfaces:**
- Produces:
  - `internal sealed class StandardFont`, with `static StandardFont Helvetica`, `PdfName BaseFont`, and `PdfDictionary CreateDictionary()`. The dictionary is `<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>`, with no `/Widths`, as D-10 allows.
  - `internal sealed class FontResources`
    - `FontResources(PdfFileWriter file)`
    - `PdfName NameOf(StandardFont font)`: names the first font `/F1`, the next `/F2` and so on, and reserves its object on first use.
    - `PdfReference ReferenceOf(StandardFont font)`
    - `void WriteAll()`: writes each used font's dictionary once.
- [ ] Tests:
  - `HelveticaDictionary`: the exact bytes.
  - `NamesAFontOnceAndReservesItOnce`: two calls give the same name and reference.
  - `WriteAllWritesEachFontOnce`: checked through a file whose object count is known.
- [ ] Red, green, checkpoint.

### Task 3: Text operators in `ContentStreamBuilder`

**Files:** modify `ContentStreamBuilder.cs`, test `ContentStreamBuilderTests.cs`.

**Interfaces:**
- Consumes: `WinAnsiEncoding.Encode`, `PdfString.WriteLiteral`, `FontResources.NameOf`.
- Produces:
  - The builder's constructor becomes `ContentStreamBuilder(FontResources fonts)`.
  - New operators:
    - `BeginText()` writes `BT`, and `EndText()` writes `ET`.
    - `SetFont(StandardFont font, double size)` writes `/F1 24 Tf`.
    - `SetTextMatrix(a, b, c, d, e, f)` writes `Tm`.
    - `ShowText(string text)` writes `(…) Tj`.
  - `IReadOnlyCollection<StandardFont> FontsUsed`
  - `Finish()` also throws when `BT` is open.
- [ ] Tests:
  - The exact bytes of each operator.
  - `ShowText("a(b)c\\d")` gives `(a\(b\)c\\d) Tj`.
  - `SetFont` is skipped when the font and size are already current, including after `ET` and in a new `BT`.
  - `q`/`Q` restores the font.
  - Each sequencing error is its own case:
    - `ShowText`, `SetFont` or `SetTextMatrix` outside `BT`;
    - `Rectangle`, `Fill`, `SaveState`, `RestoreState` or `Transform` inside `BT`;
    - nested `BT`;
    - `ET` without `BT`;
    - `ShowText` before any `SetFont`;
    - `Finish` with `BT` open.
  - `SetFillColor` inside `BT` is allowed.
  - An unencodable character in `ShowText` throws `ArgumentException`, and nothing is written.
- [ ] Red, green, checkpoint.

### Task 4: Fonts in the page resources

**Files:** modify `PdfDocumentWriter.cs` and `PdfPageWriter.cs`, test `PdfDocumentWriterTests.cs`.

**Interfaces:**
- `PdfDocumentWriter` owns one `FontResources`, and passes it to each page's builder.
- `EndPage` writes `/Resources <</Font <</F1 n 0 R>>>>` when the page used fonts, and `/Resources <<>>` otherwise.
- `Close` calls `FontResources.WriteAll()` before the page tree.
- [ ] Tests:
  - `TwoPagesShareOneFontObject`: both page dictionaries name the same `/F1 n 0 R`, and object n is the Helvetica dictionary, written once.
  - `APageWithoutTextHasNoFontResources`.
- [ ] Red, green, checkpoint.

### Task 5: Text extraction

**Files:** test `tests/Maviray.Pdf.Tests/Writer/TextExtractionTests.cs`

- [ ] Tests:
  - `PdftotextReadsAsciiTextBack`: Helvetica 24 at (72, 750), `"Hello, world"`. `Poppler.ExtractText` returns exactly `"Hello, world"`.
  - `PdftotextReadsLatin1TextBack`: the same with `"Grüße aus Köln – 25 €"`.
  - `TextPagesPassQpdf`: `Qpdf.Check` on both files.
  - From Git Bash, the first two run locally with Git for Windows' xpdf `pdftotext`, so they can be seen failing before Task 4's resources exist. CI's poppler is the authority. If xpdf and poppler disagree, the CI result stands and the log records the difference.
- [ ] Red, green, checkpoint.

### Task 6: Records

- [ ] Write the execution log, update the release index row for #12, verify, commit, push, and open the PR.
