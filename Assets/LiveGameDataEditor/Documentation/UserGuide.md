# Game Data Spreadsheet Editor User Guide

## Overview

Game Data Spreadsheet Editor is a Unity Editor tool for editing ScriptableObject-backed game data in a table-based workflow. It is intended for designers, technical designers, and developers who want to maintain balance values, content rows, or configuration data directly inside Unity.

The package is split into:

- Runtime data contracts under `Assets/LiveGameDataEditor/Runtime`.
- Editor tooling under `Assets/LiveGameDataEditor/Editor`.
- Demonstration assets under `Assets/LiveGameDataEditor/Data/Samples`.

## Requirements

- Unity 2022.3 LTS or newer.
- `com.unity.nuget.newtonsoft-json` 3.2.1 or newer.
- `com.unity.ugui` 2.0.0 or newer for the included runtime sample scene.
- TextMesh Pro support from Unity's uGUI package for the included sample UI.
- Internet access only when using Google Sheets sync.

## Quick Start

1. Import the package into a Unity project.
2. Open `Tools > GDE > Open Editor`.
3. Select one of the sample data assets in `Assets/LiveGameDataEditor/Data/Samples`.
4. Edit values in the table.
5. Use Undo/Redo to verify edits are tracked by Unity.
6. Export or import data with the JSON/CSV toolbar actions if needed.
7. Open `Assets/LiveGameDataEditor/Data/Samples/RuntimeLookupDemo.unity` and enter Play Mode to see runtime ID lookup from sample scene objects.

## Creating Custom Data

Create an entry class that implements `IGameDataEntry`, then create a ScriptableObject container that inherits from `GameDataContainerBase<T>`.

```csharp
using UnityEngine;

namespace LiveGameDataEditor
{
    public class ItemDataEntry : IGameDataEntry
    {
        public string Id { get; set; }
        public int Price;
        public bool Enabled = true;
    }

    [CreateAssetMenu(menuName = "My Game/Item Data")]
    public class ItemDataContainer : GameDataContainerBase<ItemDataEntry>
    {
    }
}
```

After Unity compiles, create the container asset through the Project window and select it in the editor window.

## Field Metadata

Use runtime attributes to improve the editor display without adding editor dependencies to runtime code.

- `ColumnHeaderAttribute` changes the displayed column name.
- `ListFieldAttribute` marks list fields for table handling.
- `GoogleSheetsTabAttribute` maps a container to a Google Sheets tab.
- `TableKeyAttribute` marks the stable row key used by references.
- `TableDisplayAttribute` marks the display label used by reference dropdowns.
- `TableReferenceAttribute` renders a string key field as a dropdown of rows from another table.
- `TableColorAttribute` renders a string field as a color picker and stores `#RRGGBB` or `#RRGGBBAA`.
- `TableAssetAttribute` renders a string field as an asset picker and stores a Unity asset GUID.
- `TableRangeAttribute` renders an int or float field as a slider with numeric input.
- `TableFlagsAttribute` renders an enum field as a flags dropdown.
- `TableTextAreaAttribute` and `TableTagsAttribute` are reserved for richer text and tag editing workflows.

### Attribute Example

```csharp
[Serializable]
public class EnemyDataEntry : IGameDataEntry
{
    string IGameDataEntry.Id { get => Id; set => Id = value; }

    [TableKey]
    public string Id;

    [TableDisplay]
    public string DisplayName;

    [TableReference(typeof(WeaponDataContainer))]
    public string WeaponId;

    [TableColor]
    public string UiColor;

    [TableAsset(typeof(Sprite))]
    public string IconGuid;

    [TableRange(0, 100)]
    public int SpawnChance;

    [TableFlags]
    public EnemyCategory Categories;
}
```

### References

`[TableReference(typeof(SomeContainer))]` is intended for string fields. The stored value remains the referenced row key, for example `iron_sword`. The editor finds assets of the target container type, reads the target row's `[TableKey]`, and shows `[TableDisplay]` text when available.

Reference validation reports missing target assets, missing key fields, duplicate target keys, and broken source references.

### Color Strings

`[TableColor]` keeps data export-friendly by storing colors as hex strings:

- `#RRGGBB`
- `#RRGGBBAA`

Invalid non-empty color strings are reported by validation.

### Asset GUID Fields

`[TableAsset(typeof(Sprite))]` stores a Unity asset GUID in a string field. In the editor, GUID resolution uses `AssetDatabase.GUIDToAssetPath`, which searches the whole project by GUID and does not need a configured search folder.

Important runtime note: Unity asset GUID lookup is an editor-side workflow. Player builds cannot load arbitrary assets by GUID through `AssetDatabase`. If runtime code needs to load icons or other assets from GUID strings, add a runtime catalog, Addressables mapping, Resources lookup, or another project-specific asset loading layer.

### Range And Flags

`[TableRange(min, max)]` supports `int` and `float` fields. User edits are clamped by the editor control, and imported out-of-range values are reported as warnings until changed.

`[TableFlags]` supports enum fields and is best used on enums marked with C# `[Flags]`. The editor warns when the enum is missing `[Flags]`.

## Validation

The editor includes built-in validation for common authoring problems such as empty IDs and duplicate IDs. Validation is designed to guide editing without silently rewriting user data.

Custom field validation is shown at both row and cell level. The row highlight summarizes that something is wrong in the row, while the specific cell highlight and tooltip show the exact field issue.

For custom collection rules, implement `IGameDataValidator` in editor code and register it through the validation service.

## Import And Export

The editor supports JSON and CSV round trips from the toolbar.

- JSON is useful for structured backups and source-control-friendly data review.
- CSV is useful for spreadsheet-style editing outside Unity.
- Imports replace the current container contents and should be tested with Undo/Redo before committing data changes.

Custom field attributes do not create hidden export formats. The underlying field values are exported:

- Reference fields export the key string.
- Color fields export the hex string.
- Asset fields export the GUID string.
- Range fields export the number.
- Flags fields export according to the existing enum serialization behavior.
- List fields keep the existing list serialization behavior.

## Runtime Usage

The editor helps author ScriptableObject data. At runtime, use the generated or custom container assets directly, then build lookup helpers that match your game's architecture.

The included runtime sample uses one scene object named `DataControllers` with one component per data set:

- `EnemyDataController`
- `WeaponDataController`

Each controller owns one container asset and exposes:

```csharp
bool TryGetEntryById(string id, out EnemyDataEntry entry);
EnemyDataEntry GetEntryById(string id);
```

Scene objects can store only a stable row ID and resolve their data at startup:

```csharp
public sealed class SampleEnemy : MonoBehaviour
{
    [SerializeField] private string enemyId;
    [SerializeField] private EnemyDataController enemyDataController;

    private void Start()
    {
        var data = enemyDataController.GetEntryById(enemyId);
        Debug.Log($"Loaded {data.DisplayName} with {data.Health} HP");
    }
}
```

Open `Assets/LiveGameDataEditor/Data/Samples/RuntimeLookupDemo.unity` to see this pattern. The demo scene contains a `DataControllers` object, three `SampleEnemy` objects with IDs, TextMesh Pro UI labels on the sample enemy prefab, and a small runtime reporter that logs resolved enemy and weapon data when entering Play Mode.

## Google Sheets Sync

Google Sheets sync is optional. The package can be used entirely offline without configuring Google Sheets.

When using Google Sheets:

- Create a `GoogleSheetsConfig` asset.
- Enter the spreadsheet ID from the Google Sheets URL.
- Choose an authentication mode: API Key, OAuth, or Service Account.
- Store credentials only in project-local config assets or external ignored files.
- Do not commit API keys, OAuth credentials, service account JSON files, or private spreadsheet IDs to public source control.

API Key mode is pull-only and requires a publicly readable sheet. OAuth and Service Account modes can pull and push, depending on the permissions configured in Google Cloud and Google Sheets.

Google API usage is subject to Google's terms, quotas, permissions, and possible costs. Users are responsible for configuring their own Google Cloud project and credentials.

## Package Dependencies And Licenses

The editor package intentionally depends on:

- `com.unity.nuget.newtonsoft-json` for JSON import/export and Google Sheets REST payload handling.
- `com.unity.ugui` for the runtime sample scene's Canvas layout and TextMesh Pro UI components.

The sample scene references TextMesh Pro UI components, so the package includes TextMesh Pro Essentials assets under `Assets/TextMesh Pro`. Keep those assets and their `.meta` files if you want the sample scene and `SampleEnemy.prefab` to work immediately after import.

Included third-party/license disclosures are summarized in `Assets/LiveGameDataEditor/Third-Party Notices.txt`. The TextMesh Pro Essentials import includes LiberationSans font files under the SIL Open Font License 1.1 and EmojiOne sample sprite attribution under `Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt`.

## Included Samples

The package includes sample data assets in `Assets/LiveGameDataEditor/Data/Samples`:

- `GameDataEntryContainer.asset` demonstrates the default entry shape.
- `EnemyDataEntryContainer.asset` demonstrates references, colors, asset GUIDs, range fields, flags, numeric fields, enum fields, list fields, and boolean fields.
- `WeaponDataContainer.asset` demonstrates a referenced table with `[TableKey]` and `[TableDisplay]`.
- `RuntimeLookupDemo.unity` demonstrates runtime lookup through data controller components.
- TextMesh Pro Essentials assets under `Assets/TextMesh Pro` support the runtime sample prefab UI.
- `GoogleSheetsConfig.asset` is a blank configuration asset for setup testing.

These samples are safe to duplicate, edit, or remove in user projects.

## Known Limitations

- Google Sheets sync requires user-provided Google credentials.
- API Key mode is read-only.
- Unity must compile custom entry/container types before they can appear in the editor.
- Unity asset GUID fields are editor-friendly data values. Runtime loading by GUID requires a game-specific catalog or loading system.
- The package does not include automated Unity Test Framework tests yet.

## Support Checklist

If the editor does not show expected data:

1. Confirm the selected asset inherits from `GameDataContainerBase<T>`.
2. Confirm the entry type implements `IGameDataEntry`.
3. Confirm there are no Unity compile errors.
4. Reopen `Tools > GDE > Open Editor`.
5. For Google Sheets, verify the spreadsheet ID, tab name, auth mode, and sheet permissions.
