# Release 0.1.0: Foundations and MVP layout

**Status:** in progress, updated 2026-11-19.

Release 0.1.0 carries phases P0 and P1 of the [design document](../../design/Maviray.Pdf-Design-Document.md). P0 has no public API, so 0.1.0 is the first release with one. Work items are GitHub issues in the milestone [v0.1.0](https://github.com/Maviray/pdf/milestone/1).

| Work item | Scope | Status |
| --- | --- | --- |
| P0 spec | Design of P0 Foundations and its five work items | Approved, 2026-11-19. Design: [P0 Foundations](specs/2026-11-19-p0-foundations-design.md) |
| [#9](https://github.com/Maviray/pdf/issues/9) P0.1 | PDF object model and serialization | In review, 2026-11-19. Plan: [issue 9](plans/2026-11-19-issue-9-pdf-object-model.md). Log: [issue 9](execution-logs/2026-11-19-issue-9-pdf-object-model.md) |
| [#10](https://github.com/Maviray/pdf/issues/10) P0.2 | Streaming file writer, checked by qpdf and poppler | In review, 2026-11-19. Plan: [issue 10](plans/2026-11-19-issue-10-streaming-file-writer.md). Log: [issue 10](execution-logs/2026-11-19-issue-10-streaming-file-writer.md) |
| [#11](https://github.com/Maviray/pdf/issues/11) P0.3 | Content-stream builder, with rectangles | In review, 2026-11-19. Plan: [issue 11](plans/2026-11-19-issue-11-content-stream-builder.md). Log: [issue 11](execution-logs/2026-11-19-issue-11-content-stream-builder.md) |
| [#12](https://github.com/Maviray/pdf/issues/12) P0.4 | Helvetica text | Not started |
| [#13](https://github.com/Maviray/pdf/issues/13) P0.5 | Hello-world gate and the first viewer check | Not started |
| P1 | MVP layout | Specified after P0 |

macOS Preview isn't checked in P0, because no Mac is available. The 0.1.0 release check covers it if a Mac is available by then, and otherwise records it as not checked.

## Layout

- `specs/`: designs for this release.
- `plans/`: the steps for one work item, one plan per issue.
- `execution-logs/`: what happened when a plan was carried out.
