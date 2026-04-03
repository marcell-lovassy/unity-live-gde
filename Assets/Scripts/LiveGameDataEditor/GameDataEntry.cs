using System;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// A single row of game data. Serializable so it works with both ScriptableObject and JsonUtility.
    /// </summary>
    [Serializable]
    public class GameDataEntry
    {
        public string Id;
        public int Value;
        public float Multiplier;
        public bool Enabled;

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
