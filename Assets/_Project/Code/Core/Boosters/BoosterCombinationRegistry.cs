using System.Collections.Generic;

namespace Match3.Core
{
    public sealed class CombinationInput
    {
        public CombinationInput(Piece first, Piece second, GridPos at, Board board, LevelRuntime level, Rng rng)
        {
            First = first;
            Second = second;
            At = at;
            Board = board;
            Level = level;
            Rng = rng;
        }

        /// <summary>The piece whose booster type has the lower enum value, so factories are unambiguous.</summary>
        public Piece First { get; }

        public Piece Second { get; }

        /// <summary>Cell the combination is centred on — where the player dropped the second booster.</summary>
        public GridPos At { get; }

        public Board Board { get; }
        public LevelRuntime Level { get; }
        public Rng Rng { get; }
    }

    public delegate void CombinationFactory(CombinationInput input, List<ActivationRequest> output);

    /// <summary>
    /// Maps an unordered pair of booster types to the activations their combination produces.
    /// A new booster type only needs its pairings registered here; nothing else changes.
    /// </summary>
    public sealed class BoosterCombinationRegistry
    {
        private const int TypeStride = 16;

        private readonly Dictionary<int, CombinationFactory> _factories = new Dictionary<int, CombinationFactory>();

        public static BoosterCombinationRegistry CreateDefault()
        {
            var registry = new BoosterCombinationRegistry();

            // Line + Line: a full cross through the swap point.
            registry.Register(BoosterType.Line, BoosterType.Line, (input, output) =>
            {
                output.Add(ActivationRequest.Line(input.At, LineOrientation.Horizontal));
                output.Add(ActivationRequest.Line(input.At, LineOrientation.Vertical));
            });

            // Line + Bomb: a cross three cells thick.
            registry.Register(BoosterType.Line, BoosterType.Bomb, (input, output) =>
            {
                output.Add(ActivationRequest.Line(input.At, LineOrientation.Horizontal, thickness: 3));
                output.Add(ActivationRequest.Line(input.At, LineOrientation.Vertical, thickness: 3));
            });

            // Line + Rainbow: every piece of the line's colour turns into a line and fires.
            registry.Register(BoosterType.Line, BoosterType.Rainbow, (input, output) =>
            {
                PieceColor color = ColorOf(input.First, input.Second, input.Level);
                bool horizontal = true;
                foreach (GridPos pos in CellsOfColor(input.Board, color))
                {
                    // Alternate orientation so the result reads as a burst, not a stack of rows.
                    output.Add(ActivationRequest.Line(pos,
                        horizontal ? LineOrientation.Horizontal : LineOrientation.Vertical));
                    horizontal = !horizontal;
                }
            });

            // Line + Plane: the plane carries the line to the most useful cell.
            registry.Register(BoosterType.Line, BoosterType.Plane, (input, output) =>
                output.Add(ActivationRequest.Plane(input.At, BoosterType.Line)));

            // Bomb + Bomb: one much larger blast.
            registry.Register(BoosterType.Bomb, BoosterType.Bomb, (input, output) =>
                output.Add(ActivationRequest.Bomb(input.At, radius: 4)));

            // Bomb + Rainbow: every piece of the bomb's colour detonates a small bomb.
            // Radius 1 rather than the usual 2, otherwise this single combination reliably
            // erases the whole board and makes every other pairing pointless.
            registry.Register(BoosterType.Bomb, BoosterType.Rainbow, (input, output) =>
            {
                PieceColor color = ColorOf(input.First, input.Second, input.Level);
                foreach (GridPos pos in CellsOfColor(input.Board, color))
                    output.Add(ActivationRequest.Bomb(pos, radius: 1));
            });

            // Bomb + Plane: the plane delivers the bomb.
            registry.Register(BoosterType.Bomb, BoosterType.Plane, (input, output) =>
                output.Add(ActivationRequest.Plane(input.At, BoosterType.Bomb)));

            // Rainbow + Rainbow: clears the entire board.
            registry.Register(BoosterType.Rainbow, BoosterType.Rainbow, (input, output) =>
                output.Add(ActivationRequest.Rainbow(input.At, PieceColor.None, entireBoard: true)));

            // Rainbow + Plane: the plane delivers a colour wipe.
            registry.Register(BoosterType.Rainbow, BoosterType.Plane, (input, output) =>
                output.Add(ActivationRequest.Plane(input.At, BoosterType.Rainbow)));

            // Plane + Plane: two planes, and the reserved-target set keeps them apart.
            registry.Register(BoosterType.Plane, BoosterType.Plane, (input, output) =>
            {
                output.Add(ActivationRequest.Plane(input.At));
                output.Add(ActivationRequest.Plane(input.At));
            });

            return registry;
        }

        public void Register(BoosterType a, BoosterType b, CombinationFactory factory)
        {
            _factories[Key(a, b)] = factory;
        }

        public bool IsRegistered(BoosterType a, BoosterType b) => _factories.ContainsKey(Key(a, b));

        /// <summary>
        /// Produces the activations for swapping two booster pieces.
        /// Returns false when the pair has no registered combination, letting the caller fall back
        /// to firing both boosters independently.
        /// </summary>
        public bool TryResolve(Piece x, Piece y, GridPos at, Board board, LevelRuntime level, Rng rng,
            List<ActivationRequest> output)
        {
            if (!_factories.TryGetValue(Key(x.Booster, y.Booster), out CombinationFactory factory))
                return false;

            // Order the pair so a factory always knows which argument is which.
            bool xFirst = (int)x.Booster <= (int)y.Booster;
            var input = new CombinationInput(
                xFirst ? x : y,
                xFirst ? y : x,
                at, board, level, rng);

            factory(input, output);
            return true;
        }

        private static int Key(BoosterType a, BoosterType b)
        {
            int lo = (int)a;
            int hi = (int)b;
            if (lo > hi)
                (lo, hi) = (hi, lo);
            return lo * TypeStride + hi;
        }

        /// <summary>
        /// Colour for the "turn every piece of a colour into a booster" combinations: the colour of
        /// the non-Rainbow partner, since the Rainbow itself is colourless.
        /// </summary>
        private static PieceColor ColorOf(Piece first, Piece second, LevelRuntime level)
        {
            if (first.Color != PieceColor.None)
                return first.Color;
            if (second.Color != PieceColor.None)
                return second.Color;
            return level.Palette.Count > 0 ? level.Palette[0] : PieceColor.None;
        }

        private static List<GridPos> CellsOfColor(Board board, PieceColor color)
        {
            var cells = new List<GridPos>();
            if (color == PieceColor.None)
                return cells;

            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece != null && piece.Color == color)
                    cells.Add(pos);
            }

            return cells;
        }
    }
}
