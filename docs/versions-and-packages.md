# Versions and packages

Status: 2026-11-24

This page covers how the two NuGet packages, `Maviray.Pdf` and `Maviray.Pdf.Fonts.Default`, are built, versioned and published. Both are packed from the same commit and carry the same version. The workflow is [`.github/workflows/publish-nuget.yml`](../.github/workflows/publish-nuget.yml), and the version rules are in [`GitVersion.yml`](../GitVersion.yml).

## Where packages go

Packages are published to GitHub Packages, in the Maviray organisation's NuGet feed:

```text
https://nuget.pkg.github.com/Maviray/index.json
```

GitHub Packages needs a GitHub login even for public packages. To install from it, add the feed with a personal access token (classic) that has the `read:packages` scope:

```bash
dotnet nuget add source https://nuget.pkg.github.com/Maviray/index.json \
  --name maviray --username <github-user> --password <token>
```

On Linux and macOS, also add `--store-password-in-clear-text`, because those platforms can't encrypt the stored token.

## When the workflow runs

| Trigger | Who can start it | What it publishes |
| --- | --- | --- |
| A merge into `main` | Automatic. The ruleset only lets approved pull requests in. | A release, such as `0.1.0`. The workflow tags the commit `v0.1.0` and creates a GitHub release with both packages attached. |
| Run by hand, on any branch | Repository admins only. Anyone else's run stops at its first step. | A pre-release of that branch, such as `0.2.0-alpha.my-feature.5`. |

Merges into `develop` don't publish anything. To try a change through a real package before it merges, run the workflow on its branch:

- **On GitHub:** open *Actions*, choose *Publish NuGet package*, then *Run workflow*, and pick the branch.
- **From a terminal:** `gh workflow run publish-nuget.yml --ref <branch>`.

GitHub only offers *Run workflow* for workflows that exist on `main`, so manual runs work once this workflow has been released.

## Version numbers

Versions follow [Semantic Versioning 2.0.0](https://semver.org/). GitVersion calculates them from git history, so nobody types a version in, and the same commit always gets the same version.

| Branch | Kind of version | Example |
| --- | --- | --- |
| `main` | Release | `0.2.0` |
| `release/vX.Y.Z` | Release candidate | `0.2.0-rc.4` |
| `hotfix/vX.Y.Z` | Release candidate | `0.1.1-rc.1` |
| `develop` | Beta | `0.2.0-beta.7` |
| `feature/<name>` | Alpha, named after the branch | `0.2.0-alpha.my-feature.5` |
| Any other branch | Alpha, named after the branch | `0.2.0-alpha.docs-readme-refresh.5` |

- **Every commit gets its own version.** The number at the end grows with each commit on the branch.
- **A published version never changes.** Running the workflow again on the same commit produces the same version, and the push skips it.
- **Labels sort `alpha` < `beta` < `rc` < release.** A branch build is never offered as a newer pre-release than `develop`, and nothing is offered ahead of a release candidate.
- **Before 1.0.0, any release may change the public API** ([SemVer item 4](https://semver.org/#spec-item-4)). 1.0.0 is the first release with a stable API.

### Choosing the next release

Pick the part of the version to raise by what changed in the public API:

| Change | Raise | Example |
| --- | --- | --- |
| A breaking change | Major | `1.4.2` → `2.0.0` |
| A backward-compatible addition | Minor | `1.4.2` → `1.5.0` |
| Fixes only | Patch | `1.4.2` → `1.4.3` |

There are two ways to set it:

- **A release branch.** Name it `release/vX.Y.Z`; merging it into `main` releases `X.Y.Z`.
- **A direct merge from `develop`.** This releases the next patch version. For a minor or major release, put `+semver: minor` or `+semver: major` in the pull request title, and merge with *Create a merge commit*. GitHub then copies the title into the merge commit's message, which is where GitVersion reads it. A squash merge of a single commit uses that commit's message instead, so the token would be lost.

### Versions inside the package

| Field | Value | Example |
| --- | --- | --- |
| Package version | The full version | `0.2.0-beta.7` |
| `AssemblyVersion` | The major version only | `0.0.0.0` for every 0.x release, `1.0.0.0` for every 1.x |
| `FileVersion` | Major, minor, patch and the pre-release number | `0.2.0.7` |
| `InformationalVersion` | The full version and the commit it was built from | `0.2.0-beta.7+<commit sha>` |

`AssemblyVersion` changes only with the major version, as .NET recommends for libraries. A minor or patch update then never breaks assembly binding in an application that uses it.

A local build is versioned `0.0.0-dev` and must never be published.
