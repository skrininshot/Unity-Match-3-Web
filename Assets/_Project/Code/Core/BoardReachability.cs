using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Works out which cells refill can actually reach, mirroring <see cref="GravityResolver"/>
    /// exactly: a cell is fed from straight above when that is possible at all, and only otherwise
    /// from one of the two cells diagonally above.
    /// <para>
    /// This exists because a layout can seal cells off by accident — put three non-falling crates
    /// side by side and the cell under the middle one can never be refilled again. A level that does
    /// that looks broken rather than hard, so the level tests check for it.
    /// </para>
    /// </summary>
    public static class BoardReachability
    {
        /// <summary>
        /// Reachability per cell, indexed by <see cref="Board.Index"/>.
        /// A cell holding a non-falling obstacle is reported as unreachable, which is correct:
        /// nothing can arrive there while that obstacle stands.
        /// </summary>
        public static bool[] Compute(Board board)
        {
            var reachable = new bool[board.Width * board.Height];

            // Top-down: a cell's answer only depends on the row above it.
            for (int y = board.Height - 1; y >= 0; y--)
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new GridPos(x, y);
                int index = board.Index(pos);

                if (!Passable(board, pos))
                {
                    reachable[index] = false;
                    continue;
                }

                if (board.IsSpawner(pos))
                {
                    reachable[index] = true;
                    continue;
                }

                var above = new GridPos(x, y + 1);
                if (Passable(board, above))
                {
                    reachable[index] = reachable[board.Index(above)];
                    continue;
                }

                // Vertical feed is permanently blocked, so the diagonals are the only option.
                reachable[index] = IsReachable(board, reachable, new GridPos(x - 1, y + 1))
                                   || IsReachable(board, reachable, new GridPos(x + 1, y + 1));
            }

            return reachable;
        }

        /// <summary>
        /// Playable cells that no refill can ever reach and that are not occupied by an obstacle.
        /// An empty list means the layout cannot strand a gap.
        /// </summary>
        public static List<GridPos> FindStrandedCells(Board board)
        {
            bool[] reachable = Compute(board);
            var stranded = new List<GridPos>();

            foreach (GridPos pos in board.Positions)
            {
                if (!board.IsPlayable(pos))
                    continue;

                // Cells held by a standing obstacle are meant to be unreachable.
                if (board.EntityAt(pos) is Obstacle obstacle && !obstacle.Falls)
                    continue;

                if (!reachable[board.Index(pos)])
                    stranded.Add(pos);
            }

            return stranded;
        }

        private static bool IsReachable(Board board, bool[] reachable, GridPos pos) =>
            board.InBounds(pos) && reachable[board.Index(pos)];

        /// <summary>Can a piece ever occupy or travel through this cell?</summary>
        private static bool Passable(Board board, GridPos pos)
        {
            if (!board.IsPlayable(pos))
                return false;

            BoardEntity occupant = board.EntityAt(pos);
            return occupant == null || occupant.Falls;
        }
    }
}
