using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Concrete ScriptableObject container for <see cref="EnemyDataEntry"/> items.
    /// Create via: Assets > Create > Live Game Data Editor > Enemy Data Container
    ///
    /// The <c>Entries</c> list and <see cref="IGameDataContainer"/> implementation are
    /// provided by <see cref="GameDataContainerBase{T}"/>.
    ///
    /// The <see cref="GoogleSheetsTabAttribute"/> tells the Google Sheets Sync feature
    /// which tab in the spreadsheet to push/pull data from.
    /// </summary>
    [GoogleSheetsTab("EnemyData")]
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Live Game Data Editor/Enemy Data Container", order = 0)]
    public class EnemyDataContainer : GameDataContainerBase<EnemyDataEntry>
    {
    }
}
