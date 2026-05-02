using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Renders an enum field as a flags editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableFlagsAttribute : Attribute
    {
    }
}