using System;
using System.Collections;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Marker interface for a ScriptableObject that holds a list of <see cref="IGameDataEntry"/> items.
    /// Implement this (via <see cref="GameDataContainerBase{T}"/>) to make any data container
    /// discoverable and editable by the Live Game Data Editor.
    /// </summary>
    public interface IGameDataContainer
    {
        /// <summary>
        /// The concrete CLR type of the entry class stored in this container.
        /// Used by the editor to reflect column definitions via
        /// <see cref="LiveGameDataEditor.Editor.GameDataColumnDefinition.FromType"/>.
        /// </summary>
        Type EntryType { get; }

        /// <summary>
        /// Returns the entries list as a non-generic <see cref="IList"/> so the editor can
        /// iterate and validate entries without knowing the concrete type.
        /// </summary>
        IList GetEntries();
    }
}
