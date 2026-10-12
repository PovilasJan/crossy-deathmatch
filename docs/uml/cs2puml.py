#!/usr/bin/env python3
"""Generate docs/uml/class-diagram.puml from the C# sources.

    python docs/uml/cs2puml.py

Every type under src/ is emitted, grouped into packages by namespace, with its members and the
relationships that can be read off the declarations:

  * generalization / realization from base lists
  * composition from fields whose type is a project type
  * association from properties whose type is a project type

Method-level dependencies are deliberately left out; with ~170 types they bury the diagram.
Framework types that are inherited or implemented land in an "External" package so no arrow dangles.

Reuses the declaration parser in csoutline.py, so the diagram cannot drift from the code: run
`python docs/uml/csoutline.py --diff` afterwards to confirm.
"""

from __future__ import annotations

import re
from pathlib import Path

import csoutline

ROOT = Path(__file__).resolve().parent.parent.parent
OUT = Path(__file__).resolve().parent / "class-diagram.puml"

PROJECT_ROOTS = ("CrossyDeathmatch.Shared", "CrossyDeathmatch.Server", "CrossyDeathmatch.Client")

VISIBILITY = {"public": "+", "private": "-", "protected": "#", "internal": "~", "file": "-"}

COLLECTION = re.compile(r"^(List|IReadOnlyList|IList|IEnumerable|HashSet|Dictionary|IReadOnlyDictionary|ConcurrentQueue|Queue|Stack|.*\[\])\b")

STEREOTYPE = {
    "record": "record",
    "enum": "enumeration",
    "interface": "interface",
}


# --------------------------------------------------------------------------- helpers

def namespace_of(path: Path) -> str:
    for line in path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\s*namespace\s+([\w.]+)", line)
        if m:
            return m.group(1)
    return "CrossyDeathmatch"


def split_params(text):
    out, depth, cur = [], 0, ""
    for ch in text:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur.strip())
            cur = ""
        else:
            cur += ch
    if cur.strip():
        out.append(cur.strip())
    return out


def last_param_list(head):
    """Index range of the parameter list: the last top-level '(' that follows an identifier."""
    angle = 0
    best = None
    for i, ch in enumerate(head):
        if ch == "<":
            angle += 1
        elif ch == ">":
            angle = max(0, angle - 1)
        elif ch == "(" and angle == 0 and re.search(r"[\w>]$", head[:i]):
            best = i
    if best is None:
        return None
    depth = 0
    for j in range(best, len(head)):
        if head[j] == "(":
            depth += 1
        elif head[j] == ")":
            depth -= 1
            if depth == 0:
                return best, j
    return None


def strip_base_call(head):
    """Drops the ": base(...)" / ": this(...)" initializer a constructor declaration may carry."""
    m = re.search(r"\)\s*:\s*(?:base|this)\s*\(", head)
    return head[:m.start() + 1] if m else head


def strip_modifiers(head):
    words = head.split()
    mods = []
    while words and words[0] in csoutline.MODIFIERS:
        mods.append(words.pop(0))
        head = " ".join(words)
    return mods, head


def clean_type(text):
    text = text.strip().rstrip("?")
    text = re.sub(r"^(params|ref|out|in|this)\s+", "", text).strip()
    return text


def parameter(text):
    """'int laneIndex = 0' -> ('laneIndex', 'int')"""
    text = text.split("=")[0].strip()
    text = re.sub(r"^(params|ref|out|in|this)\s+", "", text).strip()
    parts = text.rsplit(" ", 1)
    if len(parts) == 2:
        return parts[1].strip(), parts[0].strip()
    return text, None


def element_types(type_name):
    """Every identifier a type mentions, so List<Lane> yields Lane."""
    if not type_name:
        return []
    return re.findall(r"[A-Za-z_]\w*", type_name)


def is_collection(type_name) -> bool:
    return bool(type_name and COLLECTION.match(type_name.strip()))


# --------------------------------------------------------------------------- model

class Node:
    def __init__(self, t, namespace):
        self.t = t
        self.namespace = namespace
        self.display = t.qualified                      # GameSession::Member
        self.alias = re.sub(r"[^A-Za-z0-9_]", "_", t.qualified)
        self.simple = t.name

    @property
    def kind(self):
        return {"record": "class", "class": "class", "interface": "interface", "enum": "enum"}[self.t.kind]


def build():
    types = csoutline.outline()
    namespaces = {}
    nodes = []
    for t in types:
        ns = namespaces.setdefault(t.file, namespace_of(t.file))
        nodes.append(Node(t, ns))

    by_simple = {}
    for node in nodes:
        by_simple.setdefault(node.simple, []).append(node)

    # A plain name is usable as the PlantUML id only when it is unique and needs no quoting.
    for node in nodes:
        unique = len(by_simple[node.simple]) == 1
        node.id = node.simple if unique and node.display == node.simple else node.alias
        node.quoted = node.id != node.display

    return nodes, {n.simple: n for n in nodes if len(by_simple[n.simple]) == 1}


# --------------------------------------------------------------------------- emitting

def member_lines(node, lookup):
    lines = []
    t = node.t

    if t.is_record:
        # Positional record parameters are its public properties.
        for param in t.params:
            name, type_name = parameter(param)
            lines.append(f"+ {name} : {type_name}" if type_name else f"+ {name}")
    elif t.params:
        # A primary constructor on a class is that class's constructor.
        marker = VISIBILITY.get(next((m for m in t.mods.split() if m in VISIBILITY), "internal"), "~")
        rendered = []
        for param in t.params:
            name, type_name = parameter(param)
            rendered.append(f"{name} : {type_name}" if type_name else name)
        lines.append(f"{marker} {t.name}({', '.join(rendered)})")

    for kind, text in t.members:
        if kind == "literal":
            lines.append(text)
            continue

        mods, head = strip_modifiers(strip_base_call(text))
        marker = "+"
        for word in mods:
            if word in VISIBILITY:
                marker = VISIBILITY[word]
                break
        else:
            marker = "+" if t.kind == "interface" else "~"

        decorations = ""
        if "static" in mods:
            decorations += "{static} "
        if "abstract" in mods:
            decorations += "{abstract} "

        span = last_param_list(head)
        if span and kind in ("method", "ctor"):
            open_at, close_at = span
            signature = head[:open_at].strip()
            name = signature.split()[-1] if signature.split() else signature
            name = name.split(".")[-1]                       # explicit interface implementations
            return_type = signature[: len(signature) - len(name)].strip().split(".")[-1]
            params = []
            for raw in split_params(head[open_at + 1:close_at]):
                pname, ptype = parameter(raw)
                params.append(f"{pname} : {ptype}" if ptype else pname)
            rendered = f"{marker} {decorations}{name}({', '.join(params)})"
            if return_type and return_type not in ("void", ""):
                rendered += f" : {return_type}"
            lines.append(rendered)
        else:
            words = head.split()
            if not words:
                continue
            name = words[-1].split(".")[-1]
            type_name = " ".join(words[:-1]) or None
            lines.append(f"{marker} {decorations}{name} : {type_name}" if type_name
                         else f"{marker} {decorations}{name}")

    return lines


def relationships(nodes, lookup):
    rels = []
    seen = set()

    def add(kind, source, target, label="", multiplicity=""):
        key = (kind, source, target, label)
        if key in seen or source == target:
            return
        seen.add(key)
        rels.append((kind, source, target, label, multiplicity))

    external = {}

    def resolve(name):
        name = clean_type(name)
        base = name.split("<")[0].strip()
        if base in lookup:
            return lookup[base].id, lookup[base]
        return None, None

    for node in nodes:
        # generalization / realization
        for base in node.t.bases:
            target_id, target = resolve(base)
            if target is None:
                external.setdefault(base, base.startswith("I") and base[1:2].isupper())
                target_id = re.sub(r"[^A-Za-z0-9_]", "_", base)
                add("realization" if external[base] else "generalization", node.id, target_id)
                continue
            add("realization" if target.t.kind == "interface" else "generalization", node.id, target_id)

        owned = set()
        for kind, text in node.t.members:
            if kind not in ("field", "property"):
                continue
            mods, head = strip_modifiers(text)
            words = head.split()
            if len(words) < 2:
                continue
            member_name = words[-1].split(".")[-1]
            type_name = " ".join(words[:-1])

            for mentioned in element_types(type_name):
                target_id, target = resolve(mentioned)
                if target is None or target.t.kind == "enum" or target_id in owned:
                    continue
                owned.add(target_id)
                # A project type that is only a generic argument of a framework wrapper
                # (IHubContext<GameHub>) is used through it, not owned: a dependency.
                outer = clean_type(type_name).split("<")[0].strip()
                if "<" in type_name and not is_collection(type_name) and outer not in lookup:
                    add("dependency", node.id, target_id, member_name)
                    continue
                multiplicity = '"*"' if is_collection(type_name) else '"1"'
                arrow = "composition" if kind == "field" else "association"
                add(arrow, node.id, target_id, member_name, multiplicity)

        # Positional record parameters are properties, so they compose their element types.
        # A class primary constructor instead captures collaborators: an association.
        for param in node.t.params:
            name, type_name = parameter(param)
            for mentioned in element_types(type_name or ""):
                target_id, target = resolve(mentioned)
                if target is None or target.t.kind == "enum" or target_id in owned:
                    continue
                owned.add(target_id)
                multiplicity = '"*"' if is_collection(type_name) else '"1"'
                add("composition" if node.t.is_record else "association", node.id, target_id, name, multiplicity)

    return rels, external


def package_tree(nodes):
    tree = {}
    for node in nodes:
        parts = node.namespace.split(".")
        root = ".".join(parts[:2]) if len(parts) >= 2 else node.namespace
        rest = parts[2:]
        branch = tree.setdefault(root, {"subs": {}, "nodes": []})
        for segment in rest:
            branch = branch["subs"].setdefault(segment, {"subs": {}, "nodes": []})
        branch["nodes"].append(node)
    return tree


def write_packages(out, branch, indent, lookup):
    for node in sorted(branch["nodes"], key=lambda n: (n.t.kind, n.simple)):
        stereo = STEREOTYPE.get(node.t.kind, "")
        if "static" in node.t.mods:
            stereo = "static"
        if node.t.is_record:
            stereo = "record"
        if "::" in node.display:
            stereo = "nested"

        name = f'"{node.display}" as {node.id}' if node.quoted else node.id
        prefix = "abstract class" if "abstract" in node.t.mods and node.t.kind == "class" else node.kind
        header = f"{'  ' * indent}{prefix} {name}"
        if stereo:
            header += f" <<{stereo}>>"

        lines = member_lines(node, lookup)
        if not lines:
            out.append(header)
            continue
        out.append(header + " {")
        for line in lines:
            out.append("  " * (indent + 1) + line)
        out.append("  " * indent + "}")

    for name in sorted(branch["subs"]):
        out.append(f'{"  " * indent}package "{name}" {{')
        write_packages(out, branch["subs"][name], indent + 1, lookup)
        out.append(f'{"  " * indent}}}')


ARROWS = {
    "generalization": "{target} <|-- {source}",
    "realization": "{source} ..|> {target}",
    "composition": "{source} \"1\" *-- {multiplicity} {target}",
    "association": "{source} --> {multiplicity} {target}",
    "dependency": "{source} ..> {target}",
}


def main():
    nodes, lookup = build()
    rels, external = relationships(nodes, lookup)
    tree = package_tree(nodes)

    out = [
        "@startuml CrossyDeathmatch",
        "title Crossy Deathmatch - full class diagram",
        "",
        "' Generated from the C# sources by docs/uml/cs2puml.py - do not edit by hand.",
        "' Verify with: python docs/uml/csoutline.py --diff",
        "' Regenerate the MSOSA import files with: python docs/uml/puml2xmi.py",
        "",
        "skinparam linetype ortho",
        "skinparam classAttributeIconSize 0",
        "skinparam packageStyle rectangle",
        "skinparam class {",
        "  BackgroundColor White",
        "  BorderColor #444444",
        "  ArrowColor #444444",
        "}",
        'skinparam defaultFontName "Segoe UI"',
        "skinparam nodesep 30",
        "skinparam ranksep 45",
        "hide empty members",
        "",
    ]

    for root in PROJECT_ROOTS:
        if root not in tree:
            continue
        out.append(f'package "{root}" <<Frame>> {{')
        write_packages(out, tree[root], 1, lookup)
        out.append("}")
        out.append("")

    for root in sorted(set(tree) - set(PROJECT_ROOTS)):
        out.append(f'package "{root}" <<Frame>> {{')
        write_packages(out, tree[root], 1, lookup)
        out.append("}")
        out.append("")

    if external:
        out.append('package "External" <<Frame>> {')
        for name in sorted(external):
            alias = re.sub(r"[^A-Za-z0-9_]", "_", name)
            kind = "interface" if external[name] else "class"
            out.append(f'  {kind} "{name}" as {alias} <<framework>>')
        out.append("}")
        out.append("")

    for kind, source, target, label, multiplicity in rels:
        line = ARROWS[kind].format(source=source, target=target, multiplicity=multiplicity or '"1"')
        if label:
            line += f" : {label}"
        out.append(line)

    out.append("")
    out.append("@enduml")
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")

    kinds = {}
    for node in nodes:
        kinds[node.t.kind] = kinds.get(node.t.kind, 0) + 1
    rel_kinds = {}
    for kind, *_ in rels:
        rel_kinds[kind] = rel_kinds.get(kind, 0) + 1
    print(f"wrote {OUT}")
    print(f"  types        : {kinds} (total {len(nodes)})")
    print(f"  external     : {len(external)}")
    print(f"  relationships: {rel_kinds} (total {len(rels)})")


if __name__ == "__main__":
    main()
