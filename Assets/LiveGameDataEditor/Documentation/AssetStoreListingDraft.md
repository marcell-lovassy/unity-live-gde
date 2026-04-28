# Asset Store Listing Draft

## Required Disclosure

Google Sheets sync is optional. When enabled, it uses Google APIs and requires the user to configure their own Google Cloud credentials and spreadsheet permissions. Google API usage is subject to Google's terms, quotas, permissions, and possible costs. No API keys, OAuth credentials, service account key files, or private spreadsheet IDs are included with this package.

This package depends on `com.unity.nuget.newtonsoft-json`, provided through Unity Package Manager.

## Short Description

Game Data Spreadsheet Editor is a Unity Editor tool for editing ScriptableObject-backed game data in a spreadsheet-style UI with validation, references, import/export, and designer-friendly field controls.

## Long Description

Game Data Spreadsheet Editor provides a focused authoring workflow for ScriptableObject data. Create typed data containers, edit rows in a table, validate common data issues, build references between tables, and round trip data through JSON or CSV. Optional Google Sheets sync lets teams pull or push data from spreadsheets when they configure their own Google Cloud credentials.

The package uses UI Toolkit and separates runtime data contracts from editor-only tooling through assembly definitions. Runtime types stay build-safe, while editor services handle Undo/Redo, validation, import/export, and external sync.

Custom field attributes make common game-data workflows easier without changing the underlying serialized data. References store key strings, colors store hex strings, asset fields store GUID strings, and range/flags fields keep normal numeric and enum values. This keeps JSON, CSV, and Google Sheets data clean and reviewable.

## Key Features

- Spreadsheet-style table editing for ScriptableObject containers.
- Typed row support through `IGameDataEntry` and `GameDataContainerBase<T>`.
- Relational ScriptableObject editing with key-based reference dropdowns.
- Built-in validation for empty IDs, duplicate IDs, broken references, invalid color strings, missing asset GUIDs, range issues, and invalid attribute usage.
- Cell-level validation highlighting and tooltips.
- Color picker fields stored as hex strings.
- Sprite/icon asset picker fields stored as Unity asset GUID strings.
- Range slider fields for int and float values.
- Enum flags editing for `[Flags]` enums.
- JSON and CSV import/export.
- Optional Google Sheets pull/push workflow.
- Runtime/editor assembly separation.
- Included sample data assets, runtime lookup demo scene, and offline documentation.

## Runtime Sample

The package includes a small runtime demo scene that shows one `DataControllers` GameObject with data-set controller components such as `EnemyDataController` and `WeaponDataController`. Scene objects store row IDs and resolve ScriptableObject entries at runtime through `GetEntryById` / `TryGetEntryById`. The demo logs resolved data in Play Mode without requiring extra runtime UI packages.

Unity asset GUID fields are intended for editor-friendly storage and interchange. Runtime builds cannot use `AssetDatabase`, so projects that need runtime asset loading by GUID should add a catalog, Addressables mapping, Resources mapping, or another game-specific loading layer.

## Suggested Keywords

game data, scriptableobject, editor tool, spreadsheet, balancing, validation, references, ui toolkit, csv, json, google sheets, data editor
