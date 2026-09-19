# Contributing to OCW Offline

## Build and test

Prerequisites are in `README.md` (build) and `notes/env-setup.md`
(toolchain quirks). The short version:

```
dotnet build work/OcwOffline/OcwOffline.csproj -p:TargetFrameworks=net10.0-android
```

Run the full suite via the xUnit console runner (see `README.md`);
keep it green. A PR that breaks tests or the build is not merged.

## Prose doctrine (enforced)

These rules apply to code, comments, docs, and strings, and are enforced
by `tools/lint/prose_lint.py`, which runs in CI and inside the test
suite (`ProseLintComplianceTests`):

- **No em-dashes, anywhere.** Restructure the sentence (commas, colons,
  parentheses). Never use `--` as a dodge.
- **Comments: almost never.** If a block needs explanation, extract a
  well-named method; the name is the comment. Never narrate changes in
  code; rationale lives in `notes/decisions.md` as a dated entry.
- **Docs describe the present.** Rationale for a change goes in
  `notes/decisions.md`, never inline in the doc being changed.

## Assertion rule

The assertion library is **AwesomeAssertions** (the free
FluentAssertions fork), but it is not used everywhere. Follow the rule
recorded in `notes/decisions.md`:

- `because` (AwesomeAssertions) for asserts that guard a **distinct
  intent** on shared setup: the reason must be visible in the test
  runner's failure output, not buried in a comment.
- Plain xUnit `Assert` for **flat verification lists** (column-by-column
  persistence checks, shape checks). A `because` there adds noise.
- Data-driven theories (`[Theory]` + `[InlineData]`) take a `string
  description` as the first parameter so each case names itself in the
  runner. The body ignores it; the display name is the purpose.

## PR guidance

- Keep the suite green and the prose lint clean (`python3
  tools/lint/prose_lint.py .`).
- Follow the existing patterns: file-linked sources in the test project,
  fakes behind interfaces for MAUI statics, no new project-wide
  conventions without a `notes/decisions.md` entry.
- Do not commit secrets: no keystores, passwords, certificates, or
  provisioning profiles, ever (`.gitignore` blocks the usual files).
- Small, focused PRs. If a change needs rationale a reviewer cannot see
  in the diff, put it in `notes/decisions.md`, not in a code comment.
