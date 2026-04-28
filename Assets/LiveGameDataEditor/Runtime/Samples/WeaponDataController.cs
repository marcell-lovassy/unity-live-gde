using System.Collections.Generic;
using UnityEngine;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Runtime lookup component for one weapon data set.
    /// </summary>
    public sealed class WeaponDataController : MonoBehaviour
    {
        [SerializeField] private WeaponDataContainer data;

        private readonly Dictionary<string, WeaponDataEntry> entriesById = new Dictionary<string, WeaponDataEntry>();
        private bool indexBuilt;

        public WeaponDataContainer Data => data;
        public IReadOnlyList<WeaponDataEntry> Entries
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

        private static readonly WeaponDataEntry[] EmptyEntries = new WeaponDataEntry[0];

        public bool TryGetEntryById(string id, out WeaponDataEntry entry)
        {
            EnsureIndex();
            return entriesById.TryGetValue(id ?? string.Empty, out entry);
        }

        public WeaponDataEntry GetEntryById(string id)
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
