# Live Game Data Editor User Guide

## Overview

Live Game Data Editor is a Unity Editor tool for editing ScriptableObject-backed game data in a table-based workflow. It is intended for designers, technical designers, and developers who want to maintain balance values, content rows, or configuration data directly inside Unity.

The package is split into:

- Runtime data contracts under `Assets/LiveGameDataEditor/Runtime`.
- Editor tooling under `Assets/LiveGameDataEditor/Editor`.
- Demonstration assets under `Assets/LiveGameDataEditor/Data/Samples`.

## Requirements

- Unity 2022.3 LTS or newer.
- `com.unity.nuget.newtonsoft-json` 3.2.1 or newer.
- Internet access only when using Google Sheets sync.

## Quick Start

1. Import the package into a Unity project.
2. Open `Tools > GDE > Open Editor`.
3. Select one of the sample data assets in `Assets/LiveGameDataEditor/Data/Samples`.
4. Edit values in the table.
5. Use Undo/Redo to verify edits are tracked by Unity.
6. Export or import data with the JSON/CSV toolbar actions if needed.

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

## Validation

The editor includes built-in validation for common authoring problems such as empty IDs and duplicate IDs. Validation is designed to guide editing without silently rewriting user data.

For custom collection rules, implement `IGameDataValidator` in editor code and register it through the validation service.

## Import And Export

The editor supports JSON and CSV round trips from the toolbar.

- JSON is useful for structured backups and source-control-friendly data review.
- CSV is useful for spreadsheet-style editing outside Unity.
- Imports replace the current container contents and should be tested with Undo/Redo before committing data changes.

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

## Included Samples

The package includes sample data assets in `Assets/LiveGameDataEditor/Data/Samples`:

- `GameDataEntryContainer.asset` demonstrates the default entry shape.
- `EnemyDataEntryContainer.asset` demonstrates a typed container with numeric, enum, list, and boolean fields.
- `GoogleSheetsConfig.asset` is a blank configuration asset for setup testing.

These samples are safe to duplicate, edit, or remove in user projects.

## Known Limitations

- Google Sheets sync requires user-provided Google credentials.
- API Key mode is read-only.
- Unity must compile custom entry/container types before they can appear in the editor.
- The package does not include automated Unity Test Framework tests yet.

## Support Checklist

If the editor does not show expected data:

1. Confirm the selected asset inherits from `GameDataContainerBase<T>`.
2. Confirm the entry type implements `IGameDataEntry`.
3. Confirm there are no Unity compile errors.
4. Reopen `Tools > GDE > Open Editor`.
5. For Google Sheets, verify the spreadsheet ID, tab name, auth mode, and sheet permissions.
