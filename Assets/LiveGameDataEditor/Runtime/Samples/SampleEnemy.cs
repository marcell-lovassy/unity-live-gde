using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Small runtime sample that resolves an enemy row by ID and reads related weapon data.
    /// </summary>
    public sealed class SampleEnemy : MonoBehaviour
    {
        [SerializeField] private string enemyId;
        [SerializeField] private EnemyDataController enemyDataController;
        [SerializeField] private WeaponDataController weaponDataController;
        [SerializeField] private Renderer targetRenderer;

        public string EnemyId => enemyId;
        public EnemyDataEntry EnemyData { get; private set; }
        public WeaponDataEntry WeaponData { get; private set; }

        private void Start()
        {
            ApplyData();
        }

        public void ApplyData()
        {
            EnemyData = enemyDataController != null
                ? enemyDataController.GetEntryById(enemyId)
                : null;

            WeaponData = EnemyData != null && weaponDataController != null
                ? weaponDataController.GetEntryById(EnemyData.WeaponId)
                : null;

            ApplyColor();
        }

        public string GetDisplaySummary()
        {
            if (EnemyData == null)
            {
                return $"{enemyId}: missing enemy data";
            }

            var weaponLabel = WeaponData != null
                ? $"{WeaponData.DisplayName} ({WeaponData.Damage})"
                : $"missing weapon '{EnemyData.WeaponId}'";

            return $"{EnemyData.DisplayName} | HP {EnemyData.Health} | Damage {EnemyData.Damage} | Weapon {weaponLabel} | Spawn {EnemyData.SpawnChance}%";
        }

        private void ApplyColor()
        {
            if (targetRenderer == null || EnemyData == null)
            {
                return;
            }

            if (ColorUtility.TryParseHtmlString(EnemyData.UiColor, out var color))
            {
                targetRenderer.material.color = color;
            }
        }
    }
}
