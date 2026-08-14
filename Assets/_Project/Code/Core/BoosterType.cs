namespace Match3.Core
{
    /// <summary>
    /// Booster carried by a piece. A piece with <see cref="None"/> is an ordinary gem.
    /// </summary>
    public enum BoosterType
    {
        None = 0,

        /// <summary>Clears a whole row or column, depending on <see cref="LineOrientation"/>.</summary>
        Line = 1,

        /// <summary>Clears a square area around itself.</summary>
        Bomb = 2,

        /// <summary>Clears every piece of one colour.</summary>
        Rainbow = 3,

        /// <summary>Flies to the board cell that best advances the current level goal.</summary>
        Plane = 4,
    }

    public enum LineOrientation
    {
        Horizontal = 0,
        Vertical = 1,
    }
}
