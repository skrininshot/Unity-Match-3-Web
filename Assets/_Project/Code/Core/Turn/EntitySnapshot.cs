namespace Match3.Core
{
    /// <summary>
    /// Immutable description of an entity at the moment an event was recorded.
    /// The view builds its visuals from these, so it never reads live core state and
    /// can therefore lag behind the logic while animations play out.
    /// </summary>
    public sealed class EntitySnapshot
    {
        public long Id { get; private set; }
        public GridPos Anchor { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public bool IsPiece { get; private set; }

        // Piece fields
        public PieceColor Color { get; private set; }
        public BoosterType Booster { get; private set; }
        public LineOrientation Orientation { get; private set; }

        // Obstacle fields
        public string ObstacleId { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public PieceColor RequiredColor { get; private set; }
        public bool Indestructible { get; private set; }

        public static EntitySnapshot Of(BoardEntity entity)
        {
            var snapshot = new EntitySnapshot
            {
                Id = entity.Id,
                Anchor = entity.Anchor,
                Width = entity.Width,
                Height = entity.Height,
            };

            if (entity is Piece piece)
            {
                snapshot.IsPiece = true;
                snapshot.Color = piece.Color;
                snapshot.Booster = piece.Booster;
                snapshot.Orientation = piece.Orientation;
            }
            else if (entity is Obstacle obstacle)
            {
                snapshot.IsPiece = false;
                snapshot.ObstacleId = obstacle.Config.Id;
                snapshot.Hp = obstacle.Hp;
                snapshot.MaxHp = obstacle.MaxHp;
                snapshot.RequiredColor = obstacle.RequiredColor;
                snapshot.Indestructible = obstacle.IsIndestructible;
            }

            return snapshot;
        }

        /// <summary>
        /// A copy with a different required colour, for the colour-changing crate.
        /// Snapshots are immutable so the view can hold on to one safely.
        /// </summary>
        public EntitySnapshot WithRequiredColor(PieceColor color)
        {
            var copy = (EntitySnapshot)MemberwiseClone();
            copy.RequiredColor = color;
            return copy;
        }

        public override string ToString() =>
            IsPiece
                ? $"piece#{Id} {Color}{(Booster == BoosterType.None ? "" : "/" + Booster)} at {Anchor}"
                : $"{ObstacleId}#{Id} at {Anchor} hp={Hp}/{MaxHp}";
    }
}
