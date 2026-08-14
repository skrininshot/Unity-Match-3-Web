using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>Mutable per-attempt state of a level: moves left, goal progress, outcome.</summary>
    public sealed class LevelRuntime
    {
        public LevelRuntime(LevelConfig config)
        {
            Config = config;
            Palette = new List<PieceColor>(config.Palette);
            Goals = new GoalTracker(config.Goals);
            MovesLeft = config.MoveLimit;
            Outcome = LevelOutcome.InProgress;
        }

        public LevelConfig Config { get; }

        /// <summary>Colours new pieces are drawn from.</summary>
        public IReadOnlyList<PieceColor> Palette { get; }

        public GoalTracker Goals { get; }

        public int MovesLeft { get; internal set; }

        /// <summary>Number of player actions taken so far. Drives per-turn obstacle behaviour.</summary>
        public int TurnNumber { get; internal set; }

        public LevelOutcome Outcome { get; internal set; }

        public bool IsOver => Outcome != LevelOutcome.InProgress;
    }
}
