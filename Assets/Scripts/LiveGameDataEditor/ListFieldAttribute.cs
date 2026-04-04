using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Marks a <c>List&lt;T&gt;</c> or <c>T[]</c> field for display and editing in the
    /// Live Game Data Editor table. Without this attribute, collection fields are shown
    /// as a read-only label.
    ///
    /// In the table the list is rendered as a single text cell whose items are joined
    /// by the separator: <c>"sword, shield, bow"</c>.
    /// Editing the text and pressing Tab/Enter splits it back by the same separator.
    ///
    /// Supported element types: <c>string</c>, <c>int</c>, <c>float</c>.
    ///
    /// JSON export/import always uses a proper JSON array, regardless of the separator.
    /// </summary>
    /// <example>
    /// <code>
    /// [ListField(", ")]
    /// public List&lt;string&gt; Tags;
    ///
    /// [ListField("|")]
    /// public string[] AbilityIds;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class ListFieldAttribute : Attribute
    {
        /// <summary>
        /// Delimiter used to join items for display and to split the edited text back
        /// into individual items. Defaults to <c>", "</c>.
        /// </summary>
        public string Separator { get; }

        /// <param name="separator">Delimiter string. Defaults to <c>", "</c>.</param>
        public ListFieldAttribute(string separator = ", ") => Separator = separator;
    }
}
