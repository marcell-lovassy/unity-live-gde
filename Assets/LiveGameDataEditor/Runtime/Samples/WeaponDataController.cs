using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Runtime lookup component for one weapon data set.
    /// </summary>
    public sealed class WeaponDataController : MonoBehaviour
    {
        private static readonly WeaponData[] EmptyEntries = new WeaponData[0];
        [SerializeField] private WeaponDataContainer data;

        private readonly Dictionary<string, WeaponData> entriesById = new();
        private bool indexBuilt;

        public WeaponDataContainer Data => data;

        public IReadOnlyList<WeaponData> Entries
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
            indexBuilt = false;
        }

        public bool TryGetEntryById(string id, out WeaponData entry)
        {
            EnsureIndex();
            return entriesById.TryGetValue(id ?? string.Empty, out entry);
        }

        public WeaponData GetEntryById(string id)
        {
            TryGetEntryById(id, out var entry);
            return entry;
        }

        public void RebuildIndex()
        {
            entriesById.Clear();
            indexBuilt = true;

            if (data == null || data.Entries == null) return;

            foreach (var entry in data.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;

                entriesById[entry.Id] = entry;
            }
        }

        private void EnsureIndex()
        {
            if (!indexBuilt) RebuildIndex();
        }
    }
}