using System.Collections.Generic;
using System.Linq;
using Match3.Core;

namespace Match3.Tests
{
    /// <summary>
    /// Wires an exact ASCII board up to a real <see cref="TurnResolver"/>.
    /// <para>
    /// Refill is off by default: with an empty palette gravity never spawns, so a test sees only the
    /// consequences of what it set up instead of random new pieces cascading on top. Tests that care
    /// about refill turn it on explicitly.
    /// </para>
    /// </summary>
    public sealed class TestHarness
    {
        public Board Board { get; internal set; }
        public LevelRuntime Level { get; internal set; }
        public TurnResolver Resolver { get; internal set; }
        public Rng Rng { get; internal set; }
        public BoosterRegistry Boosters { get; internal set; }
        public BoosterCombinationRegistry Combinations { get; internal set; }

        public string Render() => TestBoard.Render(Board);

        public TurnResult Swap(int ax, int ay, int bx, int by) =>
            Resolver.Swap(new GridPos(ax, ay), new GridPos(bx, by));

        public TurnResult Tap(int x, int y) => Resolver.ActivateBoosterAt(new GridPos(x, y));

        public Piece PieceAt(int x, int y) => Board.PieceAt(new GridPos(x, y));

        public Obstacle ObstacleAt(int x, int y) => Board.ObstacleAt(new GridPos(x, y));

        /// <summary>Turns the piece at a cell into a booster, for setting up booster scenarios.</summary>
        public Piece MakeBooster(int x, int y, BoosterType type,
            LineOrientation orientation = LineOrientation.Horizontal, PieceColor? color = null)
        {
            Piece piece = PieceAt(x, y);
            if (piece == null)
                throw new System.InvalidOperationException($"No piece at ({x},{y}) to promote.");

            piece.Booster = type;
            piece.Orientation = orientation;

            if (color.HasValue)
                piece.Color = color.Value;
            else if (type == BoosterType.Rainbow)
                piece.Color = PieceColor.None;

            return piece;
        }

        /// <summary>Promotes every piece on the board, for chain-reaction stress scenarios.</summary>
        public void MakeEveryPieceABooster(BoosterType type)
        {
            foreach (GridPos pos in Board.Positions)
            {
                Piece piece = Board.PieceAt(pos);
                if (piece != null)
                    MakeBooster(pos.X, pos.Y, type);
            }
        }

        /// <summary>Places a board element directly, for cases the layout alphabet cannot express.</summary>
        public Obstacle PutObstacle(int x, int y, string configId, int hp = 0,
            PieceColor color = PieceColor.None, EntitySpec contains = null,
            int width = 0, int height = 0)
        {
            Obstacle obstacle = Board.SpawnObstacle(new GridPos(x, y),
                TestBoard.Catalog.Get(configId), hp, color, contains, width, height);
            obstacle.ColorPalette = Level.Palette.Count > 0 ? Level.Palette : TestBoard.DefaultPalette;
            Board.RecomputeSpawners();
            return obstacle;
        }

        /// <summary>Runs one booster effect in isolation and returns what it produced.</summary>
        public BoosterContext ResolveEffect(ActivationRequest request)
        {
            if (!Boosters.TryGet(request.Type, out IBoosterEffect effect))
                throw new System.InvalidOperationException($"No effect registered for {request.Type}.");

            var context = new BoosterContext(Board, Level, Rng, new HashSet<GridPos>());
            effect.Resolve(request, context);
            return context;
        }
    }

    public static class TestGame
    {
        public static LevelConfig Config(
            int width, int height,
            int moveLimit = 50,
            PieceColor goalColor = PieceColor.Red,
            int goalCount = 9999,
            IEnumerable<PieceColor> palette = null,
            IEnumerable<string> layout = null,
            int seed = 7)
        {
            var config = new LevelConfig
            {
                Id = "test",
                Name = "test level",
                Width = width,
                Height = height,
                MoveLimit = moveLimit,
                Seed = seed,
                Palette = (palette ?? TestBoard.DefaultPalette).ToList(),
                Goals = new List<LevelGoal> { new LevelGoal(goalColor, goalCount) },
            };

            if (layout != null)
                config.Layout = layout.ToList();

            return config;
        }

        /// <summary>
        /// Builds a harness around the exact board described by <paramref name="art"/>.
        /// </summary>
        public static TestHarness FromArt(
            string art,
            int seed = 7,
            int moveLimit = 50,
            PieceColor goalColor = PieceColor.Red,
            int goalCount = 9999,
            bool refill = false,
            IEnumerable<PieceColor> refillPalette = null)
        {
            var rng = new Rng(seed);
            Board board = TestBoard.Parse(art, rng);

            // The level always knows its real colour set — that is what the Rainbow picks from.
            // Refill is disabled separately, by handing the resolver an empty refill palette.
            List<PieceColor> palette = (refillPalette ?? TestBoard.DefaultPalette).ToList();
            IReadOnlyList<PieceColor> refillFrom = refill ? palette : new PieceColor[0];

            LevelConfig config = Config(board.Width, board.Height, moveLimit, goalColor, goalCount,
                palette, seed: seed);

            var level = new LevelRuntime(config);
            var boosters = BoosterRegistry.CreateDefault();
            var combinations = BoosterCombinationRegistry.CreateDefault();

            return new TestHarness
            {
                Board = board,
                Level = level,
                Rng = rng,
                Boosters = boosters,
                Combinations = combinations,
                Resolver = new TurnResolver(board, level, rng, TestBoard.Catalog,
                    new MatchDetector(), new GravityResolver(), boosters, combinations, refillFrom),
            };
        }

        /// <summary>Cells cleared during a turn, in the order the core reported them.</summary>
        public static List<GridPos> ClearedCells(TurnResult result) =>
            result.EventsOf<EntityClearedEvent>().Select(e => e.At).ToList();

        public static List<BoosterActivatedEvent> Activations(TurnResult result) =>
            result.EventsOf<BoosterActivatedEvent>().ToList();
    }
}
