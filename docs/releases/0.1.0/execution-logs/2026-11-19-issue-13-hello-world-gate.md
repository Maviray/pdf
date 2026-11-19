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
