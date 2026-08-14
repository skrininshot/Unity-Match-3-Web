namespace Match3.Core
{
    /// <summary>
    /// One booster firing. Combinations and chain reactions are expressed as lists of these,
    /// which keeps the turn resolver ignorant of what any particular booster does.
    /// </summary>
    public sealed class ActivationRequest
    {
        public BoosterType Type { get; private set; }
        public GridPos At { get; private set; }

        /// <summary>Id of the piece that fired, or 0 for a synthetic activation from a combination.</summary>
        public long SourceId { get; private set; }

        public LineOrientation Orientation { get; private set; }

        /// <summary>Colour a Rainbow should erase. <see cref="PieceColor.None"/> lets the effect choose.</summary>
        public PieceColor TargetColor { get; private set; }

        /// <summary>Bomb blast radius; 0 uses the default (2, i.e. 5x5).</summary>
        public int Radius { get; private set; }

        /// <summary>Line thickness in cells; 0 uses the default (1).</summary>
        public int Thickness { get; private set; }

        /// <summary>Rainbow variant that erases the whole board (Rainbow + Rainbow).</summary>
        public bool EntireBoard { get; private set; }

        /// <summary>Booster a Plane carries and detonates once it reaches its target.</summary>
        public BoosterType Payload { get; private set; }

        public static ActivationRequest FromPiece(Piece piece) => new ActivationRequest
        {
            Type = piece.Booster,
            At = piece.Anchor,
            SourceId = piece.Id,
            Orientation = piece.Orientation,
            TargetColor = PieceColor.None,
        };

        public static ActivationRequest Line(GridPos at, LineOrientation orientation,
            int thickness = 0, long sourceId = 0) => new ActivationRequest
        {
            Type = BoosterType.Line,
            At = at,
            Orientation = orientation,
            Thickness = thickness,
            SourceId = sourceId,
        };

        public static ActivationRequest Bomb(GridPos at, int radius = 0, long sourceId = 0) =>
            new ActivationRequest
            {
                Type = BoosterType.Bomb,
                At = at,
                Radius = radius,
                SourceId = sourceId,
            };

        public static ActivationRequest Rainbow(GridPos at, PieceColor targetColor,
            bool entireBoard = false, long sourceId = 0) => new ActivationRequest
        {
            Type = BoosterType.Rainbow,
            At = at,
            TargetColor = targetColor,
            EntireBoard = entireBoard,
            SourceId = sourceId,
        };

        public static ActivationRequest Plane(GridPos at, BoosterType payload = BoosterType.None,
            long sourceId = 0) => new ActivationRequest
        {
            Type = BoosterType.Plane,
            At = at,
            Payload = payload,
            SourceId = sourceId,
        };

        public override string ToString()
        {
            string extra = string.Empty;
            if (Type == BoosterType.Line) extra = $" {Orientation} thickness={(Thickness == 0 ? 1 : Thickness)}";
            if (Type == BoosterType.Bomb) extra = $" radius={(Radius == 0 ? 2 : Radius)}";
            if (Type == BoosterType.Rainbow) extra = EntireBoard ? " everything" : $" {TargetColor}";
            if (Type == BoosterType.Plane && Payload != BoosterType.None) extra = $" carrying {Payload}";
            return $"{Type}@{At}{extra}";
        }
    }
}
