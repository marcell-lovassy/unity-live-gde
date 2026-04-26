# Game Data Spreadsheet Editor

**Game Data Spreadsheet Editor** is a Unity Editor extension for editing ScriptableObject-backed game data in a spreadsheet-style workflow. It gives designers and developers a focused data-editing window inside Unity, with typed rows, validation, import/export tools, and optional Google Sheets synchronization.

The project is built with **UI Toolkit** and split into clean runtime/editor assemblies so game data types can stay build-safe while authoring tools remain editor-only.

---

## At A Glance

| | |
|---|---|
| **Minimum Unity** | 2022.3 LTS |
| **Tested on** | Unity 6 (6000.0.x) |
| **UI System** | UI Toolkit |
| **Primary data model** | ScriptableObject containers |
| **Required UPM dependency** | `com.unity.nuget.newtonsoft-json` |
| **Runtime assembly** | `LiveGameDataEditor.Runtime` |
| **Editor assembly** | `LiveGameDataEditor.Editor` |

---

## Project Description

Game Data Spreadsheet Editor is designed for projects where game balance, configuration, and content tables need to be edited quickly without leaving Unity. Instead of hand-editing serialized assets or maintaining separate tooling, users can open a dedicated editor window, select a data container, and work with rows and columns in a familiar table interface.

The runtime layer defines the data contracts: entries, containers, field metadata attributes, and sample typed data. The editor layer handles the authoring experience: asset selection, table rendering, row editing, validation, Undo/Redo, import/export, and Google Sheets integration.

This separation keeps the package practical for production projects: runtime data can ship with the game, while editor workflows stay isolated from player builds.

---

## Features

- **Spreadsheet-style table editing** for ScriptableObject data containers.
- **Typed data support** through `IGameDataEntry` and `GameDataContainerBase<T>`.
- **Custom column metadata** with attributes such as `ColumnHeaderAttribute`, `ListFieldAttribute`, and `GoogleSheetsTabAttribute`.
- **Designer-friendly validation** for issues such as empty IDs and duplicate IDs.
- **Undo/Redo support** for editor-driven data mutations.
- **Multi-row selection** for bulk operations.
- **JSON and CSV import/export** for local data round trips.
- **Google Sheets integration** for pull/push workflows.
- **UI Toolkit editor UI** with styling in USS.
- **Runtime/editor asmdef isolation** to keep builds clean.

---

## Getting Started

1. Open the project in **Unity 2022.3 LTS** or **Unity 6**.
2. From the menu bar, choose **Tools > GDE > Open Editor**.
3. Select a sample data asset from `Assets/LiveGameDataEditor/Data/Samples`, or create a compatible ScriptableObject data asset.
4. Edit rows in the table.
5. Use validation, import/export, Undo/Redo, or Google Sheets tools as needed.

To create custom data, define an entry type that implements `IGameDataEntry`, then create a concrete container type that inherits from `GameDataContainerBase<T>`.

```csharp
using UnityEngine;

namespace LiveGameDataEditor
{
    [CreateAssetMenu(menuName = "My Game/Enemy Data")]
    public class EnemyDataContainer : GameDataContainerBase<EnemyDataEntry>
    {
    }
}
```

---

## Project Structure

```text
Assets/
  LiveGameDataEditor/
    Runtime/
      GameDataEntry.cs
      GameDataContainer.cs
      GameDataContainerBase.cs
      IGameDataEntry.cs
      IGameDataContainer.cs
      ColumnHeaderAttribute.cs
      ListFieldAttribute.cs
      GoogleSheetsTabAttribute.cs
      LiveGameDataRuntime.asmdef
      Samples/
        EnemyDataEntry.cs
        EnemyDataContainer.cs

    Editor/
      LiveGameDataEditorWindow.cs
      GameDataTableView.cs
      GameDataRowView.cs
      GameDataService.cs
      GameDataValidationService.cs
      GameDataJsonSerializer.cs
      GameDataCsvSerializer.cs
      LiveGameDataEditor.uss
      LiveGameDataEditor.asmdef
      GoogleSheets/
        GoogleSheetsConfig.cs
        GoogleSheetsService.cs
        GoogleSheetsSyncPanel.cs

    Data/
      Samples/
        GameDataEntryContainer.asset
        EnemyDataEntryContainer.asset
        GoogleSheetsConfig.asset

    Documentation/
      UserGuide.md
    Third-Party Notices.txt
```

---

## Architecture

```text
ScriptableObject data container
    -> GameDataService
    -> LiveGameDataEditorWindow
    -> GameDataTableView
    -> GameDataRowView
    -> service-mediated update
    -> Undo.RecordObject + dirty asset
```

### Design Principles

- **Runtime code stays build-safe.** Runtime files must not reference `UnityEditor`.
- **Editor code owns authoring behavior.** UI, asset creation, validation, serialization, Undo/Redo, and Google Sheets sync live in the editor assembly.
- **Rows do not own persistence.** Row views collect edits and send changes through editor services so data mutation remains consistent.
- **Attributes describe data shape.** Runtime attributes provide metadata for editor display and sheet mapping without pulling editor dependencies into builds.
- **Import/export is editor-facing.** File operations and external sync are authoring tools, not runtime systems.

---

## Extension Points

| Extension point | Purpose |
|---|---|
| `IGameDataEntry` | Defines a row type that can be edited in the table. |
| `GameDataContainerBase<T>` | Defines a ScriptableObject container for typed rows. |
| `IGameDataValidator` | Adds collection-level validation rules. |
| `IGameDataSerializer` | Adds alternative serialization formats. |
| `ColumnHeaderAttribute` | Customizes displayed column names. |
| `ListFieldAttribute` | Marks list-style fields for editor handling. |
| `GoogleSheetsTabAttribute` | Maps a data type to a Google Sheets tab. |

---

## Documentation

Package user documentation is included at:

- `Assets/LiveGameDataEditor/Documentation/UserGuide.md`
- `Assets/LiveGameDataEditor/Documentation/AssetStoreListingDraft.md`
- `Assets/LiveGameDataEditor/Third-Party Notices.txt`

Agent-facing project documentation is available under `Docs/AgentDoc`:

- `ProjectArchitecture.md`
- `UnityWorkflow.md`
- `TestingAndValidation.md`
- `AssetStoreRelease.md`

These files describe implementation boundaries, Unity workflow rules, validation expectations, and release checks for future coding agents.

---

## Google Sheets And API Usage

Google Sheets sync is optional. The editor works offline for local ScriptableObject editing, JSON export/import, and CSV export/import.

When Google Sheets sync is enabled, users must provide their own Google Cloud credentials. API keys, OAuth credentials, service account key files, private spreadsheet IDs, and related secrets should not be committed to public source control. Google API usage is subject to Google's terms, quotas, permissions, and possible costs.

---

## License

MIT - see [LICENSE](LICENSE).
