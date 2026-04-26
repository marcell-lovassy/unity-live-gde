# Testing And Validation

## Unity Compile Check

After C# changes, verify the Unity project compiles. If Unity is not available in the current environment, inspect the changed files for namespace, asmdef, and runtime/editor reference issues and state that Unity compilation was not run.

## Editor Window Smoke Test

Use this manual flow for editor-facing changes:

1. Open the project in Unity.
2. Open `Tools > GDE > Open Editor`.
3. Select an existing data asset or create a new one.
4. Confirm the table renders without console errors.
5. Add a row, edit values, remove a row, and save.
6. Reopen the asset or editor window and confirm data persisted.

## Data Editing Scenarios

Test these scenarios when table, row, service, or selection logic changes:

- Add a new row.
- Edit ID, value, multiplier, enabled state, or custom typed fields.
- Remove one row.
- Select multiple rows with click, Ctrl+click, and Shift+click.
- Remove selected rows.
- Undo and redo each user-facing mutation.
- Switch between data assets without leaking state from the previous asset.

## Validation Scenarios

Test these scenarios when validators or field mapping change:

- Valid rows produce no errors.
- Empty IDs are reported.
- Duplicate IDs are reported.
- Validation state refreshes after editing a field.
- Validation state refreshes after import.
- Validation messages point to the correct row or field where possible.

## Serialization Scenarios

Test these scenarios when JSON, CSV, import, export, or field reflection changes:

- Export a populated asset.
- Import the exported file into the same asset.
- Import the exported file into a fresh asset.
- Confirm row count and field values round trip.
- Confirm unsupported or malformed input reports a clear error.
- Confirm import is undoable where the UI presents it as an editor mutation.

## Google Sheets Scenarios

Test these scenarios when Google Sheets code or mapping attributes change:

- Configuration asset can be selected or created.
- Sheet/tab metadata maps to the expected data type.
- Sync errors are surfaced without corrupting local data.
- Local editing still works without a configured sheet.

## Review Checklist

- Runtime assembly has no editor-only references.
- Editor mutations preserve Undo/Redo where expected.
- Import/export does not silently change valid data.
- New public runtime APIs are serializable and Unity-friendly.
- `.meta` files are present for new Unity assets.
- Generated folders and project files are not part of the change unless intentionally requested.
