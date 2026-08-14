using System.Collections.Generic;
using Match3.Core;

namespace Match3.Tests
{
    public struct PlaythroughResult
    {
        public bool Won;
        public int TurnsUsed;

        /// <summary>Average fraction of each goal that was collected, 0..1.</summary>
        public float GoalFraction;
    }

    /// <summary>
    /// A crude stand-in for a competent player: it prefers swaps that clear the colours the level
    /// actually asks for, breaks crates that are in the way, and spends boosters.
    /// <para>
    /// It exists so level tuning can be checked automatically. A purely random player loses levels
    /// a person would win, so random play says nothing about whether a move limit is fair.
    /// </para>
    /// </summary>
    public static class GreedyPlayer
    {
        public static PlaythroughResult Play(Match3Game game, Rng rng, int maxTurns)
        {
            int turns = 0;

            while (turns < maxTurns && !game.Level.IsOver)
            {
                if (!PlayOneTurn(game, rng))
                    break;

                turns++;
            }

            return new PlaythroughResult
            {
                Won = game.Level.Outcome == LevelOutcome.Won,
                TurnsUsed = turns,
                GoalFraction = MeasureGoalFraction(game.Level),
            };
        }

        public static float MeasureGoalFraction(LevelRuntime level)
        {
            IReadOnlyList<GoalState> goals = level.Goals.Goals;
            if (goals.Count == 0)
                return 1f;

            float total = 0f;
            foreach (GoalState goal in goals)
                total += goal.Required <= 0 ? 1f : (float)goal.Collected / goal.Required;

            return total / goals.Count;
        }

        /// <summary>Plays the best action it can find. Returns false if there was nothing to do.</summary>
        public static bool PlayOneTurn(Match3Game game, Rng rng)
        {
            Board board = game.Board;
            MatchDetector detector = game.Resolver.Detector;
            LevelRuntime level = game.Level;

            int bestScore = int.MinValue;
            var bestSwaps = new List<BoardMove>();
            var bestTaps = new List<GridPos>();

            foreach (BoardMove move in MoveFinder.FindAll(board, detector))
            {
                int score = ScoreSwap(board, detector, level, move);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSwaps.Clear();
                    bestTaps.Clear();
                    bestSwaps.Add(move);
                }
                else if (score == bestScore)
                {
                    bestSwaps.Add(move);
                }
            }

            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece == null || !piece.IsBooster)
                    continue;

                int score = ScoreTap(piece);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSwaps.Clear();
                    bestTaps.Clear();
                    bestTaps.Add(pos);
                }
                else if (score == bestScore)
                {
                    bestTaps.Add(pos);
                }
            }

            if (bestSwaps.Count == 0 && bestTaps.Count == 0)
                return false;

            // When a swap and a tap tie, take the swap: it leaves the booster on the board for later.
            TurnResult result;
            if (bestSwaps.Count > 0)
            {
                BoardMove chosen = rng.Pick(bestSwaps);
                result = game.Swap(chosen.A, chosen.B);
            }
            else
            {
                result = game.ActivateBooster(rng.Pick(bestTaps));
            }

            if (!result.Accepted)
                throw new System.InvalidOperationException(
                    "GreedyPlayer chose an illegal action: " + result.RejectionReason);

            return true;
        }

        private static int ScoreTap(Piece booster)
        {
            switch (booster.Booster)
            {
                case BoosterType.Rainbow: return 40;
                case BoosterType.Bomb: return 30;
                case BoosterType.Line: return 25;
                case BoosterType.Plane: return 25;
                default: return 0;
            }
        }

        private static int ScoreSwap(Board board, MatchDetector detector, LevelRuntime level, BoardMove move)
        {
            switch (move.Kind)
            {
                case SwapKind.BoosterCombo:
                    return 200;

                case SwapKind.RainbowColor:
                {
                    Piece a = board.PieceAt(move.A);
                    Piece b = board.PieceAt(move.B);
                    Piece partner = a != null && a.Booster == BoosterType.Rainbow ? b : a;
                    if (partner == null)
                        return 20;
                    return level.Goals.IsWantedColor(partner.Color) ? 180 : 20;
                }

                case SwapKind.BoosterRelocate:
                {
                    Piece a = board.PieceAt(move.A);
                    Piece b = board.PieceAt(move.B);
                    Piece booster = a != null && a.IsBooster ? a : b;
                    return booster != null ? ScoreTap(booster) : 0;
                }
            }

            // Actually perform the swap to see what it would produce, then undo it.
            board.SwapCells(move.A, move.B);
            int score = ScoreResultingMatches(board, detector, level);
            board.SwapCells(move.A, move.B);
            return score;
        }

        private static int ScoreResultingMatches(Board board, MatchDetector detector, LevelRuntime level)
        {
            int score = 0;

            foreach (MatchGroup group in detector.FindMatches(board))
            {
                bool wanted = level.Goals.IsWantedColor(group.Color);

                foreach (GridPos cell in group.Cells)
                {
                    score += wanted ? 10 : 1;

                    // Breaking crates rarely scores directly but opens the board up.
                    foreach (GridPos offset in GridPos.Orthogonal)
                    {
                        Obstacle obstacle = board.ObstacleAt(cell + offset);
                        if (obstacle != null && obstacle.Accepts(DamageSource.FromMatch(group.Color)))
                            score += 8;
                    }
                }

                if (group.AwardedBooster != BoosterType.None)
                    score += 15;
            }

            return score;
        }
    }
}
