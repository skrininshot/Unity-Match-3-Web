using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Rearranges the loose pieces when the board has no legal move left.
    /// Without this a player can be stuck staring at a board that cannot be played, which no
    /// amount of polish elsewhere would excuse.
    /// </summary>
    public static class BoardShuffler
    {
        private const int MaxAttempts = 60;

        /// <summary>
        /// Shuffles single-cell pieces between their cells until the board has no match and at
        /// least one legal move. Returns false if it could not improve anything.
        /// </summary>
        public static bool Shuffle(Board board, MatchDetector detector, Rng rng, List<BoardEvent> events)
        {
            var cells = new List<GridPos>();
            var pieces = new List<Piece>();

            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece == null || piece.IsMultiCell)
                    continue;

                cells.Add(pos);
                pieces.Add(piece);
            }

            if (pieces.Count < 2)
                return false;

            var originalPositions = new Dictionary<long, GridPos>(pieces.Count);
            foreach (Piece piece in pieces)
                originalPositions[piece.Id] = piece.Anchor;

            foreach (Piece piece in pieces)
                board.Remove(piece);

            bool success = false;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                rng.Shuffle(pieces);

                for (int i = 0; i < cells.Count; i++)
                    board.Place(pieces[i], cells[i]);

                if (!detector.HasAnyMatch(board) && MoveFinder.HasAny(board, detector))
                {
                    success = true;
                    break;
                }

                if (attempt == MaxAttempts - 1)
                    break; // keep the last arrangement rather than leaving the board empty

                foreach (Piece piece in pieces)
                    board.Remove(piece);
            }

            foreach (Piece piece in pieces)
            {
                GridPos from = originalPositions[piece.Id];
                if (from != piece.Anchor)
                    events.Add(new EntityMovedEvent(piece.Id, from, piece.Anchor, MoveReason.Shuffle));
            }

            return success || events.Count > 0;
        }
    }
}
