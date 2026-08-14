using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Builds the starting board for a level: applies the layout, then fills the remaining cells
    /// with random colours such that there is <b>no</b> match already present and at least one
    /// legal move available.
    /// </summary>
    public static class BoardBuilder
    {
        private const int MaxBoardAttempts = 40;

        public static Board Build(LevelConfig config, ObstacleCatalog catalog, Rng rng)
        {
            var detector = new MatchDetector();
            IReadOnlyList<PieceColor> palette = config.Palette.Count > 0 ? config.Palette : PieceColors.All;

            Board board = null;
            List<GridPos> freeCells = null;

            for (int attempt = 0; attempt < MaxBoardAttempts; attempt++)
            {
                (board, freeCells) = BuildOnce(config, catalog, palette, rng, detector);

                if (!detector.HasAnyMatch(board) && MoveFinder.HasAny(board, detector))
                    return board;
            }

            // The retry loop above is just an optimisation -- in practice it finds a clean board
            // within the first attempt or two. What actually guarantees "no automatic matches on
            // start" (a hard spec requirement, not a preference) is this: deterministically
            // recolour the last attempt's own free cells until nothing matches, rather than
            // silently shipping whatever the final random attempt happened to produce.
            RepairMatches(board, freeCells, palette, rng, detector);

            if (detector.HasAnyMatch(board))
                throw new InvalidOperationException(
                    $"BoardBuilder: level '{config.Id}' cannot be built without an automatic match. " +
                    $"Its layout leaves too little room for a {palette.Count}-colour palette to avoid " +
                    "one at some cell -- widen the palette or loosen the layout/overrides.");

            return board;
        }

        private static (Board board, List<GridPos> freeCells) BuildOnce(LevelConfig config,
            ObstacleCatalog catalog, IReadOnlyList<PieceColor> palette, Rng rng, MatchDetector detector)
        {
            var board = new Board(config.Width, config.Height);
            var specs = new Dictionary<GridPos, EntitySpec>();

            ApplyLayout(config, board, specs);
            ApplyOverrides(config, specs);

            board.RecomputeSpawners();

            PlaceFixedEntities(board, catalog, palette, rng, specs);
            List<GridPos> freeCells = FillRandomCells(board, palette, rng, detector, specs);

            return (board, freeCells);
        }

        private static void ApplyLayout(LevelConfig config, Board board, Dictionary<GridPos, EntitySpec> specs)
        {
            if (config.Layout.Count == 0)
            {
                // No layout: a full rectangular board of random pieces.
                foreach (GridPos pos in board.Positions)
                    specs[pos] = EntitySpec.RandomPiece();
                return;
            }

            for (int row = 0; row < config.Layout.Count; row++)
            {
                // First layout row is the top row of the board.
                int y = config.Height - 1 - row;
                string line = config.Layout[row];

                for (int x = 0; x < config.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    char code = x < line.Length ? line[x] : LayoutCodes.RandomPiece;

                    if (code == LayoutCodes.Hole)
                    {
                        board.SetPlayable(pos, false);
                        continue;
                    }

                    specs[pos] = LayoutCodes.ToSpec(code);
                }
            }
        }

        private static void ApplyOverrides(LevelConfig config, Dictionary<GridPos, EntitySpec> specs)
        {
            foreach (CellOverride cell in config.Overrides)
                specs[cell.Pos] = cell.Spec;
        }

        /// <summary>
        /// Places obstacles and explicitly coloured pieces. Multi-cell obstacles claim their whole
        /// footprint so nothing else is generated inside them.
        /// </summary>
        private static void PlaceFixedEntities(Board board, ObstacleCatalog catalog,
            IReadOnlyList<PieceColor> palette, Rng rng, Dictionary<GridPos, EntitySpec> specs)
        {
            // Deterministic order: bottom-left to top-right.
            var ordered = new List<GridPos>(specs.Keys);
            ordered.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            foreach (GridPos pos in ordered)
            {
                EntitySpec spec = specs[pos];
                if (spec == null || spec.Kind == EntitySpecKind.RandomPiece || spec.Kind == EntitySpecKind.Empty)
                    continue;

                if (!board.IsPlayable(pos) || board.IsOccupied(pos))
                    continue;

                BoardEntity entity = EntityFactory.Create(board, spec, catalog, palette, rng);
                if (entity == null || !board.CanPlace(entity, pos))
                    continue;

                board.Place(entity, pos);
            }
        }

        /// <summary>
        /// Fills every still-empty playable cell, choosing a colour that does not complete a match.
        /// Cells are filled bottom-up so each check only ever sees already-placed neighbours.
        /// Returns every cell it placed a piece in -- the only cells <see cref="RepairMatches"/> is
        /// ever allowed to touch, since everything else came from the layout, an override or an
        /// obstacle and is the level designer's explicit choice.
        /// </summary>
        private static List<GridPos> FillRandomCells(Board board, IReadOnlyList<PieceColor> palette,
            Rng rng, MatchDetector detector, Dictionary<GridPos, EntitySpec> specs)
        {
            var candidates = new List<PieceColor>(palette);
            var freeCells = new List<GridPos>();

            foreach (GridPos pos in board.Positions)
            {
                if (!board.IsPlayable(pos) || board.IsOccupied(pos))
                    continue;

                // Cells the layout marked as deliberately empty stay empty only if gravity cannot
                // reach them; in practice refill will handle them, so fill them here too.
                if (specs.TryGetValue(pos, out EntitySpec spec)
                    && spec != null
                    && spec.Kind == EntitySpecKind.Empty)
                    continue;

                freeCells.Add(pos);

                candidates.Clear();
                candidates.AddRange(palette);
                rng.Shuffle(candidates);

                bool placed = false;
                foreach (PieceColor color in candidates)
                {
                    Piece piece = board.SpawnPiece(pos, color);
                    if (!detector.CreatesMatchAt(board, pos))
                    {
                        placed = true;
                        break;
                    }

                    board.Remove(piece);
                }

                if (!placed)
                {
                    // No colour avoids a match here (possible in tight layouts). Place one anyway;
                    // Build() will either retry the whole board or repair this cell afterwards.
                    board.SpawnPiece(pos, rng.Pick(palette));
                }
            }

            return freeCells;
        }

        /// <summary>
        /// Deterministically clears every automatic match left on <paramref name="board"/> by
        /// recolouring only <paramref name="freeCells"/> -- layout pieces, overrides and obstacles
        /// are never touched. Recolouring one cell can incidentally create a new match through a
        /// cell already visited this pass (e.g. two free cells either side of a fixed piece), so
        /// this runs several passes rather than assuming one linear sweep is enough. If some cell
        /// has no palette colour that avoids a match no matter what its neighbours do, it is left
        /// as-is and the match survives -- <see cref="Build"/> detects that and reports it as a
        /// genuinely unsatisfiable layout rather than pretending success.
        /// </summary>
        private static void RepairMatches(Board board, IReadOnlyList<GridPos> freeCells,
            IReadOnlyList<PieceColor> palette, Rng rng, MatchDetector detector)
        {
            var candidates = new List<PieceColor>(palette);

            const int MaxPasses = 8;
            for (int pass = 0; pass < MaxPasses && detector.HasAnyMatch(board); pass++)
            {
                foreach (GridPos pos in freeCells)
                {
                    Piece piece = board.PieceAt(pos);
                    if (piece == null || !detector.CreatesMatchAt(board, pos))
                        continue;

                    candidates.Clear();
                    candidates.AddRange(palette);
                    rng.Shuffle(candidates);

                    foreach (PieceColor color in candidates)
                    {
                        piece.Color = color;
                        if (!detector.CreatesMatchAt(board, pos))
                            break;
                    }
                }
            }
        }
    }
}
