using System.Collections.Generic;

namespace Match3.Core
{
    public enum SwapKind
    {
        /// <summary>Not a legal move.</summary>
        Invalid = 0,

        /// <summary>An ordinary swap that creates at least one match.</summary>
        Match = 1,

        /// <summary>Two boosters swapped together — always legal, resolved by the combination registry.</summary>
        BoosterCombo = 2,

        /// <summary>A Rainbow swapped with a coloured piece — always legal, erases that colour.</summary>
        RainbowColor = 3,

        /// <summary>
        /// A single non-Rainbow booster swapped with an ordinary piece — always legal, exactly like
        /// tapping it, even when the swap makes no match. Moving it onto the target is how the
        /// reference games let you aim a Plane/Line/Bomb rather than only tapping it in place.
        /// </summary>
        BoosterRelocate = 4,
    }

    /// <summary>
    /// The single source of truth for "is this swap legal", shared by the turn resolver and the
    /// move finder so that hint/dead-end logic can never disagree with what the game accepts.
    /// </summary>
    public static class SwapRules
    {
        public static SwapKind Classify(Board board, MatchDetector detector, GridPos a, GridPos b)
        {
            if (!a.IsOrthogonalNeighbourOf(b))
                return SwapKind.Invalid;

            Piece pa = board.PieceAt(a);
            Piece pb = board.PieceAt(b);
            if (pa == null || pb == null || pa.IsMultiCell || pb.IsMultiCell)
                return SwapKind.Invalid;

            if (pa.IsBooster && pb.IsBooster)
                return SwapKind.BoosterCombo;

            // A Rainbow next to a coloured gem wipes that colour, as in the reference games.
            if (pa.Booster == BoosterType.Rainbow && !pb.IsBooster && pb.Color != PieceColor.None)
                return SwapKind.RainbowColor;
            if (pb.Booster == BoosterType.Rainbow && !pa.IsBooster && pa.Color != PieceColor.None)
                return SwapKind.RainbowColor;

            // A coloured booster is itself matchable (see Piece.IsMatchable), so a swap that lines
            // one up into a run of its colour must resolve as an ordinary match -- the booster still
            // fires, but as part of that match's clear rather than pre-empting it. Only fall back to
            // a plain relocate (always legal, match or not) when the swap makes no match at all.
            if (detector.WouldSwapMatch(board, a, b))
                return SwapKind.Match;

            // A lone Line/Bomb/Plane relocated onto anything is always legal, match or not; Rainbow
            // is excluded because it was already handled above (and has no meaning without a colour
            // to target).
            if (pa.IsBooster != pb.IsBooster)
                return SwapKind.BoosterRelocate;

            return SwapKind.Invalid;
        }

        public static bool CanActivateBoosterAt(Board board, GridPos pos)
        {
            Piece piece = board.PieceAt(pos);
            return piece != null && piece.IsBooster;
        }
    }

    public readonly struct BoardMove
    {
        public readonly GridPos A;
        public readonly GridPos B;
        public readonly SwapKind Kind;

        public BoardMove(GridPos a, GridPos b, SwapKind kind)
        {
            A = a;
            B = b;
            Kind = kind;
        }

        public override string ToString() => $"{A}<->{B} ({Kind})";
    }

    /// <summary>Enumerates the legal actions available on a board. Used for dead-end detection.</summary>
    public static class MoveFinder
    {
        /// <summary>
        /// Every legal swap. Only right and up neighbours are tested, so each pair appears once.
        /// </summary>
        public static List<BoardMove> FindAll(Board board, MatchDetector detector)
        {
            var moves = new List<BoardMove>();

            foreach (GridPos pos in board.Positions)
            {
                TryAdd(board, detector, pos, pos + GridPos.Right, moves);
                TryAdd(board, detector, pos, pos + GridPos.Up, moves);
            }

            return moves;
        }

        /// <summary>
        /// True if the player has anything at all to do: a legal swap, or a booster to tap.
        /// </summary>
        public static bool HasAny(Board board, MatchDetector detector)
        {
            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece != null && piece.IsBooster)
                    return true;

                if (SwapRules.Classify(board, detector, pos, pos + GridPos.Right) != SwapKind.Invalid)
                    return true;
                if (SwapRules.Classify(board, detector, pos, pos + GridPos.Up) != SwapKind.Invalid)
                    return true;
            }

            return false;
        }

        private static void TryAdd(Board board, MatchDetector detector, GridPos a, GridPos b,
            List<BoardMove> moves)
        {
            SwapKind kind = SwapRules.Classify(board, detector, a, b);
            if (kind != SwapKind.Invalid)
                moves.Add(new BoardMove(a, b, kind));
        }
    }
}
