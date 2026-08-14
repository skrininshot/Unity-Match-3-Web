using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// One or more <see cref="MatchShape"/>s that share cells, therefore clear together and
    /// award at most one booster. L- and T-shapes arrive here as a single group containing a
    /// horizontal and a vertical line.
    /// </summary>
    public sealed class MatchGroup
    {
        public MatchGroup(PieceColor color, IReadOnlyList<GridPos> cells, IReadOnlyList<MatchShape> shapes)
        {
            Color = color;
            Cells = cells;
            Shapes = shapes;

            int longest = 0;
            bool hasHorizontalLine = false;
            bool hasVerticalLine = false;
            bool hasSquare = false;

            foreach (MatchShape shape in shapes)
            {
                if (shape.Kind == MatchShapeKind.Square)
                {
                    hasSquare = true;
                    continue;
                }

                if (shape.Length > longest)
                    longest = shape.Length;

                if (shape.Orientation == LineOrientation.Horizontal)
                    hasHorizontalLine = true;
                else
                    hasVerticalLine = true;
            }

            LongestLine = longest;
            HasSquare = hasSquare;
            HasCorner = hasHorizontalLine && hasVerticalLine;
        }

        public PieceColor Color { get; }

        /// <summary>Every cell that clears, ordered bottom-left first for deterministic replay.</summary>
        public IReadOnlyList<GridPos> Cells { get; }

        public IReadOnlyList<MatchShape> Shapes { get; }

        /// <summary>Length of the longest straight run in the group; 0 if the group is only a square.</summary>
        public int LongestLine { get; }

        /// <summary>True when a horizontal and a vertical run cross — an L or T shape.</summary>
        public bool HasCorner { get; }

        public bool HasSquare { get; }

        public int Size => Cells.Count;

        /// <summary>
        /// Booster this group awards, following the spec:
        /// 5-in-a-row gives Rainbow, a corner gives Bomb, exactly 4 gives Line, a 2x2 gives Plane.
        /// </summary>
        public BoosterType AwardedBooster
        {
            get
            {
                if (LongestLine >= 5) return BoosterType.Rainbow;
                if (HasCorner) return BoosterType.Bomb;
                if (LongestLine == 4) return BoosterType.Line;
                if (HasSquare) return BoosterType.Plane;
                return BoosterType.None;
            }
        }

        /// <summary>
        /// Orientation for an awarded <see cref="BoosterType.Line"/>: a horizontal match yields a
        /// booster that clears its row, matching player expectation from the reference games.
        /// </summary>
        public LineOrientation AwardedLineOrientation
        {
            get
            {
                foreach (MatchShape shape in Shapes)
                    if (shape.Kind == MatchShapeKind.Line && shape.Length >= 4)
                        return shape.Orientation;
                return LineOrientation.Horizontal;
            }
        }

        public override string ToString() =>
            $"{Color} group of {Size} (longest={LongestLine}, corner={HasCorner}, square={HasSquare}) -> {AwardedBooster}";
    }
}
