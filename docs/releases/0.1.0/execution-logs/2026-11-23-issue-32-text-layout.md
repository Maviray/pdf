# P1.7 Text layout: execution log

Issue [#32](https://github.com/Maviray/pdf/issues/32). Plan: [2026-11-23-issue-32-text-layout.md](../plans/2026-11-23-issue-32-text-layout.md).

The branch `feature/32-text-layout` is stacked on `feature/31-line-breaking` (PR #41), which is stacked on #40. Neither was merged yet.

## 2026-11-23

- **Planning.**
  - The font parser already read the `OS/2` typographic metrics and the `post` underline values. It lacked the strikeout values and `USE_TYPO_METRICS`.
  - `ShowGlyphs` had only the `Tj` form.
  - A throwaway script counted what the glyph rules test: 65 Cc, 17 Zs and 4,174 default ignorables. The flags take only 17 distinct blocks, so the table is small. U+00AD is default ignorable, so its own rule has to come first, as the spec's table orders it.
  - The plan's self-check corrected two of its own expected values before execution: the mixed-size line is 20 pt high, not 30, and the underline with letter spacing is 26.5 pt wide, not 25.5, because the inner space is a drawn cluster.
- **Task 1, the parser additions:** the committed Noto Sans Regular reads the spec's values: strikeout 50 at 322, underline −100 and 50, typographic 1069, −293, 0, with `USE_TYPO_METRICS` set. The fontTools comparison of the new values runs in CI only.
- **Task 2, `TJ`:** the bytes match the spec's example, `[<00010002> -50 <0003>] TJ`, and every refusal happens before anything is written. Tasks 1 and 2 were committed as `794b619`.
- **Task 3, the character table:** 10,880 bytes of data: 8,704 in the index and 2,176 in its 17 blocks. The lookups agree with the model for every code point.
  - **Slip:** the model was written in the same step as its tests, so those weren't seen failing first. They pin values from the independent script.
- **Task 4, styles and metrics:** Noto Sans's normal line height comes out at 1.362, with 10.69 pt above and 2.93 pt below at 10 pt, as T-08 says.
- **Task 5, preparation:** each of T-07's rules is pinned on the layout test font.
  - **Ruling:** `GlyphsAt` and `IsClusterStart` expose what preparation resolved, so the rules can be tested before glyphs are placed.
- **Task 6, filling:**
  - **Ruling:** `TextLayout.Source`, and the run and decoration types, were created here, because `TextLine` declares them.
  - **Found by its own test:** a word too wide for any line was quadratic. `ALongWordAtATinyWidthStaysLinear` timed out at 10 s with 200,000 letters at 1 pt, because each cut scanned the rest of the word.
    - **Ruling:** a cut now stops at the first cluster that doesn't fit. Widths only fall as a cluster is added when a negative letter spacing is larger than a cluster's advance, and then a cut line holds fewer clusters than could fit.
- **Task 7, glyph runs:** offsets, letter spacing on each cluster's last glyph, marks without spacing, one run per span segment, the cross-span cluster, the hyphen glyph copying out as U+00AD, and nothing for hanging spaces.
- **Task 8, decorations:** extents, the fallbacks, and Noto Sans at the spec's positions: the underline 1 to 1.5 pt below the baseline, and the strikethrough 3.22 to 2.72 pt above it.
- **Task 9, the round trip and a PDF page:**
  - **The round-trip test failed on 4 of about 4,000 fills** (`SEED=1459252044`).
    - Each failing line held a soft hyphen followed by something narrower than the hyphen: a no-break space, or another soft hyphen. T-09 tested a segment that ends at a soft hyphen with its hyphen, "even if a later segment then joins the line". So the line ended at that soft hyphen could be wider than the finished line, and filling at the finished line's width broke it earlier.
    - The spec's fit rule contradicted its own round trip, which auto widths in the layout spec depend on.
    - **Ruling:** segments join while the line fits without the hyphen, and the line ends after the last of them with which it fits as drawn, with the hyphen where it ends at a soft hyphen. T-09's wording is amended, with a dated note. `ASoftHyphenFollowedByLessThanAHyphenKeepsItsLine` pins the case.
    - The failing seed passes now, and so do 20 more seeds, about 40,000 paragraphs.
  - The PDF test paints German prose in Noto Sans Regular at 200 pt, with an underlined span, letter spacing and a soft hyphen. Every glyph is in the font, every line is within the width, and the bytes repeat. qpdf checks it in CI; it isn't installed locally.
  - **Ruling:** `LayoutPainter.Paint` takes the layout alone, since `TextLayout.Source` gives its paragraph.
  - **Full suite:**
    - Debug: 787 tests, 758 passed and 29 skipped;
    - Release: 787 tests, 759 passed and 28 skipped (the 27 validators and the new qpdf check);
    - without ICU: the text tests passed 279 of 280, the qpdf check skipped.
  - The engine was committed as `c87671d`.
- **Whole-branch review** by a fresh reviewer, which was told not to download or install anything: merge with fixes, none of them critical.
  - **An independent reference.** The reviewer wrote its own filler in F#, from the spec's words, and compared it with `Fill` on about 120,000 random fills. These included CR, U+2028, ZWSP, U+4E00, U+0001, fonts without U+2010, and every boundary width.
    - After one rule difference, the cut, there were no differences across about 61,000 fills: in line ranges, widths, offsets, heights, baselines, runs, glyph positions and extra advances, copy-out code points, the hyphen glyph, and decorations.
    - A simulated viewer walk of the `TJ` arrays matched every glyph position.
  - **Performance:** `Fill` is linear at 50,000 to 200,000 characters and allocates nothing per glyph.
  - **Fixed, important:**
    - **A line holding only a hyphen.** A segment that fits without its hyphen but not with it, with nothing after it fitting, was cut before its soft hyphen. The soft hyphen then made a line with a hyphen alone: `Kunden` SHY `nummer` in Noto Sans at 38 pt, and four of five real compounds at some narrow widths. Now the cut takes the whole segment and draws no hyphen, and a soft hyphen draws a hyphen only after drawn text on its line. Three tests failed first, the Noto Sans case among them.
    - **The extent below the baseline was clamped at 0**, against T-08's negative leading: a line height of 0.5 gave 5.5 pt instead of 5. `NegativeLeadingShrinksTheLine` failed first.
    - **Invisible characters drawn.** T-07 put "mapped by the font" before the default ignorables, so Noto Sans drew U+2060 as a 6 pt gap, and U+034F, U+200E and U+200F as glyphs with outlines, against the spec's anti-goal.
      - **Ruling:** default ignorables and controls draw nothing whatever the font maps, and T-07 is amended.
      - The test failed first on all six code points.
    - **The round trip with negative letter spacing.** The amended rule stopped at the first segment whose plain width doesn't fit, but a line can narrow as it grows: a zero-advance mark after ZWSP, under negative spacing.
      - **Ruling:** a line ends after the last segment, up to the next required break, with which it fits as drawn. The scan stops once a lower bound on any later end, the plain width plus every negative contribution still possible, is past the limit. Without negative letter spacing that bound is the plain width, so ordinary text fills as before. T-09 is amended again.
      - `ALineThatNarrowsAsItGrowsKeepsItsLine` and the widened round trip (`SEED=129047434`) failed first. 20 more seeds pass.
  - **Fixed, graded up from minor:** the cross-span decoration the plan said was pinned now is. The round-trip alphabet adds ZWSP, CR, U+2028, `.` and `,`, and half the spans use a font without U+2010.
  - **Full suite:** Debug 799 tests, 770 passed and 29 skipped; Release 799 tests, 771 passed and 28 skipped. Without ICU, the text tests passed 291 of 292.
  - **Deferred, minor:**
    - a cut stops at the first prefix that doesn't fit, which isn't the most clusters that fit when prefixes aren't monotone;
    - `Fill(0)` throws, while an empty or all-space paragraph measures 0, so P1.11's text element must not fill at a measured width of 0;
    - adjacent decorated spans with letter spacing leave a gap of that spacing between their rectangles, as T-11 is written;
    - `GlyphsAt` returns an `ArraySegment`, which `IList` can write through, and the layout's lists are `List<T>` behind `IReadOnlyList`;
    - small nits: the fallback's integer division (0.7998 em at 2,048 units), `TJ` writing 0 for an advance that rounds to 0, and no bounds check in `IsClusterStart`.

## 2026-11-24

- **CI** on `d0f698b`, [run 45220173024](https://github.com/Maviray/pdf/actions/runs/45220173024): 799 of 799 tests passed, and without ICU the text tests passed 292 of 292. qpdf and the other validators ran there, which are skipped locally.
- **#40 merged** into develop as `17b2a8d`. This branch took it through `feature/31-line-breaking` with a merge whose only change is the release index row for #30.
