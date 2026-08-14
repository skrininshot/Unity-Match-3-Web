using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Registry of board-element templates, addressed by string id from level data.
    /// <para>
    /// Deliberately small: extra lives, larger footprints and nested contents are per-instance
    /// overrides on <see cref="EntitySpec"/>, not new catalog entries. Adding a genuinely new
    /// mechanic means registering one more config (and possibly one more damage rule) here.
    /// </para>
    /// </summary>
    public sealed class ObstacleCatalog
    {
        public const string Box = "box";
        public const string ColoredBox = "box_colored";
        public const string CyclingBox = "box_cycling";
        public const string Blocker = "blocker";

        private readonly Dictionary<string, ObstacleConfig> _configs = new Dictionary<string, ObstacleConfig>();

        public static ObstacleCatalog CreateDefault()
        {
            var catalog = new ObstacleCatalog();

            // Crates stay where the level puts them. Gravity-affected crates are supported by
            // ObstacleConfig.Falls, but a falling crate would slide to the bottom of the board on
            // the first turn, which makes a designed layout mean nothing.
            //
            // Ordinary crate: any adjacent match breaks it.
            catalog.Register(new ObstacleConfig(
                Box, new AnyMatchDamageRule()));

            // Coloured crate: needs an adjacent match of its own colour.
            catalog.Register(new ObstacleConfig(
                ColoredBox, new ColorMatchDamageRule(),
                defaultRequiredColor: PieceColor.Red));

            // Colour-changing crate: rerolls its required colour every turn.
            catalog.Register(new ObstacleConfig(
                CyclingBox, new CyclingColorDamageRule(),
                defaultRequiredColor: PieceColor.Red));

            // Blocker: indestructible and immovable; pieces slide around it.
            catalog.Register(new ObstacleConfig(
                Blocker, new IndestructibleDamageRule(), falls: false,
                damageableByBoosters: false));

            return catalog;
        }

        public void Register(ObstacleConfig config)
        {
            _configs[config.Id] = config;
        }

        public bool TryGet(string id, out ObstacleConfig config) => _configs.TryGetValue(id, out config);

        public ObstacleConfig Get(string id)
        {
            if (!_configs.TryGetValue(id, out ObstacleConfig config))
                throw new KeyNotFoundException($"Unknown obstacle id '{id}'. Registered: {string.Join(", ", _configs.Keys)}");
            return config;
        }

        public IEnumerable<ObstacleConfig> All => _configs.Values;
    }
}
