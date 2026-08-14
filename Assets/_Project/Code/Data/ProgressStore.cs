using UnityEngine;

namespace Match3.Data
{
    /// <summary>
    /// Which levels the player has unlocked. Backed by PlayerPrefs, which is the one storage API
    /// that works unchanged in a WebGL build.
    /// <para>
    /// Deliberately never calls PlayerPrefs.Save(): WebGL's PlayerPrefs backend syncs through an
    /// IndexedDB bridge, and forcing that sync mid-session (e.g. right as a level finishes, which is
    /// the first time this class writes anything in a fresh session) has been observed to bring the
    /// whole runtime down with an opaque "RuntimeError: null function" trap. SetInt already updates
    /// the in-memory copy other calls in this class read from, and Unity flushes prefs to storage on
    /// its own on shutdown/visibility change, so the only real cost is not persisting progress across
    /// a hard-killed tab that never got a chance to shut down cleanly -- an acceptable trade for not
    /// crashing on every single level completion.
    /// </para>
    /// </summary>
    public sealed class ProgressStore
    {
        private const string UnlockedKey = "match3.progress.unlockedCount";
        private const string LastPlayedKey = "match3.progress.lastPlayed";

        private int _unlockedCount;
        private int _lastPlayedIndex;

        public ProgressStore()
        {
            _unlockedCount = Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));
            _lastPlayedIndex = Mathf.Max(0, PlayerPrefs.GetInt(LastPlayedKey, 0));
        }

        /// <summary>Number of levels available from the start of the list. Always at least one.</summary>
        public int UnlockedCount => _unlockedCount;

        public int LastPlayedIndex => _lastPlayedIndex;

        public bool IsUnlocked(int levelIndex) => levelIndex >= 0 && levelIndex < _unlockedCount;

        /// <summary>Unlocks everything up to and including <paramref name="levelIndex"/>.</summary>
        public void UnlockThrough(int levelIndex)
        {
            int required = levelIndex + 1;
            if (required <= _unlockedCount)
                return;

            _unlockedCount = required;
            PlayerPrefs.SetInt(UnlockedKey, _unlockedCount);
        }

        public void SetLastPlayed(int levelIndex)
        {
            if (levelIndex == _lastPlayedIndex)
                return;

            _lastPlayedIndex = Mathf.Max(0, levelIndex);
            PlayerPrefs.SetInt(LastPlayedKey, _lastPlayedIndex);
        }

        public void Reset()
        {
            _unlockedCount = 1;
            _lastPlayedIndex = 0;
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(LastPlayedKey);
        }
    }
}
