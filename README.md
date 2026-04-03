# Live Game Data Editor

A Unity Editor tool for editing game data in a **spreadsheet-style interface**, built with **UI Toolkit**.

Designed for designer workflows. Publish-ready for the Unity Asset Store.

---

## Compatibility

| | |
|---|---|
| **Minimum Unity** | 2022.3 LTS |
| **Tested on** | Unity 6 (6000.0.x) |
| **UI System** | UI Toolkit (no IMGUI) |
| **External dependencies** | None |

---

## Features (MVP)

- 📋 **Spreadsheet table UI** — editable rows with Id, Value, Multiplier, Enabled columns
- 📦 **ScriptableObject integration** — pick or create a `GameDataContainer` asset
- ↩️ **Full Undo/Redo** — every change, add, remove, and import is undoable
- 📥 **JSON Import / Export** — round-trip your data via `JsonUtility`
- ☑️ **Multi-row selection** — click, Ctrl+click, Shift+click; remove all selected at once
- ➕ **Add / Remove rows** inline from the editor window

---

## Getting Started

1. Open the project in **Unity 6** (or 2022.3 LTS+).
2. From the menu bar choose **Tools > Game Data Editor**.
3. Click **Create New Data Asset** (or drag an existing `GameDataContainer` into the asset field).
4. Start editing!

---

## Project Structure

```
Assets/
  Editor/
    LiveGameDataEditor/
      LiveGameDataEditorWindow.cs   ← EditorWindow host
      GameDataTableView.cs          ← Table VisualElement
      GameDataRowView.cs            ← Row VisualElement (one per entry)
      GameDataService.cs            ← Load / save / import / export logic
      LiveGameDataEditor.uss        ← Stylesheet
      LiveGameDataEditor.asmdef     ← Editor-only assembly
  Scripts/
    LiveGameDataEditor/
      GameDataEntry.cs              ← [Serializable] data class
      GameDataContainer.cs          ← ScriptableObject
      IDataValidator.cs             ← Future validation hook (stub)
      LiveGameDataRuntime.asmdef    ← Runtime assembly
```

---

## Architecture

```
GameDataContainer (ScriptableObject)
    ↓  loaded by
GameDataService
    ↓  passed to
LiveGameDataEditorWindow
    ↓  passes to
GameDataTableView
    ↓  creates
GameDataRowView  ×N
    ↓  on change →
GameDataService.UpdateEntry()  →  Undo.RecordObject + EditorUtility.SetDirty
```

### Key design decisions

- **Row views never mutate the container directly.** Each `GameDataRowView` keeps local copies of field values and emits a cloned `GameDataEntry` on change. `GameDataService.UpdateEntry` then calls `Undo.RecordObject` _before_ committing, ensuring correct undo state capture.
- **No UXML for MVP** — all layout is C# VisualElement construction; USS handles only styles.
- **`asmdef` isolation** — the editor assembly references the runtime assembly; the runtime assembly has zero editor-only references, keeping builds clean.

---

## Extension Points (future)

| Hook | Where | Purpose |
|---|---|---|
| `IDataValidator` | `Assets/Scripts/LiveGameDataEditor/IDataValidator.cs` | Plug in per-field validation |
| `GameDataService.OnDataImported` | `GameDataService.cs` | React to bulk imports (e.g. Google Sheets sync) |
| `GameDataService.OnValidateEntry` | `GameDataService.cs` | Called after every field change — pass a validator |

---

## License

MIT — see [LICENSE](LICENSE).
