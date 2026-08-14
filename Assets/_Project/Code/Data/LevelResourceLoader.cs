using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Data
{
    /// <summary>
    /// Loads level definitions from JSON text assets under <c>Resources/Levels</c>.
    /// <para>
    /// Levels are plain files rather than serialized objects, which is what makes the two
    /// infrastructure requirements cheap: dropping in another file adds a level, and
    /// <see cref="LoadFromText"/> can install or replace one at runtime from any source at all.
    /// </para>
    /// </summary>
    public static class LevelResourceLoader
    {
        public const string DefaultFolder = "Levels";

        public static LevelLibrary Load(string folder = DefaultFolder)
        {
            var library = new LevelLibrary();
            LoadInto(library, folder);
            return library;
        }

        /// <summary>
        /// Adds every level found in <paramref name="folder"/> to <paramref name="library"/>,
        /// replacing any level that already has the same id. Returns how many were loaded.
        /// </summary>
        public static int LoadInto(LevelLibrary library, string folder = DefaultFolder)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(folder);
            int loaded = 0;

            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                    continue;

                // One malformed file must not cost the player the whole game.
                try
                {
                    LevelConfig config = LevelJson.Parse(asset.text);

                    if (string.IsNullOrEmpty(config.Id))
                        config.Id = asset.name;

                    List<string> problems = config.Validate();
                    if (problems.Count > 0)
                    {
                        Debug.LogError($"Level '{asset.name}' is invalid and was skipped: "
                                       + string.Join("; ", problems));
                        continue;
                    }

                    library.AddOrReplace(config);
                    loaded++;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"Level '{asset.name}' could not be parsed and was skipped: {exception.Message}");
                }
            }

            library.SortById();
            return loaded;
        }

        /// <summary>
        /// Installs a level from raw JSON, replacing an existing level with the same id.
        /// This is the entry point for side-loading extra levels while the game is running.
        /// </summary>
        public static LevelConfig LoadFromText(LevelLibrary library, string json)
        {
            LevelConfig config = LevelJson.Parse(json);

            List<string> problems = config.Validate();
            if (problems.Count > 0)
                throw new Core.Json.JsonException(
                    $"Level '{config.Id}' is invalid: {string.Join("; ", problems)}");

            library.AddOrReplace(config);
            return config;
        }
    }
}
