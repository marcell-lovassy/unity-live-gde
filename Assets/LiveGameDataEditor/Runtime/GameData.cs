using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     A single row of game data. Serializable so it works with both ScriptableObject and JsonUtility.
    ///     Implements <see cref="IGameData" /> via explicit property so the public field
    ///     remains the serialization target (Unity serializes fields, not auto-properties).
    /// </summary>
    [GameData(DisplayName = "Game Data")]
    [Serializable]
    public class GameData : IGameData
    {
        public string Id;
        public int Value;
        public float Multiplier;
        public bool Enabled;

        public GameData()
        {
            Id = "new_entry";
            Value = 0;
            Multiplier = 1f;
            Enabled = true;
        }

        string IGameData.Id
        {
            get => Id;
            set => Id = value;
        }

        /// <summary>Creates a shallow copy of this entry.</summary>
        public GameData Clone()
        {
            return new GameData
            {
                Id = Id,
                Value = Value,
                Multiplier = Multiplier,
                Enabled = Enabled
            };
        }
    }
}