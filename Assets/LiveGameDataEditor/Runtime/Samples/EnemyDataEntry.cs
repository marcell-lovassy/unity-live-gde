using System;
using System.Collections.Generic;

namespace LiveGameDataEditor
{
    /// <summary>Difficulty tier of an enemy — demonstrates enum field support.</summary>
    public enum EnemyType { Normal, Elite, Boss }

    /// <summary>
    /// Example game data entry demonstrating all supported field types:
    ///   string, int, bool, enum, and List&lt;T&gt; with [ListField].
    /// </summary>
    [GameData(DisplayName = "Enemy")]
    [Serializable]
    public class EnemyDataEntry : IGameDataEntry
    {
        string IGameDataEntry.Id { get => Id; set => Id = value; }
        
        public string Id;
        
        [ColumnHeader("HP")]
        public int Health;
        
        public int Damage;
        public EnemyType EnemyType;
        
        [ColumnHeader("Drop Tags")]
        [ListField(",")]
        public List<string> DropTags;
        
        public bool Enabled;

        public EnemyDataEntry()
        {
            Id        = "new_entry";
            Health    = 100;
            Damage    = 10;
            EnemyType = EnemyType.Normal;
            DropTags  = new List<string>();
            Enabled   = true;
        }
    }
}

