using System;
using System.Collections.Generic;
using UnityEngine;

namespace Majulizi.Animalese
{
    [CreateAssetMenu(fileName = "PhonemeMap", menuName = "Animalese/PhonemeMap")]
    public class PhonemeMapSO : ScriptableObject
    {
        [Serializable]
        public class Phone
        {
            public string id;
            public AudioClip clip;
        }

        [Tooltip("List of English and base phoneme mappings.")]
        public Phone[] entries_english = Array.Empty<Phone>();

        [NonSerialized]
        private Dictionary<string, AudioClip> _clipLookup;

        [NonSerialized]
        private string[] _validPhonemeIds;

        private void OnEnable()
        {
            RebuildLookup();
        }

        /// <summary>
        /// Rebuilds the internal dictionary lookup for fast O(1) retrieval.
        /// </summary>
        public void RebuildLookup()
        {
            if (_clipLookup == null)
            {
                _clipLookup = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _clipLookup.Clear();
            }

            var validList = new List<string>();

            if (entries_english != null)
            {
                foreach (var entry in entries_english)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.clip != null)
                    {
                        var key = entry.id.Trim();
                        _clipLookup[key] = entry.clip;
                        validList.Add(key);
                    }
                }
            }

            _validPhonemeIds = validList.ToArray();
        }

        private void EnsureLookup()
        {
            if (_clipLookup == null || _validPhonemeIds == null || _clipLookup.Count == 0)
            {
                RebuildLookup();
            }
        }

        /// <summary>
        /// Attempts to get the AudioClip for the specified phoneme ID.
        /// </summary>
        public bool TryGetClip(string id, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            EnsureLookup();
            return _clipLookup.TryGetValue(id, out clip);
        }

        /// <summary>
        /// Gets the number of currently valid phoneme IDs available.
        /// </summary>
        public int AvailableCount
        {
            get
            {
                EnsureLookup();
                return _validPhonemeIds != null ? _validPhonemeIds.Length : 0;
            }
        }

        /// <summary>
        /// Fallback retrieval using hash/modulo for non-English Unicode characters to produce a deterministic phoneme.
        /// </summary>
        public bool GetFallbackClip(int hash, out string phonemeId, out AudioClip clip)
        {
            clip = null;
            phonemeId = string.Empty;

            EnsureLookup();
            if (_validPhonemeIds == null || _validPhonemeIds.Length == 0)
            {
                return false;
            }

            // Handle negative hash
            int index = Math.Abs(hash) % _validPhonemeIds.Length;
            phonemeId = _validPhonemeIds[index];
            return _clipLookup.TryGetValue(phonemeId, out clip);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Populate Eileen Phonemes From Samples")]
        public void AutoPopulateFromSamples()
        {
            const string samplePath = "Packages/com.majulizi.animalese/Samples/Example/eileen2";
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { samplePath });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning($"[PhonemeMapSO] No AudioClip assets found at {samplePath}.");
                return;
            }

            var list = new List<Phone>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    string id = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    list.Add(new Phone { id = id, clip = clip });
                }
            }

            entries_english = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            RebuildLookup();
            Debug.Log($"[PhonemeMapSO] Successfully populated {entries_english.Length} phonemes from Samples!");
        }
#endif
    }
}