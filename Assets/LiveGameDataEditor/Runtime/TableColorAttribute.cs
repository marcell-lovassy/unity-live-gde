using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders a string field as a color picker while storing HTML hex color text.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableColorAttribute : Attribute
    {
        public bool IncludeAlpha { get; }

        public TableColorAttribute(bool includeAlpha = true)
        {
            IncludeAlpha = includeAlpha;
        }
    }
}
