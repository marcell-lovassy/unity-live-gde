using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders a List&lt;string&gt; field as lightweight tags.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableTagsAttribute : Attribute
    {
    }
}
