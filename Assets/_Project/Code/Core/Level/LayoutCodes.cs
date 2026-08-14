namespace Match3.Core
{
    /// <summary>
    /// Character set for the single-character board layout in level data.
    /// <code>
    ///   #            hole — cell is not part of the board
    ///   .            random piece from the level palette
    ///   r g b y p o  piece of that specific colour
    ///   =            plain crate      (any adjacent match damages it)
    ///   *            colour-changing crate
    ///   X            blocker          (indestructible)
    ///   R G B Y P O  coloured crate requiring that colour
    /// </code>
    /// Anything richer — extra lives, a footprint larger than one cell, nested contents —
    /// goes through <see cref="CellOverride"/> instead of growing this alphabet.
    /// </summary>
    public static class LayoutCodes
    {
        public const char Hole = '#';
        public const char RandomPiece = '.';
        public const char PlainCrate = '=';
        public const char CyclingCrate = '*';
        public const char Blocker = 'X';

        public static bool IsKnown(char code)
        {
            if (code == Hole || code == RandomPiece || code == PlainCrate
                || code == CyclingCrate || code == Blocker)
                return true;

            char lower = char.ToLowerInvariant(code);
            return PieceColors.TryFromCode(lower, out _);
        }

        /// <summary>Translates one layout character into a declarative spec.</summary>
        public static EntitySpec ToSpec(char code)
        {
            switch (code)
            {
                case Hole:
                    return null; // caller marks the cell as not playable
                case RandomPiece:
                    return EntitySpec.RandomPiece();
                case PlainCrate:
                    return EntitySpec.Obstacle(ObstacleCatalog.Box);
                case CyclingCrate:
                    return EntitySpec.Obstacle(ObstacleCatalog.CyclingBox);
                case Blocker:
                    return EntitySpec.Obstacle(ObstacleCatalog.Blocker);
            }

            if (PieceColors.TryFromCode(code, out PieceColor lowerColor))
                return EntitySpec.ColoredPiece(lowerColor);

            char lower = char.ToLowerInvariant(code);
            if (PieceColors.TryFromCode(lower, out PieceColor upperColor))
                return EntitySpec.Obstacle(ObstacleCatalog.ColoredBox, color: upperColor);

            throw new System.ArgumentException($"Unknown layout code '{code}'.");
        }
    }
}
