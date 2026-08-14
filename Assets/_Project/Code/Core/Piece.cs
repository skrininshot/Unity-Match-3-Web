namespace Match3.Core
{
    /// <summary>
    /// A coloured gem, optionally carrying a booster.
    /// Booster pieces keep their colour so that they can still be matched normally
    /// (matching a booster into a line destroys it, which activates it — that is the
    /// main source of chain reactions). The Rainbow booster is the exception: it is
    /// colourless and therefore never participates in matches.
    /// </summary>
    public sealed class Piece : BoardEntity
    {
        public Piece(long id, PieceColor color, BoosterType booster = BoosterType.None,
            LineOrientation orientation = LineOrientation.Horizontal)
            : base(id)
        {
            Color = color;
            Booster = booster;
            Orientation = orientation;
        }

        public PieceColor Color { get; internal set; }
        public BoosterType Booster { get; internal set; }
        public LineOrientation Orientation { get; internal set; }

        public bool IsBooster => Booster != BoosterType.None;

        /// <summary>Only colourful, non-Rainbow pieces can be part of a match.</summary>
        public bool IsMatchable => Color != PieceColor.None && Booster != BoosterType.Rainbow;

        public override bool Falls => true;
        public override bool BlocksFalling => false;

        public override string ToString() =>
            IsBooster ? $"{Color}/{Booster}#{Id}" : $"{Color}#{Id}";
    }
}
