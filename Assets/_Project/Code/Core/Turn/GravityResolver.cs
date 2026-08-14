using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Settles the board after a clear: entities fall, gaps refill from the top.
    /// <para>
    /// Vertical falling comes first. Then any gap that <b>cannot</b> ever be fed from directly above
    /// — because a hole, a blocker or a wedged crate sits over it — pulls a piece in from one of the
    /// two cells diagonally above it.
    /// </para>
    /// <para>
    /// Note the direction of that rule: it is expressed from the empty cell's point of view, not the
    /// piece's. Asking instead "can this piece slide aside?" looks equivalent but is not — a piece
    /// whose own path down is clear would never slide, so cells tucked under a blocker would stay
    /// empty forever. A piece resting on a blocker therefore stays put, exactly as it does in the
    /// reference games, while the gap beneath the blocker still gets filled from the side.
    /// </para>
    /// <para>
    /// Every move strictly decreases the entity's Y, which is why the relaxation loop always
    /// terminates. Moves are collapsed per entity, so the view animates one smooth fall from the
    /// starting cell to the final cell rather than a stutter per row.
    /// </para>
    /// </summary>
    public sealed class GravityResolver
    {
        private readonly Dictionary<long, GridPos> _origins = new Dictionary<long, GridPos>();
        private readonly List<long> _movedOrder = new List<long>();
        private readonly List<long> _spawnedOrder = new List<long>();
        private readonly HashSet<long> _spawned = new HashSet<long>();

        /// <summary>
        /// Applies gravity and refill until the board is stable.
        /// Returns false if the iteration budget was exhausted, which would indicate a bug.
        /// </summary>
        public bool Settle(Board board, IReadOnlyList<PieceColor> palette, Rng rng, List<BoardEvent> events)
        {
            _origins.Clear();
            _movedOrder.Clear();
            _spawnedOrder.Clear();
            _spawned.Clear();

            // Each pass moves at least one entity down by one cell or spawns one piece. Filling a
            // deep pocket under a blocker costs one pass per cell, hence the generous budget.
            int budget = board.Width * board.Height * 2 + 32;
            bool stable = false;

            for (int iteration = 0; iteration < budget; iteration++)
            {
                bool changed = FallStraight(board);
                changed |= PullDiagonally(board);
                changed |= Spawn(board, palette, rng);

                if (!changed)
                {
                    stable = true;
                    break;
                }
            }

            EmitEvents(board, events);
            return stable;
        }

        private bool FallStraight(Board board)
        {
            bool moved = false;

            // Bottom-up so a cell vacated during this pass can be filled in the same pass.
            foreach (BoardEntity entity in board.EntitiesBottomUp())
            {
                if (!entity.Falls)
                    continue;

                var target = new GridPos(entity.Anchor.X, entity.Anchor.Y - 1);
                if (!board.CanPlace(entity, target))
                    continue;

                RecordOrigin(entity);
                board.MoveTo(entity, target);
                moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Fills gaps that can never be fed from straight above by pulling a piece in from one of
        /// the two cells diagonally above.
        /// </summary>
        private bool PullDiagonally(Board board)
        {
            bool moved = false;

            // Bottom-up: settle the lowest gap first so pieces do not overshoot past it.
            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var target = new GridPos(x, y);
                if (!board.IsEmpty(target))
                    continue;

                // Cells at the top of a column are refilled by the spawner instead.
                if (board.IsSpawner(target))
                    continue;

                var above = new GridPos(x, y + 1);
                if (MayStillFeed(board, above))
                    continue; // a vertical feed is coming, so waiting is correct

                if (TryPullFrom(board, new GridPos(x - 1, y + 1), target)
                    || TryPullFrom(board, new GridPos(x + 1, y + 1), target))
                    moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Could <paramref name="source"/> eventually hand a piece down to the cell below it?
        /// True when it is empty (something may yet arrive) or holds an entity that can still
        /// descend. False for holes, blockers and crates wedged in place.
        /// </summary>
        private static bool MayStillFeed(Board board, GridPos source)
        {
            if (!board.IsPlayable(source))
                return false;

            BoardEntity occupant = board.EntityAt(source);
            if (occupant == null)
                return true;

            if (!occupant.Falls)
                return false;

            return board.CanPlace(occupant, new GridPos(occupant.Anchor.X, occupant.Anchor.Y - 1));
        }

        private bool TryPullFrom(Board board, GridPos source, GridPos target)
        {
            BoardEntity entity = board.EntityAt(source);
            if (entity == null || !entity.Falls || entity.IsMultiCell)
                return false;

            if (!board.CanPlace(entity, target))
                return false;

            RecordOrigin(entity);
            board.MoveTo(entity, target);
            return true;
        }

        private bool Spawn(Board board, IReadOnlyList<PieceColor> palette, Rng rng)
        {
            if (palette == null || palette.Count == 0)
                return false;

            bool spawned = false;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = board.Height - 1; y >= 0; y--)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsSpawner(pos))
                        continue;

                    if (board.IsEmpty(pos))
                    {
                        Piece piece = board.SpawnPiece(pos, rng.Pick(palette));
                        _spawned.Add(piece.Id);
                        _spawnedOrder.Add(piece.Id);
                        spawned = true;
                    }

                    break; // at most one spawner per column
                }
            }

            return spawned;
        }

        private void RecordOrigin(BoardEntity entity)
        {
            if (_origins.ContainsKey(entity.Id))
                return;

            _origins[entity.Id] = entity.Anchor;
            _movedOrder.Add(entity.Id);
        }

        private void EmitEvents(Board board, List<BoardEvent> events)
        {
            // Spawned pieces are reported once, already at their final cell, with a flag telling
            // the view to fly them in from above. That keeps one entity to one animation.
            foreach (long id in _spawnedOrder)
            {
                BoardEntity entity = board.FindById(id);
                if (entity == null)
                    continue;

                events.Add(new EntitySpawnedEvent(EntitySnapshot.Of(entity), fromOutside: true));
            }

            foreach (long id in _movedOrder)
            {
                if (_spawned.Contains(id))
                    continue; // its travel is already implied by the spawn event

                BoardEntity entity = board.FindById(id);
                if (entity == null)
                    continue;

                GridPos from = _origins[id];
                if (from != entity.Anchor)
                    events.Add(new EntityMovedEvent(id, from, entity.Anchor, MoveReason.Fall));
            }
        }
    }
}
