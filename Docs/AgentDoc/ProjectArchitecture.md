# Project Architecture

## Summary

Game Data Spreadsheet Editor is a Unity Editor tool for editing game data assets through a spreadsheet-style UI. Runtime code defines serializable data contracts and attributes. Editor code provides the UI, asset services, serializers, validation, and integrations.

## Assemblies

- `Assets/LiveGameDataEditor/Runtime/LiveGameDataRuntime.asmdef` contains runtime-safe data models, interfaces, and attributes.
- `Assets/LiveGameDataEditor/Editor/LiveGameDataEditor.asmdef` contains editor UI, services, serializers, validators, asset creation, welcome UI, and Google Sheets integration.
- The editor assembly may depend on the runtime assembly.
- The runtime assembly must remain free of `UnityEditor` references.

## Core Data Flow

The normal editor flow is:

```text
GameDataContainer or typed container
-> GameDataService
-> LiveGameDataEditorWindow
-> GameDataTableView
-> GameDataRowView
-> service-mediated data update
-> Undo.RecordObject + dirty asset
```

Row views should present and edit values, but durable changes should go through services so Undo/Redo, validation, and asset dirtying remain consistent.

## Runtime Layer

The runtime layer owns build-safe data definitions:

- Base container and entry interfaces/classes.
- Attributes that describe game data fields, column headers, list fields, and Google Sheets tabs.
- Sample runtime data types under `Runtime/Samples`.
- Serializable shapes that work with Unity serialization.

Runtime code should avoid editor-only APIs, file dialogs, asset database access, UI Toolkit editor APIs, and import/export implementation details.

## Editor Layer

The editor layer owns authoring behavior:

- Editor window entry points and UI Toolkit visual elements.
- Table, row, browser, and selection UI.
- Asset creation and selection workflows.
- Validation services and built-in validators.
- JSON/CSV serialization services.
- Google Sheets configuration, sync, and editor-only integration code.
- USS styling for the editor UI.

Editor code should preserve Undo/Redo behavior for user-facing data mutations and keep UI state synchronized with the selected data asset.

## Validation

Validation is editor-owned. Built-in validators check common authoring errors such as empty IDs and duplicate IDs. Validation should report actionable results without corrupting or silently rewriting user data.

When adding validation rules, keep them separate from row rendering so the table can display results consistently and future validators can be added without rewriting UI controls.

## Serialization

Serialization is editor-owned. JSON and CSV import/export should round trip supported data without changing valid values unexpectedly.

When changing serialization, verify:

- Exported files contain the expected field names and row values.
- Imported files restore the intended row count and values.
- Invalid input produces a clear editor-facing error.
- Undo/Redo behavior remains correct after import.

## Google Sheets

Google Sheets support is editor-only. Runtime types may include metadata attributes for mapping, but network/auth/sync logic belongs in the editor assembly.

Keep Google Sheets code isolated from core table editing so local asset editing, validation, and import/export remain usable without sheets configuration.
