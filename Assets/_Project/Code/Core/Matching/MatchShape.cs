using System.Collections.Generic;

namespace Match3.Core
{
    public enum MatchShapeKind
    {
        /// <summary>A straight run of 3 or more same-coloured pieces.</summary>
        Line = 0,

        /// <summary>A 2x2 block of same-coloured pieces. Required because the spec awards the Plane for it.</summary>
        Square = 1,
    }

    /// <summary>One elementary matched figure found on the board.</summary>
    public sealed class MatchShape
    {
        public MatchShape(MatchShapeKind kind, PieceColor color, LineOrientation orientation,
            GridPos start, int length, IReadOnlyList<GridPos> cells)
        {
            Kind = kind;
            Color = color;
            Orientation = orientation;
            Start = start;
            Length = length;
            Cells = cells;
        }

        public MatchShapeKind Kind { get; }
        public PieceColor Color { get; }

        /// <summary>Meaningful for <see cref="MatchShapeKind.Line"/> only.</summary>
        public LineOrientation Orientation { get; }

        /// <summary>Lowest-left cell of the figure.</summary>
        public GridPos Start { get; }

        /// <summary>Number of cells along the run; 2 for a square (2x2).</summary>
        public int Length { get; }

        public IReadOnlyList<GridPos> Cells { get; }

        public override string ToString() =>
            Kind == MatchShapeKind.Square
                ? $"square {Color} at {Start}"
                : $"{Orientation} line {Color} x{Length} at {Start}";
    }
}
