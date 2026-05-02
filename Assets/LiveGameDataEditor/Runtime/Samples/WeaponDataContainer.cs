using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Concrete ScriptableObject container for <see cref="WeaponData" /> items.
    ///     Used by the enemy sample to demonstrate [TableReference].
    /// </summary>
    [GoogleSheetsTab("WeaponData")]
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Game Data Spreadsheet Editor/Weapon Data Container",
        order = 0)]
    public class WeaponDataContainer : GameDataContainerBase<WeaponData>
    {
    }
}