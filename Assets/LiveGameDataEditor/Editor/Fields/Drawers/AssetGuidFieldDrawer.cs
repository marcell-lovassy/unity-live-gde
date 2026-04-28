using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class AssetGuidFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableAssetAttribute>() != null;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var attribute = context.FieldInfo.GetCustomAttribute<TableAssetAttribute>();
            if (context.FieldType != typeof(string))
            {
                return CreateUnsupported(context, "[TableAsset] can only be used on string fields.");
            }

            if (!AssetGuidUtility.IsValidAssetType(attribute.AssetType))
            {
                return CreateUnsupported(context, "[TableAsset] requires a UnityEngine.Object asset type.");
            }

            var asset = AssetGuidUtility.LoadAsset(context.CurrentValue as string, attribute.AssetType);

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;

            var preview = new Image();
            preview.style.width = 24;
            preview.style.height = 24;
            preview.style.marginRight = 4;
            preview.scaleMode = UnityEngine.ScaleMode.ScaleToFit;
            preview.image = AssetGuidUtility.GetPreview(asset);
            root.Add(preview);

            var field = new ObjectField
            {
                objectType = attribute.AssetType,
                allowSceneObjects = false,
                value = asset
            };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                preview.image = AssetGuidUtility.GetPreview(evt.newValue);
                if (evt.newValue == null)
                {
                    context.SetValue(string.Empty);
                    return;
                }

                context.SetValue(AssetGuidUtility.TryGetGuid(evt.newValue, out var guid)
                    ? guid
                    : string.Empty);
            });
            root.Add(field);

            return root;
        }

        private static VisualElement CreateUnsupported(TableFieldContext context, string tooltip)
        {
            var unsupported = new Label(context.CurrentValue?.ToString() ?? string.Empty);
            unsupported.AddToClassList("col-readonly");
            unsupported.tooltip = tooltip;
            return unsupported;
        }
    }
}
