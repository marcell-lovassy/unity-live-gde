# Unity Workflow

## Unity Project Rules

- Open the project with Unity 2022.3 LTS or Unity 6.
- Do not edit generated folders: `Library`, `Temp`, `Logs`, or `obj`.
- Do not manually edit generated `.csproj` or `.sln` files unless explicitly requested.
- Keep Unity `.meta` files paired with their assets.
- When moving an asset, move its `.meta` file with it.
- When deleting an asset, delete its `.meta` file as part of the same change.

## Folder Layout

```text
Assets/LiveGameDataEditor/
  Runtime/   Runtime-safe data contracts, attributes, and samples
  Editor/    Editor windows, UI Toolkit views, services, serializers, validators
  Data/      Sample or local data assets
```

The README may contain user-facing overview material, but implementation work should follow the actual folder layout in the repository.

## Assembly Definitions

- Runtime asmdef: `Assets/LiveGameDataEditor/Runtime/LiveGameDataRuntime.asmdef`
- Editor asmdef: `Assets/LiveGameDataEditor/Editor/LiveGameDataEditor.asmdef`
- Runtime code must compile into player builds.
- Editor code may use `UnityEditor` and should stay under an editor assembly or editor-only folder.

Before finishing assembly or namespace changes, check that runtime files do not reference:

- `UnityEditor`
- `UnityEditor.UIElements`
- `AssetDatabase`
- `EditorWindow`
- `Undo`
- editor file panels or editor menu APIs

## UI Toolkit

- Prefer UI Toolkit for editor UI.
- Keep layout and behavior in C# VisualElement code unless the project intentionally adopts UXML.
- Keep visual styling in USS where practical.
- Avoid introducing IMGUI for new UI unless it is required for a narrow compatibility reason.
- Keep controls stable for repeated data-entry workflows: table rows, selection, validation messages, and import/export actions should not shift unexpectedly.

## ScriptableObject Editing

- User-facing mutations should be undoable.
- Call `Undo.RecordObject` before changing serialized data.
- Mark changed assets dirty after successful mutations.
- Route durable changes through editor services so row views do not directly own persistence.
- Keep selection and validation state synchronized after add, remove, import, and undo operations.

## Assets And Samples

- Samples should remain easy to import, inspect, and remove.
- Avoid placing editor-only sample code in runtime folders.
- Keep sample assets compatible with the current runtime data model.
- For Asset Store readiness, avoid committing project-local test artifacts unless they are intentional samples.
