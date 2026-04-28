using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders an enum field as a flags editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableFlagsAttribute : Attribute
    {
    }
}
