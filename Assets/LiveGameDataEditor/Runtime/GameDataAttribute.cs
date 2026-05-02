using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Optional metadata attribute for game data entry classes.
    ///     Apply to any class implementing <see cref="IGameData" /> to provide a
    ///     human-readable display name shown in the Game Data Spreadsheet Editor window.
    ///     If absent, the editor falls back to the class's type name.
    /// </summary>
    /// <example>
    ///     <code>
    /// [GameData(DisplayName = "Enemies")]
    /// public class EnemyData : IGameDataEntry { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GameDataAttribute : Attribute
    {
        /// <summary>
        ///     Human-readable label shown in the editor window header when this
        ///     entry type is loaded. Defaults to the class name if not set.
        /// </summary>
        public string DisplayName { get; set; }
    }
}