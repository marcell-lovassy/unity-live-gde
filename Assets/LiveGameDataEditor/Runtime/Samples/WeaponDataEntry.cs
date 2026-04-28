using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Example referenced table row for enemy weapon relationships.
    /// </summary>
    [GameData(DisplayName = "Weapon")]
    [Serializable]
    public class WeaponDataEntry : IGameDataEntry
    {
        string IGameDataEntry.Id { get => Id; set => Id = value; }

        [TableKey]
        public string Id;

        [TableDisplay]
        public string DisplayName;

        public int Damage;

        public WeaponDataEntry()
        {
            Id = "new_weapon";
            DisplayName = "New Weapon";
            Damage = 10;
        }
    }
}
