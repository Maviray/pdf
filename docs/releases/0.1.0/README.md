# Release 0.1.0: Foundations and MVP layout

**Status:** in progress, updated 2026-11-23.

Release 0.1.0 carries phases P0 and P1 of the [design document](../../design/Maviray.Pdf-Design-Document.md). P0 has no public API, so 0.1.0 is the first release with one. Work items are GitHub issues in the milestone [v0.1.0](https://github.com/Maviray/pdf/milestone/1).

| Work item | Scope | Status |
| --- | --- | --- |
| P0 spec | Design of P0 Foundations and its five work items | Approved, 2026-11-19. Design: [P0 Foundations](specs/2026-11-19-p0-foundations-design.md) |
| [#9](https://github.com/Maviray/pdf/issues/9) P0.1 | PDF object model and serialization | Merged 2026-11-19 in [#14](https://github.com/Maviray/pdf/pull/14). Plan: [issue 9](plans/2026-11-19-issue-9-pdf-object-model.md). Log: [issue 9](execution-logs/2026-11-19-issue-9-pdf-object-model.md) |
| [#10](https://github.com/Maviray/pdf/issues/10) P0.2 | Streaming file writer, checked by qpdf and poppler | Merged 2026-11-19 in [#15](https://github.com/Maviray/pdf/pull/15). Plan: [issue 10](plans/2026-11-19-issue-10-streaming-file-writer.md). Log: [issue 10](execution-logs/2026-11-19-issue-10-streaming-file-writer.md) |
| [#11](https://github.com/Maviray/pdf/issues/11) P0.3 | Content-stream builder, with rectangles | Merged 2026-11-19 in [#16](https://github.com/Maviray/pdf/pull/16). Plan: [issue 11](plans/2026-11-19-issue-11-content-stream-builder.md). Log: [issue 11](execution-logs/2026-11-19-issue-11-content-stream-builder.md) |
| [#12](https://github.com/Maviray/pdf/issues/12) P0.4 | Helvetica text | Merged 2026-11-19 in [#17](https://github.com/Maviray/pdf/pull/17). Plan: [issue 12](plans/2026-11-19-issue-12-helvetica-text.md). Log: [issue 12](execution-logs/2026-11-19-issue-12-helvetica-text.md) |
| [#13](https://github.com/Maviray/pdf/issues/13) P0.5 | Hello-world gate and the first viewer check | Merged 2026-11-19 in [#18](https://github.com/Maviray/pdf/pull/18); the issue stays open until the viewer check by hand passes. Plan: [issue 13](plans/2026-11-19-issue-13-hello-world-gate.md). Log: [issue 13](execution-logs/2026-11-19-issue-13-hello-world-gate.md). Checklist: [viewer check](../../qa/checklists/viewer-check.md) |
| P1 fonts spec | Design of P1 Fonts and its four work items | Approved, 2026-11-20. Design: [P1 Fonts](specs/2026-11-20-p1-fonts-design.md) |
| [#20](https://github.com/Maviray/pdf/issues/20) P1.1 | TrueType parser | Merged 2026-11-23 in [#24](https://github.com/Maviray/pdf/pull/24). Plan: [issue 20](plans/2026-11-20-issue-20-truetype-parser.md). Log: [issue 20](execution-logs/2026-11-20-issue-20-truetype-parser.md) |
| [#21](https://github.com/Maviray/pdf/issues/21) P1.2 | TrueType subsetter | Merged 2026-11-23 in [#25](https://github.com/Maviray/pdf/pull/25). Plan: [issue 21](plans/2026-11-23-issue-21-truetype-subsetter.md). Log: [issue 21](execution-logs/2026-11-23-issue-21-truetype-subsetter.md) |
| [#22](https://github.com/Maviray/pdf/issues/22) P1.3 | Embedded TrueType fonts in the PDF | Merged 2026-11-23 in [#26](https://github.com/Maviray/pdf/pull/26). Plan: [issue 22](plans/2026-11-23-issue-22-embedded-truetype-fonts.md). Log: [issue 22](execution-logs/2026-11-23-issue-22-embedded-truetype-fonts.md) |
| [#23](https://github.com/Maviray/pdf/issues/23) P1.4 | Fonts gate | In review, 2026-11-23. Plan: [issue 23](plans/2026-11-23-issue-23-fonts-gate.md). Log: [issue 23](execution-logs/2026-11-23-issue-23-fonts-gate.md). Checklist: [viewer check](../../qa/checklists/viewer-check.md) |
| P1 | MVP layout: text, then layout with the fluent API | Each specified after the work before it |

macOS Preview isn't checked in P0, because no Mac is available. The 0.1.0 release check covers it if a Mac is available by then, and otherwise records it as not checked.

## Layout

- `specs/`: designs for this release.
- `plans/`: the steps for one work item, one plan per issue.
- `execution-logs/`: what happened when a plan was carried out.
