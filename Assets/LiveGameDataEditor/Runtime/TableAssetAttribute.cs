using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders a string field as an asset picker while storing the Unity asset GUID.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableAssetAttribute : Attribute
    {
        public Type AssetType { get; }

        public TableAssetAttribute(Type assetType)
        {
            AssetType = assetType;
        }
    }
}
