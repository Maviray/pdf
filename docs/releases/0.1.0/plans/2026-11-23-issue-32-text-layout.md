# P1.7 Text layout: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The text engine: prepare a paragraph of styled spans once, then fill it at any width into lines of positioned glyphs and decorations. Its layout draws on a PDF page that passes qpdf.

**Architecture:** `PreparedParagraph.Prepare` cleans and normalizes each span (P1.5), joins them, finds clusters and break opportunities (P1.6), and resolves every code point to glyphs by T-07's rules. `Fill` walks the clusters greedily from one break opportunity to the next, measures each candidate line as if it ended there, and then places the glyphs, the alignment, the letter spacing and the decorations. The font parser reads three more values, and `ContentStreamBuilder` gains the `TJ` form of `ShowGlyphs`.

**Tech Stack:** .NET 10, C#, xUnit v3 on MTP, qpdf in CI.

**Spec:** [P1 Text](../specs/2026-11-23-p1-text-design.md), decisions T-07 to T-14 (and T-03 for spans). Work item [#32](https://github.com/Maviray/pdf/issues/32). It builds on P1.5 (#30, PR #40) and P1.6 (#31, PR #41).

## Global Constraints

- Every P1.5 and P1.6 constraint holds: `internal` types in `src/Maviray.Pdf/Text/` (namespace `Maviray.Pdf.Text`), generated tables only in `src/Maviray.Pdf/Text/Unicode/`, one type per file (a private nested type is allowed, as in `ContentStreamBuilder`), no partial classes, no ICU and no culture, warnings as errors, trimming- and AOT-safe.
- The text engine uses the font engine and nothing else. It never uses the writer (spec Constraints). Only the tests draw a layout into a PDF.
- Prepared paragraphs and layouts are immutable, and share nothing mutable, so they can be used across threads.
- Lengths are points, in `double`. Positions are from the paragraph's left edge and each line's top, with y growing downward (T-14).
- Test sources stay ASCII. Characters beyond ASCII are built from code points (`char.ConvertFromUtf32`, `(char)0x00AD`), never typed as `\u` escapes, which tool input turns into the characters themselves.
- Expected values come from the spec, the test font's own numbers (set by the builder), or the committed Noto Sans file as the spec quotes it. A value the engine computes is never its own expectation.
- `Fill` runs many times per paragraph. It doesn't repeat preparation, and doesn't use LINQ or allocate per glyph beyond the layout it returns.
- The work is committed as three commits: the font and content-stream additions, the text engine, and the records.

## Decisions this plan makes

- **Names.** `TextStyle`, `TextSpan`, `TextAlignment`, `Paragraph`, `PreparedParagraph`, `TextLayout`, `TextLine`, `GlyphRun`, `PositionedGlyph`, `TextDecoration`, `DecorationKind`, `MissingGlyph`, `FontLineMetrics`, `CharacterProperties` and the generated `CharacterData`, as **Interfaces** fixes them.
- **Clusters own their style.** A cluster belongs to the span of its first code point: that span's style gives the letter spacing after it, and its decoration run. A cluster can cross spans when a span starts with a combining mark (T-03), and its glyphs then sit in the runs of their own spans.
- **Lines partition the text.** Each line's range runs to the next line's start, so it holds its hanging spaces and its break character. Concatenating every line's range gives the text back.
- **Missing glyphs** are listed once per code point and span, in the order of first occurrence.
- **A hyphen at a soft hyphen** is drawn when the line ends right after the soft hyphen and another line follows. It is the last drawn cluster of its line, so no letter spacing follows it, and it belongs to the soft hyphen's span. It copies out as U+00AD.
- **Glyph positions** are absolute from the paragraph's left edge, so they include the line's offset. A run's `X` is its first glyph's `X`.
- **The glyph-rule table** holds only what T-07 tests: general category Cc, general category Zs, and `Default_Ignorable_Code_Point`. Its 17 distinct blocks (128 code points each) make a small table.

## Data

- Noto Sans Regular, as the spec quotes it (T-08, T-11): `unitsPerEm` 1000; `OS/2` typographic 1069, −293, 0 with `USE_TYPO_METRICS` set; `hhea` the same; `post.underlinePosition` −100 and `underlineThickness` 50; `OS/2.yStrikeoutPosition` 322 and `yStrikeoutSize` 50. So its normal line height is 1.362, and at 10 pt the underline spans 1 to 1.5 pt below the baseline and the strikethrough 3.22 to 2.72 pt above it.
- From the UCD 16.0.0 files, by a throwaway script: 65 code points are Cc; 17 are Zs (U+0020, U+00A0, U+1680, U+2000 to U+200A, U+202F, U+205F and U+3000); 4,174 are `Default_Ignorable_Code_Point`, among them U+00AD, U+034F, U+200B, U+200D, U+2060, U+FE0F and U+FEFF, but not U+0009 or U+00A0.
- **The layout test font**, built with `TestFontBuilder` in every layout test: `unitsPerEm` 1000; glyph 0 `.notdef` advance 500; glyph 1 space, advance 250, for U+0020; glyph 2 a letter, advance 500, for `a` to `z`, `A` to `Z`, `0` to `9`, `.` and `,`; glyph 3 hyphen-minus, advance 300, for U+002D; glyph 4 hyphen, advance 320, for U+2010; glyph 5 combining acute, advance 0, for U+0301; glyph 6 `W`, advance 900. `hhea` and typographic metrics 800, −200, 0, `USE_TYPO_METRICS` not set; underline −100 and 50; strikeout 50 at 300. At size 10 a letter is 5 pt, a space 2.5 pt and a line 10 pt.

## Review Focus

1. **A cluster that crosses spans**: span 2 starts with U+0301 after span 1's `e`. NFC per span leaves it decomposed, the cluster stays whole, and its letter spacing and decoration follow span 1. Pinned in Task 7.
2. **Soft hyphens in every position**: at a line end with text after it (hyphen drawn), at the paragraph's end and before a newline (nothing drawn), and in the middle of a line (nothing, no width). Pinned in Task 6.
3. **The width round trip** with letter spacing, negative letter spacing, mixed sizes and hanging spaces, over random paragraphs and widths with the seed logged. Pinned in Task 9.
4. **Text that is only spaces or only break characters**, and spaces before a required break: they hang, so `"a   \nb"` is two lines of width 5. Pinned in Task 6.
5. **Widths below one cluster**: every line still takes one cluster, the overflowing line reports its true width, and filling ends. Pinned in Task 6.

---

### Task 1: Font parser additions and the test font builder

**Files:**
- Modify: `src/Maviray.Pdf/Fonts/Os2Table.cs`, `src/Maviray.Pdf/Fonts/TrueTypeFont.cs`
- Modify (tests): `tests/Maviray.Pdf.Tests/Fonts/TestFontBuilder.cs`, `tests/Maviray.Pdf.Tests/Validation/fonttools_check.py`, `tests/Maviray.Pdf.Tests/Validation/FontDump.cs`, `tests/Maviray.Pdf.Tests/Fonts/FontToolsAgreementTests.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/TrueTypeFontTests.cs` (adds tests)

**Interfaces:**
- `Os2Table` gains `short StrikeoutSize` (offset 26) and `short StrikeoutPosition` (offset 28), read in every version.
- `TrueTypeFont` gains `int? StrikeoutSize`, `int? StrikeoutPosition` (null without `OS/2`) and `bool UseTypographicMetrics` (`fsSelection` bit 7; false without `OS/2`).
- `TestFontBuilder` gains `TypographicMetrics(short ascender, short descender, short lineGap)`, `Underline(short position, short thickness)` and `Strikeout(short size, short position)`. Its defaults stay as they are (typographic 800, −200, 0; underline −100, 50), with a strikeout of 0 and 0.

- [ ] **Step 1: Failing tests** (`TrueTypeFontTests`):
  - `ReadsStrikeoutAndTypoMetricsFlag`: a built font with `Strikeout(50, 300)` and `Os2(4, 400, 0x00C0)` reads 50, 300 and true.
  - `NoOs2MeansNoStrikeout`: `Without("OS/2")` reads null, null and false.
  - `NotoSansRegularMatchesTheSpec`: the committed Regular reads strikeout 50 at 322, underline −100 and 50, typographic 1069, −293, 0, and `UseTypographicMetrics` true.
- [ ] **Step 2:** Verify they fail to compile, implement, and verify they pass.
- [ ] **Step 3:** The fontTools dump adds `strikeoutSize`, `strikeoutPosition` and `useTypoMetrics` (`fsSelection & 0x80 != 0`), from `OS/2` or 0, 0, false. `FontDump` reads them, and `FontToolsAgreementTests` compares them (T-12). Locally the test skips without fontTools; CI runs it.

### Task 2: The `TJ` form of `ShowGlyphs`

**Files:**
- Modify: `src/Maviray.Pdf/Writer/ContentStreamBuilder.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/ContentStreamGlyphsTests.cs` (adds tests)

**Interfaces:**
- `ContentStreamBuilder ShowGlyphs(ReadOnlySpan<ushort> cids, ReadOnlySpan<double> extraAdvances)`: one extra advance in points after each CID. It makes the checks of the `Tj` form, and also refuses before writing anything: lengths that differ (`ArgumentException`), an advance that isn't finite (`ArgumentOutOfRangeException`), and a font size of 0 (`InvalidOperationException`). Each run of CIDs with no extra advance between them is one hex string; a non-zero advance d is written after its CID as −d × 1000 ÷ the font size, in the P0 number format; a zero advance writes nothing.

- [ ] **Step 1: Failing tests:** at size 10, CIDs 1, 2, 3 with advances 0, 0.5, 0 write exactly `[<00010002> -50 <0003>] TJ` (the spec's example); all zero writes `[<000100020003>] TJ`; −0.25 after CID 1 writes `[<0001> 25 <0002>] TJ`; an advance after the last CID is written after its string; each refusal leaves the stream unchanged.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.
- [ ] **Step 3: Commit** Tasks 1 and 2: `#32 read the strikeout and typo-metrics flag, and add the TJ form of ShowGlyphs`.

### Task 3: The glyph-rule table

**Files:**
- Modify (tests): `tests/Maviray.Pdf.Tests/Text/Unicode/UnicodeTableGenerator.cs`
- Create (tests): `tests/Maviray.Pdf.Tests/Text/Unicode/CharacterModel.cs`, `CharacterModelTests.cs`
- Create (src): `src/Maviray.Pdf/Text/CharacterProperties.cs`; generated `src/Maviray.Pdf/Text/Unicode/CharacterData.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/CharacterPropertiesTests.cs`

**Interfaces:**
- `CharacterModel` (tests): `bool IsControl(int)` (Cc), `bool IsSpaceSeparator(int)` (Zs), `bool IsDefaultIgnorable(int)`, from `UnicodeData.txt` and `DerivedCoreProperties.txt`.
- Generated `CharacterData`: a two-stage table like the others, with one `byte` entry per code point: bit 0 Cc, bit 1 Zs, bit 2 default ignorable. The generator's two-stage method takes the entry type.
- `CharacterProperties` (src): `IsControl`, `IsSpaceSeparator`, `IsDefaultIgnorable`, false outside the code space.

- [ ] **Step 1: Failing tests:** the counts and examples of **Data**; the lookups agree with the model for every code point (the encoding check); values outside the code space give false.
- [ ] **Step 2:** Verify they fail, add the model and the table (the staleness test fails first, naming `CharacterData.cs`), write the table, implement the lookups, and verify all pass.

### Task 4: Styles, spans, paragraphs and line metrics

**Files:**
- Create: `src/Maviray.Pdf/Text/TextStyle.cs`, `TextSpan.cs`, `TextAlignment.cs`, `Paragraph.cs`, `FontLineMetrics.cs`
- Create (tests): `tests/Maviray.Pdf.Tests/Text/LayoutTestFont.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/TextStyleTests.cs`, `FontLineMetricsTests.cs`

**Interfaces:**
- `sealed class TextStyle(TrueTypeFont font, double size, double? lineHeight = null, double letterSpacing = 0, bool underline = false, bool strikethrough = false)`, with the properties of the same names. `ArgumentNullException` for no font; `ArgumentOutOfRangeException`, naming the parameter, for a size or line height that isn't positive and finite, or a letter spacing that isn't finite.
- `sealed class TextSpan(string text, TextStyle style)`; `enum TextAlignment { Left, Center, Right }`; `sealed class Paragraph(IEnumerable<TextSpan> spans, TextAlignment alignment = TextAlignment.Left)` with `IReadOnlyList<TextSpan> Spans` (a copy) and `Alignment`. Null spans, a null span or an undefined alignment throw.
- `readonly record struct FontLineMetrics(int Ascent, int Descent, int LineGap, int UnitsPerEm)`, `static FontLineMetrics Of(TrueTypeFont font)`: T-08's metric set. Methods `double NormalLineHeight` ((A − D + G) ÷ `unitsPerEm`) and `(double Above, double Below) Extent(double size, double? lineHeight)`: with m the line height or the normal one, L = m·s − (A − D)·s ÷ `unitsPerEm`; above A·s ÷ `unitsPerEm` + L/2, below −D·s ÷ `unitsPerEm` + L/2.
- `LayoutTestFont` (tests): `static TrueTypeFont Create(Action<TestFontBuilder>? change = null)`, the font of **Data**, and `static TextStyle Style(double size = 10, ...)` over it.

- [ ] **Step 1: Failing tests:**
  - `TextStyleTests`: each refusal names its parameter (size 0, −1, NaN, ∞; line height 0; letter spacing NaN); valid values are kept.
  - `FontLineMetricsTests`: bit 7 set picks the typographic values and unset picks `hhea` (built with different values for each); without `OS/2`, `hhea`; a negative line gap counts as 0; A − D of 0 gives 0.8 and −0.2 em; Noto Sans Regular's normal line height is 1.362, and at 10 pt its extent is 10.69 above and 2.93 below; with line height 2 at 10 pt on the test font the leading is 10, so 13 above and 7 below.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 5: Preparing a paragraph

**Files:**
- Create: `src/Maviray.Pdf/Text/PreparedParagraph.cs`, `MissingGlyph.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/PreparedParagraphTests.cs`

**Interfaces:**
- `sealed class PreparedParagraph`, `static PreparedParagraph Prepare(Paragraph paragraph)`:
  - `string Text`: each span's text through `TextNormalizer.ToNfc`, joined;
  - `IReadOnlyList<int> SpanStarts`: each span's start in `Text`, then `Text.Length`;
  - `IReadOnlyList<MissingGlyph> Missing`, with `readonly record struct MissingGlyph(int CodePoint, int SpanIndex)`;
  - `Paragraph Source`;
  - internally, per code point: its UTF-16 start, its span, its glyphs (glyph ID, advance in points, copy-out code point), and what kind it is (break character, hanging space, soft hyphen, other); per boundary, the cluster starts and `BreakOpportunities.Find`'s kinds.
- T-07's rules in order, with the font of the code point's span: break characters (U+000A, U+000B, U+000C, U+000D, U+0085, U+2028, U+2029) draw nothing; U+0009 draws U+0020's glyph and advance, or nothing, and copies out as U+0009; U+00AD draws nothing at preparation (Task 6 decides); a mapped code point draws its glyph; an unmapped default ignorable or Cc code point draws nothing; an unmapped Zs draws U+0020's glyph and copies out as itself; an unmapped code point whose full canonical decomposition the font maps entirely draws those glyphs, each with its own code point; anything else draws glyph 0 and is reported missing.

- [ ] **Step 1: Failing tests:** each rule of the table, on the test font: a tab; U+00A0 and U+202F drawn as spaces; U+200B, U+FEFF and U+0001 drawing nothing and not reported; U+00C5, which the test font doesn't map, drawn as its decomposition: glyph 2 for U+0041 and glyph 5 for U+030A, which this test's font also maps; U+4E00 drawn as glyph 0 and reported once with its span, even when it appears twice; a span boundary inside a cluster (span 2 starts with U+0301) keeps the cluster whole; spans' texts normalized one by one (`"Ko"` plus U+0308 `"ln"` becomes `Köln`); empty spans and no spans give empty text; unpaired surrogates become U+FFFD.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 6: Filling lines

**Files:**
- Create: `src/Maviray.Pdf/Text/TextLayout.cs`, `TextLine.cs`
- Modify: `src/Maviray.Pdf/Text/PreparedParagraph.cs` (adds `Fill`)
- Test: `tests/Maviray.Pdf.Tests/Text/FillTests.cs`

**Interfaces:**
- `TextLayout PreparedParagraph.Fill(double width)`: `ArgumentOutOfRangeException` unless the width is positive and finite or positive infinity.
- `sealed class TextLayout`: `double Width` (the widest line's width), `double Height` (the lines' heights summed), `IReadOnlyList<TextLine> Lines`.
- `sealed class TextLine`: `int Start`, `int Length`, `double Width`, `double Offset`, `double Top`, `double Height`, `double Baseline` (from the line's top), `IReadOnlyList<GlyphRun> Runs`, `IReadOnlyList<TextDecoration> Decorations`. Tasks 7 and 8 fill the last two; this task leaves them empty and `Offset` 0.
- **Algorithm (T-09):** fill each piece between required breaks on its own. Keep the line's width as if it ended after the last segment taken: the drawn advances, plus each drawn cluster's letter spacing except the last one's, without trailing spaces (U+0020, U+0009) and the break character, plus the hyphen's advance when the line would end at a soft hyphen. A segment joins the line when that width, with the segment, is at most the width plus 10⁻⁶. *Changed during Task 9:* segments join while the line fits without the hyphen, and the line ends after the last of them with which it fits as drawn, because the round-trip test found that the first rule breaks the round trip (the execution log, and T-09's amendment). A segment that doesn't fit on an empty line is cut between clusters: the most clusters that fit, at least one, with no hyphen. Then T-08: each line's height above and below the baseline is the largest of the spans with a code point on it; a line with only a break character takes that character's span.

- [ ] **Step 1: Failing tests**, at size 10 on the test font (letter 5 pt, space 2.5 pt, line 10 pt):
  - `GreedyFill`: `"aa bb cc"` at 25 pt gives `"aa bb "` and `"cc"`, widths 22.5 and 10; at 22.4999995 too (the tolerance) and at 22.49 `"aa "`, `"bb "`, `"cc"`.
  - `HangingSpaces`: `"a   \nb"` gives two lines of width 5, and a line of only spaces has width 0.
  - `RequiredBreaks`: `"a\n\nb"` three lines, `"\n"` one empty line of height 10, `""` none and height 0; `"a\r\nb"` two lines; every line's range together gives the text back.
  - `SoftHyphen` (built with `(char)0x00AD`): `"aaa"`, SHY, `"bbb"` at 18.5 pt gives `"aaa"` and the SHY, width 18.2 with U+2010's 3.2 pt, then `"bbb"`. Without U+2010 in the font, U+002D's 3 pt is used and the width is 18. At the paragraph's end, before `\n`, and in the middle of a line, the SHY adds no width.
  - `LongWord`: `"aaaaaa"` at 12 pt gives `"aa"`, `"aa"`, `"aa"`; `"W"` at 1 pt gives one line of width 9, which overflows; normal breaking resumes after a cut (`"aaaaa bb"` at 12 pt gives `"aa"`, `"aa"`, `"a "`, `"bb"`).
  - `LetterSpacing` (Task 7 places it; here only width): 1 pt of letter spacing makes `"aa"` 11 pt wide and `"aa "` still 11.
  - `MixedSizes`: a 10 pt and a 20 pt span on one line give 16 above and 4 below, a height of 20, from the test font's 0.8 and 0.2 em at the larger size; a line with only the 10 pt span is 10 high, 8 above and 2 below.
  - `InvalidWidths`: 0, −1, NaN and −∞ throw, naming `width`.
  - `Infinity`: at infinity each piece is one line.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 7: Alignment, letter spacing and glyph runs

**Files:**
- Create: `src/Maviray.Pdf/Text/GlyphRun.cs`, `PositionedGlyph.cs`
- Modify: `src/Maviray.Pdf/Text/PreparedParagraph.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/GlyphRunTests.cs`

**Interfaces:**
- `sealed class GlyphRun`: `int SpanIndex`, `double X`, `IReadOnlyList<PositionedGlyph> Glyphs`; `readonly record struct PositionedGlyph(ushort GlyphId, int CodePoint, double X, double ExtraAdvance)`.
- **T-10:** a line's offset is 0, (W − w) ÷ 2 or W − w, and 0 when w > W or W is infinite. Each drawn glyph of a line, hanging spaces excepted, is placed in order: a cluster's last glyph carries its style's letter spacing as `ExtraAdvance`, except the line's last drawn cluster; other glyphs carry 0. A run ends where the span changes.

- [ ] **Step 1: Failing tests:** left, centre and right offsets at 30 pt for `"aa"` (0, 10, 20), and 0 for an overflowing line and at infinity; glyph X values of `"ab"` with 1 pt letter spacing (0 and 6), the last glyph's extra 0; `e` U+0301 `b` with 1 pt of letter spacing puts `e` at 0 with extra 0, the mark at 5 with extra 1, and `b` at 6; two spans give two runs with their span indexes; the cross-span cluster of the Review Focus follows span 1's letter spacing; the soft hyphen's hyphen glyph has code point U+00AD and extra 0; hanging spaces aren't in any run; a tab's glyph has code point U+0009.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 8: Underline and strikethrough

**Files:**
- Create: `src/Maviray.Pdf/Text/TextDecoration.cs`, `DecorationKind.cs`
- Modify: `src/Maviray.Pdf/Text/PreparedParagraph.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/DecorationTests.cs`

**Interfaces:**
- `enum DecorationKind { Underline, Strikethrough }`; `readonly record struct TextDecoration(DecorationKind Kind, int SpanIndex, double X, double Width, double Top, double Thickness)`, with `Top` measured down from the line's top.
- **T-11:** one rectangle per run of consecutive clusters of one decorated span on a line, from the first cluster's X to the end of the last cluster's advance, inner spaces and letter spacing included, hanging spaces not. Underline top `post.underlinePosition` and thickness `post.underlineThickness`; with a thickness of 0, top −0.1 em and thickness 0.05 em. Strikethrough `yStrikeoutPosition` and `yStrikeoutSize`; without them or with a size of 0, top 0.3 em with the underline's thickness.

- [ ] **Step 1: Failing tests:** `"ab cd "` underlined at 10 pt on the test font gives one underline of width 22.5 (hanging space excluded), top 1 pt below the baseline, 0.5 thick; with letter spacing 1 the width is 26.5, the inner space being a drawn cluster too; a strikethrough with the test font's 50 at 300 is 3 pt above the baseline and 0.5 thick, and with a strikeout of 0 it is at 3 pt with the underline's thickness; a font with underline thickness 0 gets 1 pt below and 0.5 thick; two adjacent decorated spans give two rectangles; Noto Sans Regular at 10 pt gives the spec's 1 to 1.5 pt below and 3.22 to 2.72 pt above.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 9: The round trip and a PDF page

**Files:**
- Create (tests): `tests/Maviray.Pdf.Tests/Text/LayoutPainter.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/RoundTripTests.cs`, `tests/Maviray.Pdf.Tests/Text/TextLayoutPdfTests.cs`

**Interfaces:**
- `LayoutPainter` (tests), `static void Paint(PdfPageWriter page, PreparedParagraph paragraph, TextLayout layout, IReadOnlyDictionary<TrueTypeFont, EmbeddedFont> fonts, double left, double top)`: per run, `BeginText`, `SetFont`, `SetTextMatrix` at the run's X and the line's baseline (the page's y flipped), `ShowGlyphs` with the run's CIDs (from `EmbeddedFont.Encode(glyphId, codePoint)`) and extra advances, `EndText`; each decoration a filled rectangle. P1.8's gate reuses it.

- [ ] **Step 1: Tests:**
  - `RoundTripTests.FillingAtTheMeasuredWidthGivesTheSameLines`: 2,000 random paragraphs from a seed written to the test output (`re-run with SEED=…`). Each has one to four spans with sizes 6 to 20, letter spacing −0.5 to 2 and random decorations, over an alphabet of letters, `W`, spaces, tabs, U+00A0, U+00AD, `-`, U+2010, U+0301 and `\n`, filled at random widths from 1 to 300 pt and at infinity. When no line is wider than its width plus 10⁻⁶, filling again at the layout's width gives the same line ranges. The awkward values occur by construction: each paragraph has at least one soft hyphen and one run of spaces.
  - `TextLayoutPdfTests.ParagraphDrawsOnAPageThatPassesQpdf`: a German paragraph of about 40 words in Noto Sans Regular at 10 pt, with a soft hyphen, an underlined span and 0.5 pt letter spacing, filled at 200 pt and painted on an A4 page. qpdf accepts it (skipped locally without qpdf); writing it twice gives the same bytes; the file is saved under `generated/`.
- [ ] **Step 2:** Run them. A round-trip failure prints its seed and the paragraph; it is diagnosed with superpowers:systematic-debugging before any change. Expected: both pass.
- [ ] **Step 3:** Run the full suite in Debug and Release, and the text tests without ICU. Expected: all pass, no warnings.
- [ ] **Step 4: Commit** Tasks 3 to 9: `#32 add the text engine: prepare a paragraph, then fill it at a width`.

### Task 10: Records

- [ ] **Step 1:** Add the row for #32 to the release index, and write the execution log.
- [ ] **Step 2: Commit** the plan, the log and the index: `#32 add the P1.7 plan and execution log`. Push, and open the pull request into `develop`, stacked on #41.
