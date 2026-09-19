#!/usr/bin/env python3
"""Prose lint: em-dashes, em-dash dodges, and diary comments.

Canonical source: ~/workspace/muse-ops/lint/prose_lint.py
Projects keep a copy at tools/lint/prose_lint.py so each repo is self-contained.

Usage:
    python3 prose_lint.py [root]

Scans text files under root and reports violations as path:line:RULE: excerpt.
Exit 0 when clean, 1 when violations are found.

Rules:
  EMDASH  U+2014 anywhere (code, comments, docs, strings). It has no legitimate
          use in code or CLI docs; restructure the sentence instead.
  DODGE   "--" bounded by whitespace or line edges, in prose. Fenced code blocks
          and inline code spans are excluded first, so CLI flags (--flag),
          SQL comments (-- comment) and decrement (i--) in samples are spared.
          Catches the " -- " mid-sentence dodge.
  DIARY   Diary phrasing ("used to", "previously", "now we", "changed from",
          ...) in .cs and .md prose. Rationale for a change belongs in
          notes/decisions.md as a dated entry -- that file is the sanctioned
          diary and is exempt from this rule. (Heuristic: reword, don't argue.)
"""

import os
import re
import sys

EMDASH = "\u2014"

# "--" not adjacent to any non-whitespace: matches " -- ", line-start "-- ",
# line-end " --". Spares --flag, i--, and --- (horizontal rules).
DODGE_RE = re.compile(r"(?<!\S)--(?!\S)")

DIARY_RES = [
    re.compile(r"\bused to\b", re.IGNORECASE),
    re.compile(r"\bpreviously\b", re.IGNORECASE),
    re.compile(r"\bnow (we|it|this|they)\b", re.IGNORECASE),
    re.compile(r"\bchanged from\b", re.IGNORECASE),
    re.compile(r"\brenamed from\b", re.IGNORECASE),
    re.compile(r"\bmoved from\b", re.IGNORECASE),
]

SCAN_EXTS = {
    ".cs", ".md", ".csproj", ".props", ".targets", ".yml", ".yaml",
    ".http", ".bicep", ".bicepparam", ".ps1", ".sh",
}
DIARY_EXTS = {".cs", ".md"}
SKIP_DIRS = {
    "bin", "obj", ".git", ".vs", "node_modules", "__pycache__",
    ".idea", "TestResults", "drop",
    # Project-local addition (deviation from canonical script, recorded in
    # notes/decisions.md 2026-09-18): android-tools/ is a vendored third-party
    # toolchain (JDK/Android SDK). Its legal/*.md files are not ours to edit.
    "android-tools",
}
# The sanctioned diary: dated decision log. DIARY rule does not apply there.
DIARY_EXEMPT = {"decisions.md"}
# Frozen archives: the v43 experiment's own records (turn-by-turn audit trail
# and developer handoff), kept both under specs/ and under archive/
# (the v43-process-rules archive). They are historical source material, not
# living docs; rewriting them would falsify the record. Exempt from all rules.
# Deviation from canonical script, recorded in notes/decisions.md 2026-09-18.
ARCHIVE_EXEMPT = {
    "specs/AUDIT_TRAIL_v43.md",
    "specs/HANDOFF_v43.md",
    "archive/v43-process-rules/AUDIT_TRAIL_v43.md",
    "archive/v43-process-rules/HANDOFF_v43.md",
}

INLINE_CODE_RE = re.compile(r"`[^`\n]+`")


def iter_prose_lines(text, ext):
    """Yield (lineno, line) for prose lines.

    For markdown: fenced code blocks are skipped and inline code spans are
    stripped, so CLI/SQL samples don't trip the DODGE rule. Line numbers
    always match the original file.
    """
    if ext != ".md":
        yield from enumerate(text.splitlines(), 1)
        return
    in_fence = False
    for i, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("```") or stripped.startswith("~~~"):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        yield i, INLINE_CODE_RE.sub("", line)


def lint_file(path):
    violations = []
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except (UnicodeDecodeError, OSError):
        return violations
    ext = os.path.splitext(path)[1].lower()
    name = os.path.basename(path)

    for i, line in enumerate(text.splitlines(), 1):
        if EMDASH in line:
            violations.append((path, i, "EMDASH", line.strip()[:120]))

    for i, line in iter_prose_lines(text, ext):
        if DODGE_RE.search(line):
            violations.append((path, i, "DODGE", line.strip()[:120]))

    if ext in DIARY_EXTS and name not in DIARY_EXEMPT:
        for i, line in enumerate(text.splitlines(), 1):
            for rx in DIARY_RES:
                if rx.search(line):
                    violations.append((path, i, "DIARY", line.strip()[:120]))
                    break
    return violations


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    violations = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in sorted(filenames):
            full = os.path.join(dirpath, fn)
            if os.path.relpath(full, root) in ARCHIVE_EXEMPT:
                continue
            if os.path.splitext(fn)[1].lower() in SCAN_EXTS:
                violations.extend(lint_file(full))

    for path, lineno, rule, excerpt in violations:
        print(f"{os.path.relpath(path, root)}:{lineno}: {rule}: {excerpt}")

    if violations:
        print(f"\n{len(violations)} prose violation(s). Fix them; do not suppress.")
        return 1
    print("Prose lint clean.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
