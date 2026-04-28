using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Runtime lookup component for one enemy data set.
    /// Drop this on a scene object and assign an EnemyDataContainer asset.
    /// </summary>
    public sealed class EnemyDataController : MonoBehaviour
    {
        [SerializeField] private EnemyDataContainer data;

        private readonly Dictionary<string, EnemyDataEntry> entriesById = new Dictionary<string, EnemyDataEntry>();
        private bool indexBuilt;

        public EnemyDataContainer Data => data;
        public IReadOnlyList<EnemyDataEntry> Entries
        {
            get
            {
                if (data == null || data.Entries == null)
                {
                    return EmptyEntries;
                }

                return data.Entries;
            }
        }

        private static readonly EnemyDataEntry[] EmptyEntries = new EnemyDataEntry[0];

        public bool TryGetEntryById(string id, out EnemyDataEntry entry)
        {
            EnsureIndex();
            return entriesById.TryGetValue(id ?? string.Empty, out entry);
        }

        public EnemyDataEntry GetEntryById(string id)
        {
            TryGetEntryById(id, out var entry);
            return entry;
        }

        public void RebuildIndex()
        {
            entriesById.Clear();
            indexBuilt = true;

            if (data == null || data.Entries == null)
            {
                return;
            }

            foreach (var entry in data.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id))
                {
                    continue;
                }

                entriesById[entry.Id] = entry;
            }
        }

        private void Awake()
        {
            RebuildIndex();
        }

        private void EnsureIndex()
        {
            if (!indexBuilt)
            {
                RebuildIndex();
            }
        }

        private void OnValidate()
        {
            indexBuilt = false;
        }
    }
}
