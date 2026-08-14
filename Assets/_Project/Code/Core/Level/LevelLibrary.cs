using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The ordered set of levels the game knows about.
    /// <para>
    /// Mutable on purpose: <see cref="AddOrReplace"/> is what lets extra levels be loaded, or an
    /// existing one be swapped for an edited version, while the game is running — no scene reload
    /// and no rebuild.
    /// </para>
    /// </summary>
    public sealed class LevelLibrary
    {
        private readonly List<LevelConfig> _levels = new List<LevelConfig>();

        public IReadOnlyList<LevelConfig> Levels => _levels;

        public int Count => _levels.Count;

        public LevelConfig this[int index] =>
            index >= 0 && index < _levels.Count ? _levels[index] : null;

        /// <summary>Appends a level, or replaces the existing one with the same id in place.</summary>
        public void AddOrReplace(LevelConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            int index = IndexOf(config.Id);
            if (index >= 0)
                _levels[index] = config;
            else
                _levels.Add(config);
        }

        public bool Remove(string id)
        {
            int index = IndexOf(id);
            if (index < 0)
                return false;

            _levels.RemoveAt(index);
            return true;
        }

        public void Clear() => _levels.Clear();

        public int IndexOf(string id)
        {
            for (int i = 0; i < _levels.Count; i++)
                if (string.Equals(_levels[i].Id, id, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public LevelConfig ById(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? _levels[index] : null;
        }

        /// <summary>The level after <paramref name="id"/>, or null if it was the last one.</summary>
        public LevelConfig After(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? this[index + 1] : null;
        }

        /// <summary>Sorts by id, which is how file-name-ordered level sets keep their intended order.</summary>
        public void SortById() =>
            _levels.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Problems across the whole set: bad levels and duplicate ids.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (LevelConfig level in _levels)
            {
                if (!seen.Add(level.Id))
                    problems.Add($"duplicate level id '{level.Id}'");

                foreach (string problem in level.Validate())
                    problems.Add($"{level.Id}: {problem}");
            }

            return problems;
        }
    }
}
