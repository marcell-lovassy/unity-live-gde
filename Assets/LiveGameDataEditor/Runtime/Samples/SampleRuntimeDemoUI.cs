using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Minimal runtime reporter that logs resolved sample enemy rows without extra UI dependencies.
    /// </summary>
    public sealed class SampleRuntimeDemoUI : MonoBehaviour
    {
        [SerializeField] private SampleEnemy[] enemies;
        [SerializeField] private bool logOnStart = true;

        private void Start()
        {
            Refresh();
            if (logOnStart) LogResolvedData();
        }

        [ContextMenu("Refresh From Data Controllers")]
        public void Refresh()
        {
            if (enemies == null) return;

            foreach (var enemy in enemies)
                if (enemy != null)
                    enemy.ApplyData();
        }

        [ContextMenu("Log Resolved Data")]
        public void LogResolvedData()
        {
            Debug.Log(
                "[LiveGameDataEditor] Runtime sample: scene objects resolve ScriptableObject rows by ID through data controller components.");
            if (enemies == null)
            {
                Debug.Log("[LiveGameDataEditor] Runtime sample has no enemies assigned.");
                return;
            }

            foreach (var enemy in enemies)
                Debug.Log(enemy != null
                    ? $"[LiveGameDataEditor] {enemy.GetDisplaySummary()}"
                    : "[LiveGameDataEditor] Missing SampleEnemy reference");
        }
    }
}