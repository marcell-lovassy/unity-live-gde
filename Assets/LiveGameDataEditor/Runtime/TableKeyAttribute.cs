using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Marks a field as the stable key used by table references.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableKeyAttribute : Attribute
    {
    }
}
