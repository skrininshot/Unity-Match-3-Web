using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// What one booster type does when it fires. Adding a booster means adding one of these and
    /// registering it — the turn resolver never learns about individual booster types.
    /// </summary>
    public interface IBoosterEffect
    {
        BoosterType Type { get; }
        void Resolve(ActivationRequest request, BoosterContext context);
    }

    /// <summary>Clears a full row or column. Thickness &gt; 1 widens it (used by Line + Bomb).</summary>
    public sealed class LineBoosterEffect : IBoosterEffect
    {
        public BoosterType Type => BoosterType.Line;

        public void Resolve(ActivationRequest request, BoosterContext context)
        {
            int thickness = request.Thickness > 0 ? request.Thickness : 1;
            int spread = (thickness - 1) / 2;
            Board board = context.Board;

            if (request.Orientation == LineOrientation.Horizontal)
            {
                for (int dy = -spread; dy <= spread; dy++)
                for (int x = 0; x < board.Width; x++)
                    context.AddCell(new GridPos(x, request.At.Y + dy));
            }
            else
            {
                for (int dx = -spread; dx <= spread; dx++)
                for (int y = 0; y < board.Height; y++)
                    context.AddCell(new GridPos(request.At.X + dx, y));
            }
        }
    }

    /// <summary>Clears a square area. The default radius of 2 gives the 5x5 the spec asks for.</summary>
    public sealed class BombBoosterEffect : IBoosterEffect
    {
        public const int DefaultRadius = 2;

        public BoosterType Type => BoosterType.Bomb;

        public void Resolve(ActivationRequest request, BoosterContext context)
        {
            int radius = request.Radius > 0 ? request.Radius : DefaultRadius;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                context.AddCell(new GridPos(request.At.X + dx, request.At.Y + dy));
        }
    }

    /// <summary>
    /// Clears every piece of one colour. When no colour was chosen for it (a tapped Rainbow or a
    /// chain reaction), it picks the colour that best serves the level goal, falling back to the
    /// most common colour on the board.
    /// </summary>
    public sealed class RainbowBoosterEffect : IBoosterEffect
    {
        public BoosterType Type => BoosterType.Rainbow;

        public void Resolve(ActivationRequest request, BoosterContext context)
        {
            Board board = context.Board;

            if (request.EntireBoard)
            {
                context.ChosenColor = PieceColor.None;
                foreach (GridPos pos in board.Positions)
                    context.AddCell(pos);
                return;
            }

            PieceColor color = request.TargetColor != PieceColor.None
                ? request.TargetColor
                : ChooseColor(context);

            context.ChosenColor = color;
            if (color == PieceColor.None)
                return;

            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece != null && piece.Color == color)
                    context.AddCell(pos);
            }
        }

        private static PieceColor ChooseColor(BoosterContext context)
        {
            var counts = new Dictionary<PieceColor, int>();
            foreach (GridPos pos in context.Board.Positions)
            {
                Piece piece = context.Board.PieceAt(pos);
                if (piece == null || piece.Color == PieceColor.None)
                    continue;

                counts.TryGetValue(piece.Color, out int current);
                counts[piece.Color] = current + 1;
            }

            PieceColor best = PieceColor.None;
            int bestScore = -1;

            // Deterministic scan order: palette order, not dictionary order.
            foreach (PieceColor color in context.Level.Palette)
            {
                if (!counts.TryGetValue(color, out int count) || count == 0)
                    continue;

                // Colours the goal still needs are worth far more than sheer quantity.
                int score = count + (context.Level.Goals.IsWantedColor(color) ? 1000 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = color;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Flies to the single board cell that best advances the current goal and destroys it.
    /// If it carries a payload (Plane + another booster), that booster detonates at the target.
    /// </summary>
    public sealed class PlaneBoosterEffect : IBoosterEffect
    {
        public BoosterType Type => BoosterType.Plane;

        public void Resolve(ActivationRequest request, BoosterContext context)
        {
            GridPos? target = ChooseTarget(request, context);
            if (target == null)
                return;

            GridPos cell = target.Value;
            context.ReservedTargets.Add(cell);
            context.FlyTarget = cell;
            context.AddCell(cell);

            if (request.Payload != BoosterType.None)
                context.FollowUps.Add(BuildPayload(request.Payload, cell, context));
        }

        private static ActivationRequest BuildPayload(BoosterType payload, GridPos at, BoosterContext context)
        {
            switch (payload)
            {
                case BoosterType.Line:
                    LineOrientation orientation = context.Rng.Chance(0.5)
                        ? LineOrientation.Horizontal
                        : LineOrientation.Vertical;
                    return ActivationRequest.Line(at, orientation);
                case BoosterType.Bomb:
                    return ActivationRequest.Bomb(at);
                case BoosterType.Rainbow:
                    return ActivationRequest.Rainbow(at, PieceColor.None);
                case BoosterType.Plane:
                    return ActivationRequest.Plane(at);
                default:
                    return ActivationRequest.Bomb(at);
            }
        }

        /// <summary>
        /// Scores every cell by how much destroying it helps, and picks the best.
        /// Ties are broken with the seeded rng so repeated planes feel varied but stay reproducible.
        /// </summary>
        private static GridPos? ChooseTarget(ActivationRequest request, BoosterContext context)
        {
            Board board = context.Board;
            var best = new List<GridPos>();
            int bestScore = 0;

            foreach (GridPos pos in board.Positions)
            {
                if (context.ReservedTargets.Contains(pos) || pos == request.At)
                    continue;

                int score = ScoreCell(board, context.Level, pos);
                if (score <= 0)
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(pos);
                }
                else if (score == bestScore)
                {
                    best.Add(pos);
                }
            }

            if (best.Count == 0)
                return null;

            return context.Rng.Pick(best);
        }

        private static int ScoreCell(Board board, LevelRuntime level, GridPos pos)
        {
            BoardEntity entity = board.EntityAt(pos);
            if (entity == null)
                return 0;

            if (entity is Obstacle obstacle)
            {
                if (obstacle.IsIndestructible || !obstacle.Config.DamageableByBoosters)
                    return 0;

                // Obstacles usually gate progress, so they are worth more than a loose gem.
                int score = 300;
                if (obstacle.RequiredColor != PieceColor.None && level.Goals.IsWantedColor(obstacle.RequiredColor))
                    score += 100;
                return score;
            }

            if (!(entity is Piece piece))
                return 0;

            // Hitting another booster is valuable: it chains.
            if (piece.IsBooster)
                return 400;

            if (level.Goals.IsWantedColor(piece.Color))
            {
                // Prefer a goal-coloured piece sitting next to more of its own colour, since the
                // resulting collapse is more likely to cascade.
                int neighbours = 0;
                foreach (GridPos offset in GridPos.Orthogonal)
                {
                    Piece other = board.PieceAt(pos + offset);
                    if (other != null && other.Color == piece.Color)
                        neighbours++;
                }

                return 500 + neighbours * 10;
            }

            return 1;
        }
    }

    /// <summary>Maps booster types to their effects.</summary>
    public sealed class BoosterRegistry
    {
        private readonly Dictionary<BoosterType, IBoosterEffect> _effects =
            new Dictionary<BoosterType, IBoosterEffect>();

        public static BoosterRegistry CreateDefault()
        {
            var registry = new BoosterRegistry();
            registry.Register(new LineBoosterEffect());
            registry.Register(new BombBoosterEffect());
            registry.Register(new RainbowBoosterEffect());
            registry.Register(new PlaneBoosterEffect());
            return registry;
        }

        public void Register(IBoosterEffect effect) => _effects[effect.Type] = effect;

        public bool TryGet(BoosterType type, out IBoosterEffect effect) => _effects.TryGetValue(type, out effect);
    }
}
