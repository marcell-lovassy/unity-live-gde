using System.Reflection;
using System.Collections.Generic;
using UnityEditor;

namespace LiveGameDataEditor.Editor
{
    public sealed class AssetGuidFieldValidator : ITableFieldValidator
    {
        public bool CanValidate(TableValidationContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableAssetAttribute>() != null;
        }

        public IEnumerable<ValidationResult> Validate(TableValidationContext context)
        {
            var attribute = context.FieldInfo.GetCustomAttribute<TableAssetAttribute>();
            if (context.FieldType != typeof(string))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableAsset] can only be used on string fields: {context.FieldInfo.Name}.",
                    ValidationSeverity.Error);
                yield break;
            }

            if (!AssetGuidUtility.IsValidAssetType(attribute.AssetType))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    "[TableAsset] requires a UnityEngine.Object asset type.",
                    ValidationSeverity.Error);
                yield break;
            }

            var guid = context.CurrentValue as string;
            if (string.IsNullOrWhiteSpace(guid))
            {
                yield break;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"Missing asset GUID: {guid}.",
                    ValidationSeverity.Error);
                yield break;
            }

            var asset = AssetDatabase.LoadAssetAtPath(path, attribute.AssetType);
            if (asset == null)
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"Asset GUID {guid} does not resolve to a {attribute.AssetType.Name}.",
                    ValidationSeverity.Error);
            }
        }
    }
}
