# Asset Store Release

## Release Goal

Prepare the Unity project as a clean, documented Editor extension that can be reviewed by users and the Unity Asset Store without requiring project-local setup artifacts.

## Compatibility

- Minimum supported Unity version: 2022.3 LTS.
- Tested Unity version target: Unity 6.
- UI system: UI Toolkit.
- Runtime assembly remains player-build safe.
- Editor-only functionality remains isolated under the editor assembly.

## Package Contents

Before release, confirm the package includes:

- Runtime scripts needed by users.
- Editor scripts and USS files needed by the tool.
- Sample runtime types and sample data assets that demonstrate the workflow.
- TextMesh Pro Essentials assets required by the runtime sample UI, with `.meta` files preserved.
- README and license files.
- Offline package documentation under `Assets/LiveGameDataEditor/Documentation`.
- Third-party notices for package dependencies and API integrations.
- Required `.meta` files for all Unity assets.

Before release, confirm the package excludes:

- `Library`
- `Temp`
- `Logs`
- `obj`
- `.idea`
- `.google`
- IDE caches
- Generated `.csproj` and `.sln` files unless the distribution process explicitly requires them
- `UserSettings`
- `Docs/AgentDoc/temp`
- Local credentials, tokens, or private Google Sheets data
- Empty root folders outside `Assets/LiveGameDataEditor`

## User-Facing Checks

- README matches the actual folder structure and menu paths.
- `Tools > GDE > Open Editor` opens the editor window.
- A new user can create or select a data asset without extra setup.
- Sample data assets exist under `Assets/LiveGameDataEditor/Data/Samples`.
- Samples demonstrate a real workflow and do not depend on private services.
- Error messages are understandable for designers and technical artists.

## Technical Checks

- Runtime code has no `UnityEditor` references.
- Editor assembly references the runtime assembly correctly.
- Data mutations use Undo/Redo where expected.
- JSON/CSV import and export round trip sample data.
- Validation catches empty IDs and duplicate IDs.
- Google Sheets integration fails gracefully when not configured.
- `Packages/manifest.json` includes only dependencies required by the package: `com.unity.nuget.newtonsoft-json` and `com.unity.ugui`.
- `com.unity.ide.rider` and other development-only IDE packages are not included.

## Documentation Checks

- Public extension points are documented.
- Samples are documented.
- Known limitations are documented.
- Version compatibility is documented.
- License is present and accurate.
- Third-party notices disclose Unity packages, TextMesh Pro Essentials assets, included fonts/sprites, and Google Sheets API terms/costs.

## Final Smoke Test

1. Open a clean checkout in a supported Unity version.
2. Let Unity import the project from scratch.
3. Confirm there are no compile errors.
4. Open `Tools > GDE > Open Editor`.
5. Create or select a data asset.
6. Add, edit, remove, import, export, undo, and redo.
7. Confirm no private files or generated folders are included in the release payload.
