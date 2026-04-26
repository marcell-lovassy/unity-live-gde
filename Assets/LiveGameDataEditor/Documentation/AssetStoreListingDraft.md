# Asset Store Listing Draft

## Required Disclosure

Google Sheets sync is optional. When enabled, it uses Google APIs and requires the user to configure their own Google Cloud credentials and spreadsheet permissions. Google API usage is subject to Google's terms, quotas, permissions, and possible costs. No API keys, OAuth credentials, service account key files, or private spreadsheet IDs are included with this package.

This package depends on `com.unity.nuget.newtonsoft-json`, provided through Unity Package Manager.

## Short Description

Live Game Data Editor is a Unity Editor tool for editing ScriptableObject-backed game data in a spreadsheet-style UI. It helps designers and developers manage balance values, content rows, and configuration data directly inside Unity.

## Long Description

Live Game Data Editor provides a focused authoring workflow for ScriptableObject data. Create typed data containers, edit rows in a table, validate common data issues, and round trip data through JSON or CSV. Optional Google Sheets sync lets teams pull or push data from spreadsheets when they configure their own Google Cloud credentials.

The package uses UI Toolkit and separates runtime data contracts from editor-only tooling through assembly definitions. Runtime types stay build-safe, while editor services handle Undo/Redo, validation, import/export, and external sync.

## Key Features

- Spreadsheet-style table editing for ScriptableObject containers.
- Typed row support through `IGameDataEntry` and `GameDataContainerBase<T>`.
- Built-in validation for empty IDs and duplicate IDs.
- JSON and CSV import/export.
- Optional Google Sheets pull/push workflow.
- Runtime/editor assembly separation.
- Included sample data assets and offline documentation.

## Suggested Keywords

game data, scriptableobject, editor tool, spreadsheet, balancing, ui toolkit, csv, json, google sheets, data editor
