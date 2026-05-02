using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Renders a string field as a color picker while storing HTML hex color text.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableColorAttribute : Attribute
    {
        public TableColorAttribute(bool includeAlpha = true)
        {
            IncludeAlpha = includeAlpha;
        }

        public bool IncludeAlpha { get; }
    }
}