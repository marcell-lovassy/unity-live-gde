using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LiveGameDataEditor.Editor
{
    public static class AssetGuidUtility
    {
        public static bool IsValidAssetType(Type assetType)
        {
            return assetType != null && typeof(Object).IsAssignableFrom(assetType);
        }

        public static Object LoadAsset(string guid, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(guid) || !IsValidAssetType(assetType)) return null;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath(path, assetType);
        }

        public static bool TryGetGuid(Object asset, out string guid)
        {
            guid = string.Empty;
            if (asset == null) return false;

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return false;

            guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid);
        }

        public static Texture2D GetPreview(Object asset)
        {
            if (asset == null) return null;

            return AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
        }
    }
}