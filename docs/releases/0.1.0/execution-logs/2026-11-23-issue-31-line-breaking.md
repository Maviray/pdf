# P1.6 Line breaking: execution log

Issue [#31](https://github.com/Maviray/pdf/issues/31). Plan: [2026-11-23-issue-31-line-breaking.md](../plans/2026-11-23-issue-31-line-breaking.md).

The branch `feature/31-line-breaking` is stacked on `feature/30-unicode-data-nfc` (PR #40), which wasn't merged yet.

## 2026-11-23

- **Planning.**
  - Reading UAX #14 online wasn't permitted from this session. The plan's **Rules** section is therefore a transcription of UAX #14 for 16.0 from the implementer's knowledge, with `LineBreakTest.txt` as the judge.
  - The committed files confirmed its shape:
    - `LineBreak.txt` 16.0.0 has the 48 classes the plan lists, AK, AP, AS, VF and VI among them, and no HH;
    - the test file cites every rule of the transcription, including LB15a to LB15d, LB19a, LB20a, LB21a, LB21b, LB28a, LB30a and LB30b.
  - A throwaway script read the values the model tests pin. It found:
    - no `@missing` defaults but XX and N;
    - 1,477 unassigned `Extended_Pictographic` code points;
    - 210 distinct blocks for a 128-code-point block, so the index fits in bytes.
- **Task 1, the model and its table:**
  - The model tests failed to compile, then passed, 35 of 35.
  - The generator's two-stage table code is now one method for both tables. The existing tables came out byte for byte the same. Then the staleness test failed, naming the missing `LineBreakData.cs`, and passed once the table was written.
  - The table holds 62,464 bytes of data: 26,880 in blocks and 8,704 in the index.
- **Task 2, the lookups:** they agree with the model for every code point, and give AL with no flags outside the code space.
- **Task 3, the breaker:**
  - The unit tests passed, 14 of 14. They include the 100,000-long runs of spaces, regional indicators and full stops, within their time limit.
  - **The conformance test failed on 4 of 16,672 lines** (6561, 6563, 13217 and 13219). Each was a word-initial hyphen, U+002D or U+2010, before the Hebrew letter U+05D0, and the file expects a break there.
    - **Ruling:** LB20a forbids a break before AL only. The plan had transcribed it as `× (AL | HL)`, and its **Rules** line is corrected.
    - After that every line passed.
- **Task 4, clusters and break opportunities:**
  - The cluster tests pin T-05's cases, all built from code points.
  - `DropsBreaksInsideClusters` uses a space followed by U+0301: UAX #14 allows a break there (LB18), but the mark belongs to the space's cluster.
  - Across every line of the conformance file, each required break falls at a cluster start.
- **Full suite:**
  - Debug: 696 tests, 668 passed and 28 skipped.
  - Release: 696 tests, 669 passed and 27 skipped (the validators, which aren't installed locally).
  - The text tests without ICU passed 194 of 194.
- **Whole-branch review** by a fresh reviewer, which was told not to download or install anything: merge with fixes, none of them critical, and no defect in the breaker.
  - **An independent reference.** The reviewer wrote its own UAX #14 16.0 in Python, with naive backward scans, and it passes the whole conformance file.
    - It agreed with `LineBreaker` exactly, boundary by boundary, on about 370,000 random strings. They were drawn from every class and flag, from an alphabet aimed at the rules that look back and ahead, and up to 200 code points long.
    - 15 mutations of the reference showed that those strings exercise LB8a, LB15a to LB15c, LB16, LB17, LB19a, LB20a, LB21a, LB25, LB28a and LB30a.
  - Times scale linearly: a million regional indicators take 78 ms.
  - It confirmed the LB20a ruling.
  - **Fixed, important:** no test pinned a ZWJ that joined a base, which LB8a still treats as a ZWJ. `ZwjAttachedToABaseForbidsTheBreakAfterIt` fails on a mutant that applies LB8a only to a ZWJ that joined nothing.
  - **Fixed, graded up from minor:**
    - **Required and allowed breaks.** The conformance test counted both as a break, so a rule that returned `Required` by mistake, which would force a new line, passed. It now expects `Required` exactly at the end and after a break character, from the spec's list. A mutant that makes every allowed break required fails it.
    - **Long runs.** Two of the three runs couldn't catch a quadratic rule, and the LB25 check was met by `IS × NU`. The test now uses a million regional indicators, the one run a naive rule scans at every boundary, and a number ending in `%`. A mutant that recounts the run at each boundary hits the time limit.
    - **Unpaired surrogates.** The plan said Task 3 pinned them, and nothing did. `ReadsUnpairedSurrogatesAsAlphabetic` fails on a mutant that decodes with `char.ConvertToUtf32(string, int)`.
  - **Full suite:** Debug 698 tests, 670 passed and 28 skipped; Release 698 tests, 671 passed and 27 skipped. Without ICU, the text tests passed 196 of 196.
  - **Deferred, minor:**
    - no test covers `(PO | PR) × OP IS NU` of LB25;
    - `UcdFiles` adds a third private `Hex` helper.
- **First CI run** (PR #41, run 45207879743, at `c9c0cfd`): 698 of 698 tests succeeded, with 0 skipped. The text tests without ICU passed 196 of 196.
