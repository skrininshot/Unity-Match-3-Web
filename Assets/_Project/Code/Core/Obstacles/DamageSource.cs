namespace Match3.Core
{
    public enum DamageKind
    {
        /// <summary>A match happened in a cell orthogonally adjacent to the obstacle.</summary>
        AdjacentMatch = 0,

        /// <summary>A booster blast covered one of the obstacle's cells.</summary>
        BoosterBlast = 1,

        /// <summary>Damage applied directly (e.g. the Plane picking this obstacle as its target).</summary>
        Direct = 2,
    }

    /// <summary>Describes what is trying to damage an obstacle.</summary>
    public readonly struct DamageSource
    {
        public readonly DamageKind Kind;

        /// <summary>Colour of the match that caused the damage; <see cref="PieceColor.None"/> for blasts.</summary>
        public readonly PieceColor Color;

        public DamageSource(DamageKind kind, PieceColor color = PieceColor.None)
        {
            Kind = kind;
            Color = color;
        }

        public static DamageSource FromMatch(PieceColor color) => new DamageSource(DamageKind.AdjacentMatch, color);
        public static DamageSource FromBlast() => new DamageSource(DamageKind.BoosterBlast);
        public static DamageSource Direct() => new DamageSource(DamageKind.Direct);
    }
}
