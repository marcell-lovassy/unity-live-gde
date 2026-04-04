using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Concrete ScriptableObject container for <see cref="GameDataEntry"/> items.
    /// Create via: Assets > Create > Live Game Data Editor > Game Data Container
    ///
    /// The <c>Entries</c> list and <see cref="IGameDataContainer"/> implementation are
    /// provided by <see cref="GameDataContainerBase{T}"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewEnemyData",
        menuName = "Live Game Data Editor/Enemy Data Container",
        order = 0)]
    public class EnemyDataContainer : GameDataContainerBase<EnemyDataEntry>
    {
    }
}
