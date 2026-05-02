using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Example referenced table row for enemy weapon relationships.
    /// </summary>
    [GameData(DisplayName = "Weapon")]
    [Serializable]
    public class WeaponData : IGameData
    {
        [TableKey] public string Id;

        [TableDisplay] public string DisplayName;

        public int Damage;

        public WeaponData()
        {
            Id = "new_weapon";
            DisplayName = "New Weapon";
            Damage = 10;
        }

        string IGameData.Id
        {
            get => Id;
            set => Id = value;
        }
    }
}