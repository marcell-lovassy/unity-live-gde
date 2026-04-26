# AGENTS.md

## Project

Live Game Data Editor is a Unity Editor extension for editing ScriptableObject-backed game data in a spreadsheet-style UI built with UI Toolkit.

The project is intended to be publish-ready for the Unity Asset Store and should remain compatible with Unity 2022.3 LTS and Unity 6.

## Repository Rules

- Treat the current Unity folder structure as the source of truth.
- Runtime code lives under `Assets/LiveGameDataEditor/Runtime`.
- Editor-only code lives under `Assets/LiveGameDataEditor/Editor`.
- Sample data and sample types live under `Assets/LiveGameDataEditor/.../Samples`.
- Do not edit generated Unity or IDE output such as `Library`, `Temp`, `Logs`, `obj`, `.csproj`, or `.sln` files unless explicitly requested.
- Preserve Unity `.meta` files when adding, moving, or deleting assets.

## Assembly Boundaries

- `LiveGameDataEditor.Runtime` must stay runtime-safe and must not reference `UnityEditor`.
- `LiveGameDataEditor.Editor` may reference `UnityEditor`, UI Toolkit editor APIs, `Undo`, `AssetDatabase`, file dialogs, and editor-only services.
- Editor assemblies may reference runtime assemblies; runtime assemblies must not reference editor assemblies.

## Coding Conventions

- Prefer UI Toolkit over IMGUI for editor UI.
- Keep data mutations routed through editor services rather than directly mutating ScriptableObject data from row views.
- Use `Undo.RecordObject` before editor data mutations that should be undoable.
- Mark modified ScriptableObject assets dirty after service-mediated changes.
- Keep runtime APIs simple and serializable for Unity.
- Avoid broad refactors unless they are required for the requested change.

## Validation

- After code changes, verify Unity compilation when possible.
- For UI changes, smoke test the editor window from `Tools > Game Data Editor`.
- For data changes, test add, edit, remove, multi-selection, Undo/Redo, and asset persistence.
- For serialization changes, test JSON/CSV import and export round trips.
- For validation changes, test empty IDs, duplicate IDs, and valid rows.
- Confirm runtime code has no `UnityEditor` references before finishing.

## Important Docs

- Architecture: `Docs/AgentDoc/ProjectArchitecture.md`
- Unity workflow: `Docs/AgentDoc/UnityWorkflow.md`
- Testing and validation: `Docs/AgentDoc/TestingAndValidation.md`
- Asset Store release checklist: `Docs/AgentDoc/AssetStoreRelease.md`
