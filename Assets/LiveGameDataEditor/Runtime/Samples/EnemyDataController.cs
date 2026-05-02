using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Runtime lookup component for one enemy data set.
    ///     Drop this on a scene object and assign an EnemyDataContainer asset.
    /// </summary>
    public sealed class EnemyDataController : MonoBehaviour
    {
        private static readonly EnemyData[] EmptyEntries = new EnemyData[0];
        [SerializeField] private EnemyDataContainer data;

        private readonly Dictionary<string, EnemyData> _entriesById = new();
        private bool _indexBuilt;

        public EnemyDataContainer Data => data;

        public IReadOnlyList<EnemyData> Entries
        {
            get
            {
                if (data == null || data.Entries == null) return EmptyEntries;

                return data.Entries;
            }
        }

        private void Awake()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            _indexBuilt = false;
        }

        public bool TryGetEntryById(string id, out EnemyData entry)
        {
            EnsureIndex();
            return _entriesById.TryGetValue(id ?? string.Empty, out entry);
        }

        public EnemyData GetEntryById(string id)
        {
            TryGetEntryById(id, out var entry);
            return entry;
        }

        public void RebuildIndex()
        {
            _entriesById.Clear();
            _indexBuilt = true;

            if (data == null || data.Entries == null) return;

            foreach (var entry in data.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;

                _entriesById[entry.Id] = entry;
            }
        }

        private void EnsureIndex()
        {
            if (!_indexBuilt) RebuildIndex();
        }
    }
}