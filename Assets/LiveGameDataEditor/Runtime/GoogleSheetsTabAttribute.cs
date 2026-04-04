using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Declares which Google Sheet tab this container's data maps to when using
    /// the Live Game Data Editor's Google Sheets Sync feature.
    ///
    /// Place this attribute on your <see cref="GameDataContainerBase{T}"/> subclass.
    /// If the attribute is absent, the sync service falls back to the entry type name.
    ///
    /// Example:
    /// <code>
    /// [GoogleSheetsTab("Enemies")]
    /// [CreateAssetMenu(menuName = "My Game/Enemy Data")]
    /// public class EnemyDataContainer : GameDataContainerBase&lt;EnemyDataEntry&gt; { }
    /// </code>
    ///
    /// One <c>GoogleSheetsConfig</c> asset can then be shared across all containers
    /// in the project — each container maps to its own tab in the same spreadsheet.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class GoogleSheetsTabAttribute : Attribute
    {
        /// <summary>The exact name of the tab (sheet) inside the Google Spreadsheet.</summary>
        public string TabName { get; }

        public GoogleSheetsTabAttribute(string tabName)
        {
            TabName = tabName;
        }
    }
}
