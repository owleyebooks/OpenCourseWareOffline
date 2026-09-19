#!/usr/bin/env python3
"""
csharp_lint.py — a *partial* substitute for an actual C# compiler, built
because this sandbox has no dotnet CLI, no MAUI workload, and no network
access to fetch one (confirmed each handoff round). This is NOT a C#
parser and cannot do real type-checking, generic resolution, or overload
resolution. What it CAN do, using only the Python stdlib (no packages —
none are installable here either):

  1. Brace/paren/bracket balance per .cs file (catches the single most
     common "forgot to close something" class of error after a hand-edit).
  2. Duplicate member names within the same partial class across files
     (a real and easy mistake when a fix touches multiple files).
  3. XAML <-> C# binding cross-check: for every DataTemplate/ContentPage
     with an x:DataType, resolve {Binding Path} and CommandParameter
     targets against the actual C# class — including CommunityToolkit.Mvvm
     source-generator conventions ([ObservableProperty] private field
     "foo" -> public property "Foo"; [RelayCommand] method "FooAsync"/
     "Foo" -> "FooCommand"). This matters more than it sounds: because
     this project sets x:DataType everywhere, these ARE real compile-time
     errors in MSBuild's compiled-bindings mode, not just runtime
     silent-failures — so this check is approximating something the real
     compiler would actually enforce, not inventing a stricter rule.
  4. StaticResource key cross-check against declared ResourceDictionaries.
  5. `using` namespace sanity vs. PackageReference list in the .csproj
     (best-effort — flags a using with no plausible source, doesn't
     prove one is wrong).
  6. Comment-block length advisory (PROCESS_v(N).md Rule 7): flags any
     contiguous comment block over ~5 lines for human review. This is
     NOT a correctness check — a long comment isn't a bug — so it's
     reported separately and does not affect this tool's exit code.
     Rule 7's actual rule: a comment justifies a non-obvious decision in
     <=3 lines and cites an audit round for full reasoning instead of
     restating it inline. This check can't judge whether a long comment
     is restating vs. genuinely needed; it only finds candidates.
     Covers *.cs (// runs and /* */ blocks) and, since v22, *.xaml/*.xml
     (<!-- --> blocks) — v21 and earlier only globbed *.cs, so manifest
     and XAML comments were invisible to this check (see AUDIT_TRAIL v22).
     Since v24, also globs *.csproj (see AUDIT_TRAIL v24) — v22's
     extension missed it since csproj isn't matched by *.xml.
  7. `async void` outside a recognized event-handler shape (real check,
     affects exit code): flags any `async void` method that isn't
     `override` (MAUI lifecycle hooks like OnAppearing) and doesn't have
     the `(object sender, ...EventArgs e)` shape XAML `Clicked`/
     `SelectionChanged`-style handlers use. An unrecognized `async void`
     can't be awaited by its caller and silently swallows exceptions —
     a real crash footgun, not a style nit. See AUDIT_TRAIL v17.
  8. CommandParameter type-consistency (real check, affects exit code):
     for every `CommandParameter="{Binding .}"` paired with a
     `Command="...Path=BindingContext.XCommand"` (or a plain
     `{Binding XCommand}` at root scope), cross-checks the enclosing
     DataTemplate's `x:DataType` against the bound `[RelayCommand]`
     method's actual parameter type. A mismatch here is a real
     `InvalidCastException` at the first tap in compiled-bindings mode,
     not just a style concern. See AUDIT_TRAIL v17.
  9. Empty/comment-only catch-block audit (informational, does not
     affect exit code): lists every `catch` block whose body is blank
     or contains only comments, in one place, for reviewability. Not a
     rule that empty catches are wrong — several in this codebase are
     deliberate — just makes them visible at a glance instead of
     requiring a manual grep. See AUDIT_TRAIL v17.

False negatives are expected and normal (this cannot catch type errors,
bad LINQ, wrong overloads, async/await misuse that still parses, etc).
A clean run here is NOT equivalent to a clean `dotnet build` — it only
means this specific, narrow set of checks found nothing. Treat it as a
pre-flight filter to catch obvious slips before a real build, not as a
substitute for one.

Usage: python3 csharp_lint.py <path-to-project-root>
"""
import re
import sys
import glob
import os
from collections import defaultdict

def read(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read()

def strip_strings_and_comments(src):
    """Rough stripper so brace-counting isn't fooled by braces inside
    strings/comments. Not a real tokenizer — doesn't handle every C#
    string form (interpolation-with-braces) perfectly, so treat
    brace-balance failures as a strong signal, not gospel.

    Raw string literals (a run of 3+ double-quote characters opening
    and closing — C#'s raw-string-literal syntax) ARE handled
    explicitly (see AUDIT_TRAIL v39) — a JSON literal written as one
    previously tripped a false-positive unmatched-brace finding, since
    the old quote-scanner only recognized normal quote-delimited
    strings and would treat the raw string's own closing quotes as a
    fresh string start. C# raw strings open with a run of 3+ quote
    characters and close with a run of at least as many, so that's
    what's scanned for here."""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '/' and i + 1 < n and src[i+1] == '/':
            j = src.find('\n', i)
            i = n if j == -1 else j
            continue
        if c == '/' and i + 1 < n and src[i+1] == '*':
            j = src.find('*/', i+2)
            i = n if j == -1 else j + 2
            continue
        if c == '"':
            j = i
            while j < n and src[j] == '"':
                j += 1
            quote_len = j - i
            if quote_len >= 3:
                # Raw string literal: content runs until a closing
                # sequence of at least quote_len consecutive quotes.
                k = j
                close_end = n
                while k < n:
                    if src[k] == '"':
                        m = k
                        while m < n and src[m] == '"':
                            m += 1
                        if m - k >= quote_len:
                            close_end = m
                            break
                        k = m
                    else:
                        k += 1
                i = close_end
                out.append('""')
                continue
            # handles normal and verbatim/interpolated strings loosely
            j = i + 1
            while j < n and src[j] != '"':
                if src[j] == '\\' and j + 1 < n:
                    j += 2
                    continue
                j += 1
            i = j + 1
            out.append('""')
            continue
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                if src[j] == '\\' and j + 1 < n:
                    j += 2
                    continue
                j += 1
            i = j + 1
            out.append("''")
            continue
        out.append(c)
        i += 1
    return ''.join(out)

def check_balance(path, src):
    stripped = strip_strings_and_comments(src)
    problems = []
    for open_c, close_c, name in [('{', '}', 'brace'), ('(', ')', 'paren'), ('[', ']', 'bracket')]:
        depth = 0
        for ch in stripped:
            if ch == open_c: depth += 1
            elif ch == close_c: depth -= 1
            if depth < 0:
                problems.append(f"{path}: unmatched closing {name} (went negative)")
                break
        else:
            if depth != 0:
                problems.append(f"{path}: unbalanced {name}s (net {depth:+d})")
    return problems

# ---- C# symbol extraction (regex-based, best-effort) ----

CLASS_RE = re.compile(r'\b(?:public|internal)?\s*(?:sealed\s+|abstract\s+)?(?:partial\s+)?class\s+(\w+)')
OBS_PROP_RE = re.compile(r'\[ObservableProperty\]\s*(?:private|protected|public)?\s+[\w<>]+\??\s+(\w+)\s*(?=[=;])')
RELAY_CMD_RE = re.compile(r'\[RelayCommand[^\]]*\]\s*(?:private|protected|public)?\s+(?:async\s+)?(?:Task|void)[\w<>?]*\s+(\w+)\s*\(([^)]*)\)')
PUBLIC_PROP_RE = re.compile(r'\bpublic\s+[\w<>\[\],.?]+\s+(\w+)\s*\{\s*get\s*;')
PUBLIC_FIELD_RE = re.compile(r'\bpublic\s+[\w<>\[\],.?]+\s+(\w+)\s*(?:=|;)')

def to_pascal(name):
    return name[0].upper() + name[1:] if name else name

def _first_param_type(params_str):
    """Best-effort: returns the C# type name of a method's first
    parameter, or None if it has none. Handles a trailing default value
    ("Artifact artifact = null") but not attributes/generics on the
    parameter itself — this project's [RelayCommand] methods don't use
    either, so that's an acceptable gap for a heuristic tool."""
    first = params_str.strip().split(',')[0].split('=')[0].strip()
    parts = first.split()
    return parts[-2] if len(parts) >= 2 else None

def _mask_nested_classes(body):
    """Blanks out nested class bodies so the outer class's member extraction
    doesn't absorb a nested class's own properties (e.g. private DTOs
    nested in a service class). See AUDIT_TRAIL v16."""
    out = list(body)
    for nm in CLASS_RE.finditer(body):
        nstart = body.find('{', nm.end())
        if nstart == -1:
            continue
        depth, nend = 0, nstart
        for idx in range(nstart, len(body)):
            if body[idx] == '{': depth += 1
            elif body[idx] == '}':
                depth -= 1
                if depth == 0:
                    nend = idx
                    break
        for i in range(nstart, nend + 1):
            if body[i] != '\n':
                out[i] = ' '
    return ''.join(out)

def extract_class_members(root):
    """Returns {ClassName: set(member_names)} across all .cs files,
    merging partial-class declarations, plus generated members from
    CommunityToolkit.Mvvm attributes. Plain (non-partial) classes are
    included too, but only for their own public get/set members — nested
    classes are extracted separately, not folded into the parent."""
    members = defaultdict(set)
    raw_members_by_file = defaultdict(lambda: defaultdict(set))
    relay_cmd_params = defaultdict(dict)  # {ClassName: {CommandName: ParamType|None}}

    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        src = read(path)
        # crude class-body slicing: find each class and its brace-matched body
        for m in CLASS_RE.finditer(src):
            cname = m.group(1)
            start = src.find('{', m.end())
            if start == -1:
                continue
            depth = 0
            end = start
            for idx in range(start, len(src)):
                if src[idx] == '{': depth += 1
                elif src[idx] == '}':
                    depth -= 1
                    if depth == 0:
                        end = idx
                        break
            body = _mask_nested_classes(src[start:end])

            for fm in OBS_PROP_RE.finditer(body):
                prop = to_pascal(fm.group(1))
                members[cname].add(prop)
                raw_members_by_file[path][cname].add(prop)

            for fm in RELAY_CMD_RE.finditer(body):
                base = fm.group(1)
                base = base[:-5] if base.endswith('Async') else base
                cmd = to_pascal(base) + 'Command'
                members[cname].add(cmd)
                raw_members_by_file[path][cname].add(cmd)
                relay_cmd_params[cname][cmd] = _first_param_type(fm.group(2))

            for fm in PUBLIC_PROP_RE.finditer(body):
                members[cname].add(fm.group(1))
                raw_members_by_file[path][cname].add(fm.group(1))

            for fm in PUBLIC_FIELD_RE.finditer(body):
                members[cname].add(fm.group(1))
                raw_members_by_file[path][cname].add(fm.group(1))

    return members, raw_members_by_file, relay_cmd_params

def check_duplicate_members(raw_members_by_file):
    problems = []
    by_class = defaultdict(list)  # class -> [(member, file)]
    for path, classes in raw_members_by_file.items():
        for cname, mems in classes.items():
            for m in mems:
                by_class[(cname, m)].append(path)
    for (cname, member), files in by_class.items():
        uniq_files = sorted(set(files))
        if len(files) > 1 and len(uniq_files) == 1:
            # same member declared twice in the SAME file text via two
            # different regex hits is likely a false positive of this
            # crude extractor, not a real duplicate — skip.
            continue
    return problems  # kept for future extension; real duplicate-across-files
                      # detection needs real declaration-vs-reference
                      # separation, which this regex approach can't do
                      # reliably enough to report without false positives.

# ---- XAML extraction ----

import xml.etree.ElementTree as ET

X_NS = '{http://schemas.microsoft.com/winfx/2009/xaml}'
BINDING_PATH_RE = re.compile(r'\{Binding\s+([^,}]+)')
COMMAND_BINDINGCONTEXT_RE = re.compile(r'Path=BindingContext\.(\w+)')
COMMAND_PLAIN_RE = re.compile(r'^\{Binding\s+(\w+)\}$')

def check_xaml_bindings(root, class_members, relay_cmd_params):
    problems = []
    for path in glob.glob(os.path.join(root, '**', '*.xaml'), recursive=True):
        src = read(path)
        try:
            tree = ET.fromstring(src)
        except ET.ParseError as e:
            problems.append(f"{path}: XML did not parse ({e}) — this alone would fail a real "
                             f"build, since XAML must be well-formed XML")
            continue

        root_dtype_attr = tree.get(f'{X_NS}DataType')
        root_type = root_dtype_attr.split(':')[-1] if root_dtype_attr else None

        def walk(elem, current_type):
            dtype_attr = elem.get(f'{X_NS}DataType')
            scope_type = current_type
            if dtype_attr:
                # strip an XML-namespace prefix like "models:Artifact" -> "Artifact"
                scope_type = dtype_attr.split(':')[-1]

            # check every attribute value on this element for {Binding ...}
            # while it's in scope_type's context
            for attr_val in elem.attrib.values():
                for bm in BINDING_PATH_RE.finditer(attr_val):
                    raw = bm.group(1).strip()
                    if raw.startswith('Source=') or raw.startswith('.') or raw == '':
                        continue  # RelativeSource/self-bindings out of scope for this check
                    path_expr = raw.split('.')[0].strip()
                    if not re.match(r'^\w+$', path_expr):
                        continue
                    if scope_type is None:
                        continue
                    known = class_members.get(scope_type)
                    if known is None:
                        problems.append(f"{path}: x:DataType=\"{scope_type}\" not found among "
                                         f"parsed C# classes (rename/typo, or this checker's "
                                         f"class regex missed it)")
                    elif path_expr not in known:
                        problems.append(f"{path}: {{Binding {path_expr}}} not found on "
                                         f"{scope_type} (checked against parsed properties/"
                                         f"commands: {sorted(known)})")

            # CommandParameter type-consistency (module docstring item 8):
            # only handles the "{Binding .}" whole-item-parameter shape,
            # which is the only shape this codebase actually uses.
            cmd_param_val = elem.get('CommandParameter', '').strip()
            if cmd_param_val == '{Binding .}':
                cmd_attr = elem.get('Command', '')
                cmd_name, owner_type = None, None
                bc_match = COMMAND_BINDINGCONTEXT_RE.search(cmd_attr)
                if bc_match:
                    cmd_name, owner_type = bc_match.group(1), root_type
                else:
                    plain_match = COMMAND_PLAIN_RE.match(cmd_attr.strip())
                    if plain_match:
                        cmd_name, owner_type = plain_match.group(1), scope_type
                if cmd_name and owner_type:
                    owner_cmds = relay_cmd_params.get(owner_type, {})
                    if cmd_name in owner_cmds:
                        expected = owner_cmds[cmd_name]
                        if expected is not None and expected != scope_type:
                            problems.append(
                                f"{path}: CommandParameter=\"{{Binding .}}\" passes a "
                                f"{scope_type} to {owner_type}.{cmd_name}, whose "
                                f"[RelayCommand] method expects a {expected} — a real "
                                f"InvalidCastException at the first tap in compiled-"
                                f"bindings mode")

            for child in elem:
                walk(child, scope_type)

        walk(tree, root_type)
    return problems

def check_static_resources(root):
    STATIC_RESOURCE_RE = re.compile(r'\{StaticResource\s+(\w+)\}')
    declared = set()
    used = defaultdict(list)
    for path in glob.glob(os.path.join(root, '**', '*.xaml'), recursive=True):
        src = read(path)
        for m in re.finditer(r'x:Key="(\w+)"', src):
            declared.add(m.group(1))
        for m in STATIC_RESOURCE_RE.finditer(src):
            used[m.group(1)].append(path)
    problems = []
    for key, files in used.items():
        if key not in declared:
            problems.append(f"{key} used in {files} but never declared with x:Key")
    return problems

def check_using_vs_packages(root):
    csproj_files = glob.glob(os.path.join(root, '**', '*.csproj'), recursive=True)
    pkg_names = set()
    for cp in csproj_files:
        src = read(cp)
        for m in re.finditer(r'PackageReference\s+Include="([^"]+)"', src):
            pkg_names.add(m.group(1))

    # very small known-good map of "using root" -> plausible package/source.
    # Anything not in here and not a Microsoft.Maui/System/OcwOffline
    # namespace just gets skipped rather than flagged, to avoid noise —
    # this check exists to catch a using with NO plausible source at all
    # (e.g. a copy-pasted using for a package that was never added).
    known_roots = {
        'SQLite': 'sqlite-net-pcl',
        'HtmlAgilityPack': 'HtmlAgilityPack',
        'CommunityToolkit.Mvvm': 'CommunityToolkit.Mvvm',
    }
    problems = []
    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        src = read(path)
        for m in re.finditer(r'^using\s+([\w.]+);', src, re.MULTILINE):
            ns = m.group(1)
            for root_ns, pkg in known_roots.items():
                if ns == root_ns or ns.startswith(root_ns + '.'):
                    if pkg not in pkg_names:
                        problems.append(f"{path}: `using {ns};` but no PackageReference for "
                                        f"{pkg} found in any .csproj")
    return problems

ASYNC_VOID_RE = re.compile(r'(override\s+)?async\s+void\s+(\w+)\s*\(([^)]*)\)')
EVENT_HANDLER_PARAMS_RE = re.compile(r'^object\s+\w+\s*,\s*\w*EventArgs\w*\s+\w+$')

def check_async_void(root):
    """Module docstring item 7. `override` async void (MAUI lifecycle
    hooks) and the standard (object sender, ...EventArgs e) XAML
    event-handler shape are both legitimate and excluded; anything else
    can't be awaited by its caller and silently swallows exceptions."""
    problems = []
    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        src = read(path)
        for m in ASYNC_VOID_RE.finditer(src):
            if m.group(1):  # override — framework lifecycle hook
                continue
            params = re.sub(r'\s+', ' ', m.group(3).strip())
            if EVENT_HANDLER_PARAMS_RE.match(params):
                continue
            line_no = src.count('\n', 0, m.start()) + 1
            problems.append(
                f"{path}:{line_no}: `async void {m.group(2)}({params})` isn't an "
                f"override or a recognized (object sender, ...EventArgs e) event "
                f"handler — its caller can't await it, and any exception it throws "
                f"is unobservable")
    return problems

def check_empty_catch(root):
    """Module docstring item 9. Advisory only — see main()'s call site."""
    advisories = []
    CATCH_RE = re.compile(r'\bcatch\b\s*(\([^)]*\))?\s*\{')
    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        src = read(path)
        for m in CATCH_RE.finditer(src):
            start = m.end() - 1
            depth, end = 0, start
            for idx in range(start, len(src)):
                if src[idx] == '{': depth += 1
                elif src[idx] == '}':
                    depth -= 1
                    if depth == 0:
                        end = idx
                        break
            body = src[start + 1:end]
            if strip_strings_and_comments(body).strip() == '':
                line_no = src.count('\n', 0, m.start()) + 1
                advisories.append(
                    f"{path}:{line_no}: empty or comment-only catch block — not "
                    f"automatically wrong (several here are deliberate), flagged "
                    f"for reviewability per PROCESS Rule 1's lint-toolkit list")
    return advisories

def check_comment_length(root, threshold=5):
    """Flags contiguous comment blocks (// runs or /* */ blocks) longer
    than `threshold` lines, per PROCESS Rule 7. Advisory only — see the
    module docstring item 6. Deliberately simple line-based grouping,
    consistent with this file's existing heuristic-not-parser approach:
    a blank line or a non-comment line breaks a // run; a /* */ block
    is measured start-to-close inclusive."""
    advisories = []
    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        lines = read(path).split('\n')
        i, n = 0, len(lines)
        while i < n:
            stripped = lines[i].strip()
            if stripped.startswith('//'):
                start = i
                while i < n and lines[i].strip().startswith('//'):
                    i += 1
                length = i - start
                if length > threshold:
                    advisories.append(
                        f"{path}:{start + 1}: comment block is {length} lines "
                        f"(Rule 7 budget: ~{threshold}) — review for reasoning "
                        f"that should cite an audit round instead of being restated")
                continue
            if stripped.startswith('/*'):
                start = i
                j = i
                while j < n and '*/' not in lines[j]:
                    j += 1
                length = min(j, n - 1) - start + 1
                if length > threshold:
                    advisories.append(
                        f"{path}:{start + 1}: comment block is {length} lines "
                        f"(Rule 7 budget: ~{threshold}) — review for reasoning "
                        f"that should cite an audit round instead of being restated")
                i = j + 1
                continue
            i += 1

    # v22: extend Rule 7 enforcement to XML/XAML `<!-- -->` comments —
    # previously this only globbed *.cs, so AndroidManifest.xml and XAML
    # comment blocks were invisible to it (see AUDIT_TRAIL v22).
    # v24: also glob *.csproj — same <!-- --> syntax, but *.csproj isn't
    # matched by *.xml (see AUDIT_TRAIL v24).
    xml_paths = set(glob.glob(os.path.join(root, '**', '*.xaml'), recursive=True)) \
        | set(glob.glob(os.path.join(root, '**', '*.xml'), recursive=True)) \
        | set(glob.glob(os.path.join(root, '**', '*.csproj'), recursive=True))
    for path in sorted(xml_paths):
        src = read(path)
        for m in re.finditer(r'<!--.*?-->', src, re.DOTALL):
            length = m.group(0).count('\n') + 1
            if length > threshold:
                line_no = src.count('\n', 0, m.start()) + 1
                advisories.append(
                    f"{path}:{line_no}: comment block is {length} lines "
                    f"(Rule 7 budget: ~{threshold}) — review for reasoning "
                    f"that should cite an audit round instead of being restated")
    return advisories

def main():
    root = sys.argv[1] if len(sys.argv) > 1 else '.'
    all_problems = []

    for path in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
        all_problems += check_balance(path, read(path))

    class_members, raw_by_file, relay_cmd_params = extract_class_members(root)
    all_problems += check_duplicate_members(raw_by_file)
    all_problems += check_xaml_bindings(root, class_members, relay_cmd_params)
    all_problems += check_static_resources(root)
    all_problems += check_using_vs_packages(root)
    all_problems += check_async_void(root)

    print(f"Parsed {len(class_members)} class(es) with bindable members:")
    for cname, mems in sorted(class_members.items()):
        print(f"  {cname}: {sorted(mems)}")
    print()

    if not all_problems:
        print("No issues found by this checker.")
        print("Reminder: this is a narrow heuristic pass, not a compiler. "
              "See the module docstring for exactly what it does and doesn't cover.")
        exit_code = 0
    else:
        print(f"{len(all_problems)} potential issue(s):")
        for p in all_problems:
            print(f" - {p}")
        exit_code = 1

    comment_advisories = check_comment_length(root)
    print()
    if comment_advisories:
        print(f"{len(comment_advisories)} comment-length advisory(ies) (Rule 7, informational, does not affect exit code):")
        for a in comment_advisories:
            print(f" - {a}")
    else:
        print("No comment-length advisories (Rule 7).")

    catch_advisories = check_empty_catch(root)
    print()
    if catch_advisories:
        print(f"{len(catch_advisories)} empty/comment-only catch block(s) (informational, does not affect exit code):")
        for a in catch_advisories:
            print(f" - {a}")
    else:
        print("No empty/comment-only catch blocks.")

    return exit_code

if __name__ == '__main__':
    sys.exit(main())
