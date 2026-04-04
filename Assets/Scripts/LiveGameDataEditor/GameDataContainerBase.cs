using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Abstract generic base class for all game data containers.
    /// Subclass this (with a concrete <typeparamref name="T"/>) and add
    /// <c>[CreateAssetMenu]</c> to the subclass to create assets in the Project window.
    ///
    /// Unity does not support <c>[CreateAssetMenu]</c> on generic classes directly,
    /// so the concrete subclass is required.
    ///
    /// Example:
    /// <code>
    /// [CreateAssetMenu(menuName = "My Game/Enemy Data")]
    /// public class EnemyDataContainer : GameDataContainerBase&lt;EnemyData&gt; { }
    /// </code>
    /// </summary>
    public abstract class GameDataContainerBase<T> : ScriptableObject, IGameDataContainer
        where T : IGameDataEntry
    {
        public List<T> Entries = new List<T>();

        /// <inheritdoc/>
        public Type EntryType => typeof(T);

        /// <inheritdoc/>
        public IList GetEntries() => Entries;
    }
}
