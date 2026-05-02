using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Renders a string field as an asset picker while storing the Unity asset GUID.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableAssetAttribute : Attribute
    {
        public TableAssetAttribute(Type assetType)
        {
            AssetType = assetType;
        }

        public Type AssetType { get; }
    }
}