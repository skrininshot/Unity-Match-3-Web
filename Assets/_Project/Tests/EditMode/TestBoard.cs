using System;
using System.Collections.Generic;
using System.Text;
using Match3.Core;

namespace Match3.Tests
{
    /// <summary>
    /// Builds boards from ASCII art so that tests read like the situation they describe.
    /// The first text line is the TOP row; y = 0 is the bottom row, matching <see cref="GridPos"/>.
    /// <code>
    ///   "rgb"      top
    ///   "rgb"
    ///   "rgb"      bottom (y = 0)
    /// </code>
    /// Cell codes come from <see cref="LayoutCodes"/> — one alphabet shared with real level data —
    /// with one deliberate difference: in test art '.' means an <b>empty</b> cell, because tests
    /// need to describe gaps, whereas in level data it means "fill me with a random piece".
    /// </summary>
    public static class TestBoard
    {
        public const char Empty = '.';

        public static ObstacleCatalog Catalog { get; } = ObstacleCatalog.CreateDefault();

        public static readonly PieceColor[] DefaultPalette =
        {
            PieceColor.Red, PieceColor.Blue, PieceColor.Green, PieceColor.Yellow,
        };

        public static Board Parse(string art, Rng rng = null)
        {
            rng = rng ?? new Rng(1);
            List<string> rows = SplitRows(art);

            int height = rows.Count;
            int width = 0;
            foreach (string row in rows)
                width = Math.Max(width, row.Length);

            var board = new Board(width, height);

            // Holes first: a cell must stop being playable before anything tries to occupy it.
            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                int y = height - 1 - rowIndex;
                string row = rows[rowIndex];
                for (int x = 0; x < row.Length; x++)
                    if (row[x] == LayoutCodes.Hole)
                        board.SetPlayable(new GridPos(x, y), false);
            }

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                int y = height - 1 - rowIndex;
                string row = rows[rowIndex];

                for (int x = 0; x < width; x++)
                {
                    char code = x < row.Length ? row[x] : Empty;
                    if (code == LayoutCodes.Hole || code == Empty || code == ' ')
                        continue;

                    var pos = new GridPos(x, y);
                    EntitySpec spec = LayoutCodes.ToSpec(code);
                    BoardEntity entity = EntityFactory.Create(board, spec, Catalog, DefaultPalette, rng);
                    if (entity == null)
                        throw new ArgumentException($"Board code '{code}' at {pos} produced no entity.");

                    board.Place(entity, pos);
                }
            }

            board.RecomputeSpawners();
            return board;
        }

        /// <summary>
        /// Renders a board back to ASCII art, top row first. Boosters get their own symbols so that
        /// they never collide with the coloured-crate letters.
        /// </summary>
        public static string Render(Board board)
        {
            var sb = new StringBuilder();
            for (int y = board.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsPlayable(pos))
                    {
                        sb.Append(LayoutCodes.Hole);
                        continue;
                    }

                    BoardEntity entity = board.EntityAt(pos);
                    if (entity == null)
                        sb.Append(Empty);
                    else if (entity is Piece piece)
                        sb.Append(PieceCode(piece));
                    else if (entity is Obstacle obstacle)
                        sb.Append(ObstacleCode(obstacle));
                    else
                        sb.Append('?');
                }

                if (y > 0)
                    sb.Append('\n');
            }

            return sb.ToString();
        }

        private static char PieceCode(Piece piece)
        {
            switch (piece.Booster)
            {
                case BoosterType.Line:
                    return piece.Orientation == LineOrientation.Horizontal ? '-' : '|';
                case BoosterType.Bomb:
                    return 'o';
                case BoosterType.Rainbow:
                    return '@';
                case BoosterType.Plane:
                    return '>';
                default:
                    return PieceColors.ToCode(piece.Color);
            }
        }

        private static char ObstacleCode(Obstacle obstacle)
        {
            switch (obstacle.Config.Id)
            {
                case ObstacleCatalog.Blocker:
                    return LayoutCodes.Blocker;
                case ObstacleCatalog.Box:
                    return LayoutCodes.PlainCrate;
                case ObstacleCatalog.CyclingBox:
                    return LayoutCodes.CyclingCrate;
                case ObstacleCatalog.ColoredBox:
                    return char.ToUpperInvariant(PieceColors.ToCode(obstacle.RequiredColor));
                default:
                    return '?';
            }
        }

        private static List<string> SplitRows(string art)
        {
            var rows = new List<string>();
            foreach (string raw in art.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0)
                    rows.Add(line);
            }

            if (rows.Count == 0)
                throw new ArgumentException("Board art is empty.", nameof(art));

            return rows;
        }
    }
}
