#!/usr/bin/env python3
"""Convert docs/uml/class-diagram.puml into UML2 XMI that MSOSA 26 / MagicDraw can import.

Usage:
    python docs/uml/puml2xmi.py                      # regenerate the default outputs
    python docs/uml/puml2xmi.py <in.puml> <outbase>

Emits two flavours of the same model:
  * <base>.uml  - Eclipse UML2 5.0 XMI  (File > Import From > Eclipse UML2 XMI File)
  * <base>.xmi  - OMG UML 2.5.1 XMI     (fallback if the Eclipse importer is unavailable)
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

# --------------------------------------------------------------------------- model


class Element:
    def __init__(self, kind, name, eid, stereotypes, is_abstract, parent):
        self.kind = kind                # class | interface | enum | datatype
        self.name = name
        self.eid = eid
        self.stereotypes = stereotypes  # list[str]
        self.is_abstract = is_abstract
        self.parent = parent            # Package
        self.attributes = []
        self.operations = []
        self.literals = []
        self.generalizations = []
        self.realizations = []


class Attribute:
    def __init__(self, name, type_name, visibility, is_static, default):
        self.name = name
        self.type_name = type_name
        self.visibility = visibility
        self.is_static = is_static
        self.default = default


class Parameter:
    def __init__(self, name, type_name):
        self.name = name
        self.type_name = type_name


class Operation:
    def __init__(self, name, params, return_type, visibility, is_static, is_abstract):
        self.name = name
        self.params = params
        self.return_type = return_type
        self.visibility = visibility
        self.is_static = is_static
        self.is_abstract = is_abstract


class Package:
    def __init__(self, name, eid, parent):
        self.name = name
        self.eid = eid
        self.parent = parent
        self.packages = []
        self.elements = []


class Relation:
    def __init__(self, kind, source, target, src_mult, tgt_mult, label):
        self.kind = kind                # generalization | realization | composition
        self.source = source            # | aggregation | association | dependency
        self.target = target
        self.src_mult = src_mult
        self.tgt_mult = tgt_mult
        self.label = label


# --------------------------------------------------------------------------- ids

_used_ids = set()


def make_id(prefix, name):
    slug = re.sub(r"[^A-Za-z0-9_]", "_", name)
    base = "_" + prefix + "_" + slug
    eid = base
    n = 2
    while eid in _used_ids:
        eid = base + "_" + str(n)
        n += 1
    _used_ids.add(eid)
    return eid


# --------------------------------------------------------------------------- parsing

VIS = {"+": "public", "-": "private", "#": "protected", "~": "package"}

DECL_RE = re.compile(
    r"^(?P<abstract>abstract\s+)?(?P<kind>class|interface|enum)\s+"
    r"(?:\"(?P<qname>[^\"]+)\"|(?P<name>[^\s{<]+))"
    r"(?:\s+as\s+(?P<alias>\w+))?"
    r"(?P<stereo>(?:\s*<<[^>]*>>)*)"
    r"\s*(?P<open>\{)?\s*$"
)

PKG_RE = re.compile(
    r"^package\s+(?:\"(?P<qname>[^\"]+)\"|(?P<name>[^\s{<]+))"
    r"(?:\s+as\s+(?P<alias>\w+))?"
    r"(?:\s*<<[^>]*>>)*"
    r"\s*\{\s*$"
)

CONNECTORS = [
    "<|--", "<|..", "--|>", "..|>",
    "*--", "--*", "o--", "--o",
    "..>", "<..", "-->", "<--",
    "..", "--",
]

REL_RE = re.compile(
    r"^(?P<left>\"[^\"]+\"|[A-Za-z_][\w.:]*)\s*"
    r"(?:\"(?P<lm>[^\"]*)\"\s*)?"
    r"(?P<conn>[-.*o|<>\[\]a-z]+)\s*"
    r"(?:\"(?P<rm>[^\"]*)\"\s*)?"
    r"(?P<right>\"[^\"]+\"|[A-Za-z_][\w.:]*)"
    r"\s*(?::\s*(?P<label>.*))?$"
)

MEMBER_RE = re.compile(r"^(?P<vis>[+\-#~])?\s*(?P<body>.*)$")
MOD_RE = re.compile(r"\{(static|abstract)\}")


def split_top_level(text, sep=","):
    """Split on `sep`, ignoring separators nested in <>, () or []."""
    out, depth, cur = [], 0, ""
    for ch in text:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == sep and depth <= 0:
            out.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        out.append(cur)
    return [s.strip() for s in out if s.strip()]


class Parser:
    def __init__(self, text):
        self.lines = text.splitlines()
        self.root = Package("CrossyDeathmatch", make_id("pkg", "root"), None)
        self.index = {}          # name / alias -> Element
        self.relations = []
        self.datatypes = {}      # type name -> Element
        self.datatype_pkg = Package("ExternalTypes",
                                    make_id("pkg", "ExternalTypes"), self.root)

    # -- type resolution ---------------------------------------------------
    def type_for(self, raw):
        if not raw:
            return None
        name = raw.strip().rstrip("?").strip()
        if not name:
            return None
        if name in self.index:
            return self.index[name]
        if name not in self.datatypes:
            dt = Element("datatype", name, make_id("dt", name), [], False,
                         self.datatype_pkg)
            self.datatypes[name] = dt
            self.datatype_pkg.elements.append(dt)
        return self.datatypes[name]

    # -- member parsing ----------------------------------------------------
    def parse_member(self, owner, raw):
        m = MEMBER_RE.match(raw)
        vis = VIS.get(m.group("vis") or "", "public")
        body = m.group("body").strip()

        mods = set(MOD_RE.findall(body))
        body = MOD_RE.sub("", body).strip()
        if not body or body == "...":
            return

        # An operation's parameter list opens before the ':' that introduces its type;
        # a tuple-typed attribute ("- _hop : (Direction, long)?") is the other way round.
        paren = body.find("(")
        colon = body.find(":")
        is_operation = paren >= 0 and (colon < 0 or paren < colon)
        if is_operation:
            self.parse_operation(owner, body, vis, "static" in mods, "abstract" in mods)
        else:
            self.parse_attribute(owner, body, vis, "static" in mods)

    def parse_operation(self, owner, body, vis, is_static, is_abstract):
        open_at = body.index("(")
        name = body[:open_at].strip()
        depth, close_at = 0, None
        for i in range(open_at, len(body)):
            if body[i] == "(":
                depth += 1
            elif body[i] == ")":
                depth -= 1
                if depth == 0:
                    close_at = i
                    break
        if close_at is None:
            close_at = len(body) - 1
        inner = body[open_at + 1:close_at]
        tail = body[close_at + 1:].strip()
        ret = tail[1:].strip() if tail.startswith(":") else None

        params = []
        for chunk in split_top_level(inner):
            if chunk in ("...", ""):
                continue
            if ":" in chunk:
                pname, ptype = chunk.split(":", 1)
                params.append(Parameter(pname.strip(), ptype.strip()))
            else:
                params.append(Parameter(chunk.strip(), None))
        owner.operations.append(
            Operation(name, params, ret, vis, is_static, is_abstract))

    def parse_attribute(self, owner, body, vis, is_static):
        default = None
        if "=" in body:
            body, default = body.split("=", 1)
            body, default = body.strip(), default.strip()
        if ":" in body:
            names, type_name = body.split(":", 1)
            type_name = type_name.strip()
        else:
            names, type_name = body, None
        for nm in split_top_level(names):
            if nm and nm != "...":
                owner.attributes.append(
                    Attribute(nm, type_name, vis, is_static, default))

    # -- main loop ---------------------------------------------------------
    def parse(self):
        pkg_stack = [self.root]
        current = None          # Element whose body we are inside
        buffer = ""

        for raw in self.lines:
            line = raw.strip()
            if (not line or line.startswith("'") or line.startswith("@")
                    or line.startswith("!")):
                continue
            if current is None and (line.startswith("skinparam")
                                    or line.startswith("title")
                                    or line.startswith("hide")):
                if line.endswith("{"):
                    pkg_stack.append(pkg_stack[-1])   # skinparam block
                continue

            if current is not None:
                if line == "}":
                    if buffer:
                        self.parse_member(current, buffer)
                        buffer = ""
                    current = None
                    continue
                if current.kind == "enum":
                    lit = line.rstrip(",").strip()
                    if lit and lit != "...":
                        current.literals.append(lit)
                    continue
                buffer = (buffer + " " + line).strip() if buffer else line
                if buffer.endswith(","):
                    continue
                self.parse_member(current, buffer)
                buffer = ""
                continue

            if line == "}":
                if len(pkg_stack) > 1:
                    pkg_stack.pop()
                continue

            m = PKG_RE.match(line)
            if m:
                name = (m.group("qname") or m.group("name")).strip()
                pkg = Package(name, make_id("pkg", name), pkg_stack[-1])
                pkg_stack[-1].packages.append(pkg)
                pkg_stack.append(pkg)
                continue

            m = DECL_RE.match(line)
            if m:
                name = (m.group("qname") or m.group("name")).strip()
                alias = m.group("alias")
                stereo = re.findall(r"<<([^>]*)>>", m.group("stereo") or "")
                stereo = [s.strip() for s in stereo if s.strip()]
                kind = m.group("kind")
                is_abstract = bool(m.group("abstract")) or "abstract" in stereo
                el = Element(kind, name, make_id(kind[:3], alias or name),
                             stereo, is_abstract, pkg_stack[-1])
                pkg_stack[-1].elements.append(el)
                self.index.setdefault(name, el)
                if alias:
                    self.index.setdefault(alias, el)
                if m.group("open"):
                    current = el
                continue

            if "[hidden]" in line:
                continue

            m = REL_RE.match(line)
            if m:
                self.parse_relation(m)
                continue

        return self

    def parse_relation(self, m):
        left = m.group("left").strip("\"")
        right = m.group("right").strip("\"")
        conn = m.group("conn")
        label = (m.group("label") or "").strip()
        lm = m.group("lm") or ""
        rm = m.group("rm") or ""

        src = self.index.get(left)
        tgt = self.index.get(right)
        if src is None or tgt is None:
            return  # layout hint or package-level link

        connector = None
        for c in CONNECTORS:
            if c in conn:
                connector = c
                break
        if connector is None:
            return

        add = self.relations.append
        if connector in ("<|--", "<|.."):
            add(Relation("generalization", tgt, src, "", "", label))
        elif connector == "--|>":
            add(Relation("generalization", src, tgt, "", "", label))
        elif connector == "..|>":
            add(Relation("realization", src, tgt, "", "", label))
        elif connector == "*--":
            add(Relation("composition", src, tgt, lm, rm, label))
        elif connector == "--*":
            add(Relation("composition", tgt, src, rm, lm, label))
        elif connector == "o--":
            add(Relation("aggregation", src, tgt, lm, rm, label))
        elif connector == "--o":
            add(Relation("aggregation", tgt, src, rm, lm, label))
        elif connector in ("..>", ".."):
            add(Relation("dependency", src, tgt, "", "", label))
        elif connector == "<..":
            add(Relation("dependency", tgt, src, "", "", label))
        elif connector in ("-->", "--"):
            add(Relation("association", src, tgt, lm, rm, label))
        elif connector == "<--":
            add(Relation("association", tgt, src, rm, lm, label))


# --------------------------------------------------------------------------- emitting

def esc(text):
    return (str(text).replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace("\"", "&quot;"))


def multiplicity(raw):
    """'1' -> (1,1); '*' -> (0,-1); '0..3' -> (0,3); '' -> None."""
    raw = (raw or "").strip()
    if not raw:
        return None
    lo, hi = raw.split("..", 1) if ".." in raw else (raw, raw)
    lo, hi = lo.strip(), hi.strip()
    return ("0" if lo in ("*", "") else lo, "-1" if hi in ("*", "") else hi)


class Writer:
    def __init__(self, parser, dialect):
        self.p = parser
        self.dialect = dialect          # 'eclipse' | 'omg'
        self.out = []
        self.counter = 0

    def uid(self, prefix):
        self.counter += 1
        return "_%s%04d" % (prefix, self.counter)

    def w(self, indent, text):
        self.out.append("  " * indent + text)

    # -- helpers -----------------------------------------------------------
    def emit_multiplicity(self, indent, mult, base_id):
        if not mult:
            return
        lo, hi = mult
        self.w(indent, "<lowerValue xmi:type=\"uml:LiteralInteger\" xmi:id=\"%s_lo\" "
                       "value=\"%s\"/>" % (base_id, lo))
        self.w(indent, "<upperValue xmi:type=\"uml:LiteralUnlimitedNatural\" "
                       "xmi:id=\"%s_hi\" value=\"%s\"/>" % (base_id, hi))

    def comment(self, indent, owner_id, body):
        cid = self.uid("cmt")
        self.w(indent, "<ownedComment xmi:type=\"uml:Comment\" xmi:id=\"%s\" "
                       "annotatedElement=\"%s\">" % (cid, owner_id))
        self.w(indent + 1, "<body>%s</body>" % esc(body))
        self.w(indent, "</ownedComment>")

    # -- classifiers -------------------------------------------------------
    def emit_element(self, indent, el):
        uml_kind = {"class": "Class", "interface": "Interface",
                    "enum": "Enumeration", "datatype": "DataType"}[el.kind]
        attrs = ["xmi:type=\"uml:%s\"" % uml_kind, "xmi:id=\"%s\"" % el.eid,
                 "name=\"%s\"" % esc(el.name)]
        if el.is_abstract:
            attrs.append("isAbstract=\"true\"")
        has_body = (el.attributes or el.operations or el.literals or el.stereotypes
                    or el.generalizations or el.realizations)
        open_tag = "<packagedElement " + " ".join(attrs)
        if not has_body:
            self.w(indent, open_tag + "/>")
            return
        self.w(indent, open_tag + ">")

        if el.stereotypes:
            self.comment(indent + 1, el.eid,
                         " ".join("<<%s>>" % s for s in el.stereotypes))

        for general in el.generalizations:
            self.w(indent + 1, "<generalization xmi:type=\"uml:Generalization\" "
                               "xmi:id=\"%s\" general=\"%s\"/>"
                               % (self.uid("gen"), general.eid))

        for iface in el.realizations:
            self.w(indent + 1, "<interfaceRealization xmi:type=\"uml:InterfaceRealization\" "
                               "xmi:id=\"%s\" client=\"%s\" supplier=\"%s\" contract=\"%s\"/>"
                               % (self.uid("rlz"), el.eid, iface.eid, iface.eid))

        for attr in el.attributes:
            self.emit_attribute(indent + 1, attr)
        for op in el.operations:
            self.emit_operation(indent + 1, op)
        for lit in el.literals:
            self.w(indent + 1, "<ownedLiteral xmi:type=\"uml:EnumerationLiteral\" "
                               "xmi:id=\"%s\" name=\"%s\"/>"
                               % (self.uid("lit"), esc(lit)))

        self.w(indent, "</packagedElement>")

    def emit_attribute(self, indent, attr):
        bits = ["xmi:type=\"uml:Property\"", "xmi:id=\"%s\"" % self.uid("prp"),
                "name=\"%s\"" % esc(attr.name),
                "visibility=\"%s\"" % attr.visibility]
        if attr.is_static:
            bits.append("isStatic=\"true\"")
        t = self.p.type_for(attr.type_name)
        if t is not None:
            bits.append("type=\"%s\"" % t.eid)
        if attr.default is None:
            self.w(indent, "<ownedAttribute " + " ".join(bits) + "/>")
        else:
            self.w(indent, "<ownedAttribute " + " ".join(bits) + ">")
            self.w(indent + 1, "<defaultValue xmi:type=\"uml:LiteralString\" "
                               "xmi:id=\"%s\" value=\"%s\"/>"
                               % (self.uid("dft"), esc(attr.default)))
            self.w(indent, "</ownedAttribute>")

    def emit_operation(self, indent, op):
        bits = ["xmi:type=\"uml:Operation\"", "xmi:id=\"%s\"" % self.uid("opr"),
                "name=\"%s\"" % esc(op.name), "visibility=\"%s\"" % op.visibility]
        if op.is_static:
            bits.append("isStatic=\"true\"")
        if op.is_abstract:
            bits.append("isAbstract=\"true\"")
        if not op.params and not op.return_type:
            self.w(indent, "<ownedOperation " + " ".join(bits) + "/>")
            return
        self.w(indent, "<ownedOperation " + " ".join(bits) + ">")
        for prm in op.params:
            pbits = ["xmi:type=\"uml:Parameter\"", "xmi:id=\"%s\"" % self.uid("par"),
                     "name=\"%s\"" % esc(prm.name), "direction=\"in\""]
            t = self.p.type_for(prm.type_name)
            if t is not None:
                pbits.append("type=\"%s\"" % t.eid)
            self.w(indent + 1, "<ownedParameter " + " ".join(pbits) + "/>")
        if op.return_type:
            rbits = ["xmi:type=\"uml:Parameter\"", "xmi:id=\"%s\"" % self.uid("par"),
                     "name=\"return\"", "direction=\"return\""]
            t = self.p.type_for(op.return_type)
            if t is not None:
                rbits.append("type=\"%s\"" % t.eid)
            self.w(indent + 1, "<ownedParameter " + " ".join(rbits) + "/>")
        self.w(indent, "</ownedOperation>")

    # -- packages ----------------------------------------------------------
    def emit_package(self, indent, pkg):
        self.w(indent, "<packagedElement xmi:type=\"uml:Package\" xmi:id=\"%s\" "
                       "name=\"%s\">" % (pkg.eid, esc(pkg.name)))
        for sub in pkg.packages:
            self.emit_package(indent + 1, sub)
        for el in pkg.elements:
            self.emit_element(indent + 1, el)
        self.w(indent, "</packagedElement>")

    # -- relationships -----------------------------------------------------
    def emit_relation(self, indent, rel):
        if rel.kind in ("generalization", "realization"):
            return  # emitted inside the classifier
        if rel.kind == "dependency":
            name = rel.label.replace("<<", "").replace(">>", "").strip()
            nm = " name=\"%s\"" % esc(name) if name else ""
            self.w(indent, "<packagedElement xmi:type=\"uml:Dependency\" xmi:id=\"%s\"%s "
                           "client=\"%s\" supplier=\"%s\"/>"
                           % (self.uid("dep"), nm, rel.source.eid, rel.target.eid))
            return

        aid = self.uid("ass")
        src_end, tgt_end = aid + "_a", aid + "_b"
        label = rel.label.strip()
        role = label if re.fullmatch(r"[_A-Za-z]\w*", label) else ""
        assoc_name = "" if role else label

        agg = ""
        if rel.kind == "composition":
            agg = " aggregation=\"composite\""
        elif rel.kind == "aggregation":
            agg = " aggregation=\"shared\""

        nm = " name=\"%s\"" % esc(assoc_name) if assoc_name else ""
        self.w(indent, "<packagedElement xmi:type=\"uml:Association\" xmi:id=\"%s\"%s "
                       "memberEnd=\"%s %s\" navigableOwnedEnd=\"%s\">"
                       % (aid, nm, src_end, tgt_end, tgt_end))

        # end attached to the source (the "whole" for compositions/aggregations)
        self.w(indent + 1, "<ownedEnd xmi:type=\"uml:Property\" xmi:id=\"%s\" "
                           "type=\"%s\" association=\"%s\">"
                           % (src_end, rel.source.eid, aid))
        self.emit_multiplicity(indent + 2, multiplicity(rel.src_mult), src_end)
        self.w(indent + 1, "</ownedEnd>")

        role_attr = " name=\"%s\"" % esc(role) if role else ""
        self.w(indent + 1, "<ownedEnd xmi:type=\"uml:Property\" xmi:id=\"%s\"%s "
                           "type=\"%s\"%s association=\"%s\">"
                           % (tgt_end, role_attr, rel.target.eid, agg, aid))
        self.emit_multiplicity(indent + 2, multiplicity(rel.tgt_mult), tgt_end)
        self.w(indent + 1, "</ownedEnd>")
        self.w(indent, "</packagedElement>")

    # -- document ----------------------------------------------------------
    def render(self):
        p = self.p
        if self.dialect == "eclipse":
            header = ("<uml:Model xmi:version=\"2.1\" "
                      "xmlns:xmi=\"http://schema.omg.org/spec/XMI/2.1\" "
                      "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" "
                      "xmlns:uml=\"http://www.eclipse.org/uml2/5.0.0/UML\" "
                      "xmi:id=\"%s\" name=\"%s\">" % (p.root.eid, esc(p.root.name)))
        else:
            header = ("<uml:Model xmi:version=\"2.5.1\" "
                      "xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" "
                      "xmlns:uml=\"http://www.omg.org/spec/UML/20161101\" "
                      "xmi:id=\"%s\" name=\"%s\">" % (p.root.eid, esc(p.root.name)))

        self.w(0, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
        self.w(0, header)
        for pkg in p.root.packages:
            self.emit_package(1, pkg)
        self.emit_package(1, p.datatype_pkg)
        for rel in p.relations:
            self.emit_relation(1, rel)
        self.w(0, "</uml:Model>")
        return "\n".join(self.out) + "\n"


# --------------------------------------------------------------------------- driver

def build(puml_path):
    parser = Parser(Path(puml_path).read_text(encoding="utf-8")).parse()
    # fold inheritance into the classifiers: MSOSA expects them owned by the child
    for rel in parser.relations:
        if rel.kind == "generalization":
            rel.source.generalizations.append(rel.target)
        elif rel.kind == "realization":
            rel.source.realizations.append(rel.target)
    return parser


def walk_elements(pkg):
    for el in pkg.elements:
        yield el
    for sub in pkg.packages:
        yield from walk_elements(sub)


def count_packages(pkg):
    return len(pkg.packages) + sum(count_packages(s) for s in pkg.packages)


def main(argv):
    root = Path(__file__).resolve().parent
    src = Path(argv[1]) if len(argv) > 1 else root / "class-diagram.puml"
    base = Path(argv[2]) if len(argv) > 2 else root / "CrossyDeathmatch"

    parser = build(src)
    for dialect, ext in (("eclipse", ".uml"), ("omg", ".xmi")):
        out = base.with_suffix(ext)
        out.write_text(Writer(parser, dialect).render(), encoding="utf-8")
        print("wrote " + str(out))

    kinds = {}
    for el in walk_elements(parser.root):
        kinds[el.kind] = kinds.get(el.kind, 0) + 1
    rels = {}
    for rel in parser.relations:
        rels[rel.kind] = rels.get(rel.kind, 0) + 1
    print("  packages     : %d" % count_packages(parser.root))
    print("  classifiers  : %s" % kinds)
    print("  relationships: %s" % rels)


if __name__ == "__main__":
    main(sys.argv)
