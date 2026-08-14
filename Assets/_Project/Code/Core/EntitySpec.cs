namespace Match3.Core
{
    public enum EntitySpecKind
    {
        /// <summary>Nothing — an empty (but playable) cell.</summary>
        Empty = 0,

        /// <summary>A piece whose colour is drawn from the level palette at build time.</summary>
        RandomPiece = 1,

        /// <summary>A piece of an explicitly chosen colour.</summary>
        ColoredPiece = 2,

        /// <summary>A board element from the obstacle catalog.</summary>
        Obstacle = 3,
    }

    /// <summary>
    /// Declarative description of "what should be created here". Recursive via
    /// <see cref="Contains"/>, which is how a crate can hold another crate.
    /// Used by level layouts and by obstacles that reveal something when destroyed.
    /// </summary>
    public sealed class EntitySpec
    {
        public EntitySpecKind Kind { get; private set; }
        public PieceColor Color { get; private set; }
        public BoosterType Booster { get; private set; }
        public LineOrientation Orientation { get; private set; }

        public string ObstacleId { get; private set; }
        public int HpOverride { get; private set; }
        public int WidthOverride { get; private set; }
        public int HeightOverride { get; private set; }

        /// <summary>Nested content revealed when the obstacle described here is destroyed.</summary>
        public EntitySpec Contains { get; private set; }

        public static EntitySpec Empty() => new EntitySpec { Kind = EntitySpecKind.Empty };

        public static EntitySpec RandomPiece() => new EntitySpec { Kind = EntitySpecKind.RandomPiece };

        public static EntitySpec ColoredPiece(PieceColor color) => new EntitySpec
        {
            Kind = EntitySpecKind.ColoredPiece,
            Color = color,
        };

        public static EntitySpec BoosterPiece(BoosterType booster, PieceColor color,
            LineOrientation orientation = LineOrientation.Horizontal) => new EntitySpec
        {
            Kind = EntitySpecKind.ColoredPiece,
            Color = color,
            Booster = booster,
            Orientation = orientation,
        };

        public static EntitySpec Obstacle(
            string obstacleId,
            int hp = 0,
            PieceColor color = PieceColor.None,
            EntitySpec contains = null,
            int width = 0,
            int height = 0) => new EntitySpec
        {
            Kind = EntitySpecKind.Obstacle,
            ObstacleId = obstacleId,
            HpOverride = hp,
            Color = color,
            Contains = contains,
            WidthOverride = width,
            HeightOverride = height,
        };

        public override string ToString()
        {
            switch (Kind)
            {
                case EntitySpecKind.Empty: return "empty";
                case EntitySpecKind.RandomPiece: return "random";
                case EntitySpecKind.ColoredPiece:
                    return Booster == BoosterType.None ? Color.ToString() : $"{Color}/{Booster}";
                case EntitySpecKind.Obstacle:
                    string inner = Contains != null ? $"({Contains})" : string.Empty;
                    return $"{ObstacleId}{inner}";
                default: return Kind.ToString();
            }
        }
    }
}
