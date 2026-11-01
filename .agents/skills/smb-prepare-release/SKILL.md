---
name: smb-prepare-release
description: Set the SlimMessageBus release version in the shared host/plugin properties and configuration project. Use when preparing a SlimMessageBus release or changing its release version.
---

# Prepare a SlimMessageBus release

Set the requested NuGet release version in both files, relative to the SlimMessageBus repository root:

- `src/Host.Plugin.Properties.xml`: its `PropertyGroup/Version` element. Host and transport/feature plugin projects import this shared version.
- `src/SlimMessageBus.Host.Configuration/SlimMessageBus.Host.Configuration.csproj`: its own `PropertyGroup/Version` element. This project imports common NuGet properties rather than the shared host/plugin version.

The local release preparation that established this workflow changed both values from `3.6.0-rc101` to `3.6.0`. These are examples, not fixed release versions.

## Choose the version

Use the exact target version supplied by the user, including a prerelease suffix when requested. If the user explicitly asks to finalize the current prerelease, derive the stable version by removing its prerelease suffix only when both files agree on the current version. Otherwise, if no target can be determined from the request, ask for it before editing. Do not guess the next patch, minor, major, or release-candidate number.

## Apply the change

1. Locate the SlimMessageBus checkout and read applicable `AGENTS.md` instructions. Inspect `git status --short`, `git diff`, and `git diff --cached` before editing so staged and unstaged user work is understood.
2. Read both version elements. If local edits already set both to the requested version, leave them as they are and validate. If an existing local version edit conflicts with the requested target and the request does not clearly supersede it, ask which target to keep.
3. Update only the two release `Version` element values, preserving XML formatting, encoding, line endings, and unrelated edits. Do not reserialize entire project files.

Other packages have independent versions: `src/SlimMessageBus/SlimMessageBus.csproj`, `src/SlimMessageBus.Host.Interceptor/SlimMessageBus.Host.Interceptor.csproj`, and `src/SlimMessageBus.Host.Serialization/SlimMessageBus.Host.Serialization.csproj`. Leave those versions unchanged unless explicitly requested. Do not globally replace matching version strings or change dependency versions, sample references, test versions, or target frameworks.

A request to set the release version does not itself request release notes, commits, staging, tags, pushes, or package publication. Perform those only when separately requested.

## Verify and report

- Parse both files as XML and confirm each has exactly one direct `PropertyGroup/Version` element with the requested value. Account for the MSBuild XML namespace in `Host.Plugin.Properties.xml`.
- Inspect staged and unstaged diffs for the two files and run whitespace checks. Distinguish pre-existing changes from your edits, and preserve the index state.
- For version-only edits, XML/value validation and diff checks are sufficient; no runtime behavior has changed. Follow repository requirements if additional code changes are requested.
- Report the resulting version, the two paths, and validation performed. State any unresolved mismatch or blocked check.
