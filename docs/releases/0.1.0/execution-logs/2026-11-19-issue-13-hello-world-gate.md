# P0.5 Hello-world gate and the first viewer check: execution log

Issue [#13](https://github.com/Maviray/pdf/issues/13). Plan: [2026-11-19-issue-13-hello-world-gate.md](../plans/2026-11-19-issue-13-hello-world-gate.md).

The branch `feature/13-hello-world-gate` is stacked on `feature/12-helvetica-text` (PR #17), on top of PRs #16, #15 and #14.

## 2026-11-19

- **Task 1, the hello-world test:** `HelloWorldPassesEveryValidator` writes the gate file. On it, it runs:
  - qpdf;
  - a pixel check of both rectangles, including the white inside of the red outline;
  - text extraction of both lines.

  It skips locally, because qpdf isn't installed, so the PR's CI is its first run.
- **Task 2, the viewer checklist:** `docs/qa/checklists/viewer-check.md`.
  - It names the exact file in the artifact.
  - It describes the page well enough to judge by eye, because a PPM render can't be opened on a typical Windows machine.
  - It lists four checks per viewer, and has a report template.
- **The viewer check itself** is the maintainer's, by hand in Acrobat Reader, Chrome and Firefox. #13 stays open until the first report passes those three viewers.
- **First CI run** (PR #18, run 44528128183, at `3807328`):
  - 247 of 247 tests succeeded, with 0 skipped.
  - The downloaded artifact holds the gate file, `%PDF-1.7`, 796 bytes.
  - The checklist first gave the file's path as `generated/HelloWorldTests/…`, but upload-artifact keeps the path below `tests/`, so the file is at `Maviray.Pdf.Tests/bin/Release/net10.0/generated/HelloWorldTests/…`.
  - The checklist, the test's comment and the plan now give that path.
- **The P0.4 review fixes** were merged in from `feature/12` (`8c68ef0`). CI run 44528952280: 249 of 249, with 0 skipped. The gate file is byte-identical to the first run's.

## Final review

A fresh reviewer downloaded the artifacts and checked the gate file:
- its path;
- its bytes, including the WinAnsi codes in the content stream;
- that its output is deterministic: SHA-256 `a79964603a9d59c5322461b58ca29736303c55491276380285398769f1d9afa1`, 796 bytes;
- what each of the three viewers will copy out.

It found two important issues in the checklist, both fixed before the check by hand:

- **"Exactly" wasn't defined for a check by eye.** Viewers paste different line breaks, and an en dash looks like a hyphen.
  - The checklist now says that line breaks, blank lines and trailing spaces don't count.
  - It gives a PowerShell clipboard check that prints each copied line as character codes when they don't match. Tried on simulated pastes: a correct paste gives `Exact`, and a hyphen for the en dash shows `U+002D`.
- **The page description was too loose, and had drifted from D-13.**
  - The description now gives sizes, positions and the line weight in centimetres, and says to check the page size in each viewer's properties.
  - D-13 is amended to "matches the description in the checklist", because the render is a PPM file a typical Windows machine can't open.

Folded into the same edit:
- making sure each browser uses its own viewer;
- recording the file's SHA-256 in the report;
- a gate line in the report template.

Minor findings left for later:
- a Firefox console check;
- two small naming points in the test.
