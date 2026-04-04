using System;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Creates new <see cref="IGameDataContainer"/> ScriptableObject assets for a given
    /// entry type. Looks up the matching container type via <see cref="GameDataTypeRegistry"/>,
    /// prompts for a save path, and registers the asset with the AssetDatabase.
    /// </summary>
    public static class GameDataAssetFactory
    {
        /// <summary>
        /// Creates a new container asset for <paramref name="entryType"/>.
        /// Shows a save-file dialog; returns <c>null</c> if the user cancels or if no
        /// container type is registered for the given entry type.
        /// </summary>
        public static IGameDataContainer CreateForEntryType(Type entryType)
        {
            if (entryType == null) return null;

            var containerType = GameDataTypeRegistry.FindContainerTypeForEntry(entryType);
            if (containerType == null)
            {
                EditorUtility.DisplayDialog(
                    "No Container Type Found",
                    $"No concrete ScriptableObject implementing IGameDataContainer was found " +
                    $"for entry type '{entryType.Name}'.\n\n" +
                    $"Create a class that extends GameDataContainerBase<{entryType.Name}> and " +
                    "add [CreateAssetMenu] to it.",
                    "OK");
                return null;
            }

            string defaultName = $"New{entryType.Name}Container";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Game Data Asset",
                defaultName,
                "asset",
                $"Choose a location to save the new {containerType.Name} asset.");

            if (string.IsNullOrEmpty(path)) return null;

            var instance = ScriptableObject.CreateInstance(containerType);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LiveGameDataEditor] Created {containerType.Name} at: {path}");
            return (IGameDataContainer)instance;
        }
    }
}
