using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Concrete ScriptableObject container for <see cref="GameData" /> items.
    ///     Create via: Assets > Create > Game Data Spreadsheet Editor > Game Data Container
    ///     The <c>Entries</c> list and <see cref="IGameDataContainer" /> implementation are
    ///     provided by <see cref="GameDataContainerBase{T}" />.
    /// </summary>
    [GoogleSheetsTab("GameData")]
    [CreateAssetMenu(fileName = "NewGameData", menuName = "Game Data Spreadsheet Editor/Game Data Container",
        order = 0)]
    public class GameDataContainer : GameDataContainerBase<GameData>
    {
    }
}