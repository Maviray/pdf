# Documentation

Status: 2026-11-19

This folder holds two kinds of document. **Living docs** describe the library and its processes as they are today, and are corrected whenever that changes. **Records** describe one piece of work on the date they were written: a spec, a plan, an execution log, a test-run report. A record isn't rewritten after its work ships.

The current release is [0.1.0](releases/0.1.0/README.md).

## Layout

```text
docs/
  README.md                  this file: the layout, the naming rules and the current release
  design/                    the design document for the whole library; drafts are numbered inside it
  versions-and-packages.md   how versions are calculated and packages are published
  releases/<version>/        one folder per release
    README.md                the release index: one row per work item
    specs/                   designs for the release: what and why, with decision ids
    plans/                   the steps for one work item
    execution-logs/          what happened when a plan was carried out
  qa/                        checks that span releases
    checklists/              manual checks, never ticked in place
    reports/                 dated runs of a checklist, with the verdicts
```

A folder is created with its first document.

## Names

| Document | Name |
| --- | --- |
| Spec | `YYYY-MM-DD-<slug>-design.md` |
| Plan | `YYYY-MM-DD-issue-<n>-<slug>.md`, where `<n>` is the GitHub issue the plan delivers |
| Execution log | the issue and slug of its plan, dated the day the work was done |
| Test-run report | `YYYY-MM-DD-<checklist-name>-run.md` |
| Living doc | `<topic>.md`, with no date, version or issue number |

- Names are lowercase kebab-case.
- The date is the day the file was first written. It stays when the file is amended.
- A living doc carries a `Status: YYYY-MM-DD` line near the top.

## Who may change what

| Document | While its work is open | After the work shipped |
| --- | --- | --- |
| Spec | Amend freely | A decision that shipped work was built on isn't rewritten. A changed decision gets a new id and names the one it replaces. |
| Plan | Amend freely | Frozen. A new requirement gets a new plan. |
| Execution log, test-run report | Append while the work runs | Frozen |
| Checklist | Never ticked. Each run is a dated copy under `qa/reports/` | Same |
| Living doc | Describes what runs today, and changes in the same pull request that changes what runs | Same |
| Release index | One row per work item, updated with the work | A final status line when the release ships |
