using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// ScriptableObject that holds a list of GameDataEntry items.
    /// Create via: Assets > Create > Live Game Data Editor > Game Data Container
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewGameData",
        menuName = "Live Game Data Editor/Game Data Container",
        order = 0)]
    public class GameDataContainer : ScriptableObject
    {
        public List<GameDataEntry> Entries = new List<GameDataEntry>();
    }
}
