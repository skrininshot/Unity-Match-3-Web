namespace Match3.Core
{
    /// <summary>
    /// Logical colour of a piece. <see cref="None"/> means "colourless" and never takes part
    /// in matching — it is used by the Rainbow booster and by colour-agnostic obstacles.
    /// </summary>
    public enum PieceColor
    {
        None = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6,
    }

    public static class PieceColors
    {
        /// <summary>All real (matchable) colours, in palette order. Levels take a prefix of this.</summary>
        public static readonly PieceColor[] All =
        {
            PieceColor.Red,
            PieceColor.Blue,
            PieceColor.Green,
            PieceColor.Yellow,
            PieceColor.Purple,
            PieceColor.Orange,
        };

        /// <summary>Single-letter code used by level layout strings and JSON.</summary>
        public static char ToCode(PieceColor color)
        {
            switch (color)
            {
                case PieceColor.Red: return 'r';
                case PieceColor.Blue: return 'b';
                case PieceColor.Green: return 'g';
                case PieceColor.Yellow: return 'y';
                case PieceColor.Purple: return 'p';
                case PieceColor.Orange: return 'o';
                default: return '?';
            }
        }

        public static bool TryFromCode(char code, out PieceColor color)
        {
            switch (code)
            {
                case 'r': color = PieceColor.Red; return true;
                case 'b': color = PieceColor.Blue; return true;
                case 'g': color = PieceColor.Green; return true;
                case 'y': color = PieceColor.Yellow; return true;
                case 'p': color = PieceColor.Purple; return true;
                case 'o': color = PieceColor.Orange; return true;
                default: color = PieceColor.None; return false;
            }
        }
    }
}
