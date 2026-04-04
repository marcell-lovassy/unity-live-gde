using System.Collections.Generic;
using LiveGameDataEditor;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Hooks into Unity's asset-save pipeline.  When a <see cref="GameDataContainer"/> asset
    /// is saved and its associated <see cref="GoogleSheetsConfig"/> has
    /// <see cref="GoogleSheetsConfig.AutoPushOnSave"/> enabled, data is automatically pushed
    /// to the configured Google Sheet.
    ///
    /// The window registers/unregisters pairs via <see cref="Register"/> /
    /// <see cref="Unregister"/> as containers are loaded and unloaded.
    /// </summary>
    public sealed class GoogleSheetsAutoSaveMonitor : AssetModificationProcessor
    {
        // containerAssetGUID → (container, config)
        private static readonly Dictionary<string, (IGameDataContainer container, GoogleSheetsConfig config)>
            _registrations = new Dictionary<string, (IGameDataContainer, GoogleSheetsConfig)>();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a container for auto-push on save.  If <c>config.AutoPushOnSave</c> is
        /// false the call is a no-op (and any prior registration for the same GUID is removed).
        /// Pass <c>null</c> for <paramref name="config"/> to unregister.
        /// </summary>
        public static void Register(string containerGuid, IGameDataContainer container, GoogleSheetsConfig config)
        {
            if (string.IsNullOrEmpty(containerGuid))
            {
                return;
            }

            if (config == null || !config.AutoPushOnSave)
            {
                _registrations.Remove(containerGuid);
                return;
            }

            _registrations[containerGuid] = (container, config);
        }

        /// <summary>Removes any auto-push registration for the given container GUID.</summary>
        public static void Unregister(string containerGuid)
        {
            if (!string.IsNullOrEmpty(containerGuid))
            {
                _registrations.Remove(containerGuid);
            }
        }

        // ── AssetModificationProcessor hook ────────────────────────────────────

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!_registrations.TryGetValue(guid, out var reg))
                {
                    continue;
                }

                if (!reg.config.IsConfigured())
                {
                    Debug.LogWarning(
                        "[LiveGameDataEditor] Auto-push on save skipped: " +
                        $"GoogleSheetsConfig for '{path}' is not fully configured.");
                    continue;
                }

                // Fire-and-forget async push — result is logged by the service.
                _ = GoogleSheetsService.PushAsync(reg.container, reg.config);
            }

            return paths;
        }
    }
}
