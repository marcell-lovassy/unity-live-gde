using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Marks a field as the stable key used by table references.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableKeyAttribute : Attribute
    {
    }
}