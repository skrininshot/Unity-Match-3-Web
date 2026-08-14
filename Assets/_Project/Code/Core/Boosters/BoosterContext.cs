using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Everything a booster effect may read, plus the two outputs it may write:
    /// the cells it covers and any follow-up activations it triggers.
    /// </summary>
    public sealed class BoosterContext
    {
        public BoosterContext(Board board, LevelRuntime level, Rng rng, HashSet<GridPos> reservedTargets)
        {
            Board = board;
            Level = level;
            Rng = rng;
            ReservedTargets = reservedTargets;
        }

        public Board Board { get; }
        public LevelRuntime Level { get; }
        public Rng Rng { get; }

        /// <summary>Cells already claimed by another Plane this turn, so two planes pick two targets.</summary>
        public HashSet<GridPos> ReservedTargets { get; }

        /// <summary>Cells the blast covers. The effect fills this.</summary>
        public List<GridPos> Affected { get; } = new List<GridPos>();

        /// <summary>Activations this effect triggers, resolved in the next wave.</summary>
        public List<ActivationRequest> FollowUps { get; } = new List<ActivationRequest>();

        /// <summary>Colour a Rainbow settled on, for reporting.</summary>
        public PieceColor ChosenColor { get; set; }

        /// <summary>Cell a Plane flew to, for reporting.</summary>
        public GridPos? FlyTarget { get; set; }

        public void Reset()
        {
            Affected.Clear();
            FollowUps.Clear();
            ChosenColor = PieceColor.None;
            FlyTarget = null;
        }

        public void AddCell(GridPos pos)
        {
            if (Board.IsPlayable(pos))
                Affected.Add(pos);
        }
    }
}
