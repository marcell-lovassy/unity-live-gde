using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Marks a field as the user-facing display value for table reference labels.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableDisplayAttribute : Attribute
    {
    }
}