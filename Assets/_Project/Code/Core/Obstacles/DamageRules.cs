using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>Ordinary box: any adjacent match hurts it.</summary>
    public sealed class AnyMatchDamageRule : IObstacleDamageRule
    {
        public const string RuleId = "any-match";
        public string Id => RuleId;
        public bool IsIndestructible => false;

        public bool Accepts(Obstacle obstacle, in DamageSource source)
        {
            switch (source.Kind)
            {
                case DamageKind.AdjacentMatch: return true;
                case DamageKind.BoosterBlast: return obstacle.Config.DamageableByBoosters;
                case DamageKind.Direct: return true;
                default: return false;
            }
        }

        public bool OnTurnAdvanced(Obstacle obstacle, Rng rng) => false;
    }

    /// <summary>
    /// Coloured box: only an adjacent match of its required colour hurts it.
    /// Booster blasts ignore the colour requirement — otherwise boosters would be
    /// useless against coloured boxes, which is how the reference games behave too.
    /// </summary>
    public class ColorMatchDamageRule : IObstacleDamageRule
    {
        public const string RuleId = "color-match";
        public virtual string Id => RuleId;
        public bool IsIndestructible => false;

        public bool Accepts(Obstacle obstacle, in DamageSource source)
        {
            switch (source.Kind)
            {
                case DamageKind.AdjacentMatch:
                    return obstacle.RequiredColor != PieceColor.None
                           && source.Color == obstacle.RequiredColor;
                case DamageKind.BoosterBlast:
                    return obstacle.Config.DamageableByBoosters;
                case DamageKind.Direct:
                    return true;
                default:
                    return false;
            }
        }

        public virtual bool OnTurnAdvanced(Obstacle obstacle, Rng rng) => false;
    }

    /// <summary>
    /// Colour-changing box: same as the coloured box, but rerolls its required colour
    /// at the end of every player turn.
    /// </summary>
    public sealed class CyclingColorDamageRule : ColorMatchDamageRule
    {
        public new const string RuleId = "cycling-color";
        public override string Id => RuleId;

        public override bool OnTurnAdvanced(Obstacle obstacle, Rng rng)
        {
            IReadOnlyList<PieceColor> palette = obstacle.ColorPalette;
            if (palette == null || palette.Count == 0)
                return false;

            if (palette.Count == 1)
            {
                if (obstacle.RequiredColor == palette[0])
                    return false;
                obstacle.RequiredColor = palette[0];
                return true;
            }

            // Always land on a different colour, so the box visibly changes every turn.
            PieceColor next;
            do
            {
                next = rng.Pick(palette);
            } while (next == obstacle.RequiredColor);

            obstacle.RequiredColor = next;
            return true;
        }
    }

    /// <summary>Blocker: cannot be destroyed by anything.</summary>
    public sealed class IndestructibleDamageRule : IObstacleDamageRule
    {
        public const string RuleId = "indestructible";
        public string Id => RuleId;
        public bool IsIndestructible => true;

        public bool Accepts(Obstacle obstacle, in DamageSource source) => false;

        public bool OnTurnAdvanced(Obstacle obstacle, Rng rng) => false;
    }
}
