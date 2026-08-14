namespace Match3.Core
{
    /// <summary>
    /// Template describing a board-element type. Behaviour comes entirely from data plus a
    /// <see cref="IObstacleDamageRule"/>, so the orthogonal properties the spec asks for —
    /// multiple lives, footprint larger than one cell, containing another element — combine
    /// freely without a class per combination.
    /// </summary>
    public sealed class ObstacleConfig
    {
        public ObstacleConfig(
            string id,
            IObstacleDamageRule rule,
            int maxHp = 1,
            int width = 1,
            int height = 1,
            bool falls = false,
            bool blocksFalling = true,
            bool damageableByBoosters = true,
            PieceColor defaultRequiredColor = PieceColor.None)
        {
            Id = id;
            Rule = rule;
            MaxHp = maxHp < 1 ? 1 : maxHp;
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            Falls = falls;
            BlocksFalling = blocksFalling;
            DamageableByBoosters = damageableByBoosters;
            DefaultRequiredColor = defaultRequiredColor;
        }

        public string Id { get; }
        public IObstacleDamageRule Rule { get; }

        /// <summary>Hits required to destroy one instance.</summary>
        public int MaxHp { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Does gravity pull it down? Crates usually do, blockers never do.</summary>
        public bool Falls { get; }

        /// <summary>Do its cells stop other entities from falling through?</summary>
        public bool BlocksFalling { get; }

        public bool DamageableByBoosters { get; }

        /// <summary>Starting required colour for colour-based rules.</summary>
        public PieceColor DefaultRequiredColor { get; }

        public bool UsesColor => Rule is ColorMatchDamageRule;
    }
}
