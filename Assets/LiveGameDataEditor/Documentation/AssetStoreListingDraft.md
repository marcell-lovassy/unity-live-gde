# Asset Store Listing Draft

## Required Disclosure

Google Sheets sync is optional. When enabled, it uses Google APIs and requires the user to configure their own Google Cloud credentials and spreadsheet permissions. Google API usage is subject to Google's terms, quotas, permissions, and possible costs. No API keys, OAuth credentials, service account key files, or private spreadsheet IDs are included with this package.

This package depends on Unity Package Manager packages `com.unity.nuget.newtonsoft-json` and `com.unity.ugui`.

The included runtime sample uses TextMesh Pro UI components through Unity's uGUI/TextMesh Pro support. TextMesh Pro Essentials assets are included under `Assets/TextMesh Pro` so the sample scene and prefab render immediately after import. Included font/sprite attribution and license files are preserved with their `.meta` files.

Suggested first submission price: USD 29.99.

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
- Included sample data assets, runtime lookup demo scene with TextMesh Pro UI labels, and offline documentation.

## Runtime Sample

The package includes a small runtime demo scene that shows one `DataControllers` GameObject with data-set controller components such as `EnemyDataController` and `WeaponDataController`. Scene objects store row IDs and resolve ScriptableObject entries at runtime through `GetEntryById` / `TryGetEntryById`. The sample prefab displays resolved values with TextMesh Pro UI labels and also logs resolved data in Play Mode.

Unity asset GUID fields are intended for editor-friendly storage and interchange. Runtime builds cannot use `AssetDatabase`, so projects that need runtime asset loading by GUID should add a catalog, Addressables mapping, Resources mapping, or another game-specific loading layer.

## Dependency And License Notes

- `com.unity.nuget.newtonsoft-json` is used for JSON serialization and Google Sheets REST payload handling.
- `com.unity.ugui` is used by the included runtime sample for Canvas layout and TextMesh Pro UI components.
- TextMesh Pro Essentials assets are included under `Assets/TextMesh Pro`, including TMP settings, shaders, materials, the LiberationSans font files under the SIL Open Font License 1.1, and EmojiOne sample sprite attribution.
- Google Sheets sync is optional and requires each user to configure their own Google Cloud project, credentials, permissions, and quota/cost management.

## Suggested Keywords

game data, scriptableobject, editor tool, spreadsheet, balancing, validation, references, ui toolkit, csv, json, google sheets, data editor
