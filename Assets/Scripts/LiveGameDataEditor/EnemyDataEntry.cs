using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// A single row of game data. Serializable so it works with both ScriptableObject and JsonUtility.
    /// Implements <see cref="IGameDataEntry"/> via explicit property so the public field
    /// remains the serialization target (Unity serializes fields, not auto-properties).
    /// </summary>
    [GameData(DisplayName = "Enemy")]
    [Serializable]
    public class EnemyDataEntry : IGameDataEntry
    {
        public string Id;
        public int Health;
        public int Damage;
        public bool Enabled;

        // Explicit interface implementation — routes through the public serialized field.
        string IGameDataEntry.Id { get => Id; set => Id = value; }

        public EnemyDataEntry()
        {
            Id = "new_entry";
            Health = 100;
            Damage = 10;
            Enabled = true;
        }

        /// <summary>Creates a shallow copy of this entry.</summary>
        public EnemyDataEntry Clone() => new EnemyDataEntry
        {
            Id = Id,
            Health = Health,
            Damage = Damage,
            Enabled = Enabled
        };
    }
}
