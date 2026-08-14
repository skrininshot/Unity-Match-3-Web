using System;

namespace Match3.Core
{
    /// <summary>
    /// Integer board coordinate. X grows right, Y grows up (row 0 is the bottom row).
    /// Engine-independent so that the whole core can be unit-tested without Unity.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos Up => new GridPos(0, 1);
        public static GridPos Down => new GridPos(0, -1);
        public static GridPos Left => new GridPos(-1, 0);
        public static GridPos Right => new GridPos(1, 0);

        /// <summary>The four orthogonal neighbour offsets, in a fixed order for determinism.</summary>
        public static readonly GridPos[] Orthogonal = { Up, Right, Down, Left };

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);
        public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool IsOrthogonalNeighbourOf(GridPos other)
        {
            int dx = Math.Abs(X - other.X);
            int dy = Math.Abs(Y - other.Y);
            return dx + dy == 1;
        }

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos other && this == other;
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
    }
}
