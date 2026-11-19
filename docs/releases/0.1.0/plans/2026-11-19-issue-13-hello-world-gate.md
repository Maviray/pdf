# P0.5 Hello-world gate and the first viewer check: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The P0 gate. A hello-world file passes every validator in CI. The maintainer opens it by hand in Acrobat, Chrome and Firefox, and records the verdicts in a dated report.

**Architecture:** One test writes the hello-world file and runs qpdf, a pixel check and text extraction on it. CI uploads it in `generated-pdfs`. A living checklist says how to check it in each viewer. Each run of the checklist is a dated report.

**Tech Stack:** .NET 10, xUnit v3, qpdf and poppler in CI, Acrobat Reader, Chrome and Firefox by hand.

**Spec:** [P0 Foundations](../specs/2026-11-19-p0-foundations-design.md), decision D-13. Work item [#13](https://github.com/Maviray/pdf/issues/13). Builds on P0.4 ([plan](2026-11-19-issue-12-helvetica-text.md)).

## Global Constraints

- The constraints of P0.1 to P0.4 still hold.
- The checklist is a living doc, and it is never ticked. A run is a dated copy under `docs/qa/reports/` (D-13, `docs/README.md`).
- The gate passes only on the maintainer's verdicts. This plan delivers everything up to the run, and the report is written from those verdicts.

## Review Focus

1. **The file the checklist names is the one the test writes.** Its path in the artifact is `generated/HelloWorldTests/HelloWorldPassesEveryValidator.pdf`, and the checklist must give exactly that path.
2. **The text a viewer copies out is exactly the source text.** That includes `ü`, `ß`, `ö`, `–` and `€`.
3. **The layout is described well enough to judge by eye.** The checklist says where each element is and what colour it is, because a PPM render can't be opened on a typical Windows machine.

---

### Task 1: The hello-world test

**Files:** test `tests/Maviray.Pdf.Tests/Writer/HelloWorldTests.cs`

- [ ] `HelloWorldPassesEveryValidator` writes one A4 page, compressed:
  - `Hello, world` in Helvetica 24 at (72, 750);
  - `Grüße aus Köln – 25 €` in Helvetica 14 at (72, 720);
  - a blue filled rectangle at (72, 560, 200, 100);
  - a red stroked rectangle at (300, 560, 200, 100), with line width 4.

  Then it checks:
  - `Qpdf.Check` passes, and `Render` gives one page.
  - The pixels: (172, 610) is blue, (300, 610) is red, (400, 610) is white, and (50, 50) is white.
  - `ExtractText`, split into lines with empty lines dropped, gives exactly the two lines.
- [ ] Locally it skips, because qpdf isn't installed. CI runs it.

### Task 2: The viewer checklist

**Files:** `docs/qa/checklists/viewer-check.md`

- [ ] Where the file is, and what the page looks like.
- [ ] Four checks per viewer:
  - it opens without a warning;
  - the page looks as described;
  - the text copies out exactly;
  - Acrobat only: closing doesn't offer to save.
- [ ] The viewers are Acrobat Reader, Chrome, Firefox and macOS Preview.
- [ ] A report template: viewer versions, the OS, the CI run, and a verdict per viewer, with "not checked" plus the reason as a valid verdict.

### Task 3: Records

- [ ] Write the execution log and update the release index row for #13. #13 stays open until the first report passes the three available viewers.
- [ ] Commit, push and open the PR. Then hand the viewer check to the maintainer, with the CI run that holds the artifact.
