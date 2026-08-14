using System.Collections.Generic;

namespace Match3.Core
{
    public sealed class GoalState
    {
        public GoalState(PieceColor color, int required)
        {
            Color = color;
            Required = required;
        }

        public PieceColor Color { get; }
        public int Required { get; }
        public int Collected { get; private set; }

        public bool IsDone => Collected >= Required;
        public int Remaining => Required - Collected < 0 ? 0 : Required - Collected;

        internal bool Add(int amount)
        {
            if (amount <= 0 || IsDone)
                return false;

            Collected += amount;
            if (Collected > Required)
                Collected = Required;
            return true;
        }

        public override string ToString() => $"{Color} {Collected}/{Required}";
    }

    /// <summary>
    /// Tracks "collect N pieces of colour C" goals. A level may declare several; it is won
    /// when all of them are satisfied.
    /// </summary>
    public sealed class GoalTracker
    {
        private readonly List<GoalState> _goals = new List<GoalState>();

        public GoalTracker(IEnumerable<LevelGoal> goals)
        {
            foreach (LevelGoal goal in goals)
                _goals.Add(new GoalState(goal.Color, goal.Count));
        }

        public IReadOnlyList<GoalState> Goals => _goals;

        public bool IsComplete
        {
            get
            {
                foreach (GoalState goal in _goals)
                    if (!goal.IsDone)
                        return false;
                return true;
            }
        }

        /// <summary>Colours that still need collecting. Used by the Plane to pick a useful target.</summary>
        public bool IsWantedColor(PieceColor color)
        {
            foreach (GoalState goal in _goals)
                if (!goal.IsDone && goal.Color == color)
                    return true;
            return false;
        }

        /// <summary>
        /// Credits one cleared piece of <paramref name="color"/>.
        /// Returns the goal that changed, or null if this colour is not wanted.
        /// </summary>
        public GoalState Register(PieceColor color)
        {
            foreach (GoalState goal in _goals)
            {
                if (goal.Color != color)
                    continue;
                return goal.Add(1) ? goal : null;
            }

            return null;
        }
    }
}
