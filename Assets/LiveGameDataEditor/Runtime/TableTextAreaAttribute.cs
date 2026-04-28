using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders a string field with a larger text editing experience.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableTextAreaAttribute : Attribute
    {
        public int MinLines { get; }
        public int MaxLines { get; }

        public TableTextAreaAttribute(int minLines = 2, int maxLines = 6)
        {
            MinLines = minLines;
            MaxLines = maxLines;
        }
    }
}
