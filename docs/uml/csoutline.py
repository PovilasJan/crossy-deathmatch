#!/usr/bin/env python3
"""Outline every type and member declared under src/, and diff it against class-diagram.puml.

    python docs/uml/csoutline.py            # print the outline
    python docs/uml/csoutline.py --diff     # report drift between the code and the diagram

A declaration-level parser, not a compiler: it tracks brace depth and reads each declaration
as written. Enough to keep the UML honest; it does not resolve or infer types.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
SRC = ROOT / "src"
PUML = Path(__file__).resolve().parent / "class-diagram.puml"

MODIFIERS = ("public", "internal", "private", "protected", "file", "sealed", "abstract",
             "static", "partial", "readonly", "ref", "unsafe", "new", "virtual",
             "override", "const", "extern", "async", "event", "required", "volatile")
MOD_ALT = "|".join(MODIFIERS)

TYPE_RE = re.compile(
    r"^(?P<mods>(?:(?:" + MOD_ALT + r")\s+)*)"
    r"(?P<kind>record\s+struct|record\s+class|record|class|struct|interface|enum)\s+"
    r"(?P<name>\w+)\s*(?P<rest>.*)$"
)
MEMBER_RE = re.compile(r"^(?P<mods>(?:(?:" + MOD_ALT + r")\s+)*)(?P<body>.+)$")
KEYWORD_STARTS = ("if", "for", "foreach", "while", "switch", "return", "using", "lock",
                  "try", "catch", "finally", "else", "do", "throw", "yield", "namespace")


class Type:
    def __init__(self, kind, name, mods, bases, params, file, line, depth, parent):
        self.kind = kind.replace("record class", "record").replace("record struct", "record")
        self.name = name
        self.mods = mods
        self.bases = bases
        self.params = params
        self.file = file
        self.line = line
        self.depth = depth          # brace depth the declaration sits at
        self.parent = parent
        self.members = []           # list[(kind, text)]

    @property
    def qualified(self):
        return f"{self.parent.qualified}::{self.name}" if self.parent else self.name

    @property
    def is_record(self):
        return self.kind == "record"


def logical_lines(path):
    """Yield (line_no, text, brace_delta) with comments stripped and declarations joined."""
    text = re.sub(r"/\*.*?\*/", "", path.read_text(encoding="utf-8"), flags=re.S)
    raw = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("//"):
            raw.append("")
            continue
        raw.append(re.sub(r'//(?=(?:[^"]*"[^"]*")*[^"]*$).*$', "", line).rstrip())

    i = 0
    while i < len(raw):
        line = raw[i].strip()
        if not line:
            i += 1
            continue
        start = i
        joined = line
        # Join while a declaration is obviously unfinished: open parens, or a trailing
        # "=>" / "," / "(" that continues on the next line.
        while i + 1 < len(raw) and (joined.count("(") > joined.count(")")
                                    or joined.endswith(("=>", ",", "("))
                                    or raw[i + 1].strip().startswith((":", "."))):
            i += 1
            joined += " " + raw[i].strip()
        delta = sum(raw[j].count("{") - raw[j].count("}") for j in range(start, i + 1))
        yield start + 1, joined, delta
        i += 1


def declaration_head(body):
    """The signature part of a member: everything before '=>', '{', ';' or '=' at depth 0."""
    depth = 0
    i = 0
    while i < len(body):
        ch = body[i]
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        elif depth == 0:
            if body.startswith("=>", i) or ch in "{;":
                break
            if ch == "=" and not body.startswith("==", i) and body[i - 1] not in "!<>=":
                break
        i += 1
    return body[:i].strip().rstrip("{").strip()


def looks_like_method(head):
    """True when a top-level '(' directly follows an identifier — i.e. a parameter list."""
    previous = None
    while previous != head:      # collapse generic argument lists: Pick<T>( -> Pick(
        previous = head
        head = re.sub(r"<[^<>]*>", "", head)
    angle = paren = 0
    for i, ch in enumerate(head):
        if ch == "<":
            angle += 1
        elif ch == ">":
            angle = max(0, angle - 1)
        elif ch == "(":
            if angle == 0 and paren == 0 and re.search(r"\w$", head[:i]):
                return True
            paren += 1
        elif ch == ")":
            paren = max(0, paren - 1)
    return False


def match_parens(text, open_at):
    depth = 0
    for i in range(open_at, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return i
    return -1


def split_params(text):
    out, depth, cur = [], 0, ""
    for ch in text:
        if ch in "<([{":
            depth += 1
        elif ch in ">)]}":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur.strip())
            cur = ""
        else:
            cur += ch
    if cur.strip():
        out.append(cur.strip())
    return [p for p in out if p]


def parse_file(path):
    types, stack = [], []
    depth = 0
    lines = list(logical_lines(path))
    for index, (line_no, decl, delta) in enumerate(lines):
        next_decl = lines[index + 1][1] if index + 1 < len(lines) else ""
        owner = stack[-1] if stack else None
        in_body = owner is not None and depth == owner.depth + 1

        m = TYPE_RE.match(decl)
        is_type = m and not decl.split()[0] in KEYWORD_STARTS
        if is_type:
            rest = m.group("rest").strip()
            params, bases = [], []
            if rest.startswith("("):
                close = match_parens(rest, 0)
                if close > 0:
                    params = split_params(rest[1:close])
                    rest = rest[close + 1:].strip()
            if rest.startswith(":"):
                base_text = rest[1:].split("{")[0].split(" where ")[0]
                bases = [b.split("(")[0].strip() for b in split_params(base_text)]
            t = Type(m.group("kind"), m.group("name"), m.group("mods").strip(), bases,
                     params, path, line_no, depth, owner)
            types.append(t)
            inline = re.search(r"\{(?P<body>[^{}]*)\}", rest)
            if inline and t.kind == "enum":           # enum written on one line
                for literal in split_params(inline.group("body")):
                    t.members.append(("literal", literal.split("=")[0].strip()))
            elif not decl.rstrip().endswith(";") and delta > 0 or (
                    not decl.rstrip().endswith(";") and not inline):
                stack.append(t)                       # a positional record has no body
            depth += delta
            continue

        if in_body and owner.kind == "enum":
            for literal in split_params(decl.rstrip(",;").replace("{", "").replace("}", "")):
                if literal:
                    owner.members.append(("literal", literal))
        elif in_body and decl not in ("{", "}") and not decl.startswith(("[", "#")):
            mm = MEMBER_RE.match(decl)
            if mm:
                head = declaration_head(mm.group("body"))
                if head and not head.startswith(("//", "return", "base", "this")):
                    mods = mm.group("mods")
                    body = mm.group("body")
                    if "event" in mods.split():
                        kind = "event"
                    elif looks_like_method(head):
                        name = re.findall(r"(\w+)\s*\(", head)
                        kind = "ctor" if name and name[0] == owner.name else "method"
                    elif "{" in body or next_decl.startswith("{"):
                        kind = "property"
                    elif "=>" in body:
                        kind = "property"
                    else:
                        kind = "field"
                    owner.members.append((kind, (mods + " " + head).strip()))

        depth += delta
        while stack and depth <= stack[-1].depth:
            stack.pop()
    return types


def outline():
    all_types = []
    for path in sorted(SRC.rglob("*.cs")):
        if "obj" in path.parts:
            continue
        all_types.extend(parse_file(path))
    return all_types


def collapse(pattern, text):
    previous = None
    while previous != text:
        previous = text
        text = re.sub(pattern, "", text)
    return text


def member_name(text):
    head = collapse(r"<[^<>]*>", text)          # generics: Pick<T>( -> Pick(
    called = re.search(r"(\w+)\(", head)        # a parameter list binds tightest
    if called:
        return called.group(1)
    head = collapse(r"\([^()]*\)", head)        # tuple types: (int, int)? _x -> ? _x
    tokens = re.findall(r"\w+", head)
    return tokens[-1] if tokens else ""


def diagram_index():
    text = PUML.read_text(encoding="utf-8")
    names = {}
    for m in re.finditer(r'^\s*(?:abstract\s+)?(class|interface|enum)\s+(?:"([^"]+)"|([^\s{<]+))',
                         text, re.M):
        shown = (m.group(2) or m.group(3)).strip()
        names[shown] = m.group(1)
        names[shown.split("::")[-1].split(".")[-1]] = m.group(1)   # nested/qualified names
    members = set(re.findall(r"^\s*[+\-#~]\s*(?:\{\w+\}\s*)?([A-Za-z_]\w*)", text, re.M))
    members |= set(re.findall(r"^\s{4,}([A-Za-z_]\w*)\s*$", text, re.M))   # enum literals
    members |= set(re.findall(r"([A-Za-z_]\w*)\s*:", text))                # parameter names
    return names, members


def main():
    types = outline()

    if "--diff" not in sys.argv:
        for t in types:
            rel = t.file.relative_to(ROOT).as_posix()
            bases = f" : {', '.join(t.bases)}" if t.bases else ""
            print(f"\n{rel}:{t.line}  [{t.mods}] {t.kind} {t.qualified}{bases}")
            for p in t.params:
                print(f"    {'prop(pos)' if t.is_record else 'ctor-param'} {p}")
            for kind, text in t.members:
                print(f"    {kind:10} {text}")
        print(f"\n{len(types)} types")
        return

    names, dmembers = diagram_index()
    code = {t.name for t in types}
    print("TYPES in code, absent from the diagram:")
    for n in sorted(code - set(names)) or ["  (none)"]:
        print("  -", n)
    print("\nTYPES in the diagram, not declared under src/ (framework stand-ins):")
    external = sorted(n for n in set(names) - code if "::" not in n)
    for n in external or ["  (none)"]:
        print("  -", n)

    print("\nMEMBERS in code whose name never appears in the diagram:")
    total = 0
    for t in types:
        if t.name not in names:
            continue
        want = set()
        for p in t.params:
            bits = p.replace("?", "").split("=")[0].split()
            if len(bits) >= 2:
                want.add(bits[-1])
        for kind, text in t.members:
            want.add(text if kind == "literal" else member_name(text))
        want.discard("new")      # collection-expression initializers, not members
        missing = sorted(w for w in want if w and w not in dmembers)
        if missing:
            total += len(missing)
            print(f"  {t.qualified}: {', '.join(missing)}")
    print(f"\n  {total} member names unaccounted for")


if __name__ == "__main__":
    main()
