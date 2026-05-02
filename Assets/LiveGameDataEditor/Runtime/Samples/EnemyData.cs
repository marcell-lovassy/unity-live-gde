using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>Difficulty tier of an enemy — demonstrates enum field support.</summary>
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>Enemy category flags — demonstrates [TableFlags] enum editing.</summary>
    [Flags]
    public enum EnemyCategory
    {
        None = 0,
        Undead = 1 << 0,
        Humanoid = 1 << 1,
        Beast = 1 << 2,
        Magic = 1 << 3,
        Boss = 1 << 4
    }

    /// <summary>
    ///     Example game data entry demonstrating all supported field types:
    ///     string, int, bool, enum, List&lt;T&gt; with [ListField], and table references.
    /// </summary>
    [GameData(DisplayName = "Enemy")]
    [Serializable]
    public class EnemyData : IGameData
    {
        [TableKey] public string Id;

        [TableDisplay] public string DisplayName;

        [ColumnHeader("Weapon")] [TableReference(typeof(WeaponDataContainer))]
        public string WeaponId;

        [ColumnHeader("UI Color")] [TableColor]
        public string UiColor;

        [ColumnHeader("Icon")] [TableAsset(typeof(Sprite))]
        public string IconGuid;

        [ColumnHeader("HP")] public int Health;

        [ColumnHeader("Spawn %")] [TableRange(0, 100)]
        public int SpawnChance;

        public int Damage;
        public EnemyType EnemyType;

        [TableFlags] public EnemyCategory Categories;

        [ColumnHeader("Drop Tags")] [ListField()]
        public List<string> DropTags;

        public bool Enabled;

        public EnemyData()
        {
            Id = "new_entry";
            DisplayName = "New Enemy";
            WeaponId = "";
            UiColor = "#FFFFFFFF";
            IconGuid = "";
            Health = 100;
            SpawnChance = 50;
            Damage = 10;
            EnemyType = EnemyType.Normal;
            Categories = EnemyCategory.None;
            DropTags = new List<string>();
            Enabled = true;
        }

        string IGameData.Id
        {
            get => Id;
            set => Id = value;
        }
    }
}