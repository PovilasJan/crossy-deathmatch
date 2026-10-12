# Importing the class model into MSOSA / MagicDraw

`CrossyDeathmatch.uml` and `CrossyDeathmatch.xmi` are the same UML model in two XMI dialects,
generated from `class-diagram.puml` by `puml2xmi.py`.

| File | Dialect | Use when |
| --- | --- | --- |
| `CrossyDeathmatch.uml` | Eclipse UML2 5.0 XMI | **Default.** What MSOSA's Eclipse UML2 importer expects. |
| `CrossyDeathmatch.xmi` | OMG UML 2.5.1 XMI | Fallback if the Eclipse importer is missing or rejects the file. |

## What is in the model

Every type under `src/` with all of its fields, properties and methods
(`python docs/uml/csoutline.py --diff` reports nothing missing):

* **Packages:** `CrossyDeathmatch.Shared`, `CrossyDeathmatch.Server`, `CrossyDeathmatch.Client`,
  plus `External` (the framework base classes `Hub` and `Form`) and `ExternalTypes` (`int`,
  `string`, `List<Lane>`, `Graphics`, ... so every member has a type).
* **12 project types:** classes `Rules`, `GameState`, `Player`, `Lane`, `Obstacle`, `Game`,
  `GameHub`, `MainForm`; enums `LaneType`, `ObstacleKind`, `Animal`, `Ability`.
* **11 relationships:** 2 generalizations (`GameHub` → `Hub`, `MainForm` → `Form`),
  2 compositions, 6 associations and 1 dependency, with multiplicities and role names.

## Import steps

1. **File → Import From → Eclipse UML2 (v5.x) XMI File...** and pick `docs/uml/CrossyDeathmatch.uml`.
2. Import into the current project (or first create one with File → New Project → UML Project).
3. If that importer is missing: **File → Import From → XMI File...** and pick `CrossyDeathmatch.xmi`.

## Building the diagram

XMI carries the model, not the picture, so the diagram is drawn once by hand:

1. Right-click the `CrossyDeathmatch` model in the Containment tree → **Create Diagram → Class Diagram**.
2. Drag the three `CrossyDeathmatch.*` packages' classes onto it, plus `Hub` and `Form` from `External`.
3. Select all (`Ctrl+A`) → right-click → **Related Elements → Display Paths...** → OK.
4. **Layout → Quick Diagram Layout** (`Ctrl+Shift+L`), then tidy by hand.

With 12 types one diagram is enough. Leave `ExternalTypes` off the diagram.

## Known conversion limits

* Stereotypes (`<<static>>`, `<<enumeration>>`, `<<framework>>`) arrive as UML comments, not
  applied stereotypes. Re-apply by hand if the report needs them.
* Association source ends are unnamed; only the target role (`_state`, `Players`, ...) is.

## Regenerating

The diagram is generated from the C# sources, so it cannot drift from the code:

```bash
python docs/uml/cs2puml.py            # sources -> class-diagram.puml
python docs/uml/csoutline.py --diff   # must report nothing missing
python docs/uml/puml2xmi.py           # class-diagram.puml -> CrossyDeathmatch.uml / .xmi
java -jar plantuml.jar -charset UTF-8 -tsvg -tpng docs/uml/class-diagram.puml
```
