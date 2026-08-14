using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// A board element: crate, coloured crate, colour-changing crate, blocker — all one class.
    /// What distinguishes them is <see cref="ObstacleConfig"/> plus per-instance state, never a subclass.
    /// </summary>
    public sealed class Obstacle : BoardEntity
    {
        private readonly int _width;
        private readonly int _height;

        public Obstacle(
            long id,
            ObstacleConfig config,
            int hp = 0,
            PieceColor requiredColor = PieceColor.None,
            EntitySpec contains = null,
            int width = 0,
            int height = 0)
            : base(id)
        {
            Config = config;
            MaxHp = hp > 0 ? hp : config.MaxHp;
            Hp = MaxHp;
            RequiredColor = requiredColor != PieceColor.None ? requiredColor : config.DefaultRequiredColor;
            Contains = contains;
            _width = width > 0 ? width : config.Width;
            _height = height > 0 ? height : config.Height;
        }

        public ObstacleConfig Config { get; }

        public int MaxHp { get; }
        public int Hp { get; internal set; }

        /// <summary>Colour required to damage it, for colour-based rules.</summary>
        public PieceColor RequiredColor { get; internal set; }

        /// <summary>Entity revealed in this obstacle's anchor cell when it is destroyed. May be null.</summary>
        public EntitySpec Contains { get; internal set; }

        /// <summary>Palette the colour-changing rule rerolls from; set by the board builder.</summary>
        public IReadOnlyList<PieceColor> ColorPalette { get; internal set; }

        public override int Width => _width;
        public override int Height => _height;

        public override bool Falls => Config.Falls;
        public override bool BlocksFalling => Config.BlocksFalling;

        public bool IsIndestructible => Config.Rule.IsIndestructible;

        public bool Accepts(in DamageSource source) => Config.Rule.Accepts(this, source);

        public override string ToString()
        {
            string color = RequiredColor != PieceColor.None ? $" {RequiredColor}" : string.Empty;
            string size = IsMultiCell ? $" {Width}x{Height}" : string.Empty;
            return $"{Config.Id}{color}{size} hp={Hp}/{MaxHp}#{Id}";
        }
    }
}
