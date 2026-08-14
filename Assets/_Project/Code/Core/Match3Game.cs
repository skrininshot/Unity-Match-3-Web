using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Façade over one level in progress: owns the board, the level state and the resolver, and can
    /// swap in a different level without anything above it being torn down. That is what makes
    /// "replace or reload a level without restarting the game" a one-line operation for the app layer.
    /// </summary>
    public sealed class Match3Game
    {
        private readonly ObstacleCatalog _catalog;
        private readonly BoosterRegistry _boosters;
        private readonly BoosterCombinationRegistry _combinations;
        private readonly MatchDetector _detector = new MatchDetector();

        private int _lastSeed;

        public Match3Game(
            ObstacleCatalog catalog = null,
            BoosterRegistry boosters = null,
            BoosterCombinationRegistry combinations = null)
        {
            _catalog = catalog ?? ObstacleCatalog.CreateDefault();
            _boosters = boosters ?? BoosterRegistry.CreateDefault();
            _combinations = combinations ?? BoosterCombinationRegistry.CreateDefault();
        }

        public Board Board { get; private set; }
        public LevelRuntime Level { get; private set; }
        public TurnResolver Resolver { get; private set; }
        public LevelConfig Config { get; private set; }
        public ObstacleCatalog Catalog => _catalog;

        public bool IsLoaded => Board != null;

        /// <summary>
        /// Builds a fresh board for <paramref name="config"/>.
        /// A seed of 0 in the level data means "vary between attempts"; pass
        /// <paramref name="seedOverride"/> to reproduce an exact board.
        /// </summary>
        public void Load(LevelConfig config, int? seedOverride = null)
        {
            Config = config;
            _lastSeed = seedOverride ?? (config.Seed != 0 ? config.Seed : NextArbitrarySeed());

            var rng = new Rng(_lastSeed);
            Level = new LevelRuntime(config);
            Board = BoardBuilder.Build(config, _catalog, rng);
            Resolver = new TurnResolver(Board, Level, rng, _catalog, _detector,
                new GravityResolver(), _boosters, _combinations);
        }

        /// <summary>Restarts the current level. Uses a new board unless the level pins a seed.</summary>
        public void Reload()
        {
            if (Config == null)
                return;

            Load(Config, Config.Seed != 0 ? Config.Seed : (int?)null);
        }

        /// <summary>Replays the exact same board the last <see cref="Load"/> produced.</summary>
        public void ReloadSameBoard()
        {
            if (Config != null)
                Load(Config, _lastSeed);
        }

        public TurnResult Swap(GridPos a, GridPos b) => Resolver.Swap(a, b);

        public TurnResult ActivateBooster(GridPos pos) => Resolver.ActivateBoosterAt(pos);

        /// <summary>Legal actions available right now. Used for hints and dead-end checks.</summary>
        public List<BoardMove> AvailableMoves() => MoveFinder.FindAll(Board, _detector);

        /// <summary>
        /// Snapshot of everything on the board, for building the initial view of a level.
        /// Ordered bottom-up so the view can rely on a stable creation order.
        /// </summary>
        public List<EntitySnapshot> SnapshotEntities()
        {
            var snapshots = new List<EntitySnapshot>();
            foreach (BoardEntity entity in Board.EntitiesBottomUp())
                snapshots.Add(EntitySnapshot.Of(entity));
            return snapshots;
        }

        /// <summary>Seed source for unpinned levels. Deliberately not the engine's RNG.</summary>
        private static int NextArbitrarySeed()
        {
            unchecked
            {
                _seedCounter = _seedCounter * 1103515245 + 12345;
                int seed = (int)((_seedCounter >> 16) & 0x7FFFFFFF);
                return seed == 0 ? 1 : seed;
            }
        }

        private static long _seedCounter = System.DateTime.UtcNow.Ticks;
    }
}
