using System;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// A single row of game data. Serializable so it works with both ScriptableObject and JsonUtility.
    /// Implements <see cref="IGameDataEntry"/> via explicit property so the public field
    /// remains the serialization target (Unity serializes fields, not auto-properties).
    /// </summary>
    [Serializable]
    public class GameDataEntry : IGameDataEntry
    {
        public string Id;
        public int Value;
        public float Multiplier;
        public bool Enabled;

        // Explicit interface implementation — routes through the public serialized field.
        string IGameDataEntry.Id { get => Id; set => Id = value; }

        public GameDataEntry()
        {
            Id = "new_entry";
            Value = 0;
            Multiplier = 1f;
            Enabled = true;
        }

        /// <summary>Creates a shallow copy of this entry.</summary>
        public GameDataEntry Clone() => new GameDataEntry
        {
            Id = Id,
            Value = Value,
            Multiplier = Multiplier,
            Enabled = Enabled
        };
    }
}
