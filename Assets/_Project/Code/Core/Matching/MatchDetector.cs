using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Finds every matched figure on the board and merges overlapping figures into groups.
    /// Stateless apart from reusable scratch buffers, and completely engine-free, so the whole
    /// rule set is unit-testable.
    /// </summary>
    public sealed class MatchDetector
    {
        public const int MinLineLength = 3;

        private readonly List<MatchShape> _shapes = new List<MatchShape>();
        private readonly Dictionary<GridPos, List<int>> _shapesByCell = new Dictionary<GridPos, List<int>>();
        private readonly List<int> _parent = new List<int>();

        /// <summary>All match groups currently present on the board, in deterministic order.</summary>
        public List<MatchGroup> FindMatches(Board board)
        {
            CollectShapes(board);
            return MergeShapesIntoGroups();
        }

        public bool HasAnyMatch(Board board)
        {
            foreach (GridPos pos in board.Positions)
                if (CreatesMatchAt(board, pos))
                    return true;
            return false;
        }

        /// <summary>
        /// Cheap local check: is the piece at <paramref name="pos"/> part of any matched figure?
        /// Used for swap validation and move finding, where building full groups would be wasteful.
        /// </summary>
        public bool CreatesMatchAt(Board board, GridPos pos)
        {
            Piece piece = board.PieceAt(pos);
            if (piece == null || !piece.IsMatchable)
                return false;

            PieceColor color = piece.Color;

            int horizontal = 1
                + CountSameColor(board, pos, GridPos.Left, color)
                + CountSameColor(board, pos, GridPos.Right, color);
            if (horizontal >= MinLineLength)
                return true;

            int vertical = 1
                + CountSameColor(board, pos, GridPos.Down, color)
                + CountSameColor(board, pos, GridPos.Up, color);
            if (vertical >= MinLineLength)
                return true;

            // Any of the four 2x2 blocks that include this cell.
            for (int dx = -1; dx <= 0; dx++)
            for (int dy = -1; dy <= 0; dy++)
                if (IsSquare(board, new GridPos(pos.X + dx, pos.Y + dy), color))
                    return true;

            return false;
        }

        /// <summary>
        /// Would swapping <paramref name="a"/> and <paramref name="b"/> create a match?
        /// Performs the swap on the real board and undoes it, so callers see no side effects.
        /// </summary>
        public bool WouldSwapMatch(Board board, GridPos a, GridPos b)
        {
            Piece pa = board.PieceAt(a);
            Piece pb = board.PieceAt(b);
            if (pa == null || pb == null)
                return false;

            board.SwapCells(a, b);
            bool matched = CreatesMatchAt(board, a) || CreatesMatchAt(board, b);
            board.SwapCells(a, b);
            return matched;
        }

        // ------------------------------------------------------------------ shapes

        private void CollectShapes(Board board)
        {
            _shapes.Clear();

            // Horizontal runs.
            for (int y = 0; y < board.Height; y++)
            {
                int x = 0;
                while (x < board.Width)
                {
                    PieceColor color = MatchableColorAt(board, new GridPos(x, y));
                    if (color == PieceColor.None)
                    {
                        x++;
                        continue;
                    }

                    int length = 1;
                    while (x + length < board.Width
                           && MatchableColorAt(board, new GridPos(x + length, y)) == color)
                        length++;

                    if (length >= MinLineLength)
                        AddLine(new GridPos(x, y), LineOrientation.Horizontal, length, color);

                    x += length;
                }
            }

            // Vertical runs.
            for (int x = 0; x < board.Width; x++)
            {
                int y = 0;
                while (y < board.Height)
                {
                    PieceColor color = MatchableColorAt(board, new GridPos(x, y));
                    if (color == PieceColor.None)
                    {
                        y++;
                        continue;
                    }

                    int length = 1;
                    while (y + length < board.Height
                           && MatchableColorAt(board, new GridPos(x, y + length)) == color)
                        length++;

                    if (length >= MinLineLength)
                        AddLine(new GridPos(x, y), LineOrientation.Vertical, length, color);

                    y += length;
                }
            }

            // 2x2 squares.
            for (int y = 0; y + 1 < board.Height; y++)
            for (int x = 0; x + 1 < board.Width; x++)
            {
                var origin = new GridPos(x, y);
                PieceColor color = MatchableColorAt(board, origin);
                if (color == PieceColor.None || !IsSquare(board, origin, color))
                    continue;

                _shapes.Add(new MatchShape(
                    MatchShapeKind.Square, color, LineOrientation.Horizontal, origin, 2,
                    new[]
                    {
                        origin,
                        new GridPos(x + 1, y),
                        new GridPos(x, y + 1),
                        new GridPos(x + 1, y + 1),
                    }));
            }
        }

        private void AddLine(GridPos start, LineOrientation orientation, int length, PieceColor color)
        {
            var cells = new GridPos[length];
            for (int i = 0; i < length; i++)
                cells[i] = orientation == LineOrientation.Horizontal
                    ? new GridPos(start.X + i, start.Y)
                    : new GridPos(start.X, start.Y + i);

            _shapes.Add(new MatchShape(MatchShapeKind.Line, color, orientation, start, length, cells));
        }

        private static PieceColor MatchableColorAt(Board board, GridPos pos)
        {
            Piece piece = board.PieceAt(pos);
            return piece != null && piece.IsMatchable ? piece.Color : PieceColor.None;
        }

        private static bool IsSquare(Board board, GridPos origin, PieceColor color)
        {
            return MatchableColorAt(board, origin) == color
                   && MatchableColorAt(board, new GridPos(origin.X + 1, origin.Y)) == color
                   && MatchableColorAt(board, new GridPos(origin.X, origin.Y + 1)) == color
                   && MatchableColorAt(board, new GridPos(origin.X + 1, origin.Y + 1)) == color;
        }

        private static int CountSameColor(Board board, GridPos from, GridPos step, PieceColor color)
        {
            int count = 0;
            GridPos cursor = from + step;
            while (MatchableColorAt(board, cursor) == color)
            {
                count++;
                cursor += step;
            }

            return count;
        }

        // ------------------------------------------------------------------ merging

        private List<MatchGroup> MergeShapesIntoGroups()
        {
            var groups = new List<MatchGroup>();
            if (_shapes.Count == 0)
                return groups;

            // Union-find over shape indices: shapes sharing a cell belong to the same group.
            _parent.Clear();
            for (int i = 0; i < _shapes.Count; i++)
                _parent.Add(i);

            _shapesByCell.Clear();
            for (int i = 0; i < _shapes.Count; i++)
            {
                foreach (GridPos cell in _shapes[i].Cells)
                {
                    if (!_shapesByCell.TryGetValue(cell, out List<int> list))
                    {
                        list = new List<int>(2);
                        _shapesByCell[cell] = list;
                    }

                    if (list.Count > 0)
                        Union(list[0], i);
                    list.Add(i);
                }
            }

            var byRoot = new Dictionary<int, List<int>>();
            for (int i = 0; i < _shapes.Count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out List<int> list))
                {
                    list = new List<int>();
                    byRoot[root] = list;
                }

                list.Add(i);
            }

            foreach (List<int> shapeIndices in byRoot.Values)
            {
                var shapes = new List<MatchShape>(shapeIndices.Count);
                var cells = new List<GridPos>();
                var seen = new HashSet<GridPos>();

                foreach (int index in shapeIndices)
                {
                    MatchShape shape = _shapes[index];
                    shapes.Add(shape);
                    foreach (GridPos cell in shape.Cells)
                        if (seen.Add(cell))
                            cells.Add(cell);
                }

                cells.Sort(CompareCells);
                groups.Add(new MatchGroup(shapes[0].Color, cells, shapes));
            }

            groups.Sort((a, b) => CompareCells(a.Cells[0], b.Cells[0]));
            return groups;
        }

        private static int CompareCells(GridPos a, GridPos b)
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        }

        private int Find(int i)
        {
            while (_parent[i] != i)
            {
                _parent[i] = _parent[_parent[i]];
                i = _parent[i];
            }

            return i;
        }

        private void Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra != rb)
                _parent[rb] = ra;
        }
    }
}
